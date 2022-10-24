using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Xml.Serialization;
using OpenCVForUnity.ArucoModule;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityUtils.Helper;
using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using HoloLensWithOpenCVForUnity.UnityUtils.Helper;
using HoloLensCameraStream;
using Microsoft.MixedReality.Toolkit.Input;

namespace HoloLensWithOpenCVForUnityExample
{
    /// <summary>
    /// HoloLens ArUco Example
    /// An example of marker based AR using OpenCVForUnity on Hololens.
    /// Referring to https://github.com/opencv/opencv_contrib/blob/master/modules/aruco/samples/detect_markers.cpp.
    /// </summary>
    [RequireComponent(typeof(HLCameraStreamToMatHelper), typeof(ImageOptimizationHelper))]
    public class HLArUcoExample : MonoBehaviour
    {
        [HeaderAttribute("Preview")]

        /// <summary>
        /// The preview quad.
        /// </summary>
        public GameObject previewQuad;

        /// <summary>
        /// Determines if displays the camera preview.
        /// </summary>
        public bool displayCameraPreview;

        /// <summary>
        /// The toggle for switching the camera preview display state.
        /// </summary>
        public Toggle displayCameraPreviewToggle;


        [HeaderAttribute("Detection")]

        /// <summary>
        /// Determines if enables the detection.
        /// </summary>
        public bool enableDetection = true;

        /// <summary>
        /// Determines if restores the camera parameters when the file exists.
        /// </summary>
        public bool useStoredCameraParameters = false;

        /// <summary>
        /// The toggle for switching to use the stored camera parameters.
        /// </summary>
        public Toggle useStoredCameraParametersToggle;

        /// <summary>
        /// Determines if enable downscale.
        /// </summary>
        public bool enableDownScale;

        /// <summary>
        /// The enable downscale toggle.
        /// </summary>
        public Toggle enableDownScaleToggle;


        [HeaderAttribute("AR")]

        /// <summary>
        /// Determines if applied the pose estimation.
        /// </summary>
        public bool applyEstimationPose = true;

        /// <summary>
        /// The dictionary identifier.
        /// </summary>
        public int dictionaryId = Aruco.DICT_6X6_250;

        /// <summary>
        /// The length of the markers' side. Normally, unit is meters.
        /// </summary>
        public float markerLength = 0.188f;

        /// <summary>
        /// The AR cube.
        /// </summary>
        public GameObject arCube;

        /// <summary>
        /// The AR game object.
        /// </summary>
        public ARGameObject arGameObject;

        /// <summary>
        /// The AR camera.
        /// </summary>
        public Camera arCamera;


        [Space(10)]

        /// <summary>
        /// Determines if enable lerp filter.
        /// </summary>
        public bool enableLerpFilter;

        /// <summary>
        /// The enable lerp filter toggle.
        /// </summary>
        public Toggle enableLerpFilterToggle;

        /// <summary>
        /// The cameraparam matrix.
        /// </summary>
        Mat camMatrix;

        /// <summary>
        /// The distCoeffs.
        /// </summary>
        MatOfDouble distCoeffs;

        /// <summary>
        /// The matrix that inverts the Y-axis.
        /// </summary>
        Matrix4x4 invertYM;

        /// <summary>
        /// The matrix that inverts the Z-axis.
        /// </summary>
        Matrix4x4 invertZM;

        /// <summary>
        /// The transformation matrix.
        /// </summary>
        Matrix4x4 transformationM;

        /// <summary>
        /// The transformation matrix for AR.
        /// </summary>
        Matrix4x4 ARM;

        /// <summary>
        /// The identifiers.
        /// </summary>
        Mat ids;

        /// <summary>
        /// The corners.
        /// </summary>
        List<Mat> corners;

        /// <summary>
        /// The rejected corners.
        /// </summary>
        List<Mat> rejectedCorners;

        /// <summary>
        /// The rvecs.
        /// </summary>
        Mat rvecs;

        /// <summary>
        /// The tvecs.
        /// </summary>
        Mat tvecs;

        /// <summary>
        /// The rot mat.
        /// </summary>
        Mat rotMat;

        /// <summary>
        /// The detector parameters.
        /// </summary>
        DetectorParameters detectorParams;

        /// <summary>
        /// The dictionary.
        /// </summary>
        Dictionary dictionary;

        /// <summary>
        /// The webcam texture to mat helper.
        /// </summary>
        HLCameraStreamToMatHelper webCamTextureToMatHelper;

        /// <summary>
        /// The image optimization helper.
        /// </summary>
        ImageOptimizationHelper imageOptimizationHelper;

        Mat rgbMat4preview;
        Texture2D texture;


        readonly static Queue<Action> ExecuteOnMainThread = new Queue<Action>();
        System.Object sync = new System.Object();

        Mat downScaleMat;
        float DOWNSCALE_RATIO;

        bool _isThreadRunning = false;
        bool isThreadRunning
        {
            get
            {
                lock (sync)
                    return _isThreadRunning;
            }
            set
            {
                lock (sync)
                    _isThreadRunning = value;
            }
        }

        bool _isDetecting = false;
        bool isDetecting
        {
            get
            {
                lock (sync)
                    return _isDetecting;
            }
            set
            {
                lock (sync)
                    _isDetecting = value;
            }
        }

        bool _hasUpdatedARTransformMatrix = false;
        bool hasUpdatedARTransformMatrix
        {
            get
            {
                lock (sync)
                    return _hasUpdatedARTransformMatrix;
            }
            set
            {
                lock (sync)
                    _hasUpdatedARTransformMatrix = value;
            }
        }

        bool _isDetectingInFrameArrivedThread = false;
        bool isDetectingInFrameArrivedThread
        {
            get
            {
                lock (sync)
                    return _isDetectingInFrameArrivedThread;
            }
            set
            {
                lock (sync)
                    _isDetectingInFrameArrivedThread = value;
            }
        }

        [HeaderAttribute("Debug")]

        public Text renderFPS;
        public Text videoFPS;
        public Text trackFPS;
        public Text debugStr;


        // Use this for initialization
        protected void Start()
        {
            displayCameraPreviewToggle.isOn = displayCameraPreview;
            useStoredCameraParametersToggle.isOn = useStoredCameraParameters;
            enableDownScaleToggle.isOn = enableDownScale;
            enableLerpFilterToggle.isOn = enableLerpFilter;

            imageOptimizationHelper = gameObject.GetComponent<ImageOptimizationHelper>();
            webCamTextureToMatHelper = gameObject.GetComponent<HLCameraStreamToMatHelper>();
#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            webCamTextureToMatHelper.frameMatAcquired += OnFrameMatAcquired;
#endif
            webCamTextureToMatHelper.outputColorFormat = WebCamTextureToMatHelper.ColorFormat.GRAY;
            webCamTextureToMatHelper.Initialize();
        }

        /// <summary>
        /// Raises the web cam texture to mat helper initialized event.
        /// </summary>
        public void OnWebCamTextureToMatHelperInitialized()
        {
            Debug.Log("OnWebCamTextureToMatHelperInitialized");

            Mat grayMat = webCamTextureToMatHelper.GetMat();

            float rawFrameWidth = grayMat.width();
            float rawFrameHeight = grayMat.height();

            if (enableDownScale)
            {
                downScaleMat = imageOptimizationHelper.GetDownScaleMat(grayMat);
                DOWNSCALE_RATIO = imageOptimizationHelper.downscaleRatio;
            }
            else
            {
                downScaleMat = grayMat;
                DOWNSCALE_RATIO = 1.0f;
            }

            float width = downScaleMat.width();
            float height = downScaleMat.height();

            texture = new Texture2D((int)width, (int)height, TextureFormat.RGB24, false);
            previewQuad.GetComponent<MeshRenderer>().material.mainTexture = texture;
            previewQuad.transform.localScale = new Vector3(0.2f * width / height, 0.2f, 1);
            previewQuad.SetActive(displayCameraPreview);


            //Debug.Log("Screen.width " + Screen.width + " Screen.height " + Screen.height + " Screen.orientation " + Screen.orientation);


            DebugUtils.AddDebugStr(webCamTextureToMatHelper.outputColorFormat.ToString() + " " + webCamTextureToMatHelper.GetWidth() + " x " + webCamTextureToMatHelper.GetHeight() + " : " + webCamTextureToMatHelper.GetFPS());
            if (enableDownScale)
                DebugUtils.AddDebugStr("enableDownScale = true: " + DOWNSCALE_RATIO + " / " + width + " x " + height);


            // create camera matrix and dist coeffs.
            string loadDirectoryPath = Path.Combine(Application.persistentDataPath, "HoloLensArUcoCameraCalibrationExample");
            string calibratonDirectoryName = "camera_parameters" + rawFrameWidth + "x" + rawFrameWidth;
            string loadCalibratonFileDirectoryPath = Path.Combine(loadDirectoryPath, calibratonDirectoryName);
            string loadPath = Path.Combine(loadCalibratonFileDirectoryPath, calibratonDirectoryName + ".xml");
            if (useStoredCameraParameters && File.Exists(loadPath))
            {
                // If there is a camera parameters stored by HoloLensArUcoCameraCalibrationExample, use it

                CameraParameters param;
                XmlSerializer serializer = new XmlSerializer(typeof(CameraParameters));
                using (var stream = new FileStream(loadPath, FileMode.Open))
                {
                    param = (CameraParameters)serializer.Deserialize(stream);
                }

                double fx = param.camera_matrix[0];
                double fy = param.camera_matrix[4];
                double cx = param.camera_matrix[2];
                double cy = param.camera_matrix[5];

                camMatrix = CreateCameraMatrix(fx, fy, cx / DOWNSCALE_RATIO, cy / DOWNSCALE_RATIO);
                distCoeffs = new MatOfDouble(param.GetDistortionCoefficients());

                Debug.Log("Loaded CameraParameters from a stored XML file.");
                Debug.Log("loadPath: " + loadPath);

                DebugUtils.AddDebugStr("Loaded CameraParameters from a stored XML file.");
                DebugUtils.AddDebugStr("loadPath: " + loadPath);
            }
            else
            {
                if (useStoredCameraParameters && !File.Exists(loadPath))
                {
                    DebugUtils.AddDebugStr("The CameraParameters XML file (" + loadPath + ") does not exist.");
                }

#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API

                CameraIntrinsics cameraIntrinsics = webCamTextureToMatHelper.GetCameraIntrinsics();

                camMatrix = CreateCameraMatrix(cameraIntrinsics.FocalLengthX, cameraIntrinsics.FocalLengthY, cameraIntrinsics.PrincipalPointX / DOWNSCALE_RATIO, cameraIntrinsics.PrincipalPointY / DOWNSCALE_RATIO);
                distCoeffs = new MatOfDouble(cameraIntrinsics.RadialDistK1, cameraIntrinsics.RadialDistK2, cameraIntrinsics.RadialDistK3, cameraIntrinsics.TangentialDistP1, cameraIntrinsics.TangentialDistP2);

                Debug.Log("Created CameraParameters from VideoMediaFrame.CameraIntrinsics on device.");

                DebugUtils.AddDebugStr("Created CameraParameters from VideoMediaFrame.CameraIntrinsics on device.");

#else

                // The camera matrix value of Hololens camera 896x504 size.
                // For details on the camera matrix, please refer to this page. (http://docs.opencv.org/2.4/modules/calib3d/doc/camera_calibration_and_3d_reconstruction.html)
                // These values ​​are unique to my device, obtained from the "Windows.Media.Devices.Core.CameraIntrinsics" class. (https://docs.microsoft.com/en-us/uwp/api/windows.media.devices.core.cameraintrinsics)
                // Can get these values by using this helper script. (https://github.com/EnoxSoftware/HoloLensWithOpenCVForUnityExample/tree/master/Assets/HololensCameraIntrinsicsChecker/CameraIntrinsicsCheckerHelper)
                double fx = 1035.149;//focal length x.
                double fy = 1034.633;//focal length y.
                double cx = 404.9134;//principal point x.
                double cy = 236.2834;//principal point y.
                double distCoeffs1 = 0.2036923;//radial distortion coefficient k1.
                double distCoeffs2 = -0.2035773;//radial distortion coefficient k2.
                double distCoeffs3 = 0.0;//tangential distortion coefficient p1.
                double distCoeffs4 = 0.0;//tangential distortion coefficient p2.
                double distCoeffs5 = -0.2388065;//radial distortion coefficient k3.

                camMatrix = CreateCameraMatrix(fx, fy, cx / DOWNSCALE_RATIO, cy / DOWNSCALE_RATIO);
                distCoeffs = new MatOfDouble(distCoeffs1, distCoeffs2, distCoeffs3, distCoeffs4, distCoeffs5);

                Debug.Log("Created a dummy CameraParameters (896x504).");

                DebugUtils.AddDebugStr("Created a dummy CameraParameters (896x504).");
#endif
            }

            Debug.Log("camMatrix " + camMatrix.dump());
            Debug.Log("distCoeffs " + distCoeffs.dump());

            //DebugUtils.AddDebugStr("camMatrix " + camMatrix.dump());
            //DebugUtils.AddDebugStr("distCoeffs " + distCoeffs.dump());


            //Calibration camera
            Size imageSize = new Size(width, height);
            double apertureWidth = 0;
            double apertureHeight = 0;
            double[] fovx = new double[1];
            double[] fovy = new double[1];
            double[] focalLength = new double[1];
            Point principalPoint = new Point(0, 0);
            double[] aspectratio = new double[1];

            Calib3d.calibrationMatrixValues(camMatrix, imageSize, apertureWidth, apertureHeight, fovx, fovy, focalLength, principalPoint, aspectratio);

            Debug.Log("imageSize " + imageSize.ToString());
            Debug.Log("apertureWidth " + apertureWidth);
            Debug.Log("apertureHeight " + apertureHeight);
            Debug.Log("fovx " + fovx[0]);
            Debug.Log("fovy " + fovy[0]);
            Debug.Log("focalLength " + focalLength[0]);
            Debug.Log("principalPoint " + principalPoint.ToString());
            Debug.Log("aspectratio " + aspectratio[0]);

            // Display objects near the camera.
            arCamera.nearClipPlane = 0.01f;

            ids = new Mat();
            corners = new List<Mat>();
            rejectedCorners = new List<Mat>();
            rvecs = new Mat();
            tvecs = new Mat();
            rotMat = new Mat(3, 3, CvType.CV_64FC1);


            transformationM = new Matrix4x4();

            invertYM = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1, -1, 1));
            Debug.Log("invertYM " + invertYM.ToString());

            invertZM = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1, 1, -1));
            Debug.Log("invertZM " + invertZM.ToString());

            detectorParams = DetectorParameters.create();
            dictionary = Aruco.getPredefinedDictionary(dictionaryId);


            //If WebCamera is frontFaceing, flip Mat.
            webCamTextureToMatHelper.flipHorizontal = webCamTextureToMatHelper.IsFrontFacing();

            rgbMat4preview = new Mat();
        }

        /// <summary>
        /// Raises the web cam texture to mat helper disposed event.
        /// </summary>
        public void OnWebCamTextureToMatHelperDisposed()
        {
            Debug.Log("OnWebCamTextureToMatHelperDisposed");

#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API

            while (isDetectingInFrameArrivedThread)
            {
                //Wait detecting stop
            }

            lock (ExecuteOnMainThread)
            {
                ExecuteOnMainThread.Clear();
            }

#else

            StopThread();
            lock (ExecuteOnMainThread)
            {
                ExecuteOnMainThread.Clear();
            }
            isDetecting = false;

#endif

            hasUpdatedARTransformMatrix = false;

            if (ids != null)
                ids.Dispose();
            foreach (var item in corners)
            {
                item.Dispose();
            }
            corners.Clear();
            foreach (var item in rejectedCorners)
            {
                item.Dispose();
            }
            rejectedCorners.Clear();
            if (rvecs != null)
                rvecs.Dispose();
            if (tvecs != null)
                tvecs.Dispose();
            if (rotMat != null)
                rotMat.Dispose();

            if (rgbMat4preview != null)
                rgbMat4preview.Dispose();

            if (debugStr != null)
            {
                debugStr.text = string.Empty;
            }
            DebugUtils.ClearDebugStr();
        }

        /// <summary>
        /// Raises the web cam texture to mat helper error occurred event.
        /// </summary>
        /// <param name="errorCode">Error code.</param>
        public void OnWebCamTextureToMatHelperErrorOccurred(WebCamTextureToMatHelper.ErrorCode errorCode)
        {
            Debug.Log("OnWebCamTextureToMatHelperErrorOccurred " + errorCode);
        }

#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
        public void OnFrameMatAcquired(Mat grayMat, Matrix4x4 projectionMatrix, Matrix4x4 cameraToWorldMatrix, CameraIntrinsics cameraIntrinsics)
        {
            isDetectingInFrameArrivedThread = true;

            DebugUtils.VideoTick();

            Mat downScaleMat = null;
            float DOWNSCALE_RATIO;
            if (enableDownScale)
            {
                downScaleMat = imageOptimizationHelper.GetDownScaleMat(grayMat);
                DOWNSCALE_RATIO = imageOptimizationHelper.downscaleRatio;
            }
            else
            {
                downScaleMat = grayMat;
                DOWNSCALE_RATIO = 1.0f;
            }

            Mat camMatrix = null;
            MatOfDouble distCoeffs = null;
            if (useStoredCameraParameters)
            {
                camMatrix = this.camMatrix;
                distCoeffs = this.distCoeffs;
            }
            else
            {
                camMatrix = CreateCameraMatrix(cameraIntrinsics.FocalLengthX, cameraIntrinsics.FocalLengthY, cameraIntrinsics.PrincipalPointX / DOWNSCALE_RATIO, cameraIntrinsics.PrincipalPointY / DOWNSCALE_RATIO);
                distCoeffs = new MatOfDouble(cameraIntrinsics.RadialDistK1, cameraIntrinsics.RadialDistK2, cameraIntrinsics.RadialDistK3, cameraIntrinsics.TangentialDistP1, cameraIntrinsics.TangentialDistP2);
            }

            if (enableDetection)
            {
                // Detect markers and estimate Pose

                // undistort image.
                Calib3d.undistort(downScaleMat, downScaleMat, camMatrix, distCoeffs);
                // detect markers.
                Aruco.detectMarkers(downScaleMat, dictionary, corners, ids, detectorParams, rejectedCorners);

                if (applyEstimationPose && ids.total() > 0)
                {
                    Aruco.estimatePoseSingleMarkers(corners, markerLength, camMatrix, distCoeffs, rvecs, tvecs);

                    for (int i = 0; i < ids.total(); i++)
                    {
                        //This example can display ARObject on only first detected marker.
                        if (i == 0)
                        {
                            // Convert to unity pose data.
                            double[] rvecArr = new double[3];
                            rvecs.get(0, 0, rvecArr);
                            double[] tvecArr = new double[3];
                            tvecs.get(0, 0, tvecArr);
                            tvecArr[2] /= DOWNSCALE_RATIO;
                            PoseData poseData = ARUtils.ConvertRvecTvecToPoseData(rvecArr, tvecArr);

                            // Create transform matrix.
                            transformationM = Matrix4x4.TRS(poseData.pos, poseData.rot, Vector3.one);

                            lock (sync)
                            {
                                // Right-handed coordinates system (OpenCV) to left-handed one (Unity)
                                // https://stackoverflow.com/questions/30234945/change-handedness-of-a-row-major-4x4-transformation-matrix
                                ARM = invertYM * transformationM * invertYM;

                                // Apply Y-axis and Z-axis refletion matrix. (Adjust the posture of the AR object)
                                ARM = ARM * invertYM * invertZM;
                            }

                            hasUpdatedARTransformMatrix = true;

                            break;
                        }
                    }
                }
            }

            Mat rgbMat4preview = null;
            if (displayCameraPreview)
            {
                rgbMat4preview = new Mat();
                Imgproc.cvtColor(downScaleMat, rgbMat4preview, Imgproc.COLOR_GRAY2RGB);

                if (ids.total() > 0)
                {
                    Aruco.drawDetectedMarkers(rgbMat4preview, corners, ids, new Scalar(0, 255, 0));

                    if (applyEstimationPose)
                    {
                        for (int i = 0; i < ids.total(); i++)
                        {
                            using (Mat rvec = new Mat(rvecs, new OpenCVForUnity.CoreModule.Rect(0, i, 1, 1)))
                            using (Mat tvec = new Mat(tvecs, new OpenCVForUnity.CoreModule.Rect(0, i, 1, 1)))
                            {
                                // In this example we are processing with RGB color image, so Axis-color correspondences are X: blue, Y: green, Z: red. (Usually X: red, Y: green, Z: blue)
                                Calib3d.drawFrameAxes(rgbMat4preview, camMatrix, distCoeffs, rvec, tvec, markerLength * 0.5f);
                            }
                        }
                    }
                }
            }

            if (!useStoredCameraParameters)
            {
                camMatrix.Dispose();
                distCoeffs.Dispose();
            }

            DebugUtils.TrackTick();

            Enqueue(() =>
            {
                if (!webCamTextureToMatHelper.IsPlaying()) return;

                if (displayCameraPreview && rgbMat4preview != null)
                {
                    Utils.matToTexture2D(rgbMat4preview, texture);
                    rgbMat4preview.Dispose();
                }

                if (applyEstimationPose)
                {
                    if (hasUpdatedARTransformMatrix)
                    {
                        hasUpdatedARTransformMatrix = false;

                        lock (sync)
                        {
                            Matrix4x4 localToWorldMatrix = cameraToWorldMatrix * invertZM;
                            ARM = localToWorldMatrix * ARM;

                            if (enableLerpFilter)
                            {
                                arGameObject.SetMatrix4x4(ARM);
                            }
                            else
                            {
                                ARUtils.SetTransformFromMatrix(arGameObject.transform, ref ARM);
                            }
                        }
                    }
                }

                grayMat.Dispose();
            });

            isDetectingInFrameArrivedThread = false;
        }

        private void Update()
        {
            lock (ExecuteOnMainThread)
            {
                while (ExecuteOnMainThread.Count > 0)
                {
                    ExecuteOnMainThread.Dequeue().Invoke();
                }
            }
        }

        private void Enqueue(Action action)
        {
            lock (ExecuteOnMainThread)
            {
                ExecuteOnMainThread.Enqueue(action);
            }
        }

#else

        // Update is called once per frame
        void Update()
        {
            lock (ExecuteOnMainThread)
            {
                while (ExecuteOnMainThread.Count > 0)
                {
                    ExecuteOnMainThread.Dequeue().Invoke();
                }
            }

            if (webCamTextureToMatHelper.IsPlaying() && webCamTextureToMatHelper.DidUpdateThisFrame())
            {
                DebugUtils.VideoTick();

                if (enableDetection && !isDetecting)
                {
                    isDetecting = true;

                    Mat grayMat = webCamTextureToMatHelper.GetMat();

                    if (enableDownScale)
                    {
                        downScaleMat = imageOptimizationHelper.GetDownScaleMat(grayMat);
                        DOWNSCALE_RATIO = imageOptimizationHelper.downscaleRatio;
                    }
                    else
                    {
                        downScaleMat = grayMat;
                        DOWNSCALE_RATIO = 1.0f;
                    }

                    StartThread(ThreadWorker);
                }
            }
        }

        private void StartThread(Action action)
        {
#if WINDOWS_UWP || (!UNITY_WSA_10_0 && (NET_4_6 || NET_STANDARD_2_0))
            System.Threading.Tasks.Task.Run(() => action());
#else
            ThreadPool.QueueUserWorkItem(_ => action());
#endif
        }

        private void StopThread()
        {
            if (!isThreadRunning)
                return;

            while (isThreadRunning)
            {
                //Wait threading stop
            }
        }

        private void ThreadWorker()
        {
            isThreadRunning = true;

            DetectARUcoMarker();

            lock (ExecuteOnMainThread)
            {
                if (ExecuteOnMainThread.Count == 0)
                {
                    ExecuteOnMainThread.Enqueue(() =>
                    {
                        OnDetectionDone();
                    });
                }
            }

            isThreadRunning = false;
        }

        private void DetectARUcoMarker()
        {
            // Detect markers and estimate Pose

            // undistort image.
            Calib3d.undistort(downScaleMat, downScaleMat, camMatrix, distCoeffs);
            // detect markers.
            Aruco.detectMarkers(downScaleMat, dictionary, corners, ids, detectorParams, rejectedCorners);

            if (applyEstimationPose && ids.total() > 0)
            {
                Aruco.estimatePoseSingleMarkers(corners, markerLength, camMatrix, distCoeffs, rvecs, tvecs);

                for (int i = 0; i < ids.total(); i++)
                {
                    //This example can display ARObject on only first detected marker.
                    if (i == 0)
                    {
                        // Convert to unity pose data.
                        double[] rvecArr = new double[3];
                        rvecs.get(0, 0, rvecArr);
                        double[] tvecArr = new double[3];
                        tvecs.get(0, 0, tvecArr);
                        tvecArr[2] /= DOWNSCALE_RATIO;
                        PoseData poseData = ARUtils.ConvertRvecTvecToPoseData(rvecArr, tvecArr);

                        // Create transform matrix.
                        transformationM = Matrix4x4.TRS(poseData.pos, poseData.rot, Vector3.one);

                        // Right-handed coordinates system (OpenCV) to left-handed one (Unity)
                        // https://stackoverflow.com/questions/30234945/change-handedness-of-a-row-major-4x4-transformation-matrix
                        ARM = invertYM * transformationM * invertYM;

                        // Apply Y-axis and Z-axis refletion matrix. (Adjust the posture of the AR object)
                        ARM = ARM * invertYM * invertZM;

                        hasUpdatedARTransformMatrix = true;

                        break;
                    }
                }
            }
        }

        private void OnDetectionDone()
        {
            DebugUtils.TrackTick();

            if (displayCameraPreview)
            {
                Imgproc.cvtColor(downScaleMat, rgbMat4preview, Imgproc.COLOR_GRAY2RGB);

                if (ids.total() > 0)
                {
                    Aruco.drawDetectedMarkers(rgbMat4preview, corners, ids, new Scalar(0, 255, 0));

                    if (applyEstimationPose)
                    {
                        for (int i = 0; i < ids.total(); i++)
                        {
                            using (Mat rvec = new Mat(rvecs, new OpenCVForUnity.CoreModule.Rect(0, i, 1, 1)))
                            using (Mat tvec = new Mat(tvecs, new OpenCVForUnity.CoreModule.Rect(0, i, 1, 1)))
                            {
                                // In this example we are processing with RGB color image, so Axis-color correspondences are X: blue, Y: green, Z: red. (Usually X: red, Y: green, Z: blue)
                                Calib3d.drawFrameAxes(rgbMat4preview, camMatrix, distCoeffs, rvec, tvec, markerLength * 0.5f);
                            }
                        }
                    }
                }

                Utils.matToTexture2D(rgbMat4preview, texture);
            }

            if (applyEstimationPose)
            {
                if (hasUpdatedARTransformMatrix)
                {
                    hasUpdatedARTransformMatrix = false;

                    Matrix4x4 localToWorldMatrix = arCamera.cameraToWorldMatrix * invertZM;
                    ARM = localToWorldMatrix * ARM;

                    if (enableLerpFilter)
                    {
                        arGameObject.SetMatrix4x4(ARM);
                    }
                    else
                    {
                        ARUtils.SetTransformFromMatrix(arGameObject.transform, ref ARM);
                    }
                }
            }

            isDetecting = false;
        }
#endif

        private Mat CreateCameraMatrix(double fx, double fy, double cx, double cy)
        {
            Mat camMatrix = new Mat(3, 3, CvType.CV_64FC1);
            camMatrix.put(0, 0, fx);
            camMatrix.put(0, 1, 0);
            camMatrix.put(0, 2, cx);
            camMatrix.put(1, 0, 0);
            camMatrix.put(1, 1, fy);
            camMatrix.put(1, 2, cy);
            camMatrix.put(2, 0, 0);
            camMatrix.put(2, 1, 0);
            camMatrix.put(2, 2, 1.0f);

            return camMatrix;
        }

        void LateUpdate()
        {
            DebugUtils.RenderTick();
            float renderDeltaTime = DebugUtils.GetRenderDeltaTime();
            float videoDeltaTime = DebugUtils.GetVideoDeltaTime();
            float trackDeltaTime = DebugUtils.GetTrackDeltaTime();

            if (renderFPS != null)
            {
                renderFPS.text = string.Format("Render: {0:0.0} ms ({1:0.} fps)", renderDeltaTime, 1000.0f / renderDeltaTime);
            }
            if (videoFPS != null)
            {
                videoFPS.text = string.Format("Video: {0:0.0} ms ({1:0.} fps)", videoDeltaTime, 1000.0f / videoDeltaTime);
            }
            if (trackFPS != null)
            {
                trackFPS.text = string.Format("Track:   {0:0.0} ms ({1:0.} fps)", trackDeltaTime, 1000.0f / trackDeltaTime);
            }
            if (debugStr != null)
            {
                if (DebugUtils.GetDebugStrLength() > 0)
                {
                    if (debugStr.preferredHeight >= debugStr.rectTransform.rect.height)
                        debugStr.text = string.Empty;

                    debugStr.text += DebugUtils.GetDebugStr();
                    DebugUtils.ClearDebugStr();
                }
            }
        }

        /// <summary>
        /// Raises the destroy event.
        /// </summary>
        void OnDestroy()
        {
#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            webCamTextureToMatHelper.frameMatAcquired -= OnFrameMatAcquired;
#endif
            webCamTextureToMatHelper.Dispose();
            imageOptimizationHelper.Dispose();
        }

        /// <summary>
        /// Raises the back button click event.
        /// </summary>
        public void OnBackButtonClick()
        {
            SceneManager.LoadScene("HoloLensWithOpenCVForUnityExample");
        }

        /// <summary>
        /// Raises the play button click event.
        /// </summary>
        public void OnPlayButtonClick()
        {
            webCamTextureToMatHelper.Play();
        }

        /// <summary>
        /// Raises the pause button click event.
        /// </summary>
        public void OnPauseButtonClick()
        {
            webCamTextureToMatHelper.Pause();
        }

        /// <summary>
        /// Raises the stop button click event.
        /// </summary>
        public void OnStopButtonClick()
        {
            webCamTextureToMatHelper.Stop();
        }

        /// <summary>
        /// Raises the change camera button click event.
        /// </summary>
        public void OnChangeCameraButtonClick()
        {
            webCamTextureToMatHelper.requestedIsFrontFacing = !webCamTextureToMatHelper.IsFrontFacing();
        }

        /// <summary>
        /// Raises the display camera preview toggle value changed event.
        /// </summary>
        public void OnDisplayCamreaPreviewToggleValueChanged()
        {
            displayCameraPreview = displayCameraPreviewToggle.isOn;

            previewQuad.SetActive(displayCameraPreview);
        }

        /// <summary>
        /// Raises the use stored camera parameters toggle value changed event.
        /// </summary>
        public void OnUseStoredCameraParametersToggleValueChanged()
        {
            useStoredCameraParameters = useStoredCameraParametersToggle.isOn;

            if (webCamTextureToMatHelper != null && webCamTextureToMatHelper.IsInitialized())
            {
                webCamTextureToMatHelper.Initialize();
            }
        }

        /// <summary>
        /// Raises the enable downscale toggle value changed event.
        /// </summary>
        public void OnEnableDownScaleToggleValueChanged()
        {
            enableDownScale = enableDownScaleToggle.isOn;

            if (webCamTextureToMatHelper != null && webCamTextureToMatHelper.IsInitialized())
            {
                webCamTextureToMatHelper.Initialize();
            }
        }

        /// <summary>
        /// Raises the enable lerp filter toggle value changed event.
        /// </summary>
        public void OnEnableLerpFilterToggleValueChanged()
        {
            enableLerpFilter = enableLerpFilterToggle.isOn;
        }
    }
}
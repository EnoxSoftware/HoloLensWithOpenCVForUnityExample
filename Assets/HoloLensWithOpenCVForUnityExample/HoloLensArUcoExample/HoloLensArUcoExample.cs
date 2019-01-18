using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Xml.Serialization;
using HoloToolkit.Unity.InputModule;
using OpenCVForUnity.ArucoModule;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityUtils.Helper;
using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using HoloLensWithOpenCVForUnity.UnityUtils.Helper;

namespace HoloLensWithOpenCVForUnityExample
{
    /// <summary>
    /// HoloLens ArUco Example
    /// An example of marker based AR using OpenCVForUnity on Hololens.
    /// Referring to https://github.com/opencv/opencv_contrib/blob/master/modules/aruco/samples/detect_markers.cpp.
    /// </summary>
    [RequireComponent(typeof(HololensCameraStreamToMatHelper))]
    public class HoloLensArUcoExample : ExampleSceneBase
    {
        [HeaderAttribute ("Preview")]

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


        [HeaderAttribute ("Detection")]

        /// <summary>
        /// Determines if restores the camera parameters when the file exists.
        /// </summary>
        public bool useStoredCameraParameters = false;

        /// <summary>
        /// The toggle for switching to use the stored camera parameters.
        /// </summary>
        public Toggle useStoredCameraParametersToggle;

        /// <summary>
        /// Determines if enables the detection.
        /// </summary>
        public bool enableDetection = true;


        [HeaderAttribute ("AR")]

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
        public GameObject arGameObject;

        /// <summary>
        /// The AR camera.
        /// </summary>
        public Camera arCamera;

        [Space(10)]

        /// <summary>
        /// Determines if enable low pass filter.
        /// </summary>
        public bool enableLowPassFilter;

        /// <summary>
        /// The enable low pass filter toggle.
        /// </summary>
        public Toggle enableLowPassFilterToggle;

        /// <summary>
        /// The position low pass. (Value in meters)
        /// </summary>
        public float positionLowPass = 0.025f;

        /// <summary>
        /// The rotation low pass. (Value in degrees)
        /// </summary>
        public float rotationLowPass = 3f;

        /// <summary>
        /// The old pose data.
        /// </summary>
        PoseData oldPoseData;

        /// <summary>
        /// The cameraparam matrix.
        /// </summary>
        Mat camMatrix;

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
        HololensCameraStreamToMatHelper webCamTextureToMatHelper;

        /// <summary>
        /// The image optimization helper.
        /// </summary>
        ImageOptimizationHelper imageOptimizationHelper;

        Mat grayMat;
        Mat rgbMat4preview;
        Texture2D texture;

        // The camera matrix value of Hololens camera 896x504 size.
        // For details on the camera matrix, please refer to this page. (http://docs.opencv.org/2.4/modules/calib3d/doc/camera_calibration_and_3d_reconstruction.html)
        // These values ​​are unique to my device, obtained from the "Windows.Media.Devices.Core.CameraIntrinsics" class. (https://docs.microsoft.com/en-us/uwp/api/windows.media.devices.core.cameraintrinsics)
        // Can get these values by using this helper script. (https://github.com/EnoxSoftware/HoloLensWithOpenCVForUnityExample/tree/master/Assets/HololensCameraIntrinsicsChecker/CameraIntrinsicsCheckerHelper)
        double fx = 1035.149;//focal length x.
        double fy = 1034.633;//focal length y.
        double cx = 404.9134;//principal point x.
        double cy = 236.2834;//principal point y.
        MatOfDouble distCoeffs;
        double distCoeffs1 = 0.2036923;//radial distortion coefficient k1.
        double distCoeffs2 = -0.2035773;//radial distortion coefficient k2.
        double distCoeffs3 = 0.0;//tangential distortion coefficient p1.
        double distCoeffs4 = 0.0;//tangential distortion coefficient p2.
        double distCoeffs5 = -0.2388065;//radial distortion coefficient k3.

        readonly static Queue<Action> ExecuteOnMainThread = new Queue<Action>();
        System.Object sync = new System.Object ();
        Mat downScaleFrameMat;

        bool _isThreadRunning = false;
        bool isThreadRunning {
            get { lock (sync)
                return _isThreadRunning; }
            set { lock (sync)
                _isThreadRunning = value; }
        }

        bool _isDetecting = false;
        bool isDetecting {
            get { lock (sync)
                return _isDetecting; }
            set { lock (sync)
                _isDetecting = value; }
        }

        bool _hasUpdatedARTransformMatrix = false;
        bool hasUpdatedARTransformMatrix {
            get { lock (sync)
                return _hasUpdatedARTransformMatrix; }
            set { lock (sync)
                _hasUpdatedARTransformMatrix = value; }
        }

        // Use this for initialization
        protected override void Start ()
        {
            base.Start ();

            displayCameraPreviewToggle.isOn = displayCameraPreview;
            useStoredCameraParametersToggle.isOn = useStoredCameraParameters;
            enableLowPassFilterToggle.isOn = enableLowPassFilter;

            imageOptimizationHelper = gameObject.GetComponent<ImageOptimizationHelper> ();
            webCamTextureToMatHelper = gameObject.GetComponent<HololensCameraStreamToMatHelper> ();
            #if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            webCamTextureToMatHelper.frameMatAcquired += OnFrameMatAcquired;
            #endif

            webCamTextureToMatHelper.Initialize ();
        }

        /// <summary>
        /// Raises the web cam texture to mat helper initialized event.
        /// </summary>
        public void OnWebCamTextureToMatHelperInitialized ()
        {
            Debug.Log ("OnWebCamTextureToMatHelperInitialized");

            Mat rawSizeMat = webCamTextureToMatHelper.GetMat ();
            float rawSizeWidth = rawSizeMat.width();
            float rawSizeHeight = rawSizeMat.height();

            Mat webCamTextureMat = imageOptimizationHelper.GetDownScaleMat(rawSizeMat);

            Debug.Log ("Screen.width " + Screen.width + " Screen.height " + Screen.height + " Screen.orientation " + Screen.orientation);

            float width = webCamTextureMat.width();
            float height = webCamTextureMat.height();

            texture = new Texture2D ((int)width, (int)height, TextureFormat.RGB24, false);

            previewQuad.GetComponent<MeshRenderer>().material.mainTexture = texture;
            previewQuad.transform.localScale = new Vector3 (1, height/width, 1);
            previewQuad.SetActive (displayCameraPreview);


            // set camera parameters.
            double fx;
            double fy;
            double cx;
            double cy;

            string loadDirectoryPath = Path.Combine (Application.persistentDataPath, "HoloLensArUcoCameraCalibrationExample");
            string calibratonDirectoryName = "camera_parameters" + rawSizeWidth + "x" + rawSizeHeight;
            string loadCalibratonFileDirectoryPath = Path.Combine (loadDirectoryPath, calibratonDirectoryName);
            string loadPath = Path.Combine (loadCalibratonFileDirectoryPath, calibratonDirectoryName + ".xml");
            if (useStoredCameraParameters && File.Exists (loadPath)) {
                CameraParameters param;
                XmlSerializer serializer = new XmlSerializer( typeof( CameraParameters ) );
                using (var stream = new FileStream (loadPath, FileMode.Open)) {
                    param = (CameraParameters)serializer.Deserialize (stream);
                }
                  
                fx = param.camera_matrix[0];
                fy = param.camera_matrix[4];
                cx = param.camera_matrix[2] / imageOptimizationHelper.downscaleRatio;
                cy = param.camera_matrix[5] / imageOptimizationHelper.downscaleRatio;

                camMatrix = new Mat (3, 3, CvType.CV_64FC1);
                camMatrix.put (0, 0, fx);
                camMatrix.put (0, 1, 0);
                camMatrix.put (0, 2, cx);
                camMatrix.put (1, 0, 0);
                camMatrix.put (1, 1, fy);
                camMatrix.put (1, 2, cy);
                camMatrix.put (2, 0, 0);
                camMatrix.put (2, 1, 0);
                camMatrix.put (2, 2, 1.0f);

                distCoeffs = new MatOfDouble(param.GetDistortionCoefficients ());

                Debug.Log ("Loaded CameraParameters from a stored XML file.");
                Debug.Log ("loadPath: " + loadPath);

            } else {
                fx = this.fx;
                fy = this.fy;
                cx = this.cx / imageOptimizationHelper.downscaleRatio;
                cy = this.cy / imageOptimizationHelper.downscaleRatio;

                camMatrix = new Mat (3, 3, CvType.CV_64FC1);
                camMatrix.put (0, 0, fx);
                camMatrix.put (0, 1, 0);
                camMatrix.put (0, 2, cx);
                camMatrix.put (1, 0, 0);
                camMatrix.put (1, 1, fy);
                camMatrix.put (1, 2, cy);
                camMatrix.put (2, 0, 0);
                camMatrix.put (2, 1, 0);
                camMatrix.put (2, 2, 1.0f);

                distCoeffs = new MatOfDouble (this.distCoeffs1, this.distCoeffs2, this.distCoeffs3, this.distCoeffs4, this.distCoeffs5);

                Debug.Log ("Created a dummy CameraParameters.");
            }

            Debug.Log ("camMatrix " + camMatrix.dump ());
            Debug.Log ("distCoeffs " + distCoeffs.dump ());


            //Calibration camera
            Size imageSize = new Size (width, height);
            double apertureWidth = 0;
            double apertureHeight = 0;
            double[] fovx = new double[1];
            double[] fovy = new double[1];
            double[] focalLength = new double[1];
            Point principalPoint = new Point (0, 0);
            double[] aspectratio = new double[1];

            Calib3d.calibrationMatrixValues (camMatrix, imageSize, apertureWidth, apertureHeight, fovx, fovy, focalLength, principalPoint, aspectratio);

            Debug.Log ("imageSize " + imageSize.ToString ());
            Debug.Log ("apertureWidth " + apertureWidth);
            Debug.Log ("apertureHeight " + apertureHeight);
            Debug.Log ("fovx " + fovx [0]);
            Debug.Log ("fovy " + fovy [0]);
            Debug.Log ("focalLength " + focalLength [0]);
            Debug.Log ("principalPoint " + principalPoint.ToString ());
            Debug.Log ("aspectratio " + aspectratio [0]);

            // Display objects near the camera.
            arCamera.nearClipPlane = 0.01f;

            grayMat = new Mat ();
            ids = new Mat ();
            corners = new List<Mat> ();
            rejectedCorners = new List<Mat> ();
            rvecs = new Mat ();
            tvecs = new Mat ();
            rotMat = new Mat (3, 3, CvType.CV_64FC1);


            transformationM = new Matrix4x4 ();

            invertYM = Matrix4x4.TRS (Vector3.zero, Quaternion.identity, new Vector3 (1, -1, 1));
            Debug.Log ("invertYM " + invertYM.ToString ());

            invertZM = Matrix4x4.TRS (Vector3.zero, Quaternion.identity, new Vector3 (1, 1, -1));
            Debug.Log ("invertZM " + invertZM.ToString ());

            detectorParams = DetectorParameters.create ();
            dictionary = Aruco.getPredefinedDictionary (Aruco.DICT_6X6_250);


            //If WebCamera is frontFaceing,flip Mat.
            if (webCamTextureToMatHelper.GetWebCamDevice ().isFrontFacing) {
                webCamTextureToMatHelper.flipHorizontal = true;
            }
                
            downScaleFrameMat = new Mat ((int)height, (int)width, CvType.CV_8UC4);
            rgbMat4preview = new Mat ();
        }

        /// <summary>
        /// Raises the web cam texture to mat helper disposed event.
        /// </summary>
        public void OnWebCamTextureToMatHelperDisposed ()
        {
            Debug.Log ("OnWebCamTextureToMatHelperDisposed");

            #if !WINDOWS_UWP || DISABLE_HOLOLENSCAMSTREAM_API
            StopThread();
            lock (ExecuteOnMainThread) {
                ExecuteOnMainThread.Clear ();
            }
            isDetecting = false;
            #endif
            hasUpdatedARTransformMatrix = false;

            if (grayMat != null)
                grayMat.Dispose ();
            if (ids != null)
                ids.Dispose ();
            foreach (var item in corners) {
                item.Dispose ();
            }
            corners.Clear ();
            foreach (var item in rejectedCorners) {
                item.Dispose ();
            }
            rejectedCorners.Clear ();
            if (rvecs != null)
                rvecs.Dispose ();
            if (tvecs != null)
                tvecs.Dispose ();
            if (rotMat != null)
                rotMat.Dispose ();

            if (rgbMat4preview != null)
                rgbMat4preview.Dispose ();
        }

        /// <summary>
        /// Raises the web cam texture to mat helper error occurred event.
        /// </summary>
        /// <param name="errorCode">Error code.</param>
        public void OnWebCamTextureToMatHelperErrorOccurred(WebCamTextureToMatHelper.ErrorCode errorCode){
            Debug.Log ("OnWebCamTextureToMatHelperErrorOccurred " + errorCode);
        }

        #if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
        public void OnFrameMatAcquired (Mat bgraMat, Matrix4x4 projectionMatrix, Matrix4x4 cameraToWorldMatrix)
        {
            downScaleFrameMat = imageOptimizationHelper.GetDownScaleMat (bgraMat);

            if (enableDetection) {

                Imgproc.cvtColor (downScaleFrameMat, grayMat, Imgproc.COLOR_BGRA2GRAY);

                // Detect markers and estimate Pose
                Aruco.detectMarkers (grayMat, dictionary, corners, ids, detectorParams, rejectedCorners, camMatrix, distCoeffs);

                if (applyEstimationPose && ids.total () > 0){
                    Aruco.estimatePoseSingleMarkers (corners, markerLength, camMatrix, distCoeffs, rvecs, tvecs);

                    for (int i = 0; i < ids.total (); i++) {

                        //This example can display ARObject on only first detected marker.
                        if (i == 0) {

                            // Convert to unity pose data.
                            double[] rvecArr = new double[3];
                            rvecs.get (0, 0, rvecArr);
                            double[] tvecArr = new double[3];
                            tvecs.get (0, 0, tvecArr);
                            tvecArr[2] /= imageOptimizationHelper.downscaleRatio;
                            PoseData poseData = ARUtils.ConvertRvecTvecToPoseData (rvecArr, tvecArr);

                            // Changes in pos/rot below these thresholds are ignored.
                            if (enableLowPassFilter) {
                                ARUtils.LowpassPoseData (ref oldPoseData, ref poseData, positionLowPass, rotationLowPass);
                            }
                            oldPoseData = poseData;

                            // Create transform matrix.
                            transformationM = Matrix4x4.TRS (poseData.pos, poseData.rot, Vector3.one);

                            lock (sync){
                                // Right-handed coordinates system (OpenCV) to left-handed one (Unity)
                                ARM = invertYM * transformationM;

                                // Apply Z-axis inverted matrix.
                                ARM = ARM * invertZM;
                            }

                            hasUpdatedARTransformMatrix = true;

                            break;
                        }
                    }
                }
            }

            Mat rgbMat4preview = null;
            if (displayCameraPreview) {
                rgbMat4preview = new Mat ();
                Imgproc.cvtColor (downScaleFrameMat, rgbMat4preview, Imgproc.COLOR_BGRA2RGB);

                if (ids.total () > 0) {
                    Aruco.drawDetectedMarkers (rgbMat4preview, corners, ids, new Scalar (0, 255, 0));

                    if (applyEstimationPose) {
                        for (int i = 0; i < ids.total (); i++) {
                            using (Mat rvec = new Mat (rvecs, new OpenCVForUnity.CoreModule.Rect(0, i, 1, 1)))
                            using (Mat tvec = new Mat (tvecs, new OpenCVForUnity.CoreModule.Rect(0, i, 1, 1))) {

                                // In this example we are processing with RGB color image, so Axis-color correspondences are X: blue, Y: green, Z: red. (Usually X: red, Y: green, Z: blue)
                                Aruco.drawAxis (rgbMat4preview, camMatrix, distCoeffs, rvec, tvec, markerLength * 0.5f);
                            }
                        }
                    }
                }
            }

            Enqueue(() => {

                if (!webCamTextureToMatHelper.IsPlaying ()) return;

                if (displayCameraPreview && rgbMat4preview != null) {
                    Utils.fastMatToTexture2D (rgbMat4preview, texture);
                }

                if (applyEstimationPose) {
                    if (hasUpdatedARTransformMatrix) {
                        hasUpdatedARTransformMatrix = false;

                        lock (sync){
                            // Apply camera transform matrix.
                            ARM = cameraToWorldMatrix * invertZM * ARM;

                            ARUtils.SetTransformFromMatrix (arGameObject.transform, ref ARM);
                        }
                    }
                }

                bgraMat.Dispose ();
                if (rgbMat4preview != null){
                    rgbMat4preview.Dispose();
                }

            });
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
        void Update ()
        {
            lock (ExecuteOnMainThread) {
                while (ExecuteOnMainThread.Count > 0) {
                    ExecuteOnMainThread.Dequeue ().Invoke ();
                }
            }

            if (webCamTextureToMatHelper.IsPlaying () && webCamTextureToMatHelper.DidUpdateThisFrame ()) {
                
                if (enableDetection && !isDetecting ) {
                    isDetecting = true;

                    downScaleFrameMat = imageOptimizationHelper.GetDownScaleMat (webCamTextureToMatHelper.GetMat ());

                    StartThread (ThreadWorker);
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

        private void StopThread ()
        {
            if (!isThreadRunning)
                return;

            while (isThreadRunning) {
                //Wait threading stop
            } 
        }

        private void ThreadWorker()
        {
            isThreadRunning = true;

            DetectARUcoMarker ();

            lock (ExecuteOnMainThread) {
                if (ExecuteOnMainThread.Count == 0) {
                    ExecuteOnMainThread.Enqueue (() => {
                        OnDetectionDone ();
                    });
                }
            }

            isThreadRunning = false;
        }

        private void DetectARUcoMarker()
        {
            Imgproc.cvtColor (downScaleFrameMat, grayMat, Imgproc.COLOR_RGBA2GRAY);            

            // Detect markers and estimate Pose
            Aruco.detectMarkers (grayMat, dictionary, corners, ids, detectorParams, rejectedCorners, camMatrix, distCoeffs);

            if (applyEstimationPose && ids.total () > 0){
                Aruco.estimatePoseSingleMarkers (corners, markerLength, camMatrix, distCoeffs, rvecs, tvecs);

                for (int i = 0; i < ids.total (); i++) {

                    //This example can display ARObject on only first detected marker.
                    if (i == 0) {

                        // Convert to unity pose data.
                        double[] rvecArr = new double[3];
                        rvecs.get (0, 0, rvecArr);
                        double[] tvecArr = new double[3];
                        tvecs.get (0, 0, tvecArr);
                        tvecArr[2] /= imageOptimizationHelper.downscaleRatio;
                        PoseData poseData = ARUtils.ConvertRvecTvecToPoseData (rvecArr, tvecArr);

                        // Changes in pos/rot below these thresholds are ignored.
                        if (enableLowPassFilter) {
                            ARUtils.LowpassPoseData (ref oldPoseData, ref poseData, positionLowPass, rotationLowPass);
                        }
                        oldPoseData = poseData;

                        // Create transform matrix.
                        transformationM = Matrix4x4.TRS (poseData.pos, poseData.rot, Vector3.one);

                        // Right-handed coordinates system (OpenCV) to left-handed one (Unity)
                        ARM = invertYM * transformationM;

                        // Apply Z-axis inverted matrix.
                        ARM = ARM * invertZM;

                        hasUpdatedARTransformMatrix = true;

                        break;
                    }
                }
            }
        }

        private void OnDetectionDone()
        {
            if (displayCameraPreview) {
                Imgproc.cvtColor (downScaleFrameMat, rgbMat4preview, Imgproc.COLOR_RGBA2RGB);

                if (ids.total () > 0) {
                    Aruco.drawDetectedMarkers (rgbMat4preview, corners, ids, new Scalar (0, 255, 0));

                    if (applyEstimationPose) {
                        for (int i = 0; i < ids.total (); i++) {
                            using (Mat rvec = new Mat (rvecs, new OpenCVForUnity.CoreModule.Rect(0, i, 1, 1)))
                            using (Mat tvec = new Mat (tvecs, new OpenCVForUnity.CoreModule.Rect(0, i, 1, 1))) {

                                // In this example we are processing with RGB color image, so Axis-color correspondences are X: blue, Y: green, Z: red. (Usually X: red, Y: green, Z: blue)
                                Aruco.drawAxis (rgbMat4preview, camMatrix, distCoeffs, rvec, tvec, markerLength * 0.5f);
                            }
                        }
                    }
                }

                Utils.fastMatToTexture2D (rgbMat4preview, texture);
            }

            if (applyEstimationPose) {
                if (hasUpdatedARTransformMatrix) {
                    hasUpdatedARTransformMatrix = false;

                    // Apply the cameraToWorld matrix with the Z-axis inverted.         
                    ARM = arCamera.cameraToWorldMatrix * invertZM * ARM;

                    ARUtils.SetTransformFromMatrix (arGameObject.transform, ref ARM);
                }
            }

            isDetecting = false;
        }
        #endif
            
        /// <summary>
        /// Raises the destroy event.
        /// </summary>
        void OnDestroy ()
        {
            imageOptimizationHelper.Dispose ();
            #if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            webCamTextureToMatHelper.frameMatAcquired -= OnFrameMatAcquired;
            #endif
            webCamTextureToMatHelper.Dispose ();
        }

        /// <summary>
        /// Raises the back button click event.
        /// </summary>
        public void OnBackButtonClick ()
        {
            LoadScene ("HoloLensWithOpenCVForUnityExample");
        }

        /// <summary>
        /// Raises the play button click event.
        /// </summary>
        public void OnPlayButtonClick ()
        {
            webCamTextureToMatHelper.Play ();
        }

        /// <summary>
        /// Raises the pause button click event.
        /// </summary>
        public void OnPauseButtonClick ()
        {
            webCamTextureToMatHelper.Pause ();
        }

        /// <summary>
        /// Raises the stop button click event.
        /// </summary>
        public void OnStopButtonClick ()
        {
            webCamTextureToMatHelper.Stop ();
        }

        /// <summary>
        /// Raises the change camera button click event.
        /// </summary>
        public void OnChangeCameraButtonClick ()
        {
            webCamTextureToMatHelper.requestedIsFrontFacing = !webCamTextureToMatHelper.IsFrontFacing ();
        }

        /// <summary>
        /// Raises the display camera preview toggle value changed event.
        /// </summary>
        public void OnDisplayCamreaPreviewToggleValueChanged ()
        {
            if (displayCameraPreviewToggle.isOn) {
                displayCameraPreview = true;
            } else {
                displayCameraPreview = false;
            }
            previewQuad.SetActive (displayCameraPreview);
        }

        /// <summary>
        /// Raises the use stored camera parameters toggle value changed event.
        /// </summary>
        public void OnUseStoredCameraParametersToggleValueChanged ()
        {
            if (useStoredCameraParametersToggle.isOn) {
                useStoredCameraParameters = true;
            } else {
                useStoredCameraParameters = false;
            }

            if (webCamTextureToMatHelper != null && webCamTextureToMatHelper.IsInitialized ()) {
                webCamTextureToMatHelper.Initialize ();
            }
        }

        /// <summary>
        /// Raises the enable low pass filter toggle value changed event.
        /// </summary>
        public void OnEnableLowPassFilterToggleValueChanged ()
        {
            if (enableLowPassFilterToggle.isOn) {
                enableLowPassFilter = true;
            } else {
                enableLowPassFilter = false;
            }
        }

        /// <summary>
        /// Raises the tapped event.
        /// </summary>
        public void OnTapped ()
        {
            // Determine if a Gaze pointer is over a GUI.
            if (GazeManager.Instance.HitObject != null && (GazeManager.Instance.HitObject.GetComponent<Button>() != null || GazeManager.Instance.HitObject.GetComponent<Toggle>() != null
                 || GazeManager.Instance.HitObject.GetComponent<Text>() != null || GazeManager.Instance.HitObject.GetComponent<Image>() != null))
            {
                return;
            }            

            if (applyEstimationPose) {
                applyEstimationPose = false;
                arCube.GetComponent<MeshRenderer> ().material.color = Color.gray;
            } else {
                applyEstimationPose = true;
                arCube.GetComponent<MeshRenderer> ().material.color = Color.red;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Threading;
using HoloLensWithOpenCVForUnity.UnityIntegration.Helper.SourceToMat;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Extensions;
using OpenCVForUnity.Extensions.AR;
using OpenCVForUnity.Extensions.SourceToMat;
using OpenCVForUnity.GeometryModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.UnityIntegration;
using OpenCVForUnity.UnityIntegration.Helper.AR;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
using HoloLensCameraStream;
#endif

namespace HoloLensWithOpenCVForUnityExample
{
    /// <summary>
    /// HoloLens ArUco Example
    /// An example of marker based AR using OpenCVForUnity on Hololens.
    /// Referring to https://github.com/opencv/opencv_contrib/blob/master/modules/aruco/samples/detect_markers.cpp.
    /// </summary>
    [RequireComponent(typeof(HLCameraStreamToMatHelper))]
    public class HLArUcoExample : MonoBehaviour
    {
        // Enums
        /// <summary>
        /// Marker type enum
        /// </summary>
        public enum MarkerType
        {
            CanonicalMarker,
            //GridBoard,
            //ChArUcoBoard,
            //ChArUcoDiamondMarker
        }

        /// <summary>
        /// ArUco dictionary enum
        /// </summary>
        public enum ArUcoDictionary
        {
            DICT_4X4_50 = Objdetect.DICT_4X4_50,
            DICT_4X4_100 = Objdetect.DICT_4X4_100,
            DICT_4X4_250 = Objdetect.DICT_4X4_250,
            DICT_4X4_1000 = Objdetect.DICT_4X4_1000,
            DICT_5X5_50 = Objdetect.DICT_5X5_50,
            DICT_5X5_100 = Objdetect.DICT_5X5_100,
            DICT_5X5_250 = Objdetect.DICT_5X5_250,
            DICT_5X5_1000 = Objdetect.DICT_5X5_1000,
            DICT_6X6_50 = Objdetect.DICT_6X6_50,
            DICT_6X6_100 = Objdetect.DICT_6X6_100,
            DICT_6X6_250 = Objdetect.DICT_6X6_250,
            DICT_6X6_1000 = Objdetect.DICT_6X6_1000,
            DICT_7X7_50 = Objdetect.DICT_7X7_50,
            DICT_7X7_100 = Objdetect.DICT_7X7_100,
            DICT_7X7_250 = Objdetect.DICT_7X7_250,
            DICT_7X7_1000 = Objdetect.DICT_7X7_1000,
            DICT_ARUCO_ORIGINAL = Objdetect.DICT_ARUCO_ORIGINAL,
        }

        // Public Fields
        [HeaderAttribute("Preview")]
        public GameObject PreviewQuad;
        public Toggle DisplayCameraPreviewToggle;
        public bool DisplayCameraPreview;

        [HeaderAttribute("Detection")]
        public bool EnableDetection = true;
        public Toggle EnableDownScaleToggle;
        public bool EnableDownScale;

        [Tooltip("Ratio used to downscale the detection Mat when EnableDownScale is on.")]
        public float DownscaleRatio = 2f;

        [HeaderAttribute("AR")]
        public bool ApplyEstimationPose = true;
        public Dropdown DictionaryIdDropdown;
        public ArUcoDictionary DictionaryId = ArUcoDictionary.DICT_6X6_250;
        public Toggle EnableLowPassFilterToggle;
        public bool EnableLowPassFilter = false;
        public Toggle EnableSmoothingFilterToggle;
        public bool EnableSmoothingFilter = false;
        public Toggle EnableSOLVEPNP_ITERATIVEToggle;
        public bool EnableSOLVEPNP_ITERATIVE = false;

        [HeaderAttribute("Debug")]
        public Text RenderFPS;
        public Text VideoFPS;
        public Text TrackFPS;
        public Text DebugStr;

        [Space(10)]

        [Tooltip("The length of the markers' side. Normally, unit is meters.")]
        public float MarkerLength = 0.188f;
        public ARHelper ArHelper;
        public GameObject ArCubePrefab;

        // Private Fields
        private MarkerType _selectedMarkerType = MarkerType.CanonicalMarker;
        private Texture2D _texture;
        private HLCameraStreamToMatHelper _hlCameraStreamToMatHelper;
        private XROrigin _xrOrigin;
        private Mat _downScaleMat;
        private float _downScaleRatio = 1f;
        private Matrix4x4 _deliveredCameraToWorldMatrix = Matrix4x4.identity;
        private Mat _rgbMatForPreview;
        private Mat _camMatrix;
        private MatOfDouble _distCoeffs;

        private Mat _downScaleMatForWorker; // Thread-safe copy for worker thread
        private Mat _undistortedRgbMatForWorker; // Thread-safe undistorted image for worker thread

        // Thread-safe copies of camera parameters for worker thread (read-only, can be shared)
        private Mat _camMatrixForWorker;
        private MatOfDouble _distCoeffsForWorker;

        // for CanonicalMarker.
        private Dictionary _dictionary;
        private ArucoDetector _arucoDetector;

        private Dictionary<ArUcoIdentifier, ARGameObject> _arGameObjectCache = new Dictionary<ArUcoIdentifier, ARGameObject>();

        // Detection results for thread-safe transfer to main thread
        private struct DetectionResult
        {
            public int MarkerId;
            public Vector2[] ImagePoints;
            public Vector3[] ObjectPoints;
        }
        private List<DetectionResult> _detectionResults = new List<DetectionResult>();
        private static readonly Queue<Action> EXECUTE_ON_MAIN_THREAD = new Queue<Action>();
        private readonly object _sync = new object();
        private bool _isThreadRunningValue;

        // Private Properties
        private bool _isThreadRunning
        {
            get
            {
                lock (_sync)
                {
                    return _isThreadRunningValue;
                }
            }
            set
            {
                lock (_sync)
                {
                    _isThreadRunningValue = value;
                }
            }
        }

        private bool _isDetectingValue;
        private bool _isDetecting
        {
            get
            {
                lock (_sync)
                {
                    return _isDetectingValue;
                }
            }
            set
            {
                lock (_sync)
                {
                    _isDetectingValue = value;
                }
            }
        }

        // Unity Lifecycle Methods
        private void Start()
        {
            _xrOrigin = FindFirstObjectByType<XROrigin>();

            _hlCameraStreamToMatHelper = gameObject.GetComponent<HLCameraStreamToMatHelper>();
            _hlCameraStreamToMatHelper.FrameMatDelivered += OnFrameMatDelivered;
            _hlCameraStreamToMatHelper.UpdateFrameMatOnTick = false;
            _hlCameraStreamToMatHelper.OutputColorFormat = SourceToMatColorFormat.GRAY;
            _hlCameraStreamToMatHelper.Initialize();

            DictionaryIdDropdown.value = (int)DictionaryId;
            DisplayCameraPreviewToggle.isOn = DisplayCameraPreview;
            EnableDownScaleToggle.isOn = EnableDownScale;
            EnableLowPassFilterToggle.isOn = EnableLowPassFilter;
            EnableSmoothingFilterToggle.isOn = EnableSmoothingFilter;
            EnableSOLVEPNP_ITERATIVEToggle.isOn = EnableSOLVEPNP_ITERATIVE;
        }

        private void Update()
        {
            lock (EXECUTE_ON_MAIN_THREAD)
            {
                while (EXECUTE_ON_MAIN_THREAD.Count > 0)
                {
                    EXECUTE_ON_MAIN_THREAD.Dequeue().Invoke();
                }
            }
        }

        private void LateUpdate()
        {
            DebugUtils.RenderTick();

            if (RenderFPS != null)
            {
                DebugUtils.TryGetRenderRate(out float intervalMs, out float fps);
                RenderFPS.text = DebugUtils.FormatRateLine("Render", intervalMs, fps, true);
            }
            if (VideoFPS != null)
            {
                DebugUtils.TryGetVideoRate(out float intervalMs, out float fps, out bool isActive);
                VideoFPS.text = DebugUtils.FormatRateLine("Video", intervalMs, fps, isActive);
            }
            if (TrackFPS != null)
            {
                DebugUtils.TryGetTrackRate(out float intervalMs, out float fps, out bool isActive);
                TrackFPS.text = DebugUtils.FormatRateLine("Track", intervalMs, fps, isActive);
            }
            if (DebugStr != null)
            {
                if (DebugUtils.GetDebugStrLength() > 0)
                {
                    if (DebugStr.preferredHeight >= DebugStr.rectTransform.rect.height)
                    {
                        DebugStr.text = string.Empty;
                    }

                    DebugStr.text += DebugUtils.GetDebugStr();
                    DebugUtils.ClearDebugStr();
                }
            }
        }

        private void OnDestroy()
        {
            if (_hlCameraStreamToMatHelper != null)
            {
                _hlCameraStreamToMatHelper.FrameMatDelivered -= OnFrameMatDelivered;
            }
        }

        // Public Methods
        /// <summary>
        /// Raises the source to mat helper initialized event.
        /// </summary>
        public void OnSourceToMatHelperInitialized()
        {
            Debug.Log("OnSourceToMatHelperInitialized", this);

            Mat grayMat = _hlCameraStreamToMatHelper.FrameMat;
            SetupDownScaleWorkMat(grayMat);

            Mat previewSizeMat = EnableDownScale && _downScaleMat != null ? _downScaleMat : grayMat;
            if (previewSizeMat == null)
            {
                return;
            }

            float width = previewSizeMat.width();
            float height = previewSizeMat.height();

            if (_texture != null)
            {
                Texture2D.Destroy(_texture);
                _texture = null;
            }

            _texture = new Texture2D((int)width, (int)height, TextureFormat.RGB24, false);
            PreviewQuad.GetComponent<MeshRenderer>().material.mainTexture = _texture;
            PreviewQuad.transform.localScale = new Vector3(0.2f * width / height, 0.2f, 1);
            PreviewQuad.SetActive(DisplayCameraPreview);

            DebugUtils.AddDebugStr(_hlCameraStreamToMatHelper.OutputColorFormat.ToString() + " " + _hlCameraStreamToMatHelper.Width + " x " + _hlCameraStreamToMatHelper.Height + " : " + _hlCameraStreamToMatHelper.FPS);
            if (EnableDownScale)
            {
                DebugUtils.AddDebugStr("enableDownScale = true: " + _downScaleRatio + " / " + width + " x " + height);
            }

            float scaleX = width / Mathf.Max(1, grayMat.width());
            float scaleY = height / Mathf.Max(1, grayMat.height());

#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            CameraIntrinsics cameraIntrinsics = _hlCameraStreamToMatHelper.Intrinsics;

            _camMatrix = CreateCameraMatrix(
                cameraIntrinsics.FocalLengthX * scaleX,
                cameraIntrinsics.FocalLengthY * scaleY,
                cameraIntrinsics.PrincipalPointX * scaleX,
                cameraIntrinsics.PrincipalPointY * scaleY);
            _distCoeffs = new MatOfDouble(cameraIntrinsics.RadialDistK1, cameraIntrinsics.RadialDistK2, cameraIntrinsics.RadialDistK3, cameraIntrinsics.TangentialDistP1, cameraIntrinsics.TangentialDistP2);

            Debug.Log("Created CameraParameters from VideoMediaFrame.CameraIntrinsics on device.", this);
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

            _camMatrix = CreateCameraMatrix(fx * scaleX, fy * scaleY, cx * scaleX, cy * scaleY);
            _distCoeffs = new MatOfDouble(distCoeffs1, distCoeffs2, distCoeffs3, distCoeffs4, distCoeffs5);

            Debug.Log("Created a dummy CameraParameters (896x504).", this);
            DebugUtils.AddDebugStr("Created a dummy CameraParameters (896x504).");
#endif

            Debug.Log("camMatrix " + _camMatrix.dump(), this);
            Debug.Log("distCoeffs " + _distCoeffs.dump(), this);

            DebugUtils.AddDebugStr("camMatrix " + _camMatrix.dump());
            DebugUtils.AddDebugStr("distCoeffs " + _distCoeffs.dump());

            Size imageSize = new Size(width, height);
            double apertureWidth = 0;
            double apertureHeight = 0;
            double[] fovx = new double[1];
            double[] fovy = new double[1];
            double[] focalLength = new double[1];
            Point principalPoint = new Point(0, 0);
            double[] aspectratio = new double[1];

            Geometry.calibrationMatrixValues(_camMatrix, imageSize, apertureWidth, apertureHeight, fovx, fovy, focalLength, principalPoint, aspectratio);

            Debug.Log("imageSize " + imageSize.ToString(), this);
            Debug.Log("apertureWidth " + apertureWidth, this);
            Debug.Log("apertureHeight " + apertureHeight, this);
            Debug.Log("fovx " + fovx[0], this);
            Debug.Log("fovy " + fovy[0], this);
            Debug.Log("focalLength " + focalLength[0], this);
            Debug.Log("principalPoint " + principalPoint.ToString(), this);
            Debug.Log("aspectratio " + aspectratio[0], this);

            _dictionary = Objdetect.getPredefinedDictionary((int)DictionaryId);

            _camMatrixForWorker = _camMatrix.clone();
            _distCoeffsForWorker = new MatOfDouble(_distCoeffs);

            _undistortedRgbMatForWorker = new Mat();

            DetectorParameters detectorParams = new DetectorParameters();
            detectorParams.set_minDistanceToBorder(3);
            detectorParams.set_useAruco3Detection(true);
            detectorParams.set_cornerRefinementMethod(Objdetect.CORNER_REFINE_SUBPIX);
            detectorParams.set_minSideLengthCanonicalImg(16);
            detectorParams.set_errorCorrectionRate(0.8);
            RefineParameters refineParameters = new RefineParameters(10f, 3f, true);
            _arucoDetector = new ArucoDetector(_dictionary, detectorParams, refineParameters);

            _hlCameraStreamToMatHelper.FlipHorizontal = _hlCameraStreamToMatHelper.IsFrontFacing;

            _rgbMatForPreview = new Mat();

            if (ArHelper != null)
            {
                Camera dummyCamera = ArHelper.ARCamera != null ? ArHelper.ARCamera.GetComponent<Camera>() : null;
                if (dummyCamera != null)
                {
                    dummyCamera.nearClipPlane = 0.01f;
                }

                ArHelper.Initialize();
                if (ArHelper.ARCamera != null)
                {
                    ArHelper.ARCamera.SetCamMatrix(_camMatrix);
                    ArHelper.ARCamera.SetDistCoeffs(_distCoeffs);
                    ArHelper.ARCamera.SetARCameraParameters(Screen.width, Screen.height, (int)width, (int)height, Vector2.zero, new Vector2(1.0f, 1.0f));
                }
            }

            if (!_hlCameraStreamToMatHelper.IsPlaying && !_hlCameraStreamToMatHelper.IsPaused)
            {
                _hlCameraStreamToMatHelper.Play();
            }
        }

        /// <summary>
        /// Raises the helper released event.
        /// </summary>
        public void OnSourceToMatHelperReleased()
        {
            Debug.Log("OnSourceToMatHelperReleased", this);

            CleanupDetectionResources();
        }

        /// <summary>
        /// Raises the source to mat helper disposed event.
        /// </summary>
        public void OnSourceToMatHelperDisposed()
        {
            Debug.Log("OnSourceToMatHelperDisposed", this);

            CleanupDetectionResources();
        }

        /// <summary>
        /// Raises the helper error occurred event.
        /// </summary>
        /// <param name="errorCode">Error code.</param>
        /// <param name="message">Message.</param>
        public void OnSourceToMatHelperErrorOccurred(SourceToMatErrorCode errorCode, string message)
        {
            Debug.Log("OnSourceToMatHelperErrorOccurred " + errorCode + ":" + message, this);
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
            _hlCameraStreamToMatHelper.Play();
        }

        /// <summary>
        /// Raises the pause button click event.
        /// </summary>
        public void OnPauseButtonClick()
        {
            _hlCameraStreamToMatHelper.Pause();
        }

        /// <summary>
        /// Raises the stop button click event.
        /// </summary>
        public void OnStopButtonClick()
        {
            _hlCameraStreamToMatHelper.Stop();
        }

        /// <summary>
        /// Raises the change camera button click event.
        /// </summary>
        public void OnChangeCameraButtonClick()
        {
            _hlCameraStreamToMatHelper.RequestedIsFrontFacing = !_hlCameraStreamToMatHelper.RequestedIsFrontFacing;
        }

        /// <summary>
        /// Raises the display camera preview toggle value changed event.
        /// </summary>
        public void OnDisplayCameraPreviewToggleValueChanged()
        {
            DisplayCameraPreview = DisplayCameraPreviewToggle.isOn;

            PreviewQuad.SetActive(DisplayCameraPreview);
        }

        /// <summary>
        /// Raises the enable downscale toggle value changed event.
        /// </summary>
        public void OnEnableDownScaleToggleValueChanged()
        {
            EnableDownScale = EnableDownScaleToggle.isOn;

            if (_hlCameraStreamToMatHelper != null && _hlCameraStreamToMatHelper.IsInitialized)
            {
                _hlCameraStreamToMatHelper.Initialize();
            }
        }

        /// <summary>
        /// Raises the dictionary id dropdown value changed event.
        /// </summary>
        public void OnDictionaryIdDropdownValueChanged(int result)
        {
            if ((int)DictionaryId != result)
            {
                DictionaryId = (ArUcoDictionary)result;
                _dictionary = Objdetect.getPredefinedDictionary((int)DictionaryId);

                if (_hlCameraStreamToMatHelper != null && _hlCameraStreamToMatHelper.IsInitialized)
                {
                    _hlCameraStreamToMatHelper.Initialize();
                }
            }
        }

        /// <summary>
        /// Raises the enable low pass filter toggle value changed event.
        /// </summary>
        public void OnEnableLowPassFilterToggleValueChanged()
        {
            EnableLowPassFilter = EnableLowPassFilterToggle.isOn;

            if (ArHelper != null && ArHelper.ARGameObjects != null)
            {
                foreach (ARGameObject arGameObject in ArHelper.ARGameObjects)
                {
                    if (arGameObject != null)
                    {
                        arGameObject.UseLowPassFilter = EnableLowPassFilter;
                    }
                }
            }
        }

        /// <summary>
        /// Raises the enable smoothing filter toggle value changed event.
        /// </summary>
        public void OnEnableSmoothingFilterToggleValueChanged()
        {
            EnableSmoothingFilter = EnableSmoothingFilterToggle.isOn;

            if (ArHelper != null && ArHelper.ARGameObjects != null)
            {
                foreach (ARGameObject arGameObject in ArHelper.ARGameObjects)
                {
                    if (arGameObject != null)
                    {
                        arGameObject.UseSmoothingFilter = EnableSmoothingFilter;
                    }
                }
            }
        }

        /// <summary>
        /// Raises the enable SOLVEPNP_ITERATIVE toggle value changed event.
        /// </summary>
        public void OnEnableSOLVEPNP_ITERATIVEToggleValueChanged()
        {
            EnableSOLVEPNP_ITERATIVE = EnableSOLVEPNP_ITERATIVEToggle.isOn;

            if (ArHelper != null && ArHelper.ARGameObjects != null)
            {
                foreach (ARGameObject arGameObject in ArHelper.ARGameObjects)
                {
                    if (arGameObject != null)
                    {
                        arGameObject.UseSOLVEPNP_ITERATIVE = EnableSOLVEPNP_ITERATIVE;
                    }
                }
            }
        }

        /// <summary>
        /// Called when an ARGameObject enters the ARCamera viewport.
        /// </summary>
        /// <param name="aRHelper"></param>
        /// <param name="arCamera"></param>
        /// <param name="arGameObject"></param>
        public void OnEnterARCameraViewport(ARHelper aRHelper, ARCamera arCamera, ARGameObject arGameObject)
        {
            Debug.Log("OnEnterARCamera arCamera.name " + arCamera.name + " arGameObject.name " + arGameObject.name, this);

            StartCoroutine(arGameObject.GetComponent<ARCube>().EnterAnimation(arGameObject.gameObject, 0f, 1f, 0.5f));
        }

        /// <summary>
        /// Called when an ARGameObject exits the ARCamera viewport.
        /// </summary>
        /// <param name="aRHelper"></param>
        /// <param name="arCamera"></param>
        /// <param name="arGameObject"></param>
        public void OnExitARCameraViewport(ARHelper aRHelper, ARCamera arCamera, ARGameObject arGameObject)
        {
            Debug.Log("OnExitARCamera arCamera.name " + arCamera.name + " arGameObject.name " + arGameObject.name, this);

            StartCoroutine(arGameObject.GetComponent<ARCube>().ExitAnimation(arGameObject.gameObject, 1f, 0f, 0.2f));
        }

        // Private Methods
        private void OnFrameMatDelivered(object sender, FrameMatDeliveredEventArgs e)
        {
            Mat grayMat = e.Mat;
            if (grayMat == null)
            {
                return;
            }

            bool queuedForMainThread = false;
            try
            {
                DebugUtils.VideoTick();

                if (EnableDetection && !_isDetecting)
                {
                    _isDetecting = true;

                    EnsureDownScaleWorkMat(grayMat);

                    Mat detectMat = grayMat;
                    if (EnableDownScale && _downScaleMat != null && !_downScaleMat.empty())
                    {
                        Imgproc.resize(grayMat, _downScaleMat, _downScaleMat.size(), 0, 0, Imgproc.INTER_LINEAR);
                        detectMat = _downScaleMat;
                    }

                    lock (_sync)
                    {
                        _deliveredCameraToWorldMatrix = e.CameraToWorldMatrix;

                        if (detectMat != null && !detectMat.empty())
                        {
                            if (_downScaleMatForWorker == null || _downScaleMatForWorker.empty() ||
                                _downScaleMatForWorker.width() != detectMat.width() ||
                                _downScaleMatForWorker.height() != detectMat.height() ||
                                _downScaleMatForWorker.type() != detectMat.type())
                            {
                                _downScaleMatForWorker?.Dispose();
                                _downScaleMatForWorker = new Mat(detectMat.rows(), detectMat.cols(), detectMat.type());
                            }

                            detectMat.copyTo(_downScaleMatForWorker);
                        }
                    }

                    StartThread(ThreadWorker);
                }
            }
            finally
            {
                if (!queuedForMainThread)
                {
                    grayMat.Dispose();
                }
            }
        }

        private void EnsureDownScaleWorkMat(Mat sourceMat)
        {
            if (!EnableDownScale || sourceMat == null)
            {
                return;
            }

            float ratio = DownscaleRatio > 1f ? DownscaleRatio : 1f;
            int expectedWidth = Mathf.Max(1, Mathf.RoundToInt(sourceMat.width() / ratio));
            int expectedHeight = Mathf.Max(1, Mathf.RoundToInt(sourceMat.height() / ratio));
            if (_downScaleMat == null || _downScaleMat.empty() ||
                _downScaleMat.width() != expectedWidth ||
                _downScaleMat.height() != expectedHeight ||
                _downScaleMat.type() != sourceMat.type())
            {
                SetupDownScaleWorkMat(sourceMat);
            }
        }

        private void SetupDownScaleWorkMat(Mat sourceMat)
        {
            _downScaleMat?.Dispose();
            _downScaleMat = null;

            if (sourceMat == null || sourceMat.empty())
            {
                _downScaleRatio = 1f;
                return;
            }

            if (EnableDownScale && DownscaleRatio > 1f)
            {
                _downScaleRatio = DownscaleRatio;
                int width = Mathf.Max(1, Mathf.RoundToInt(sourceMat.width() / _downScaleRatio));
                int height = Mathf.Max(1, Mathf.RoundToInt(sourceMat.height() / _downScaleRatio));
                _downScaleMat = new Mat(height, width, sourceMat.type());
            }
            else
            {
                _downScaleRatio = 1f;
            }
        }

        private void CleanupDetectionResources()
        {
            StopThread();
            lock (EXECUTE_ON_MAIN_THREAD)
            {
                while (EXECUTE_ON_MAIN_THREAD.Count > 0)
                {
                    EXECUTE_ON_MAIN_THREAD.Dequeue().Invoke();
                }
            }
            _isDetecting = false;

            _arucoDetector?.Dispose();
            _arucoDetector = null;

            _dictionary?.Dispose();
            _dictionary = null;

            _camMatrixForWorker?.Dispose();
            _camMatrixForWorker = null;
            _distCoeffsForWorker?.Dispose();
            _distCoeffsForWorker = null;

            _downScaleMatForWorker?.Dispose();
            _downScaleMatForWorker = null;
            _undistortedRgbMatForWorker?.Dispose();
            _undistortedRgbMatForWorker = null;

            _downScaleMat?.Dispose();
            _downScaleMat = null;

            if (ArHelper != null)
            {
                RemoveAllARGameObject(ArHelper.ARGameObjects);
                ArHelper.Dispose();
            }

            _camMatrix?.Dispose();
            _camMatrix = null;
            _distCoeffs?.Dispose();
            _distCoeffs = null;

            _rgbMatForPreview?.Dispose();
            _rgbMatForPreview = null;

            if (_texture != null)
            {
                Texture2D.Destroy(_texture);
                _texture = null;
            }

            if (DebugStr != null)
            {
                DebugStr.text = string.Empty;
            }
            DebugUtils.ClearDebugStr();
        }

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

        private void StartThread(Action action)
        {
            ThreadPool.QueueUserWorkItem(_ => action());
        }

        private void StopThread()
        {
            if (!_isThreadRunning)
            {
                return;
            }

            while (_isThreadRunning)
            {
                //Wait threading stop
            }
        }

        private void ThreadWorker()
        {
            _isThreadRunning = true;

            DetectARUcoMarker();

            lock (EXECUTE_ON_MAIN_THREAD)
            {
                if (EXECUTE_ON_MAIN_THREAD.Count == 0)
                {
                    EXECUTE_ON_MAIN_THREAD.Enqueue(() =>
                    {
                        OnDetectionDone();
                    });
                }
            }

            _isThreadRunning = false;
        }

        private void DetectARUcoMarker()
        {
            // Get thread-safe copy of downScaleMat (already copied in Update())
            List<Mat> corners = new List<Mat>();
            Mat ids = new Mat();
            List<Mat> rejectedCorners = new List<Mat>();

            try
            {
                // Check if _downScaleMatForWorker is available (worker thread is exclusive, so safe to access)
                lock (_sync)
                {
                    if (_downScaleMatForWorker == null || _downScaleMatForWorker.empty())
                    {
                        lock (_sync)
                        {
                            _detectionResults = new List<DetectionResult>();
                        }
                        return;
                    }
                }

                // Detect markers using _downScaleMatForWorker and _undistortedRgbMatForWorker (already thread-safe copies from main thread)
                Imgproc.undistort(_downScaleMatForWorker, _undistortedRgbMatForWorker, _camMatrixForWorker, _distCoeffsForWorker);
                _arucoDetector.detectMarkers(_undistortedRgbMatForWorker, corners, ids, rejectedCorners);

                // Estimate pose if markers detected
                if (ApplyEstimationPose && ids.total() > 0)
                {
                    EstimatePoseCanonicalMarker(_undistortedRgbMatForWorker, corners, ids);
                }
                else
                {
                    // Store empty results for main thread processing
                    lock (_sync)
                    {
                        _detectionResults = new List<DetectionResult>();
                    }
                }
            }
            finally
            {
                // Clean up thread-local Mats
                ids?.Dispose();
                if (corners != null)
                {
                    foreach (var item in corners)
                    {
                        item.Dispose();
                    }
                }

                if (rejectedCorners != null)
                {
                    foreach (var item in rejectedCorners)
                    {
                        item.Dispose();
                    }
                }
            }
        }

        private void OnDetectionDone()
        {
            DebugUtils.TrackTick();

            Matrix4x4 deliveredCameraToWorldMatrix;
            lock (_sync)
            {
                deliveredCameraToWorldMatrix = _deliveredCameraToWorldMatrix;
            }

            if (ApplyEstimationPose && ArHelper != null && ArHelper.ARCamera != null)
            {
                // HoloLensCameraStream's cameraToWorldMatrix is relative to the Unity scene
                // origin returned by GetSceneCoordinateSystem(Pose.identity).
                // This is not necessarily the same coordinate system as the Unity world
                // used by the MRTK XROrigin.
                //
                // MRTK's XROrigin (Camera Offset) can apply an additional transform,
                // such as the Camera Y Offset depending on the Tracking Origin Mode.
                // Therefore, first convert the camera transform into the XROrigin world space.
                if (_xrOrigin != null)
                {
                    deliveredCameraToWorldMatrix = _xrOrigin.transform.localToWorldMatrix * deliveredCameraToWorldMatrix;
                }

                Matrix4x4 cameraLocalToWorldMatrix = deliveredCameraToWorldMatrix * Matrix4x4.Scale(new Vector3(1, 1, -1));
                OpenCVARUtils.SetTransformFromMatrix(ArHelper.ARCamera.transform, ref cameraLocalToWorldMatrix);

                if (_camMatrixForWorker != null && _distCoeffsForWorker != null)
                {
                    ArHelper.ARCamera.SetCamMatrix(_camMatrixForWorker);
                    ArHelper.ARCamera.SetDistCoeffs(_distCoeffsForWorker);
                }

                ArHelper.ResetARGameObjectsImagePointsAndObjectPoints();

                List<DetectionResult> detectionResults;
                lock (_sync)
                {
                    detectionResults = new List<DetectionResult>(_detectionResults);
                }

                foreach (var result in detectionResults)
                {
                    var arUcoId = new ArUcoIdentifier((int)_selectedMarkerType, (int)DictionaryId, new[] { result.MarkerId });
                    ARGameObject aRGameObject = FindOrCreateARGameObject(ArHelper.ARGameObjects, arUcoId, ArHelper.transform);
                    aRGameObject.SolvePnPFlagsMode = ARPoseEstimator.Calib3dSolvePnPFlagsMode.SOLVEPNP_IPPE_SQUARE;

                    aRGameObject.ImagePoints = result.ImagePoints;
                    aRGameObject.ObjectPoints = result.ObjectPoints;
                }

                ArHelper.CalculateARMatrix();
                ArHelper.UpdateTransform();
            }

            if (DisplayCameraPreview)
            {
                Mat previewSource = null;
                lock (_sync)
                {
                    previewSource = _downScaleMatForWorker;
                }

                if (previewSource != null && !previewSource.empty())
                {
                    Imgproc.cvtColor(previewSource, _rgbMatForPreview, Imgproc.COLOR_GRAY2RGB);

                    List<DetectionResult> detectionResults;
                    lock (_sync)
                    {
                        detectionResults = new List<DetectionResult>(_detectionResults);
                    }
                    foreach (var result in detectionResults)
                    {
                        using (MatOfPoint2f imagePoints = new MatOfPoint2f(result.ImagePoints))
                        using (MatOfPoint3f objectPoints = new MatOfPoint3f(result.ObjectPoints))
                        {
                            DebugDrawFrameAxes(_rgbMatForPreview, objectPoints, imagePoints, _camMatrixForWorker != null ? _camMatrixForWorker : _camMatrix, _distCoeffsForWorker != null ? _distCoeffsForWorker : _distCoeffs, MarkerLength * 0.5f);
                        }
                    }

                    if (_texture != null)
                    {
                        OpenCVMatUnityUtils.MatToTexture2D(_rgbMatForPreview, _texture);
                    }
                }
            }

            _isDetecting = false;
        }

        /// <summary>
        /// Finds or creates an ARGameObject with the specified AR marker identifier.
        /// </summary>
        /// <param name="arGameObjects"></param>
        /// <param name="arUcoId"></param>
        /// <param name="parentTransform"></param>
        /// <returns></returns>
        private ARGameObject FindOrCreateARGameObject(List<ARGameObject> arGameObjects, ArUcoIdentifier arUcoId, Transform parentTransform)
        {
            ARGameObject FindARGameObjectById(List<ARGameObject> arGameObjects, ArUcoIdentifier id)
            {
                if (_arGameObjectCache.TryGetValue(id, out var cachedObject) && cachedObject != null)
                {
                    return cachedObject;
                }
                return null;
            }

            ARGameObject arGameObject = FindARGameObjectById(arGameObjects, arUcoId);
            if (arGameObject == null)
            {
                arGameObject = Instantiate(ArCubePrefab, parentTransform).GetComponent<ARGameObject>();

                string markerIdsStr = arUcoId.MarkerIds != null ? string.Join(",", arUcoId.MarkerIds) : null;
                string arUcoIdNameStr;
                if (markerIdsStr != null)
                {
                    arUcoIdNameStr = (MarkerType)arUcoId.MarkerType + " " + (ArUcoDictionary)arUcoId.DictionaryId + " [" + markerIdsStr + "]";
                }
                else
                {
                    arUcoIdNameStr = (MarkerType)arUcoId.MarkerType + " " + (ArUcoDictionary)arUcoId.DictionaryId;
                }

                arGameObject.name = arUcoIdNameStr;
                arGameObject.GetComponent<ARCube>().SetInfoPlateTexture(arUcoIdNameStr);
                arGameObject.UseLowPassFilter = EnableLowPassFilter;
                arGameObject.UseSmoothingFilter = EnableSmoothingFilter;
                arGameObject.UseSOLVEPNP_ITERATIVE = EnableSOLVEPNP_ITERATIVE;
                arGameObject.OnEnterARCameraViewport.AddListener(OnEnterARCameraViewport);
                arGameObject.OnExitARCameraViewport.AddListener(OnExitARCameraViewport);
                arGameObject.gameObject.SetActive(false);
                arGameObjects.Add(arGameObject);
                _arGameObjectCache[arUcoId] = arGameObject;
            }
            return arGameObject;
        }

        /// <summary>
        /// Removes all ARGameObjects from the list and destroys them.
        /// </summary>
        /// <param name="arGameObjects"></param>
        private void RemoveAllARGameObject(List<ARGameObject> arGameObjects)
        {
            if (arGameObjects != null)
            {
                foreach (ARGameObject arGameObject in arGameObjects)
                {
                    if (arGameObject != null)
                    {
                        Destroy(arGameObject.gameObject);
                    }
                }
                arGameObjects.Clear();
            }

            _arGameObjectCache.Clear();
        }

        private void DebugDrawFrameAxes(Mat image, MatOfPoint3f objectPoints, MatOfPoint2f imagePoints, Mat cameraMatrix, MatOfDouble distCoeffs,
                                 float length, int thickness = 3)
        {
            // Calculate rvec and tvec for debug display and draw with OpenCVARUtils.SafeDrawFrameAxes()
            using (Mat rvec = new Mat(3, 1, CvType.CV_64FC1))
            using (Mat tvec = new Mat(3, 1, CvType.CV_64FC1))
            {
                // Calculate pose
                Geometry.solvePnP(objectPoints, imagePoints, cameraMatrix, distCoeffs, rvec, tvec);

                // In this example we are processing with RGB color image, so Axis-color correspondences are X: blue, Y: green, Z: red. (Usually X: red, Y: green, Z: blue)
                OpenCVARUtils.SafeDrawFrameAxes(image, cameraMatrix, distCoeffs, rvec, tvec, length, thickness);
            }
        }

        private struct ArUcoIdentifier : IEquatable<ArUcoIdentifier>
        {
            public int MarkerType;    // enum value
            public int DictionaryId;  // enum value
            public int[] MarkerIds;   // marker ID array

            public ArUcoIdentifier(int markerType, int dictionaryId, int[] markerIds)
            {
                MarkerType = markerType;
                DictionaryId = dictionaryId;
                MarkerIds = markerIds;
            }

            public override string ToString()
            {
                string markerIdsStr = MarkerIds != null ? string.Join(",", MarkerIds) : null;
                if (markerIdsStr != null)
                {
                    return $"{MarkerType} {DictionaryId} [{markerIdsStr}]";
                }
                else
                {
                    return $"{MarkerType} {DictionaryId}";
                }
            }

            public override int GetHashCode()
            {
                // fast hash calculation
                int hash = MarkerType;
                hash = hash * 31 + DictionaryId;
                if (MarkerIds != null)
                {
                    foreach (int id in MarkerIds)
                    {
                        hash = hash * 31 + id;
                    }
                }
                return hash;
            }

            public bool Equals(ArUcoIdentifier other)
            {
                if (MarkerType != other.MarkerType || DictionaryId != other.DictionaryId)
                {
                    return false;
                }

                if (MarkerIds == null)
                {
                    return other.MarkerIds == null;
                }

                if (other.MarkerIds == null)
                {
                    return false;
                }

                if (MarkerIds.Length != other.MarkerIds.Length)
                {
                    return false;
                }

                for (int i = 0; i < MarkerIds.Length; i++)
                {
                    if (MarkerIds[i] != other.MarkerIds[i])
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        private void EstimatePoseCanonicalMarker(Mat rgbMat, List<Mat> corners, Mat ids)
        {
            using (MatOfPoint3f objectPoints = new MatOfPoint3f(
                new Point3(-MarkerLength / 2f, MarkerLength / 2f, 0),
                new Point3(MarkerLength / 2f, MarkerLength / 2f, 0),
                new Point3(MarkerLength / 2f, -MarkerLength / 2f, 0),
                new Point3(-MarkerLength / 2f, -MarkerLength / 2f, 0)
                ))
            {
                // Store detection results for thread-safe transfer to main thread
                List<DetectionResult> detectionResults = new List<DetectionResult>();

                Span<int> idsValues = ids.AsSpan<int>();

                for (int i = 0; i < idsValues.Length; i++)
                {
                    using (Mat corner_4x1 = corners[i].reshape(2, 4)) // 1*4*CV_32FC2 => 4*1*CV_32FC2
                    using (MatOfPoint2f imagePoints = new MatOfPoint2f(corner_4x1))
                    {
                        // Convert to thread-safe data structures
                        DetectionResult result = new DetectionResult
                        {
                            MarkerId = idsValues[i],
                            ImagePoints = imagePoints.toVector2Array(),
                            ObjectPoints = objectPoints.toVector3Array()
                        };
                        detectionResults.Add(result);
                    }
                }

                // Store results for main thread processing
                lock (_sync)
                {
                    _detectionResults = detectionResults;
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using HoloLensWithOpenCVForUnity.UnityIntegration.Helper.SourceToMat;
using HoloLensWithOpenCVForUnityExample.RectangleTrack;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Extensions.SourceToMat;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityIntegration;
using OpenCVForUnity.XobjdetectModule;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Rect = OpenCVForUnity.CoreModule.Rect;

namespace HoloLensWithOpenCVForUnityExample
{
    /// <summary>
    /// HoloLens Face Detection Example
    /// An example of detecting face using OpenCVForUnity on Hololens.
    /// Referring to https://github.com/Itseez/opencv/blob/master/modules/objdetect/src/detection_based_tracker.cpp.
    /// </summary>
    [RequireComponent(typeof(HLCameraStreamToMatHelper))]
    public class HLFaceDetectionExample : MonoBehaviour
    {
        // Constants
        private static readonly Scalar COLOR_WHITE = new Scalar(255, 255, 255, 255);
        private static readonly Scalar COLOR_GRAY = new Scalar(128, 128, 128, 255);

        // Public Fields
        /// <summary>
        /// Determines if enables the detection.
        /// </summary>
        public bool EnableDetection = true;

        /// <summary>
        /// Determines if enable downscale.
        /// </summary>
        public bool EnableDownScale;

        /// <summary>
        /// The enable downscale toggle.
        /// </summary>
        public Toggle EnableDownScaleToggle;

        /// <summary>
        /// The downscale ratio applied when <see cref="EnableDownScale"/> is enabled.
        /// </summary>
        public float DownscaleRatio = 2f;

        /// <summary>
        /// Determines if uses separate detection.
        /// </summary>
        public bool UseSeparateDetection = false;

        /// <summary>
        /// The use separate detection toggle.
        /// </summary>
        public Toggle UseSeparateDetectionToggle;

        /// <summary>
        /// Determines if displays camera image.
        /// </summary>
        public bool DisplayCameraImage = false;

        /// <summary>
        /// The display camera image toggle.
        /// </summary>
        public Toggle DisplayCameraImageToggle;

        /// <summary>
        /// The min detection size ratio.
        /// </summary>
        public float MinDetectionSizeRatio = 0.07f;

        [HeaderAttribute("Debug")]

        public Text RenderFPS;
        public Text VideoFPS;
        public Text TrackFPS;
        public Text DebugStr;

        // Private Fields
        private readonly Queue<Action> _executeOnMainThread = new Queue<Action>();

        /// <summary>
        /// The HLCameraStreamToMatHelper.
        /// </summary>
        private HLCameraStreamToMatHelper _hlCameraStreamToMatHelper;

        /// <summary>
        /// The XROrigin used to convert cameraToWorldMatrix into Unity world space.
        /// </summary>
        private XROrigin _xrOrigin;

        /// <summary>
        /// The texture.
        /// </summary>
        private Texture2D _texture;

        /// <summary>
        /// The cascade.
        /// </summary>
        private CascadeClassifier _cascade;

        /// <summary>
        /// The quad renderer.
        /// </summary>
        private Renderer _quadRenderer;

        /// <summary>
        /// The detection result.
        /// </summary>
        private List<Rect> _detectionResult = new List<Rect>();

        private Mat _grayMat4Thread;
        private CascadeClassifier _cascade4Thread;
        private readonly object _sync = new object();

        private Mat _downScaleMat;
        private float _downScaleRatio;

        private bool _isThreadRunningValue = false;
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

        private RectangleTracker _rectangleTracker;
        private float _coeffTrackingWindowSize = 2.0f;
        private float _coeffObjectSizeToTrack = 0.85f;
        private List<Rect> _detectedObjectsInRegions = new List<Rect>();
        private List<Rect> _resultObjects = new List<Rect>();

        private bool _isDetectingValue = false;
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

        private bool _hasUpdatedDetectionResultValue = false;
        private bool _hasUpdatedDetectionResult
        {
            get
            {
                lock (_sync)
                {
                    return _hasUpdatedDetectionResultValue;
                }
            }
            set
            {
                lock (_sync)
                {
                    _hasUpdatedDetectionResultValue = value;
                }
            }
        }

        private bool _isProcessingFrameMatDeliveredValue;
        private bool _isProcessingFrameMatDelivered
        {
            get
            {
                lock (_sync)
                {
                    return _isProcessingFrameMatDeliveredValue;
                }
            }
            set
            {
                lock (_sync)
                {
                    _isProcessingFrameMatDeliveredValue = value;
                }
            }
        }

        private string _cascadeFilepath;
        private string _cascade4ThreadFilepath;

        /// <summary>
        /// The CancellationTokenSource.
        /// </summary>
        private CancellationTokenSource _cts = new CancellationTokenSource();

        // Unity Lifecycle Methods
        private async void Start()
        {
            EnableDownScaleToggle.isOn = EnableDownScale;
            UseSeparateDetectionToggle.isOn = UseSeparateDetection;
            DisplayCameraImageToggle.isOn = DisplayCameraImage;

            _xrOrigin = FindFirstObjectByType<XROrigin>();
            _hlCameraStreamToMatHelper = gameObject.GetComponent<HLCameraStreamToMatHelper>();
            _hlCameraStreamToMatHelper.FrameMatDelivered += OnFrameMatDelivered;
            _hlCameraStreamToMatHelper.UpdateFrameMatOnTick = false;
            _hlCameraStreamToMatHelper.OutputColorFormat = SourceToMatColorFormat.GRAY;

            _rectangleTracker = new RectangleTracker();

            // Asynchronously retrieves the readable file path from the StreamingAssets directory.
            if (DebugStr != null)
            {
                DebugStr.text = "Preparing file access...";
            }

            _cascadeFilepath = await OpenCVForUnityEnv.GetFilePathAsync("OpenCVForUnityExample/objdetect/lbpcascade_frontalface.xml", cancellationToken: _cts.Token);
            //cascade4Thread_filepath = await OpenCVForUnityEnv.GetFilePathTaskAsync("OpenCVForUnityExample/objdetect/haarcascade_frontalface_alt.xml", cancellationToken: cts.Token);
            _cascade4ThreadFilepath = await OpenCVForUnityEnv.GetFilePathAsync("OpenCVForUnityExample/objdetect/lbpcascade_frontalface.xml", cancellationToken: _cts.Token);

            if (DebugStr != null)
            {
                DebugStr.text = string.Empty;
            }

            Run();
        }

        private void Update()
        {
            lock (_executeOnMainThread)
            {
                while (_executeOnMainThread.Count > 0)
                {
                    _executeOnMainThread.Dequeue().Invoke();
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

            _cts?.Cancel();

            CleanupResources();

            _rectangleTracker?.Dispose();
            _rectangleTracker = null;
            _cascade?.Dispose();
            _cascade = null;
            _cascade4Thread?.Dispose();
            _cascade4Thread = null;
            _cts?.Dispose();
            _cts = null;
        }

        // Public Methods
        /// <summary>
        /// Raises the helper initialized event.
        /// Recreates the preview texture and starts playback on first initialization.
        /// Skips Play when re-initialization has already restored Playing or Paused.
        /// </summary>
        public void OnSourceToMatHelperInitialized()
        {
            Debug.Log("OnSourceToMatHelperInitialized", this);

            WaitForFrameMatDeliveredProcessing();

            Mat grayMat = _hlCameraStreamToMatHelper.FrameMat;
            SetupDownScaleWorkMat(grayMat);
            RecreatePreviewTexture(grayMat);

            DebugUtils.AddDebugStr(_hlCameraStreamToMatHelper.OutputColorFormat.ToString() + " " + _hlCameraStreamToMatHelper.Width + " x " + _hlCameraStreamToMatHelper.Height + " : " + _hlCameraStreamToMatHelper.FPS);
            if (EnableDownScale)
            {
                float width = _downScaleMat != null ? _downScaleMat.width() : grayMat.width();
                float height = _downScaleMat != null ? _downScaleMat.height() : grayMat.height();
                DebugUtils.AddDebugStr("EnableDownScale = true: " + _downScaleRatio + " / " + width + " x " + height);
            }

            ApplyProjectionMatrix();

            if (_quadRenderer != null)
            {
                _quadRenderer.sharedMaterial.SetVector("_VignetteOffset", new Vector4(0, 0));
                _quadRenderer.sharedMaterial.SetFloat("_VignetteScale", 0.0f);
            }

            _grayMat4Thread?.Dispose();
            _grayMat4Thread = new Mat();

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

            CleanupResources();
        }

        /// <summary>
        /// Raises the helper disposed event.
        /// </summary>
        public void OnSourceToMatHelperDisposed()
        {
            Debug.Log("OnSourceToMatHelperDisposed", this);

            CleanupResources();
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
        /// Raises the enable downscale toggle value changed event.
        /// </summary>
        public void OnEnableDownScaleToggleValueChanged()
        {
            EnableDownScale = EnableDownScaleToggle.isOn;

            if (_rectangleTracker != null)
            {
                lock (_rectangleTracker)
                {
                    _rectangleTracker.Reset();
                }
            }

            if (_hlCameraStreamToMatHelper != null && _hlCameraStreamToMatHelper.IsInitialized)
            {
                _hlCameraStreamToMatHelper.Initialize();
            }
        }

        /// <summary>
        /// Raises the use separate detection toggle value changed event.
        /// </summary>
        public void OnUseSeparateDetectionToggleValueChanged()
        {
            UseSeparateDetection = UseSeparateDetectionToggle.isOn;

            if (_rectangleTracker != null)
            {
                lock (_rectangleTracker)
                {
                    _rectangleTracker.Reset();
                }
            }
        }

        /// <summary>
        /// Raises the display camera image toggle value changed event.
        /// </summary>
        public void OnDisplayCameraImageToggleValueChanged()
        {
            DisplayCameraImage = DisplayCameraImageToggle.isOn;
        }

        // Private Methods
        private void OnFrameMatDelivered(object sender, FrameMatDeliveredEventArgs e)
        {
            Mat grayMat = e.Mat;
            if (grayMat == null)
            {
                return;
            }

            _isProcessingFrameMatDelivered = true;

            bool queuedForMainThread = false;
            try
            {
                DebugUtils.VideoTick();

                Mat downScaleMat;
                float downscaleRatio;
                if (EnableDownScale && _downScaleMat != null)
                {
                    Imgproc.resize(grayMat, _downScaleMat, _downScaleMat.size(), 0, 0, Imgproc.INTER_LINEAR);
                    downScaleMat = _downScaleMat;
                    downscaleRatio = _downScaleRatio;
                }
                else
                {
                    downScaleMat = grayMat;
                    downscaleRatio = 1.0f;
                }

                Imgproc.equalizeHist(downScaleMat, downScaleMat);

                if (EnableDetection && !_isDetecting)
                {
                    _isDetecting = true;

                    downScaleMat.copyTo(_grayMat4Thread);

                    StartThread(ThreadWorker);
                }

                if (!UseSeparateDetection)
                {
                    if (_hasUpdatedDetectionResult)
                    {
                        _hasUpdatedDetectionResult = false;

                        lock (_rectangleTracker)
                        {
                            _rectangleTracker.UpdateTrackedObjects(_detectionResult);
                        }
                    }

                    lock (_rectangleTracker)
                    {
                        _rectangleTracker.GetObjects(_resultObjects, true);
                    }

                    if (DisplayCameraImage)
                    {
                        Imgproc.putText(grayMat, "W:" + grayMat.width() + " H:" + grayMat.height(), new Point(5, grayMat.rows() - 10), Imgproc.FONT_HERSHEY_SIMPLEX, 1.0, new Scalar(255, 255, 255, 255), 2, Imgproc.LINE_AA, false);
                    }
                    else
                    {
                        // fill all black.
                        Imgproc.rectangle(grayMat, new Point(0, 0), new Point(grayMat.width(), grayMat.height()), new Scalar(0, 0, 0, 0), -1);
                    }

                    // draw face rect.
                    DrawDownScaleFaceRects(grayMat, _resultObjects.ToArray(), downscaleRatio, COLOR_WHITE, 6);
                }
                else
                {
                    Rect[] rectsWhereRegions;

                    if (_hasUpdatedDetectionResult)
                    {
                        _hasUpdatedDetectionResult = false;

                        lock (_rectangleTracker)
                        {
                            rectsWhereRegions = _detectionResult.ToArray();
                        }
                    }
                    else
                    {
                        lock (_rectangleTracker)
                        {
                            rectsWhereRegions = _rectangleTracker.CreateCorrectionBySpeedOfRects();
                        }
                    }

                    _detectedObjectsInRegions.Clear();
                    int len = rectsWhereRegions.Length;
                    for (int i = 0; i < len; i++)
                    {
                        DetectInRegion(downScaleMat, rectsWhereRegions[i], _detectedObjectsInRegions, _cascade);
                    }

                    lock (_rectangleTracker)
                    {
                        _rectangleTracker.UpdateTrackedObjects(_detectedObjectsInRegions);
                        _rectangleTracker.GetObjects(_resultObjects, true);
                    }

                    if (DisplayCameraImage)
                    {
                        Imgproc.putText(grayMat, "W:" + grayMat.width() + " H:" + grayMat.height(), new Point(5, grayMat.rows() - 10), Imgproc.FONT_HERSHEY_SIMPLEX, 1.0, new Scalar(255, 255, 255, 255), 2, Imgproc.LINE_AA, false);
                    }
                    else
                    {
                        // fill all black.
                        Imgproc.rectangle(grayMat, new Point(0, 0), new Point(grayMat.width(), grayMat.height()), new Scalar(0, 0, 0, 0), -1);
                    }

                    // draw previous rect.
                    DrawDownScaleFaceRects(grayMat, rectsWhereRegions, downscaleRatio, COLOR_GRAY, 1);

                    // draw face rect.
                    DrawDownScaleFaceRects(grayMat, _resultObjects.ToArray(), downscaleRatio, COLOR_WHITE, 6);
                }

                DebugUtils.TrackTick();

                Matrix4x4 projectionMatrix = e.ProjectionMatrix;
                Matrix4x4 cameraToWorldMatrix = e.CameraToWorldMatrix;

                Enqueue(() =>
                {
                    try
                    {
                        if (_hlCameraStreamToMatHelper == null || !_hlCameraStreamToMatHelper.IsPlaying)
                        {
                            return;
                        }

                        // Layout is checked here instead of OnFrameMatLayoutChanged.
                        EnsurePreviewTextureMatches(grayMat);
                        if (_texture == null)
                        {
                            return;
                        }

                        OpenCVMatUnityUtils.MatToTexture2D(grayMat, _texture);

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
                            cameraToWorldMatrix = _xrOrigin.transform.localToWorldMatrix * cameraToWorldMatrix;
                        }

                        ApplyQuadPose(cameraToWorldMatrix, projectionMatrix);
                    }
                    finally
                    {
                        grayMat.Dispose();
                    }
                });
                queuedForMainThread = true;
            }
            finally
            {
                if (!queuedForMainThread)
                {
                    grayMat.Dispose();
                }

                _isProcessingFrameMatDelivered = false;
            }
        }

        private void Run()
        {
            _cascade = new CascadeClassifier();
            _cascade.load(_cascadeFilepath);
#if !WINDOWS_UWP || UNITY_EDITOR
            // "empty" method is not working on the UWP platform.
            if (_cascade.empty())
            {
                Debug.LogError("cascade file is not loaded. Please copy from “OpenCVForUnity/StreamingAssets/OpenCVForUnityExample/objdetect/” to “Assets/StreamingAssets/OpenCVForUnityExample/objdetect/” folder. ", this);
            }
#endif

            _cascade4Thread = new CascadeClassifier();
            _cascade4Thread.load(_cascade4ThreadFilepath);
#if !WINDOWS_UWP || UNITY_EDITOR
            // "empty" method is not working on the UWP platform.
            if (_cascade4Thread.empty())
            {
                Debug.LogError("cascade file is not loaded. Please copy from “OpenCVForUnity/StreamingAssets/OpenCVForUnityExample/objdetect/” to “Assets/StreamingAssets/OpenCVForUnityExample/objdetect/” folder. ", this);
            }
#endif

            _hlCameraStreamToMatHelper.Initialize();
        }

        private void RecreatePreviewTexture(Mat imageMat)
        {
            if (imageMat == null || imageMat.cols() <= 0 || imageMat.rows() <= 0)
            {
                return;
            }

            DestroyPreviewTexture();

            // Texture dimensions must match Mat cols()/rows() (width/height may swap when rotated).
            _texture = new Texture2D(imageMat.cols(), imageMat.rows(), TextureFormat.Alpha8, false);
            _texture.wrapMode = TextureWrapMode.Clamp;

            if (_quadRenderer == null)
            {
                _quadRenderer = gameObject.GetComponent<Renderer>();
            }

            if (_quadRenderer != null)
            {
                _quadRenderer.sharedMaterial.SetTexture("_MainTex", _texture);
            }

            OpenCVMatUnityUtils.MatToTexture2D(imageMat, _texture);
        }

        private void EnsurePreviewTextureMatches(Mat imageMat)
        {
            if (imageMat == null)
            {
                return;
            }

            if (_texture != null && _texture.width == imageMat.cols() && _texture.height == imageMat.rows())
            {
                return;
            }

            RecreatePreviewTexture(imageMat);
        }

        private void DestroyPreviewTexture()
        {
            if (_texture != null)
            {
                Texture2D.Destroy(_texture);
                _texture = null;
            }
        }

        private void ApplyProjectionMatrix()
        {
            if (_quadRenderer == null)
            {
                _quadRenderer = gameObject.GetComponent<Renderer>();
            }

            if (_quadRenderer == null)
            {
                return;
            }

            Matrix4x4 projectionMatrix;
#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            projectionMatrix = _hlCameraStreamToMatHelper.ProjectionMatrix;
#else
            // This value is obtained from PhotoCapture's TryGetProjectionMatrix() method. I do not know whether this method is good.
            // Please see the discussion of this thread. Https://forums.hololens.com/discussion/782/live-stream-of-locatable-camera-webcam-in-unity
            projectionMatrix = Matrix4x4.identity;
            projectionMatrix.m00 = 2.31029f;
            projectionMatrix.m01 = 0.00000f;
            projectionMatrix.m02 = 0.09614f;
            projectionMatrix.m03 = 0.00000f;
            projectionMatrix.m10 = 0.00000f;
            projectionMatrix.m11 = 4.10427f;
            projectionMatrix.m12 = -0.06231f;
            projectionMatrix.m13 = 0.00000f;
            projectionMatrix.m20 = 0.00000f;
            projectionMatrix.m21 = 0.00000f;
            projectionMatrix.m22 = -1.00000f;
            projectionMatrix.m23 = 0.00000f;
            projectionMatrix.m30 = 0.00000f;
            projectionMatrix.m31 = 0.00000f;
            projectionMatrix.m32 = -1.00000f;
            projectionMatrix.m33 = 0.00000f;
#endif
            _quadRenderer.sharedMaterial.SetMatrix("_CameraProjectionMatrix", projectionMatrix);
        }

        private void ApplyQuadPose(Matrix4x4 cameraToWorldMatrix, Matrix4x4 projectionMatrix)
        {
            if (_quadRenderer == null)
            {
                return;
            }

            Matrix4x4 worldToCameraMatrix = cameraToWorldMatrix.inverse;
            _quadRenderer.sharedMaterial.SetMatrix("_WorldToCameraMatrix", worldToCameraMatrix);

#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            _quadRenderer.sharedMaterial.SetMatrix("_CameraProjectionMatrix", projectionMatrix);
#else
            _ = projectionMatrix;
#endif

            // Position the canvas object slightly in front of the real world web camera.
            Vector3 position = cameraToWorldMatrix.GetColumn(3) - cameraToWorldMatrix.GetColumn(2) * 2.2f;

            // Rotate the canvas object so that it faces the user.
            Quaternion rotation = Quaternion.LookRotation(-cameraToWorldMatrix.GetColumn(2), cameraToWorldMatrix.GetColumn(1));

            gameObject.transform.position = position;
            gameObject.transform.rotation = rotation;
        }

        private void CleanupResources()
        {
            WaitForFrameMatDeliveredProcessing();

            StopThread();

            lock (_executeOnMainThread)
            {
                while (_executeOnMainThread.Count > 0)
                {
                    _executeOnMainThread.Dequeue().Invoke();
                }
            }

            _hasUpdatedDetectionResult = false;
            _isDetecting = false;

            _grayMat4Thread?.Dispose();
            _grayMat4Thread = null;

            _downScaleMat?.Dispose();
            _downScaleMat = null;

            if (_rectangleTracker != null)
            {
                lock (_rectangleTracker)
                {
                    _rectangleTracker.Reset();
                }
            }

            DestroyPreviewTexture();

            if (DebugStr != null)
            {
                DebugStr.text = string.Empty;
            }
            DebugUtils.ClearDebugStr();
        }

        private void SetupDownScaleWorkMat(Mat grayMat)
        {
            _downScaleMat?.Dispose();
            _downScaleMat = null;

            if (!EnableDownScale)
            {
                _downScaleRatio = 1.0f;
                return;
            }

            _downScaleRatio = DownscaleRatio;
            if (Mathf.Approximately(_downScaleRatio, 1.0f))
            {
                return;
            }

            int width = Mathf.RoundToInt(grayMat.width() / _downScaleRatio);
            int height = Mathf.RoundToInt(grayMat.height() / _downScaleRatio);
            _downScaleMat = new Mat(height, width, grayMat.type());
        }

        private void WaitForFrameMatDeliveredProcessing()
        {
            while (_isProcessingFrameMatDelivered)
            {
                // Wait until the synchronized CV processing on the camera callback thread is finished.
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

            DetectObject(_grayMat4Thread, out _detectionResult, _cascade4Thread);

            _isThreadRunning = false;
            OnDetectionDone();
        }

        private void DetectObject(Mat img, out List<Rect> detectedObjects, CascadeClassifier cascade)
        {
            int d = Mathf.Min(img.width(), img.height());
            d = (int)Mathf.Round(d * MinDetectionSizeRatio);

            MatOfRect objects = new MatOfRect();
            if (cascade != null)
            {
                cascade.detectMultiScale(img, objects, 1.1, 2, Xobjdetect.CASCADE_SCALE_IMAGE, new Size(d, d), new Size());
            }

            detectedObjects = objects.toList();
        }

        private void OnDetectionDone()
        {
            _hasUpdatedDetectionResult = true;

            _isDetecting = false;
        }

        private void DetectInRegion(Mat img, Rect region, List<Rect> detectedObjectsInRegions, CascadeClassifier cascade)
        {
            Rect r0 = new Rect(new Point(), img.size());
            Rect r1 = new Rect(region.x, region.y, region.width, region.height);
            Rect.inflate(r1, (int)((r1.width * _coeffTrackingWindowSize) - r1.width) / 2,
                (int)((r1.height * _coeffTrackingWindowSize) - r1.height) / 2);
            r1 = Rect.intersect(r0, r1);

            if ((r1.width <= 0) || (r1.height <= 0))
            {
                Debug.Log("detectInRegion: Empty intersection", this);
                return;
            }

            int d = Math.Min(region.width, region.height);
            d = (int)Math.Round(d * _coeffObjectSizeToTrack);

            using (MatOfRect tmpobjects = new MatOfRect())
            using (Mat img1 = new Mat(img, r1)) //subimage for rectangle -- without data copying
            {
                cascade.detectMultiScale(img1, tmpobjects, 1.1, 2, 0 | Xobjdetect.CASCADE_DO_CANNY_PRUNING | Xobjdetect.CASCADE_SCALE_IMAGE | Xobjdetect.CASCADE_FIND_BIGGEST_OBJECT, new Size(d, d), new Size());

                Rect[] tmpobjectsArray = tmpobjects.toArray();
                int len = tmpobjectsArray.Length;
                for (int i = 0; i < len; i++)
                {
                    Rect tmp = tmpobjectsArray[i];
                    Rect r = new Rect(new Point(tmp.x + r1.x, tmp.y + r1.y), tmp.size());

                    detectedObjectsInRegions.Add(r);
                }
            }
        }

        private void DrawDownScaleFaceRects(Mat img, Rect[] rects, float downscaleRatio, Scalar color, int thickness)
        {
            int len = rects.Length;
            for (int i = 0; i < len; i++)
            {
                Rect rect = new Rect(
                    (int)(rects[i].x * downscaleRatio),
                    (int)(rects[i].y * downscaleRatio),
                    (int)(rects[i].width * downscaleRatio),
                    (int)(rects[i].height * downscaleRatio)
                );
                Imgproc.rectangle(img, rect, color, thickness);
            }
        }

        private void Enqueue(Action action)
        {
            lock (_executeOnMainThread)
            {
                _executeOnMainThread.Enqueue(action);
            }
        }
    }
}

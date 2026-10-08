using System;
using System.Collections.Generic;
using HoloLensWithOpenCVForUnity.UnityIntegration.Helper.SourceToMat;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Extensions.SourceToMat;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityIntegration;
using OpenCVForUnity.UnityIntegration.Helper.SourceToMat;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HoloLensWithOpenCVForUnityExample
{
    /// <summary>
    /// HLCameraStreamToMatHelper Example
    /// An example of image processing (comic filter) using OpenCVForUnity on Hololens.
    /// Referring to http://dev.classmethod.jp/smartphone/opencv-manga-2/.
    ///
    /// Demonstrates:
    /// - Receiving frames via <see cref="HLCameraStreamToMatHelper.FrameMatDelivered"/> (C# event)
    /// - Checking FrameMat layout when a frame is delivered (does not use OnFrameMatLayoutChanged)
    /// - Calling <see cref="SourceToMatHelperBase.Play"/> in OnInitialized only when not already playing or paused
    /// - Recreating the preview texture when delivered Mat size differs from the current Texture2D
    ///
    /// OpenCV classes and APIs used:
    /// - <see cref="Mat"/>, <see cref="Scalar"/>, <see cref="Point"/>
    /// - <see cref="Imgproc"/>: rectangle, putText
    /// - <see cref="HLCameraStreamToMatHelper"/>, <see cref="SourceToMatColorFormat"/>
    /// - <see cref="OpenCVMatUnityUtils"/>: MatToTexture2DRaw
    ///
    /// Unity integration:
    /// - <see cref="FrameMatDeliveredEventArgs.Mat"/> is newly allocated; this example disposes it on the main thread after display conversion
    /// - On HoloLens, <see cref="HLCameraStreamToMatHelper.FrameMatDelivered"/> runs on the camera callback thread
    /// - On WebCam fallback, the same event runs on the Unity main thread
    /// - Helper lifecycle events (OnInitialized, OnReleased, OnDisposed, OnErrorOccurred) are wired in the Inspector
    /// </summary>
    [RequireComponent(typeof(HLCameraStreamToMatHelper))]
    public class HLCameraStreamToMatHelperExample : MonoBehaviour
    {
        // Public Fields
        /// <summary>
        /// The rotate 90 degree toggle.
        /// </summary>
        public Toggle Rotate90DegreeToggle;

        /// <summary>
        /// The flip vertical toggle.
        /// </summary>
        public Toggle FlipVerticalToggle;

        /// <summary>
        /// The flip horizontal toggle.
        /// </summary>
        public Toggle FlipHorizontalToggle;

        /// <summary>
        /// Determines if applys comic filter.
        /// </summary>
        public bool ApplyComicFilter = false;

        /// <summary>
        /// The apply comic filter toggle.
        /// </summary>
        public Toggle ApplyComicFilterToggle;

        /// <summary>
        /// The vignette scale.
        /// </summary>
        public float VignetteScale = 0f;

        /// <summary>
        /// The vignette scale slider.
        /// </summary>
        public Slider VignetteScaleSlider;

        [Space(10)]
        [HeaderAttribute("Debug")]
        public Text RenderFPS;
        public Text VideoFPS;
        public Text TrackFPS;
        public Text DebugStr;

        // Private Fields
        private readonly Queue<Action> _executeOnMainThread = new Queue<Action>();
        private ComicFilter _comicFilter;
        private Texture2D _texture;
        private Renderer _quadRenderer;
        private HLCameraStreamToMatHelper _hlCameraStreamToMatHelper;
        private XROrigin _xrOrigin;

        // Unity Lifecycle Methods
        private void Start()
        {
            _xrOrigin = FindFirstObjectByType<XROrigin>();

            _hlCameraStreamToMatHelper = gameObject.GetComponent<HLCameraStreamToMatHelper>();
            _hlCameraStreamToMatHelper.FrameMatDelivered += OnFrameMatDelivered;
            _hlCameraStreamToMatHelper.UpdateFrameMatOnTick = false;
            _hlCameraStreamToMatHelper.OutputColorFormat = SourceToMatColorFormat.BGRA;
            _hlCameraStreamToMatHelper.Initialize();

            // Update GUI state
            Rotate90DegreeToggle.isOn = _hlCameraStreamToMatHelper.Rotate90Degree;
            FlipVerticalToggle.isOn = _hlCameraStreamToMatHelper.FlipVertical;
            FlipHorizontalToggle.isOn = _hlCameraStreamToMatHelper.FlipHorizontal;
            ApplyComicFilterToggle.isOn = ApplyComicFilter;
            VignetteScaleSlider.value = VignetteScale;
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

            RecreatePreviewTexture(_hlCameraStreamToMatHelper.FrameMat);

            DebugUtils.AddDebugStr(_hlCameraStreamToMatHelper.OutputColorFormat.ToString() + " " + _hlCameraStreamToMatHelper.Width + " x " + _hlCameraStreamToMatHelper.Height + " : " + _hlCameraStreamToMatHelper.FPS);

            ApplyProjectionMatrix();

            if (_quadRenderer != null)
            {
                _quadRenderer.sharedMaterial.SetFloat("_VignetteScale", VignetteScale);
            }

            _comicFilter?.Dispose();
            _comicFilter = new ComicFilter(60, 120, 3);

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

            CleanupPreviewResources();
        }

        /// <summary>
        /// Raises the helper disposed event.
        /// </summary>
        public void OnSourceToMatHelperDisposed()
        {
            Debug.Log("OnSourceToMatHelperDisposed", this);

            CleanupPreviewResources();
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
        /// Raises the rotate 90 degree toggle value changed event.
        /// </summary>
        public void OnRotate90DegreeToggleValueChanged()
        {
            if (Rotate90DegreeToggle.isOn != _hlCameraStreamToMatHelper.Rotate90Degree)
            {
                _hlCameraStreamToMatHelper.Rotate90Degree = Rotate90DegreeToggle.isOn;
            }
        }

        /// <summary>
        /// Raises the flip vertical toggle value changed event.
        /// </summary>
        public void OnFlipVerticalToggleValueChanged()
        {
            if (FlipVerticalToggle.isOn != _hlCameraStreamToMatHelper.FlipVertical)
            {
                _hlCameraStreamToMatHelper.FlipVertical = FlipVerticalToggle.isOn;
            }
        }

        /// <summary>
        /// Raises the flip horizontal toggle value changed event.
        /// </summary>
        public void OnFlipHorizontalToggleValueChanged()
        {
            if (FlipHorizontalToggle.isOn != _hlCameraStreamToMatHelper.FlipHorizontal)
            {
                _hlCameraStreamToMatHelper.FlipHorizontal = FlipHorizontalToggle.isOn;
            }
        }

        /// <summary>
        /// Raises the apply comic filter toggle value changed event.
        /// </summary>
        public void OnApplyComicFilterToggleValueChanged()
        {
            ApplyComicFilter = ApplyComicFilterToggle.isOn;
        }

        /// <summary>
        /// Raises the vignette scale slider value changed event.
        /// </summary>
        public void OnVignetteScaleSliderValueChanged()
        {
            VignetteScale = VignetteScaleSlider.value;

            if (_quadRenderer != null)
            {
                _quadRenderer.sharedMaterial.SetFloat("_VignetteScale", VignetteScale);
            }
        }

        // Private Methods
        private void OnFrameMatDelivered(object sender, FrameMatDeliveredEventArgs e)
        {
            Mat bgraMat = e.Mat;
            if (bgraMat == null)
            {
                return;
            }

            bool queuedForMainThread = false;
            try
            {
                DebugUtils.VideoTick();

                if (ApplyComicFilter)
                {
                    if (_comicFilter != null)
                    {
                        _comicFilter.Process(bgraMat, bgraMat, true);
                    }
                }
                else
                {
                    DrawFrameOverlay(bgraMat);
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
                        EnsurePreviewTextureMatches(bgraMat);
                        if (_texture == null)
                        {
                            return;
                        }

                        // For BGRA or BGR format, use the matToTexture2DRaw method.
                        OpenCVMatUnityUtils.MatToTexture2DRaw(bgraMat, _texture);

                        // HoloLensCameraStream's cameraToWorldMatrix is relative to the Unity scene
                        // origin returned by GetSceneCoordinateSystem(Pose.identity).
                        // This is not necessarily the same coordinate system as the Unity world
                        // used by the MRTK XROrigin.
                        //
                        // MRTK's XROrigin (Camera Offset) can apply an additional transform,
                        // such as the Camera Y Offset depending on the Tracking Origin Mode.
                        // Therefore, first convert the camera transform into the XROrigin world space.
                        cameraToWorldMatrix = _xrOrigin.transform.localToWorldMatrix * cameraToWorldMatrix;

                        ApplyQuadPose(cameraToWorldMatrix, projectionMatrix);
                    }
                    finally
                    {
                        bgraMat.Dispose();
                    }
                });
                queuedForMainThread = true;
            }
            finally
            {
                if (!queuedForMainThread)
                {
                    bgraMat.Dispose();
                }
            }
        }

        private void RecreatePreviewTexture(Mat imageMat)
        {
            if (imageMat == null || imageMat.cols() <= 0 || imageMat.rows() <= 0)
            {
                return;
            }

            DestroyPreviewTexture();

            // Texture dimensions must match Mat cols()/rows() (width/height may swap when rotated).
            _texture = new Texture2D(imageMat.cols(), imageMat.rows(), TextureFormat.BGRA32, false);
            _texture.wrapMode = TextureWrapMode.Clamp;

            if (_quadRenderer == null)
            {
                _quadRenderer = gameObject.GetComponent<Renderer>();
            }

            if (_quadRenderer != null)
            {
                _quadRenderer.sharedMaterial.SetTexture("_MainTex", _texture);
            }

            OpenCVMatUnityUtils.MatToTexture2DRaw(imageMat, _texture);
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

        private void CleanupPreviewResources()
        {
            // Flush queued frame callbacks so delivered Mats are disposed.
            lock (_executeOnMainThread)
            {
                while (_executeOnMainThread.Count > 0)
                {
                    _executeOnMainThread.Dequeue().Invoke();
                }
            }

            _comicFilter?.Dispose();
            _comicFilter = null;

            DestroyPreviewTexture();

            if (DebugStr != null)
            {
                DebugStr.text = string.Empty;
            }
            DebugUtils.ClearDebugStr();
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

            float halfOfVerticalFov = Mathf.Atan(1.0f / projectionMatrix.m11);
            float aspectRatio = (1.0f / Mathf.Tan(halfOfVerticalFov)) / projectionMatrix.m00;
            Debug.Log("halfOfVerticalFov " + halfOfVerticalFov, this);
            Debug.Log("aspectRatio " + aspectRatio, this);
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

        private void DrawFrameOverlay(Mat bgraMat)
        {
            Imgproc.rectangle(bgraMat, new Point(0, 0), new Point(bgraMat.width(), bgraMat.height()), new Scalar(255, 0, 0, 255), 2);
            Imgproc.putText(bgraMat, "W:" + bgraMat.width() + " H:" + bgraMat.height(), new Point(5, bgraMat.rows() - 10), Imgproc.FONT_HERSHEY_SIMPLEX, 1.0, new Scalar(255, 0, 0, 255), 2, Imgproc.LINE_AA, false);
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

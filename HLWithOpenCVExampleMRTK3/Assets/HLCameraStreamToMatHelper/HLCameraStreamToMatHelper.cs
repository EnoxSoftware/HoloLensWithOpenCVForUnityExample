using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoloLensCameraStream;
using OpenCVForUnity.Extensions.SourceToMat;
using OpenCVForUnity.UnityIntegration.Helper.SourceToMat;
using UnityEngine;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering;
#endif

#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API && !(WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API)
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Extensions.SourceToMat.Grabbers;
using OpenCVForUnity.UnityIntegration.Helper.SourceToMat.Grabbers;
using OpenCVForUnity.UnityIntegration.Helper.SourceToMat.Orientation;
#endif

namespace HoloLensWithOpenCVForUnity.UnityIntegration.Helper.SourceToMat
{
    /// <summary>
    /// Unity helper that reads HoloLens locatable-camera frames via CameraStream and exposes them as OpenCV
    /// <see cref="OpenCVForUnity.CoreModule.Mat"/> buffers. On Editor and non-UWP platforms it falls back to
    /// <c>WebCamTextureMatSource</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Primary Unity entry for HoloLens CameraStream capture. Use
    /// <see cref="SourceToMatHelperBase.MatSource"/> for advanced access.
    /// </para>
    /// <para>
    /// Source selection is compile-time: UWP with CameraStream enabled wires
    /// <c>HLCameraStreamMatSource</c>; otherwise it wires
    /// <c>WebCamTextureMatSource</c>. There is no runtime source switch.
    /// </para>
    /// <para>
    /// On the HoloLens CameraStream path, set <see cref="SourceToMatHelperBase.OutputColorFormat"/> to
    /// <see cref="SourceToMatColorFormat.BGRA"/> or <see cref="SourceToMatColorFormat.GRAY"/> so capture
    /// matches output: BGRA32 for color, NV12 (luma as GRAY) for single-channel processing. Other formats
    /// still capture BGRA32 and incur an extra color conversion in <c>HLCameraStreamMatSource</c>. Rotation
    /// and flip are unchanged. On WebCam fallback the native format may differ.
    /// </para>
    /// <para>
    /// Initialization completes in <see cref="MatSourceState.Ready"/>; call
    /// <see cref="SourceToMatHelperBase.Play"/> or <see cref="SourceToMatHelperBase.PlayAsync"/> to start
    /// delivering frames. <see cref="FrameMatDelivered"/> fires only while
    /// <see cref="SourceToMatHelperBase.IsPlaying"/> is <see langword="true"/>.
    /// </para>
    /// <para>
    /// <see cref="FrameMatDelivered"/> is the unified frame API.
    /// On HoloLens it is forwarded from <c>HLCameraStreamMatSource.FrameMatDelivered</c> on the
    /// camera callback thread and is not marshaled to the Unity main thread.
    /// The helper subscribes to that source event only while <see cref="FrameMatDelivered"/> has at least one subscriber.
    /// On WebCam fallback the helper clones <see cref="SourceToMatHelperBase.FrameMat"/> on the main thread
    /// after <see cref="IMatSourceEvents.OnFrameMatUpdated"/>.
    /// <see cref="FrameMatDeliveredEventArgs.Mat"/> is a newly allocated buffer; the subscriber must dispose it.
    /// Do not subscribe to both <see cref="FrameMatDelivered"/> and
    /// <see cref="SourceToMatHelperBase.OnFrameMatUpdated"/> for the same processing path.
    /// </para>
    /// During Play mode, inspector changes are applied as follows:
    /// <list type="bullet">
    /// <item><description>
    /// <see cref="RequestedWidth"/>, <see cref="RequestedHeight"/>, and <see cref="RequestedFPS"/>:
    /// release and re-initialize the wrapped source. When already
    /// <see cref="MatSourceState.Playing"/> or <see cref="MatSourceState.Paused"/>, that state is restored
    /// before <see cref="SourceToMatHelperBase.OnInitialized"/> is raised.
    /// </description></item>
    /// <item><description>
    /// <see cref="RequestedDeviceName"/>, <see cref="RequestedIsFrontFacing"/>, and
    /// <see cref="RequestedUseAsyncGPUReadback"/>: applied on WebCam fallback (including AsyncGPU grabber
    /// rewire when the effective path changes). Ignored on the HoloLens CameraStream path.
    /// </description></item>
    /// <item><description>
    /// <see cref="SourceToMatHelperBase.Rotate90Degree"/>, <see cref="SourceToMatHelperBase.OutputColorFormat"/>:
    /// sync to the wrapped source, which re-establishes the output layout and raises
    /// <see cref="SourceToMatHelperBase.OnFrameMatLayoutChanged"/>.
    /// </description></item>
    /// <item><description>
    /// <see cref="SourceToMatHelperBase.FlipVertical"/>, <see cref="SourceToMatHelperBase.FlipHorizontal"/>,
    /// <see cref="SourceToMatHelperBase.InitTimeoutMs"/>, and <see cref="UpdateFrameMatOnTick"/>:
    /// applied in place without re-initialization.
    /// WebCam fallback always sets the wrapped source <see cref="MatSourceBase.UpdateFrameMatOnTick"/> to
    /// <see langword="true"/>.
    /// </description></item>
    /// </list>
    /// One-shot <see cref="Initialize(HLCameraStreamOpenOptions)"/> / <see cref="InitializeAsync(HLCameraStreamOpenOptions, CancellationToken)"/>
    /// project only Width / Height / FPS. WebCam-only Inspector fields remain on the source inspector settings.
    /// </remarks>
    public class HLCameraStreamToMatHelper : SourceToMatHelperBase,
        ICameraToMatHelperControls,
        ICameraFacingToMatHelperControls,
        IUnityCameraToMatHelperControls
    {
        // Private Fields
        private int _appliedWidth;
        private int _appliedHeight;
        private float _appliedFPS;
        private bool _appliedUpdateFrameMatOnTick;
        private EventHandler<FrameMatDeliveredEventArgs> _frameMatDelivered;
#if !(WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API)
        private string _appliedDeviceName;
        private bool _appliedIsFrontFacing;
        private bool _appliedEffectiveUseAsyncGPUReadback;
#endif

#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
        private HLCameraStreamFrameGrabber _hlGrabber;
        private HLCameraStreamMatSource _hlCameraStreamMatSource;
#elif !OPENCV_DONT_USE_WEBCAMTEXTURE_API
        private WebCamTextureMatSource _webCamTextureMatSource;
        private WebCamFrameOrientationCorrector _orientationCorrector;
        private IMatSourceEvents _subscribedWebCamFrameUpdated;
        private bool _wiredUseAsyncGPUReadback;
        private ScreenOrientation _lastScreenOrientation;
        private bool _hasLoggedWebGpuAsyncGPUReadbackForce;
#endif

        [Header("Source")]

#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API
        [SerializeField]
        [Tooltip("Source-specific inspector settings. Width / Height / FPS apply on every platform. Device name, facing, and AsyncGPU readback apply to WebCam fallback only.")]
        private WebCamTextureSourceInspectorSettings _sourceSettings = CreateDefaultSourceSettings();
#else
        [SerializeField]
        [Tooltip("Requested camera frame width in pixels.")]
        private int _requestedWidth = 1280;

        [SerializeField]
        [Tooltip("Requested camera frame height in pixels.")]
        private int _requestedHeight = 720;

        [SerializeField]
        [Tooltip("Requested camera frame rate in frames per second.")]
        private float _requestedFPS = 30f;

        private string _requestedDeviceName = string.Empty;
        private bool _requestedIsFrontFacing;
        private bool _requestedUseAsyncGPUReadback;
#endif

        [SerializeField]
        [Tooltip("When enabled, Playing ticks copy grabber frames into the owned FrameMat. When disabled, ticks still grab but leave FrameMat as a layout placeholder unless derived frames are registered. WebCam fallback always updates FrameMat on tick.")]
        private bool _updateFrameMatOnTick = false;

#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API
        /// <summary>
        /// Gets the source inspector settings stored on this helper.
        /// </summary>
        internal WebCamTextureSourceInspectorSettings GetSourceInspectorSettings()
        {
            EnsureSourceSettings();
            return _sourceSettings;
        }
#endif

        // Public Properties
        /// <summary>
        /// Gets or sets the requested device name or index string.
        /// </summary>
        /// <remarks>
        /// Used by WebCam fallback. Ignored on the HoloLens CameraStream path.
        /// </remarks>
        public string RequestedDeviceName
        {
            get => GetRequestedDeviceName();
            set
            {
                if (GetRequestedDeviceName() == value)
                {
                    return;
                }

                SetRequestedDeviceName(value);
#if !(WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API)
                RequestApplyChanges();
#endif
            }
        }

        /// <summary>
        /// Gets or sets the requested frame width in pixels.
        /// </summary>
        public int RequestedWidth
        {
            get => GetRequestedWidth();
            set
            {
                int clampedValue = (int)Mathf.Clamp(value, 0f, float.MaxValue);
                if (GetRequestedWidth() != clampedValue)
                {
                    SetRequestedWidth(clampedValue);
                    RequestApplyChanges();
                }
            }
        }

        /// <summary>
        /// Gets or sets the requested frame height in pixels.
        /// </summary>
        public int RequestedHeight
        {
            get => GetRequestedHeight();
            set
            {
                int clampedValue = (int)Mathf.Clamp(value, 0f, float.MaxValue);
                if (GetRequestedHeight() != clampedValue)
                {
                    SetRequestedHeight(clampedValue);
                    RequestApplyChanges();
                }
            }
        }

        /// <summary>
        /// Gets or sets the requested frame rate in frames per second.
        /// </summary>
        public float RequestedFPS
        {
            get => GetRequestedFPS();
            set
            {
                float clampedValue = Mathf.Clamp(value, -1f, float.MaxValue);
                if (!Mathf.Approximately(GetRequestedFPS(), clampedValue))
                {
                    SetRequestedFPS(clampedValue);
                    RequestApplyChanges();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the front-facing camera is requested.
        /// </summary>
        /// <remarks>
        /// Used by WebCam fallback. Ignored on the HoloLens CameraStream path.
        /// </remarks>
        public bool RequestedIsFrontFacing
        {
            get => GetRequestedIsFrontFacing();
            set
            {
                if (GetRequestedIsFrontFacing() == value)
                {
                    return;
                }

                SetRequestedIsFrontFacing(value);
#if !(WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API)
                RequestApplyChanges();
#endif
            }
        }

        /// <summary>
        /// Gets or sets whether AsyncGPU readback is requested for WebCam capture.
        /// </summary>
        /// <remarks>
        /// Used by WebCam fallback. Ignored on the HoloLens CameraStream path.
        /// When <see cref="RequiresAsyncGPUReadback"/> is <see langword="true"/>, setting this to
        /// <see langword="false"/> is ignored and <see cref="EffectiveUseAsyncGPUReadback"/> remains
        /// <see langword="true"/>.
        /// </remarks>
        public bool RequestedUseAsyncGPUReadback
        {
            get => GetRequestedUseAsyncGPUReadback();
            set
            {
                if (RequiresAsyncGPUReadback && !value)
                {
                    return;
                }

                if (GetRequestedUseAsyncGPUReadback() == value)
                {
                    return;
                }

                SetRequestedUseAsyncGPUReadback(value);
#if !(WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API)
                RequestApplyChanges();
#endif
            }
        }

        /// <summary>
        /// Gets whether this environment requires AsyncGPU readback (WebGPU).
        /// </summary>
        public static bool RequiresAsyncGPUReadback
        {
            get
            {
#if UNITY_6000_0_OR_NEWER
                return SystemInfo.graphicsDeviceType == GraphicsDeviceType.WebGPU;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Gets whether the AsyncGPU grabber is selected for the next (or current) WebCam wiring.
        /// </summary>
        public bool EffectiveUseAsyncGPUReadback => RequestedUseAsyncGPUReadback || RequiresAsyncGPUReadback;

        /// <summary>
        /// Gets or sets whether Playing ticks copy grabber frames into the owned <see cref="SourceToMatHelperBase.FrameMat"/>.
        /// </summary>
        /// <remarks>
        /// Default is <see langword="false"/>. When the wrapped source is wired, this assignment is copied to
        /// <see cref="MatSourceBase.UpdateFrameMatOnTick"/> immediately on the HoloLens CameraStream path, including
        /// while initialization is in progress.
        /// Inspector edits still apply after that delay via <see cref="OnValidate"/>.
        /// WebCam fallback always sets the wrapped source to <see langword="true"/> so tick-cloned
        /// <see cref="FrameMatDelivered"/> delivery keeps working.
        /// When derived frames are registered, the wrapped source still updates
        /// <see cref="SourceToMatHelperBase.FrameMat"/> on tick.
        /// </remarks>
        public bool UpdateFrameMatOnTick
        {
            get => _updateFrameMatOnTick;
            set
            {
                if (_updateFrameMatOnTick == value)
                {
                    return;
                }

                _updateFrameMatOnTick = value;
                ApplyUpdateFrameMatOnTickToWiredMatSource();
                _appliedUpdateFrameMatOnTick = _updateFrameMatOnTick;
            }
        }

        /// <summary>
        /// Gets the effective device name after initialization.
        /// Returns an empty string when the source is not wired.
        /// </summary>
        public string DeviceName
        {
            get
            {
                return MatSource is ICameraMatSource cameraMatSource ? cameraMatSource.DeviceName : string.Empty;
            }
        }

        /// <summary>
        /// Gets the effective frame rate reported by the wrapped source.
        /// Returns <c>-1</c> when the source is not wired.
        /// </summary>
        public float FPS
        {
            get
            {
                return MatSource is ICameraMatSource cameraMatSource ? cameraMatSource.FPS : -1f;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the front-facing camera is active.
        /// </summary>
        /// <remarks>
        /// Always <see langword="false"/> on the HoloLens CameraStream path and when the source is not wired.
        /// </remarks>
        public bool IsFrontFacing
        {
            get
            {
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API && !(WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API)
                return _webCamTextureMatSource != null && _webCamTextureMatSource.IsFrontFacing;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Gets the camera-to-world matrix from the wrapped <see cref="IUnityCameraPoseProvider"/>.
        /// Returns <see cref="Matrix4x4.identity"/> when the source is not wired.
        /// </summary>
        public Matrix4x4 CameraToWorldMatrix
        {
            get
            {
                return MatSource is IUnityCameraPoseProvider poseProvider ? poseProvider.CameraToWorldMatrix : Matrix4x4.identity;
            }
        }

        /// <summary>
        /// Gets the projection matrix from the wrapped <see cref="IUnityCameraPoseProvider"/>.
        /// Returns <see cref="Matrix4x4.identity"/> when the source is not wired.
        /// </summary>
        public Matrix4x4 ProjectionMatrix
        {
            get
            {
                return MatSource is IUnityCameraPoseProvider poseProvider ? poseProvider.ProjectionMatrix : Matrix4x4.identity;
            }
        }

        /// <summary>
        /// Gets the latest locatable-camera intrinsics.
        /// </summary>
        /// <remarks>
        /// WebCam fallback returns <see langword="default"/>.
        /// </remarks>
        public CameraIntrinsics Intrinsics
        {
            get
            {
#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
                if (_hlCameraStreamMatSource != null)
                {
                    return _hlCameraStreamMatSource.Intrinsics;
                }
#endif
                return default;
            }
        }

        /// <summary>
        /// Gets the list of connected camera devices. May be empty when enumeration is unsupported or unavailable.
        /// </summary>
        public IReadOnlyList<CameraDeviceInfo> SupportedDevices
        {
            get
            {
                return MatSource is ICameraMatSource cameraMatSource
                    ? cameraMatSource.SupportedDevices
                    : Array.Empty<CameraDeviceInfo>();
            }
        }

        /// <summary>
        /// Gets the list of connected Unity WebCam devices, including facing and kind.
        /// May be empty when enumeration is unsupported or unavailable.
        /// </summary>
        public IReadOnlyList<UnityCameraDeviceInfo> SupportedUnityDevices
        {
            get
            {
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API && !(WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API)
                if (_webCamTextureMatSource != null)
                {
                    return _webCamTextureMatSource.SupportedUnityDevices;
                }
#endif
                return Array.Empty<UnityCameraDeviceInfo>();
            }
        }

        /// <summary>
        /// Gets supported resolutions for the currently resolved device.
        /// </summary>
        public IReadOnlyList<CameraResolution> SupportedResolutions
        {
            get
            {
                return MatSource is ICameraMatSource cameraMatSource
                    ? cameraMatSource.SupportedResolutions
                    : Array.Empty<CameraResolution>();
            }
        }

        // Public Events
        /// <summary>
        /// Raised when a new converted frame is available while <see cref="SourceToMatHelperBase.IsPlaying"/>
        /// is <see langword="true"/>.
        /// </summary>
        /// <remarks>
        /// On HoloLens this is forwarded from <c>HLCameraStreamMatSource.FrameMatDelivered</c> on the
        /// camera callback thread and is not marshaled to the Unity main thread.
        /// The helper subscribes to that source event only while this event has at least one subscriber.
        /// On WebCam fallback the helper clones <see cref="SourceToMatHelperBase.FrameMat"/> on the Unity main
        /// thread when <see cref="SourceToMatHelperBase.DidUpdateThisFrame"/> is <see langword="true"/>.
        /// <see cref="FrameMatDeliveredEventArgs.Mat"/> is a newly allocated buffer; the subscriber must dispose it.
        /// Does not fire while paused, ready, or uninitialized.
        /// </remarks>
        public event EventHandler<FrameMatDeliveredEventArgs> FrameMatDelivered
        {
            add
            {
                _frameMatDelivered += value;
                SyncDeviceFrameMatDeliveredSubscription();
            }
            remove
            {
                _frameMatDelivered -= value;
                SyncDeviceFrameMatDeliveredSubscription();
            }
        }

        // Unity Lifecycle Methods
        protected virtual void Awake()
        {
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API && !(WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API)
            _lastScreenOrientation = Screen.orientation;
#endif
            EnsureMatSourceWired();
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API && !(WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API)
            if (_orientationCorrector != null)
            {
                _orientationCorrector.NotifyDisplayOrientationChanged((int)_lastScreenOrientation);
            }
#endif
            SyncInspectorToMatSource();
            CaptureAppliedSnapshot();
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API && !(WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API)
            LogWebGpuAsyncGPUReadbackForceOnce();
#endif
        }

        /// <inheritdoc/>
        protected override void Update()
        {
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API && !(WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API)
            ScreenOrientation currentOrientation = Screen.orientation;
            if (_lastScreenOrientation != currentOrientation)
            {
                _lastScreenOrientation = currentOrientation;
                if (_orientationCorrector != null)
                {
                    _orientationCorrector.NotifyDisplayOrientationChanged((int)currentOrientation);
                }
            }
#endif
            base.Update();
        }

        /// <inheritdoc/>
        protected override void OnValidate()
        {
            EnsureSourceSettings();
            ClampRequestedDimensions();
            base.OnValidate();
        }

        /// <summary>
        /// Suspends or resumes HoloLens CameraStream capture when application focus changes.
        /// </summary>
        /// <param name="hasFocus"><see langword="true"/> when the application gained focus.</param>
        /// <remarks>
        /// WebCam fallback is a no-op. The OS does not close the locatable camera on focus loss, so the
        /// HoloLens path delegates to grabber <c>SuspendAsync</c> / <c>ResumeAsync</c>.
        /// </remarks>
        private void OnApplicationFocus(bool hasFocus)
        {
#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            _ = HandleApplicationFocusAsync(hasFocus);
#else
            _ = hasFocus;
#endif
        }

        /// <inheritdoc/>
        protected override void OnDestroy()
        {
            UnsubscribeFrameMatDeliveredBridge();
            base.OnDestroy();
        }

        // Public Methods
        /// <summary>
        /// Gets supported resolutions for the specified device name or index string.
        /// </summary>
        /// <param name="deviceNameOrIndex">A device name or index string.</param>
        /// <returns>
        /// Supported resolutions for the device, or an empty list when the device is unknown,
        /// unsupported, or the backend does not enumerate capture modes (platform-dependent).
        /// </returns>
        public IReadOnlyList<CameraResolution> GetSupportedResolutions(string deviceNameOrIndex)
        {
            return MatSource is ICameraMatSource cameraMatSource
                ? cameraMatSource.GetSupportedResolutions(deviceNameOrIndex)
                : Array.Empty<CameraResolution>();
        }

        /// <summary>
        /// Initializes the helper without awaiting completion.
        /// Playback remains in the ready state until <see cref="SourceToMatHelperBase.Play"/> or
        /// <see cref="SourceToMatHelperBase.PlayAsync"/> is called.
        /// </summary>
        public new void Initialize()
        {
            _ = InitializeAsync();
        }

        /// <summary>
        /// Initializes the helper asynchronously.
        /// Playback remains in the ready state until <see cref="SourceToMatHelperBase.PlayAsync"/> is called.
        /// When initialization is already in progress, the call is ignored.
        /// </summary>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A task that completes when initialization finishes, or immediately when initialization is already in progress.</returns>
        public new async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitializing)
            {
                return;
            }

#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API && !(WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API)
            LogWebGpuAsyncGPUReadbackForceOnce();
#endif
            EnsureMatSourceWired();
            if (MatSource == null)
            {
                return;
            }

            SyncInspectorToMatSource();
            await RunInitializeAsync(PlaybackRestoreMode.Ready, cancellationToken);
            SyncCommonSettingsAndCaptureAppliedSnapshot();
        }

        /// <summary>
        /// Initializes the helper from one-shot HoloLens CameraStream open options without awaiting completion.
        /// Playback remains in the ready state until <see cref="SourceToMatHelperBase.Play"/> or
        /// <see cref="SourceToMatHelperBase.PlayAsync"/> is called.
        /// </summary>
        /// <param name="options">Open options that project Width / Height / FPS before initialization.</param>
        public void Initialize(HLCameraStreamOpenOptions options)
        {
            _ = InitializeAsync(options);
        }

        /// <summary>
        /// Initializes the helper from one-shot HoloLens CameraStream open options asynchronously.
        /// Playback remains in the ready state until <see cref="SourceToMatHelperBase.PlayAsync"/> is called.
        /// When initialization is already in progress, the call is ignored.
        /// </summary>
        /// <param name="options">Open options that project Width / Height / FPS before initialization.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A task that completes when initialization finishes, or immediately when initialization is already in progress.</returns>
        public async Task InitializeAsync(HLCameraStreamOpenOptions options, CancellationToken cancellationToken = default)
        {
            if (_isInitializing)
            {
                return;
            }

            if (options != null)
            {
                EnsureSourceSettings();
                SetRequestedWidth((int)Mathf.Clamp(options.Width, 0f, float.MaxValue));
                SetRequestedHeight((int)Mathf.Clamp(options.Height, 0f, float.MaxValue));
                SetRequestedFPS(Mathf.Clamp(options.FPS, -1f, float.MaxValue));
            }

            await InitializeAsync(cancellationToken);
        }

        // Protected Methods
        /// <inheritdoc/>
        protected override bool HasHeavyRequestedChanges()
        {
            EnsureSourceSettings();
#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            return GetRequestedWidth() != _appliedWidth
                || GetRequestedHeight() != _appliedHeight
                || !Mathf.Approximately(GetRequestedFPS(), _appliedFPS);
#else
            return GetRequestedDeviceName() != _appliedDeviceName
                || GetRequestedWidth() != _appliedWidth
                || GetRequestedHeight() != _appliedHeight
                || !Mathf.Approximately(GetRequestedFPS(), _appliedFPS)
                || GetRequestedIsFrontFacing() != _appliedIsFrontFacing
                || EffectiveUseAsyncGPUReadback != _appliedEffectiveUseAsyncGPUReadback;
#endif
        }

        /// <inheritdoc/>
        protected override async Task ApplyHeavyRequestedChangesAsync(CancellationToken cancellationToken = default)
        {
            EnsureSourceSettings();
            int applyingWidth = GetRequestedWidth();
            int applyingHeight = GetRequestedHeight();
            float applyingFPS = GetRequestedFPS();
#if !(WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API)
            string applyingDeviceName = GetRequestedDeviceName();
            bool applyingIsFrontFacing = GetRequestedIsFrontFacing();
            bool applyingEffectiveUseAsyncGPUReadback = EffectiveUseAsyncGPUReadback;
#endif

            try
            {
                if (!IsInitialized)
                {
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API && !(WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API)
                    RewireMatSourceIfGrabberPathChanged();
#endif
                    SyncInspectorToMatSource();
                    if (_hasCompletedInitialize)
                    {
                        await RecoverInitializeForHeavyApplyAsync(cancellationToken);
                    }

                    return;
                }

                PlaybackRestoreMode mode = CapturePlaybackRestoreMode();
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API && !(WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API)
                bool needsGrabberRewire = EffectiveUseAsyncGPUReadback != _wiredUseAsyncGPUReadback;
                if (needsGrabberRewire)
                {
                    if (MatSource != null && MatSource.IsInitialized)
                    {
                        await MatSource.ReleaseAsync(cancellationToken);
                    }

                    RewireMatSourceIfGrabberPathChanged();
                    SyncInspectorToMatSource();
                    await RunInitializeAsync(mode, cancellationToken);
                    return;
                }
#endif
                SyncInspectorToMatSource();
                await RunReinitializeAsync(mode, cancellationToken);
            }
            finally
            {
                SyncCommonSettingsAndCaptureAppliedSnapshot();
                _appliedWidth = applyingWidth;
                _appliedHeight = applyingHeight;
                _appliedFPS = applyingFPS;
#if !(WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API)
                _appliedDeviceName = applyingDeviceName;
                _appliedIsFrontFacing = applyingIsFrontFacing;
                _appliedEffectiveUseAsyncGPUReadback = applyingEffectiveUseAsyncGPUReadback;
#endif
            }
        }

        /// <inheritdoc/>
        protected override void CaptureAppliedSnapshot()
        {
            EnsureSourceSettings();
            _appliedWidth = GetRequestedWidth();
            _appliedHeight = GetRequestedHeight();
            _appliedFPS = GetRequestedFPS();
            _appliedUpdateFrameMatOnTick = _updateFrameMatOnTick;
#if !(WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API)
            _appliedDeviceName = GetRequestedDeviceName();
            _appliedIsFrontFacing = GetRequestedIsFrontFacing();
            _appliedEffectiveUseAsyncGPUReadback = EffectiveUseAsyncGPUReadback;
#endif
            base.CaptureAppliedSnapshot();
        }

        /// <inheritdoc/>
        protected override bool HasCheapRequestedChanges()
        {
            return base.HasCheapRequestedChanges() || _updateFrameMatOnTick != _appliedUpdateFrameMatOnTick;
        }

        /// <inheritdoc/>
        protected override void ApplyCheapRequestedChanges()
        {
            ApplyUpdateFrameMatOnTickToWiredMatSource();
            _appliedUpdateFrameMatOnTick = _updateFrameMatOnTick;
            base.ApplyCheapRequestedChanges();
        }

        // Private Methods
        private void EnsureMatSourceWired(bool forceRewire = false)
        {
#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            EnsureHlMatSourceWired(forceRewire);
#elif !OPENCV_DONT_USE_WEBCAMTEXTURE_API
            EnsureWebCamMatSourceWired(forceRewire);
#else
            _ = forceRewire;
            Debug.LogWarning(
                "HLCameraStreamToMatHelper: no camera source is available. Enable HoloLens CameraStream on UWP or the WebCamTexture API.",
                this);
#endif
        }

#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
        private void EnsureHlMatSourceWired(bool forceRewire)
        {
            if (!forceRewire && _hlCameraStreamMatSource != null)
            {
                return;
            }

            UnsubscribeFrameMatDeliveredBridge();
            IMatSource previousMatSource = MatSource;
            _hlGrabber = new HLCameraStreamFrameGrabber();
            _hlCameraStreamMatSource = new HLCameraStreamMatSource(_hlGrabber);
            SetMatSource(_hlCameraStreamMatSource);
            _hlCameraStreamMatSource.UpdateFrameMatOnTick = _updateFrameMatOnTick;
            SyncDeviceFrameMatDeliveredSubscription();

            if (previousMatSource != null && !ReferenceEquals(previousMatSource, MatSource))
            {
                previousMatSource.Dispose();
            }
        }

        private void OnHlFrameMatDelivered(object sender, FrameMatDeliveredEventArgs e)
        {
            _ = sender;
            if (e == null)
            {
                return;
            }

            EventHandler<FrameMatDeliveredEventArgs> handler = _frameMatDelivered;
            if (!IsPlaying || handler == null)
            {
                e.Mat?.Dispose();
                return;
            }

            handler(this, e);
        }

        private async Task HandleApplicationFocusAsync(bool hasFocus)
        {
            if (this == null)
            {
                return;
            }

            if (!IsInitialized || _hlGrabber == null)
            {
                return;
            }

            try
            {
                if (hasFocus)
                {
                    await _hlGrabber.ResumeAsync();
                }
                else
                {
                    await _hlGrabber.SuspendAsync();
                }

                if (this == null)
                {
                    return;
                }
            }
            catch (Exception exception)
            {
                if (this == null)
                {
                    return;
                }

                Debug.LogException(exception, this);
            }
        }
#elif !OPENCV_DONT_USE_WEBCAMTEXTURE_API
        private void RewireMatSourceIfGrabberPathChanged()
        {
            if (_webCamTextureMatSource != null && EffectiveUseAsyncGPUReadback == _wiredUseAsyncGPUReadback)
            {
                return;
            }

            IMatSource previousMatSource = MatSource;
            EnsureMatSourceWired(forceRewire: true);

            if (previousMatSource != null && !ReferenceEquals(previousMatSource, MatSource))
            {
                previousMatSource.Dispose();
            }
        }

        private void EnsureWebCamMatSourceWired(bool forceRewire)
        {
            bool wantAsyncGPUReadback = EffectiveUseAsyncGPUReadback;
            if (!forceRewire && _webCamTextureMatSource != null && _wiredUseAsyncGPUReadback == wantAsyncGPUReadback)
            {
                return;
            }

            if (_orientationCorrector == null)
            {
                _orientationCorrector = new WebCamFrameOrientationCorrector();
            }

            IFrameGrabber grabber;
            if (wantAsyncGPUReadback)
            {
                grabber = new WebCamTextureAsyncGPUReadbackFrameGrabber();
            }
            else
            {
                grabber = new WebCamTextureFrameGrabber();
            }

            UnsubscribeFrameMatDeliveredBridge();
            _webCamTextureMatSource = new WebCamTextureMatSource(grabber, _orientationCorrector);
            _wiredUseAsyncGPUReadback = wantAsyncGPUReadback;
            SetMatSource(_webCamTextureMatSource);
            _webCamTextureMatSource.UpdateFrameMatOnTick = true;
            SubscribeWebCamFrameMatUpdated();
        }

        private void SubscribeWebCamFrameMatUpdated()
        {
            UnsubscribeFrameMatDeliveredBridge();
            if (_webCamTextureMatSource is not IMatSourceEvents events)
            {
                return;
            }

            events.OnFrameMatUpdated += OnWebCamFrameMatUpdated;
            _subscribedWebCamFrameUpdated = events;
        }

        private void OnWebCamFrameMatUpdated()
        {
            RaiseFrameMatDeliveredFromFrameMat();
        }

        private void RaiseFrameMatDeliveredFromFrameMat()
        {
            if (!IsPlaying || !DidUpdateThisFrame)
            {
                return;
            }

            EventHandler<FrameMatDeliveredEventArgs> handler = _frameMatDelivered;
            if (handler == null)
            {
                return;
            }

            IMatSource matSource = MatSource;
            if (matSource == null)
            {
                return;
            }

            Mat frameMat = matSource.FrameMat;
            if (frameMat == null || frameMat.IsDisposed)
            {
                return;
            }

            Mat deliveredMat = new Mat();
            bool ownershipTransferred = false;
            try
            {
                frameMat.copyTo(deliveredMat);
                if (!IsPlaying)
                {
                    return;
                }

                handler = _frameMatDelivered;
                if (handler == null)
                {
                    return;
                }

                handler(
                    this,
                    new FrameMatDeliveredEventArgs(
                        deliveredMat,
                        ProjectionMatrix,
                        CameraToWorldMatrix,
                        default));
                ownershipTransferred = true;
            }
            finally
            {
                if (!ownershipTransferred)
                {
                    deliveredMat?.Dispose();
                }
            }
        }

        private void LogWebGpuAsyncGPUReadbackForceOnce()
        {
            if (!RequiresAsyncGPUReadback || _hasLoggedWebGpuAsyncGPUReadbackForce)
            {
                return;
            }

            _hasLoggedWebGpuAsyncGPUReadbackForce = true;
            Debug.Log(
                "HLCameraStreamToMatHelper: WebGPU requires AsyncGPU readback; EffectiveUseAsyncGPUReadback is forced to true.",
                this);
        }
#endif

        private void SyncDeviceFrameMatDeliveredSubscription()
        {
#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            if (_hlCameraStreamMatSource == null)
            {
                return;
            }

            _hlCameraStreamMatSource.FrameMatDelivered -= OnHlFrameMatDelivered;
            if (_frameMatDelivered != null)
            {
                _hlCameraStreamMatSource.FrameMatDelivered += OnHlFrameMatDelivered;
            }
#endif
        }

        private void UnsubscribeFrameMatDeliveredBridge()
        {
#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            if (_hlCameraStreamMatSource != null)
            {
                _hlCameraStreamMatSource.FrameMatDelivered -= OnHlFrameMatDelivered;
            }
#elif !OPENCV_DONT_USE_WEBCAMTEXTURE_API
            if (_subscribedWebCamFrameUpdated != null)
            {
                _subscribedWebCamFrameUpdated.OnFrameMatUpdated -= OnWebCamFrameMatUpdated;
                _subscribedWebCamFrameUpdated = null;
            }
#endif
        }

        private void SyncInspectorToMatSource()
        {
#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            if (_hlCameraStreamMatSource != null)
            {
                _hlCameraStreamMatSource.RequestedDeviceName = GetRequestedDeviceName();
                _hlCameraStreamMatSource.RequestedWidth = GetRequestedWidth();
                _hlCameraStreamMatSource.RequestedHeight = GetRequestedHeight();
                _hlCameraStreamMatSource.RequestedFPS = GetRequestedFPS();
            }
#elif !OPENCV_DONT_USE_WEBCAMTEXTURE_API
            if (_webCamTextureMatSource != null)
            {
                _webCamTextureMatSource.RequestedDeviceName = GetRequestedDeviceName();
                _webCamTextureMatSource.RequestedWidth = GetRequestedWidth();
                _webCamTextureMatSource.RequestedHeight = GetRequestedHeight();
                _webCamTextureMatSource.RequestedFPS = GetRequestedFPS();
                _webCamTextureMatSource.RequestedIsFrontFacing = GetRequestedIsFrontFacing();
            }
#endif
            if (MatSource == null)
            {
                return;
            }

            ApplyUpdateFrameMatOnTickToWiredMatSource();
            SyncCommonSettingsToMatSource();
            SyncDerivedFramesToMatSource();
        }

        private void ApplyUpdateFrameMatOnTickToWiredMatSource()
        {
#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            SyncUpdateFrameMatOnTickToMatSource(_updateFrameMatOnTick);
#elif !OPENCV_DONT_USE_WEBCAMTEXTURE_API
            SyncUpdateFrameMatOnTickToMatSource(true);
#endif
        }

        private void EnsureSourceSettings()
        {
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API
            if (_sourceSettings == null)
            {
                _sourceSettings = CreateDefaultSourceSettings();
            }
#endif
        }

        private void ClampRequestedDimensions()
        {
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API
            EnsureSourceSettings();
            _sourceSettings.ClampDimensions();
#else
            _requestedWidth = (int)Mathf.Clamp(_requestedWidth, 0f, float.MaxValue);
            _requestedHeight = (int)Mathf.Clamp(_requestedHeight, 0f, float.MaxValue);
            _requestedFPS = Mathf.Clamp(_requestedFPS, -1f, float.MaxValue);
#endif
        }

        private string GetRequestedDeviceName()
        {
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API
            EnsureSourceSettings();
            return _sourceSettings.RequestedDeviceName;
#else
            return _requestedDeviceName ?? string.Empty;
#endif
        }

        private void SetRequestedDeviceName(string value)
        {
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API
            EnsureSourceSettings();
            _sourceSettings.RequestedDeviceName = value;
#else
            _requestedDeviceName = value ?? string.Empty;
#endif
        }

        private int GetRequestedWidth()
        {
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API
            EnsureSourceSettings();
            return _sourceSettings.RequestedWidth;
#else
            return _requestedWidth;
#endif
        }

        private void SetRequestedWidth(int value)
        {
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API
            EnsureSourceSettings();
            _sourceSettings.RequestedWidth = value;
#else
            _requestedWidth = value;
#endif
        }

        private int GetRequestedHeight()
        {
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API
            EnsureSourceSettings();
            return _sourceSettings.RequestedHeight;
#else
            return _requestedHeight;
#endif
        }

        private void SetRequestedHeight(int value)
        {
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API
            EnsureSourceSettings();
            _sourceSettings.RequestedHeight = value;
#else
            _requestedHeight = value;
#endif
        }

        private float GetRequestedFPS()
        {
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API
            EnsureSourceSettings();
            return _sourceSettings.RequestedFPS;
#else
            return _requestedFPS;
#endif
        }

        private void SetRequestedFPS(float value)
        {
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API
            EnsureSourceSettings();
            _sourceSettings.RequestedFPS = value;
#else
            _requestedFPS = value;
#endif
        }

        private bool GetRequestedIsFrontFacing()
        {
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API
            EnsureSourceSettings();
            return _sourceSettings.RequestedIsFrontFacing;
#else
            return _requestedIsFrontFacing;
#endif
        }

        private void SetRequestedIsFrontFacing(bool value)
        {
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API
            EnsureSourceSettings();
            _sourceSettings.RequestedIsFrontFacing = value;
#else
            _requestedIsFrontFacing = value;
#endif
        }

        private bool GetRequestedUseAsyncGPUReadback()
        {
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API
            EnsureSourceSettings();
            return _sourceSettings.RequestedUseAsyncGPUReadback;
#else
            return _requestedUseAsyncGPUReadback;
#endif
        }

        private void SetRequestedUseAsyncGPUReadback(bool value)
        {
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API
            EnsureSourceSettings();
            _sourceSettings.RequestedUseAsyncGPUReadback = value;
#else
            _requestedUseAsyncGPUReadback = value;
#endif
        }

#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API
        private static WebCamTextureSourceInspectorSettings CreateDefaultSourceSettings()
        {
            return new WebCamTextureSourceInspectorSettings
            {
                RequestedWidth = 1280,
                RequestedHeight = 720,
                RequestedFPS = 30f
            };
        }
#endif
    }
}

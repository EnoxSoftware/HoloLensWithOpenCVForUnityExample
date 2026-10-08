#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoloLensCameraStream;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Extensions;
using OpenCVForUnity.Extensions.SourceToMat;
using OpenCVForUnity.Extensions.SourceToMat.Grabbers;
using OpenCVForUnity.UnityIntegration.Helper.SourceToMat.Grabbers;
using UnityEngine;

#if XR_PLUGIN_OPENXR
using Windows.Perception.Spatial;
#endif

namespace HoloLensWithOpenCVForUnity.UnityIntegration.Helper.SourceToMat
{
    /// <summary>
    /// Camera-thread payload raised by <see cref="HLCameraStreamFrameGrabber.RawFrameAcquired"/>.
    /// </summary>
    /// <remarks>
    /// Raised on the HoloLens camera callback thread. <see cref="ImageBytes"/> is a grabber-owned buffer
    /// that is reused on later frames; copy the pixels before returning from the handler.
    /// </remarks>
    public sealed class HLCameraStreamRawFrameEventArgs : EventArgs
    {
        // Public Properties
        /// <summary>
        /// Gets the grabber-owned image buffer. The buffer is reused; copy before returning.
        /// </summary>
        public byte[] ImageBytes { get; }

        /// <summary>
        /// Gets the frame width in pixels.
        /// </summary>
        public int FrameWidth { get; }

        /// <summary>
        /// Gets the frame height in pixels.
        /// </summary>
        public int FrameHeight { get; }

        /// <summary>
        /// Gets the locatable-camera projection matrix.
        /// </summary>
        public Matrix4x4 ProjectionMatrix { get; }

        /// <summary>
        /// Gets the locatable-camera camera-to-world matrix.
        /// </summary>
        public Matrix4x4 CameraToWorldMatrix { get; }

        /// <summary>
        /// Gets the camera intrinsics for this frame.
        /// </summary>
        public CameraIntrinsics Intrinsics { get; }

        // Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="HLCameraStreamRawFrameEventArgs"/> class.
        /// </summary>
        /// <param name="imageBytes">Grabber-owned image buffer for this frame.</param>
        /// <param name="frameWidth">Frame width in pixels.</param>
        /// <param name="frameHeight">Frame height in pixels.</param>
        /// <param name="projectionMatrix">Locatable-camera projection matrix.</param>
        /// <param name="cameraToWorldMatrix">Locatable-camera camera-to-world matrix.</param>
        /// <param name="intrinsics">Camera intrinsics for this frame.</param>
        public HLCameraStreamRawFrameEventArgs(
            byte[] imageBytes,
            int frameWidth,
            int frameHeight,
            Matrix4x4 projectionMatrix,
            Matrix4x4 cameraToWorldMatrix,
            CameraIntrinsics intrinsics)
        {
            ImageBytes = imageBytes;
            FrameWidth = frameWidth;
            FrameHeight = frameHeight;
            ProjectionMatrix = projectionMatrix;
            CameraToWorldMatrix = cameraToWorldMatrix;
            Intrinsics = intrinsics;
        }
    }

    /// <summary>
    /// HoloLens CameraStream <see cref="IFrameGrabber"/> that converts camera-thread callbacks into pull frames.
    /// </summary>
    /// <remarks>
    /// Copies raw BGRA or NV12 (GRAY) bytes plus locatable-camera metadata. Color conversion, 90-degree rotation,
    /// and flip are not applied here. <see cref="OpenAsync"/> waits for the first frame, then calls
    /// <c>StopVideoMode</c> so <c>Ready</c> does not keep streaming. Playback is started again from
    /// <see cref="IUnityFrameGrabberPlayback.BeginPlaybackAsync"/>.
    /// The Open body runs inside <see cref="SourceToMatSynchronizationContextScope"/>.
    /// </remarks>
    public sealed class HLCameraStreamFrameGrabber :
        IFrameGrabber,
        IFrameGrabberLayoutFrame,
        IFrameGrabberOpenState,
        IUnityFrameGrabberPlayback,
        ISourceToMatErrorSource
    {
        // Private Fields
        private readonly int _mainThreadId;
        private readonly SynchronizationContext _mainThreadContext;
        private readonly SemaphoreSlim _videoModeGate = new SemaphoreSlim(1, 1);
        private readonly object _snapshotLock = new object();
        private readonly object _latestImageBytesLockObject = new object();
        private readonly object _videoCaptureLockObject = new object();

        private VideoCapture _videoCapture;
        private CameraParameters _cameraParams;
        private Mat _frameMat;
        private byte[] _latestImageBytes;
        private int _operationGeneration;
        private HLCameraStreamMatSource _deliveryMatSource;
        private int _requestedWidth = 1280;
        private int _requestedHeight = 720;
        private float _requestedFPS = 30f;
        private SourceToMatColorFormat _requestedColorFormat = SourceToMatColorFormat.BGRA;
        private bool _hasCachedSnapshot;
        private CaptureSnapshot _latestSnapshot;
        private bool _isOpen;
        private bool _isPlaybackActive;
        private bool _hasUnreportedFrame;
        private volatile bool _hasFirstFrameArrived;
        private bool _wasPlayingBeforeSuspended;
        private bool _isSuspended;
        private bool _disposed;

#if XR_PLUGIN_OPENXR
        private SpatialCoordinateSystem _spatialCoordinateSystem;
#elif XR_PLUGIN_WINDOWSMR || BUILTIN_XR
        private IntPtr _spatialCoordinateSystemPtr;
#endif

        // Public Properties
        /// <summary>
        /// Gets or sets the requested frame width in pixels. Must be set before <see cref="OpenAsync"/>.
        /// </summary>
        public int RequestedWidth
        {
            get => _requestedWidth;
            set => _requestedWidth = (int)Mathf.Clamp(value, 0f, float.MaxValue);
        }

        /// <summary>
        /// Gets or sets the requested frame height in pixels. Must be set before <see cref="OpenAsync"/>.
        /// </summary>
        public int RequestedHeight
        {
            get => _requestedHeight;
            set => _requestedHeight = (int)Mathf.Clamp(value, 0f, float.MaxValue);
        }

        /// <summary>
        /// Gets or sets the requested frame rate. Must be set before <see cref="OpenAsync"/>.
        /// </summary>
        public float RequestedFPS
        {
            get => _requestedFPS;
            set => _requestedFPS = Mathf.Clamp(value, -1f, float.MaxValue);
        }

        /// <summary>
        /// Gets or sets the requested grabber color format. <see cref="SourceToMatColorFormat.GRAY"/> selects NV12;
        /// any other value selects BGRA32. Must be set before <see cref="OpenAsync"/>.
        /// </summary>
        public SourceToMatColorFormat RequestedColorFormat
        {
            get => _requestedColorFormat;
            set => _requestedColorFormat = value;
        }

        /// <inheritdoc/>
        public bool IsOpen => _isOpen;

        /// <summary>
        /// Gets the effective capture frame rate after a successful open, or <c>-1</c> when closed.
        /// </summary>
        public float FPS => _isOpen ? _cameraParams.frameRate : -1f;

        /// <summary>
        /// Gets the captured frame width in pixels, or <c>0</c> before the first sample.
        /// </summary>
        public int FrameWidth =>
            TryGetPublishedSnapshot(out CaptureSnapshot snapshot) ? snapshot.FrameWidth : 0;

        /// <summary>
        /// Gets the captured frame height in pixels, or <c>0</c> before the first sample.
        /// </summary>
        public int FrameHeight =>
            TryGetPublishedSnapshot(out CaptureSnapshot snapshot) ? snapshot.FrameHeight : 0;

        /// <summary>
        /// Gets the latest locatable-camera camera-to-world matrix snapshotted with the most recent frame sample.
        /// </summary>
        public Matrix4x4 CameraToWorldMatrix =>
            TryGetPublishedSnapshot(out CaptureSnapshot snapshot) ? snapshot.CameraToWorldMatrix : Matrix4x4.identity;

        /// <summary>
        /// Gets the latest locatable-camera projection matrix snapshotted with the most recent frame sample.
        /// </summary>
        public Matrix4x4 ProjectionMatrix =>
            TryGetPublishedSnapshot(out CaptureSnapshot snapshot) ? snapshot.ProjectionMatrix : Matrix4x4.identity;

        /// <summary>
        /// Gets the latest locatable-camera intrinsics snapshotted with the most recent frame sample.
        /// </summary>
        public CameraIntrinsics Intrinsics =>
            TryGetPublishedSnapshot(out CaptureSnapshot snapshot) ? snapshot.Intrinsics : default;

        // Public Events
        /// <inheritdoc/>
        public event Action<SourceToMatErrorCode, string> ErrorOccurred;

        /// <summary>
        /// Raised on the camera callback thread when a new raw frame is copied.
        /// </summary>
        public event EventHandler<HLCameraStreamRawFrameEventArgs> RawFrameAcquired;

        // Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="HLCameraStreamFrameGrabber"/> class.
        /// Construct on the Unity main thread so host marshaling can capture the player-loop context.
        /// </summary>
        public HLCameraStreamFrameGrabber()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _mainThreadContext = SynchronizationContext.Current;
        }

        // Public Methods
        /// <inheritdoc/>
        public async Task OpenAsync(InitTimeoutBudget budget, CancellationToken cancellationToken = default)
        {
            if (budget == null)
            {
                throw new ArgumentNullException(nameof(budget));
            }

            await RunSerializedAsync(() => OpenCoreAsync(budget, cancellationToken), cancellationToken);
        }

        /// <inheritdoc/>
        public async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                return;
            }

            await RunSerializedAsync(
                async () =>
                {
                    await EnsureMainThreadAsync(cancellationToken);
                    SynchronizationContext hostLoopContext = ResolveHostLoopContext();
                    using (new SourceToMatSynchronizationContextScope(hostLoopContext))
                    using (InitTimeoutBudget budget = InitTimeoutBudget.FromMilliseconds(0, cancellationToken))
                    {
                        await CloseCoreAsync(budget, cancellationToken, hostLoopContext);
                    }
                },
                cancellationToken);
        }

        /// <inheritdoc/>
        public bool TryGrab(out Mat frame)
        {
            frame = null;

            if (!_isOpen || _frameMat == null || _frameMat.IsDisposed)
            {
                return false;
            }

            if (!TryCopyUnreportedFrameToMat())
            {
                return false;
            }

            frame = _frameMat;
            return !frame.empty();
        }

        /// <inheritdoc/>
        public bool TryGetLayoutFrame(out Mat frame)
        {
            frame = null;

            if (!_isOpen || _frameMat == null || _frameMat.IsDisposed || _frameMat.empty())
            {
                return false;
            }

            TryCopyLatestBytesToFrameMat();
            frame = _frameMat;
            return !frame.empty();
        }

        /// <inheritdoc/>
        public Task BeginPlaybackAsync(CancellationToken cancellationToken = default)
        {
            return RunSerializedAsync(
                () => ChangeVideoModeAsync(startStreaming: true, clearUnreportedFrame: false, cancellationToken),
                cancellationToken);
        }

        /// <inheritdoc/>
        public Task PausePlaybackAsync(CancellationToken cancellationToken = default)
        {
            return RunSerializedAsync(
                () => ChangeVideoModeAsync(startStreaming: false, clearUnreportedFrame: false, cancellationToken),
                cancellationToken);
        }

        /// <inheritdoc/>
        public Task StopPlaybackAsync(CancellationToken cancellationToken = default)
        {
            return RunSerializedAsync(
                () => ChangeVideoModeAsync(startStreaming: false, clearUnreportedFrame: true, cancellationToken),
                cancellationToken);
        }

        /// <summary>
        /// Stops streaming and disposes <c>VideoCapture</c> while keeping this grabber open.
        /// Used when the HoloLens app loses focus; the OS does not close the camera automatically.
        /// </summary>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A task that completes when the capture object has been released.</returns>
        public Task SuspendAsync(CancellationToken cancellationToken = default)
        {
            return RunSerializedAsync(() => SuspendCoreAsync(cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Recreates <c>VideoCapture</c>, restores the spatial coordinate origin, and restarts streaming
        /// when playback was active at <see cref="SuspendAsync"/>.
        /// </summary>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A task that completes when resume work finishes.</returns>
        public Task ResumeAsync(CancellationToken cancellationToken = default)
        {
            return RunSerializedAsync(() => ResumeCoreAsync(cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Returns capture resolutions from the active <c>VideoCapture</c>, or an empty array when closed.
        /// </summary>
        /// <returns>Supported locatable-camera resolutions.</returns>
        public HoloLensCameraStream.Resolution[] GetSupportedResolutions()
        {
            lock (_videoCaptureLockObject)
            {
                if (_videoCapture == null)
                {
                    return Array.Empty<HoloLensCameraStream.Resolution>();
                }

                return _videoCapture.GetSupportedResolutions().ToArray();
            }
        }

        /// <summary>
        /// Releases native camera resources. Prefer <see cref="CloseAsync"/> when an async teardown is available.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _isOpen = false;
            _isPlaybackActive = false;
            InvalidatePendingCallbacks();
            ReleaseVideoCaptureBestEffort();
            ReleaseBuffers();
            SetDeliveryMatSource(null);
            _videoModeGate.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Registers the mat source that produces delivered frames while the image-bytes lock is held.
        /// </summary>
        /// <param name="matSource">
        /// Delivery mat source, or <see langword="null"/> to clear the registration.
        /// </param>
        internal void SetDeliveryMatSource(HLCameraStreamMatSource matSource)
        {
            lock (_latestImageBytesLockObject)
            {
                _deliveryMatSource = matSource;
            }
        }

        // Private Methods
        private async Task OpenCoreAsync(InitTimeoutBudget budget, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (budget.IsExpired)
                {
                    throw new OperationCanceledException();
                }

                await EnsureMainThreadAsync(cancellationToken);

                SynchronizationContext hostLoopContext = ResolveHostLoopContext();
                using (new SourceToMatSynchronizationContextScope(hostLoopContext))
                {
                    await CloseCoreAsync(budget, cancellationToken, hostLoopContext);

                    CacheSpatialCoordinateSystem();

                    VideoCapture videoCapture = await CreateVideoCaptureAsync(budget, cancellationToken, hostLoopContext);
                    if (videoCapture == null)
                    {
                        RaiseError(
                            SourceToMatErrorCode.CAMERA_DEVICE_NOT_EXIST,
                            "Did not find a video capture object. You may not be using the HoloLens.");
                        return;
                    }

                    lock (_videoCaptureLockObject)
                    {
                        _videoCapture = videoCapture;
                    }

                    ApplyWorldOrigin();

                    if (!TryCreateCameraParams(videoCapture, out _cameraParams))
                    {
                        RaiseError(
                            SourceToMatErrorCode.CAMERA_RESOLUTION_UNSUPPORTED,
                            "No supported HoloLens camera resolution was found.");
                        ReleaseVideoCaptureImmediate();
                        return;
                    }

                    SubscribeFrameSampleAcquired();

                    if (!await StartVideoModeAsync(budget, cancellationToken, hostLoopContext))
                    {
                        RaiseError(SourceToMatErrorCode.CAMERA_START_FAILED, "StartVideoModeAsync failed.");
                        ReleaseVideoCaptureImmediate();
                        return;
                    }

                    bool didUpdate = await SourceToMatHostWait.WaitUntilOnHostLoopAsync(
                        () => _hasFirstFrameArrived,
                        budget,
                        cancellationToken,
                        hostLoopContext);
                    if (!didUpdate)
                    {
                        throw new OperationCanceledException();
                    }

                    if (!EnsureFrameMat())
                    {
                        RaiseError(SourceToMatErrorCode.CAMERA_START_FAILED, "The first camera frame was empty.");
                        ReleaseVideoCaptureImmediate();
                        return;
                    }

                    TryCopyLatestBytesToFrameMat();

                    // Ready must not keep the locatable camera streaming.
                    await budget.RunOutsideTimeoutAsync(
                        ct => StopVideoModeAsync(ct, hostLoopContext),
                        cancellationToken);

                    _isPlaybackActive = false;
                    _isSuspended = false;
                    SetHasUnreportedFrame(true);
                    _isOpen = true;
                }
            }
            catch (OperationCanceledException)
            {
                InvalidatePendingCallbacks();
                ReleaseVideoCaptureImmediate();
                ResetOpenState();
                throw;
            }
        }

        private async Task CloseCoreAsync(
            InitTimeoutBudget budget,
            CancellationToken cancellationToken,
            SynchronizationContext hostLoopContext)
        {
            _isOpen = false;
            _isPlaybackActive = false;
            _isSuspended = false;
            SetHasUnreportedFrame(false);
            _hasFirstFrameArrived = false;
            InvalidatePendingCallbacks();
            UnsubscribeFrameSampleAcquired();

            VideoCapture videoCapture = PeekVideoCapture();
            if (videoCapture != null && videoCapture.IsStreaming)
            {
                await StopVideoModeAsync(budget, cancellationToken, hostLoopContext);
            }

            ReleaseVideoCaptureImmediate();
            ReleaseBuffers();
        }

        private async Task ChangeVideoModeAsync(
            bool startStreaming,
            bool clearUnreportedFrame,
            CancellationToken cancellationToken)
        {
            if (!_isOpen || _disposed)
            {
                return;
            }

            await EnsureMainThreadAsync(cancellationToken);
            SynchronizationContext hostLoopContext = ResolveHostLoopContext();
            using (new SourceToMatSynchronizationContextScope(hostLoopContext))
            using (InitTimeoutBudget budget = InitTimeoutBudget.FromMilliseconds(0, cancellationToken))
            {
                VideoCapture videoCapture = PeekVideoCapture();
                if (videoCapture == null)
                {
                    return;
                }

                if (startStreaming)
                {
                    if (videoCapture.IsStreaming)
                    {
                        _isPlaybackActive = true;
                        return;
                    }

                    _isPlaybackActive = true;
                    if (!await StartVideoModeAsync(budget, cancellationToken, hostLoopContext))
                    {
                        _isPlaybackActive = false;
                        RaiseError(SourceToMatErrorCode.CAMERA_START_FAILED, "StartVideoModeAsync failed.");
                    }

                    return;
                }

                _isPlaybackActive = false;
                if (clearUnreportedFrame)
                {
                    SetHasUnreportedFrame(false);
                }

                if (!videoCapture.IsStreaming)
                {
                    return;
                }

                await StopVideoModeAsync(budget, cancellationToken, hostLoopContext);
            }
        }

        private async Task SuspendCoreAsync(CancellationToken cancellationToken)
        {
            if (!_isOpen || _disposed || _isSuspended)
            {
                return;
            }

            await EnsureMainThreadAsync(cancellationToken);
            SynchronizationContext hostLoopContext = ResolveHostLoopContext();
            using (new SourceToMatSynchronizationContextScope(hostLoopContext))
            using (InitTimeoutBudget budget = InitTimeoutBudget.FromMilliseconds(0, cancellationToken))
            {
                VideoCapture videoCapture = PeekVideoCapture();
                _wasPlayingBeforeSuspended = _isPlaybackActive || (videoCapture != null && videoCapture.IsStreaming);
                _isPlaybackActive = false;
                InvalidatePendingCallbacks();
                UnsubscribeFrameSampleAcquired();

                if (videoCapture != null && videoCapture.IsStreaming)
                {
                    await StopVideoModeAsync(budget, cancellationToken, hostLoopContext);
                }

                ReleaseVideoCaptureImmediate();
                _isSuspended = true;
            }
        }

        private async Task ResumeCoreAsync(CancellationToken cancellationToken)
        {
            if (!_isOpen || _disposed || !_isSuspended)
            {
                return;
            }

            await EnsureMainThreadAsync(cancellationToken);
            SynchronizationContext hostLoopContext = ResolveHostLoopContext();
            using (new SourceToMatSynchronizationContextScope(hostLoopContext))
            using (InitTimeoutBudget budget = InitTimeoutBudget.FromMilliseconds(0, cancellationToken))
            {
                CacheSpatialCoordinateSystem();

                VideoCapture videoCapture = await CreateVideoCaptureAsync(budget, cancellationToken, hostLoopContext);
                if (videoCapture == null)
                {
                    RaiseError(
                        SourceToMatErrorCode.CAMERA_DEVICE_NOT_EXIST,
                        "Did not find a video capture object. You may not be using the HoloLens.");
                    return;
                }

                lock (_videoCaptureLockObject)
                {
                    _videoCapture = videoCapture;
                }

                ApplyWorldOrigin();
                SubscribeFrameSampleAcquired();
                _isSuspended = false;

                if (!_wasPlayingBeforeSuspended)
                {
                    return;
                }

                _isPlaybackActive = true;
                if (!await StartVideoModeAsync(budget, cancellationToken, hostLoopContext))
                {
                    _isPlaybackActive = false;
                    RaiseError(SourceToMatErrorCode.CAMERA_START_FAILED, "StartVideoModeAsync failed.");
                }
            }
        }

        private async Task<VideoCapture> CreateVideoCaptureAsync(
            InitTimeoutBudget budget,
            CancellationToken cancellationToken,
            SynchronizationContext hostLoopContext)
        {
            var tcs = new TaskCompletionSource<VideoCapture>(TaskCreationOptions.RunContinuationsAsynchronously);
            int generation = _operationGeneration;

            using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
            {
                VideoCapture.CreateAync(videoCapture =>
                {
                    if (generation != _operationGeneration)
                    {
                        videoCapture?.Dispose();
                        return;
                    }

                    tcs.TrySetResult(videoCapture);
                });

                await WaitForTaskCompletionAsync(tcs.Task, budget, cancellationToken, hostLoopContext);
                return await tcs.Task;
            }
        }

        private Task<bool> StartVideoModeAsync(
            InitTimeoutBudget budget,
            CancellationToken cancellationToken,
            SynchronizationContext hostLoopContext)
        {
            VideoCapture videoCapture = PeekVideoCapture();
            CameraParameters cameraParams = _cameraParams;
            if (videoCapture == null || cameraParams.cameraResolutionWidth <= 0)
            {
                return Task.FromResult(false);
            }

            return AwaitVideoModeResultAsync(
                onCompleted => videoCapture.StartVideoModeAsync(cameraParams, result => onCompleted(result)),
                budget,
                cancellationToken,
                hostLoopContext);
        }

        private Task<bool> StopVideoModeAsync(
            InitTimeoutBudget budget,
            CancellationToken cancellationToken,
            SynchronizationContext hostLoopContext)
        {
            return StopVideoModeAsync(cancellationToken, hostLoopContext, budget);
        }

        private async Task<bool> StopVideoModeAsync(
            CancellationToken cancellationToken,
            SynchronizationContext hostLoopContext,
            InitTimeoutBudget budget = null)
        {
            VideoCapture videoCapture = PeekVideoCapture();
            if (videoCapture == null || !videoCapture.IsStreaming)
            {
                return true;
            }

            bool ownsBudget = budget == null;
            if (ownsBudget)
            {
                budget = InitTimeoutBudget.FromMilliseconds(0, cancellationToken);
            }

            try
            {
                return await AwaitVideoModeResultAsync(
                    onCompleted => videoCapture.StopVideoModeAsync(result => onCompleted(result)),
                    budget,
                    cancellationToken,
                    hostLoopContext);
            }
            finally
            {
                if (ownsBudget)
                {
                    budget.Dispose();
                }
            }
        }

        private async Task<bool> AwaitVideoModeResultAsync(
            Action<Action<VideoCaptureResult>> startOperation,
            InitTimeoutBudget budget,
            CancellationToken cancellationToken,
            SynchronizationContext hostLoopContext)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int generation = _operationGeneration;

            using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
            {
                startOperation(result =>
                {
                    if (generation != _operationGeneration)
                    {
                        return;
                    }

                    tcs.TrySetResult(result.success);
                });

                await WaitForTaskCompletionAsync(tcs.Task, budget, cancellationToken, hostLoopContext);
                return await tcs.Task;
            }
        }

        private async Task WaitForTaskCompletionAsync(
            Task task,
            InitTimeoutBudget budget,
            CancellationToken cancellationToken,
            SynchronizationContext hostLoopContext)
        {
            bool completed = await SourceToMatHostWait.WaitUntilOnHostLoopAsync(
                () => task.IsCompleted,
                budget,
                cancellationToken,
                hostLoopContext);
            if (!completed)
            {
                InvalidatePendingCallbacks();
                throw new OperationCanceledException();
            }
        }

        private void OnFrameSampleAcquired(VideoCaptureSample sample)
        {
            byte[] imageBytes = null;
            Mat deliveredMat = null;
            bool producedDeliveredMat = false;
            HLCameraStreamMatSource deliveryMatSource = null;
            CaptureSnapshot snapshot = default;
            int generation = _operationGeneration;

            try
            {
                lock (_latestImageBytesLockObject)
                {
                    if (generation != _operationGeneration)
                    {
                        return;
                    }

                    if (_latestImageBytes == null || _latestImageBytes.Length < sample.dataLength)
                    {
                        _latestImageBytes = new byte[sample.dataLength];
                    }

                    sample.CopyRawImageDataIntoBuffer(_latestImageBytes);
                    imageBytes = _latestImageBytes;
                    _hasUnreportedFrame = true;
                    _hasFirstFrameArrived = true;

                    deliveryMatSource = _deliveryMatSource;
                    if (deliveryMatSource != null)
                    {
                        producedDeliveredMat = deliveryMatSource.TryProduceDeliveredMatUnderLock(
                            _latestImageBytes,
                            sample.FrameWidth,
                            sample.FrameHeight,
                            generation,
                            out deliveredMat);
                    }
                }

                snapshot = CreateCaptureSnapshot(sample);
            }
            finally
            {
                sample.Dispose();
            }

            if (producedDeliveredMat)
            {
                bool raised = deliveryMatSource != null
                    && deliveryMatSource.RaiseFrameMatDeliveredAfterLock(
                        deliveredMat,
                        snapshot.ProjectionMatrix,
                        snapshot.CameraToWorldMatrix,
                        snapshot.Intrinsics,
                        _operationGeneration);
                if (!raised)
                {
                    deliveredMat?.Dispose();
                }
            }

            RaiseRawFrameAcquired(snapshot, imageBytes);
        }

        private bool TryCreateCameraParams(VideoCapture videoCapture, out CameraParameters cameraParams)
        {
            cameraParams = default;

            HoloLensCameraStream.Resolution[] resolutions = videoCapture.GetSupportedResolutions()?.ToArray();
            if (resolutions == null || resolutions.Length == 0)
            {
                return false;
            }

            int requestedArea = _requestedWidth * _requestedHeight;
            int minAreaDelta = resolutions.Min(resolution => Mathf.Abs((resolution.width * resolution.height) - requestedArea));
            HoloLensCameraStream.Resolution selectedResolution = resolutions.First(
                resolution => Mathf.Abs((resolution.width * resolution.height) - requestedArea) == minAreaDelta);

            float[] frameRates = videoCapture.GetSupportedFrameRatesForResolution(selectedResolution)?.ToArray();
            if (frameRates == null || frameRates.Length == 0)
            {
                return false;
            }

            float minFpsDelta = frameRates.Min(frameRate => Mathf.Abs(frameRate - _requestedFPS));
            float selectedFrameRate = frameRates.First(frameRate => Mathf.Abs(frameRate - _requestedFPS) == minFpsDelta);

            cameraParams = new CameraParameters
            {
                cameraResolutionHeight = selectedResolution.height,
                cameraResolutionWidth = selectedResolution.width,
                frameRate = Mathf.RoundToInt(selectedFrameRate),
                pixelFormat = _requestedColorFormat == SourceToMatColorFormat.GRAY
                    ? CapturePixelFormat.NV12
                    : CapturePixelFormat.BGRA32,
                rotateImage180Degrees = false,
                enableHolograms = false,
                enableVideoStabilization = false,
                recordingIndicatorVisible = false
            };
            return true;
        }

        private bool EnsureFrameMat()
        {
            if (!TryGetPublishedSnapshot(out CaptureSnapshot snapshot))
            {
                return false;
            }

            int frameWidth = snapshot.FrameWidth;
            int frameHeight = snapshot.FrameHeight;
            if (frameWidth <= 0 || frameHeight <= 0)
            {
                return false;
            }

            int cvType = _requestedColorFormat == SourceToMatColorFormat.GRAY
                ? CvType.CV_8UC1
                : CvType.CV_8UC4;

            if (_frameMat != null &&
                !_frameMat.IsDisposed &&
                _frameMat.cols() == frameWidth &&
                _frameMat.rows() == frameHeight &&
                _frameMat.type() == cvType)
            {
                return true;
            }

            _frameMat?.Dispose();
            _frameMat = new Mat(frameHeight, frameWidth, cvType);
            return true;
        }

        private bool TryCopyUnreportedFrameToMat()
        {
            lock (_latestImageBytesLockObject)
            {
                if (!_hasUnreportedFrame || _latestImageBytes == null)
                {
                    return false;
                }

                if (!EnsureFrameMat() || _frameMat == null || _frameMat.IsDisposed)
                {
                    return false;
                }

                MatBufferUtils.CopyToMat(_latestImageBytes, _frameMat);
                _hasUnreportedFrame = false;
                return !_frameMat.empty();
            }
        }

        private bool TryCopyLatestBytesToFrameMat()
        {
            lock (_latestImageBytesLockObject)
            {
                if (_latestImageBytes == null || !EnsureFrameMat() || _frameMat == null || _frameMat.IsDisposed)
                {
                    return false;
                }

                MatBufferUtils.CopyToMat(_latestImageBytes, _frameMat);
                return !_frameMat.empty();
            }
        }

        private void SetHasUnreportedFrame(bool value)
        {
            lock (_latestImageBytesLockObject)
            {
                _hasUnreportedFrame = value;
            }
        }

        private void CacheSpatialCoordinateSystem()
        {
#if XR_PLUGIN_WINDOWSMR
            _spatialCoordinateSystemPtr = UnityEngine.XR.WindowsMR.WindowsMREnvironment.OriginSpatialCoordinateSystem;
#elif XR_PLUGIN_OPENXR
            _spatialCoordinateSystem =
                Microsoft.MixedReality.OpenXR.PerceptionInterop.GetSceneCoordinateSystem(UnityEngine.Pose.identity) as SpatialCoordinateSystem;
#elif BUILTIN_XR
#if UNITY_2017_2_OR_NEWER
            _spatialCoordinateSystemPtr = UnityEngine.XR.WSA.WorldManager.GetNativeISpatialCoordinateSystemPtr();
#else
            _spatialCoordinateSystemPtr = UnityEngine.VR.WSA.WorldManager.GetNativeISpatialCoordinateSystemPtr();
#endif
#endif
        }

        private void ApplyWorldOrigin()
        {
            VideoCapture videoCapture = PeekVideoCapture();
            if (videoCapture == null)
            {
                return;
            }

#if XR_PLUGIN_OPENXR
            videoCapture.WorldOrigin = _spatialCoordinateSystem;
#elif XR_PLUGIN_WINDOWSMR || BUILTIN_XR
            videoCapture.WorldOriginPtr = _spatialCoordinateSystemPtr;
#endif
        }

        private void SubscribeFrameSampleAcquired()
        {
            VideoCapture videoCapture = PeekVideoCapture();
            if (videoCapture == null)
            {
                return;
            }

            videoCapture.FrameSampleAcquired -= OnFrameSampleAcquired;
            videoCapture.FrameSampleAcquired += OnFrameSampleAcquired;
        }

        private void UnsubscribeFrameSampleAcquired()
        {
            VideoCapture videoCapture = PeekVideoCapture();
            if (videoCapture == null)
            {
                return;
            }

            videoCapture.FrameSampleAcquired -= OnFrameSampleAcquired;
        }

        private VideoCapture PeekVideoCapture()
        {
            lock (_videoCaptureLockObject)
            {
                return _videoCapture;
            }
        }

        private void InvalidatePendingCallbacks()
        {
            _operationGeneration++;
        }

        private void ReleaseVideoCaptureImmediate()
        {
            VideoCapture videoCapture;
            lock (_videoCaptureLockObject)
            {
                videoCapture = _videoCapture;
                _videoCapture = null;
            }

            if (videoCapture == null)
            {
                return;
            }

            videoCapture.FrameSampleAcquired -= OnFrameSampleAcquired;
            videoCapture.Dispose();
        }

        private void ReleaseVideoCaptureBestEffort()
        {
            VideoCapture videoCapture;
            lock (_videoCaptureLockObject)
            {
                videoCapture = _videoCapture;
                _videoCapture = null;
            }

            if (videoCapture == null)
            {
                return;
            }

            videoCapture.FrameSampleAcquired -= OnFrameSampleAcquired;
            if (videoCapture.IsStreaming)
            {
                videoCapture.StopVideoModeAsync(_ => videoCapture.Dispose());
                return;
            }

            videoCapture.Dispose();
        }

        private void ReleaseBuffers()
        {
            lock (_latestImageBytesLockObject)
            {
                _latestImageBytes = null;
                _hasUnreportedFrame = false;
            }

            _hasFirstFrameArrived = false;
            ClearPublishedSnapshot();
            _frameMat?.Dispose();
            _frameMat = null;
            _cameraParams = default;
        }

        private void ResetOpenState()
        {
            _isOpen = false;
            _isPlaybackActive = false;
            _isSuspended = false;
            SetHasUnreportedFrame(false);
            _hasFirstFrameArrived = false;
            ClearPublishedSnapshot();
        }

        private async Task RunSerializedAsync(Func<Task> action, CancellationToken cancellationToken)
        {
            await _videoModeGate.WaitAsync(cancellationToken);
            try
            {
                if (_disposed)
                {
                    return;
                }

                await action();
            }
            finally
            {
                _videoModeGate.Release();
            }
        }

        private Task EnsureMainThreadAsync(CancellationToken cancellationToken)
        {
            return SourceToMatHostMarshaling.EnsureCapturedMainThreadAsync(
                _mainThreadId,
                _mainThreadContext,
                cancellationToken);
        }

        private SynchronizationContext ResolveHostLoopContext()
        {
            return SourceToMatHostMarshaling.ResolveCapturedSynchronizationContext(
                _mainThreadId,
                _mainThreadContext);
        }

        private void RaiseError(SourceToMatErrorCode errorCode, string message)
        {
            ErrorOccurred?.Invoke(errorCode, message);
        }

        private void RaiseRawFrameAcquired(CaptureSnapshot snapshot, byte[] imageBytes)
        {
            EventHandler<HLCameraStreamRawFrameEventArgs> handler = RawFrameAcquired;
            if (handler == null)
            {
                return;
            }

            handler(
                this,
                new HLCameraStreamRawFrameEventArgs(
                    imageBytes,
                    snapshot.FrameWidth,
                    snapshot.FrameHeight,
                    snapshot.ProjectionMatrix,
                    snapshot.CameraToWorldMatrix,
                    snapshot.Intrinsics));
        }

        private void PublishSnapshot(CaptureSnapshot snapshot)
        {
            lock (_snapshotLock)
            {
                _latestSnapshot = snapshot;
                _hasCachedSnapshot = true;
            }
        }

        private bool TryGetPublishedSnapshot(out CaptureSnapshot snapshot)
        {
            lock (_snapshotLock)
            {
                if (!_hasCachedSnapshot)
                {
                    snapshot = default;
                    return false;
                }

                snapshot = _latestSnapshot;
                return true;
            }
        }

        private void ClearPublishedSnapshot()
        {
            lock (_snapshotLock)
            {
                _hasCachedSnapshot = false;
                _latestSnapshot = default;
            }
        }

        private CaptureSnapshot CreateCaptureSnapshot(VideoCaptureSample sample)
        {
            if (!TryGetPublishedSnapshot(out CaptureSnapshot snapshot))
            {
                snapshot = CreateDefaultSnapshot(sample.FrameWidth, sample.FrameHeight);
            }

            snapshot.FrameWidth = sample.FrameWidth;
            snapshot.FrameHeight = sample.FrameHeight;
            snapshot.Intrinsics = sample.cameraIntrinsics;

            if (sample.TryGetCameraToWorldMatrix(out float[] cameraToWorldMatrixAsFloat) &&
                cameraToWorldMatrixAsFloat != null &&
                cameraToWorldMatrixAsFloat.Length >= 16)
            {
                snapshot.CameraToWorldMatrix = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(cameraToWorldMatrixAsFloat);
            }

            if (sample.TryGetProjectionMatrix(out float[] projectionMatrixAsFloat) &&
                projectionMatrixAsFloat != null &&
                projectionMatrixAsFloat.Length >= 16)
            {
                snapshot.ProjectionMatrix = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(projectionMatrixAsFloat);
            }

            PublishSnapshot(snapshot);
            return snapshot;
        }

        private static CaptureSnapshot CreateDefaultSnapshot(int frameWidth, int frameHeight)
        {
            return new CaptureSnapshot
            {
                FrameWidth = frameWidth,
                FrameHeight = frameHeight,
                ProjectionMatrix = Matrix4x4.identity,
                CameraToWorldMatrix = Matrix4x4.identity,
                Intrinsics = default
            };
        }

        private struct CaptureSnapshot
        {
            public int FrameWidth;
            public int FrameHeight;
            public Matrix4x4 ProjectionMatrix;
            public Matrix4x4 CameraToWorldMatrix;
            public CameraIntrinsics Intrinsics;
        }
    }
}

#endif

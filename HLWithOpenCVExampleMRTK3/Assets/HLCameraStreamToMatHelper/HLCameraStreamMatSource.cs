#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using HoloLensCameraStream;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Extensions;
using OpenCVForUnity.Extensions.SourceToMat;
using OpenCVForUnity.UnityIntegration.Helper.SourceToMat;
using UnityEngine;

namespace HoloLensWithOpenCVForUnity.UnityIntegration.Helper.SourceToMat
{
    /// <summary>
    /// HoloLens CameraStream-backed <see cref="MatSourceBase"/> that implements
    /// <see cref="ICameraMatSource"/> and <see cref="IUnityCameraPoseProvider"/>.
    /// </summary>
    /// <remarks>
    /// Does not apply an orientation corrector and does not implement
    /// <see cref="ICameraFacingControllable"/> or
    /// <see cref="IUnityCameraMatSource"/>.
    /// <see cref="FrameMatDelivered"/> is raised on the camera callback thread while
    /// <see cref="MatSourceBase.IsPlaying"/> is <see langword="true"/>.
    /// When <see cref="MatSourceBase.UpdateFrameMatOnTick"/> is
    /// <see langword="false"/>, owned <see cref="MatSourceBase.FrameMat"/> pixels may be a placeholder.
    /// </remarks>
    public sealed class HLCameraStreamMatSource : MatSourceBase, ICameraMatSource, IUnityCameraPoseProvider
    {
        // Private Fields
        private readonly HLCameraStreamFrameGrabber _hlGrabber;
        private Mat _colorConversionScratch;
        private int _produceOperationGeneration;
        private bool _usedUnsupportedColorFallback;

        // Public Properties
        /// <inheritdoc/>
        public string DeviceName => string.Empty;

        /// <inheritdoc/>
        public string RequestedDeviceName { get; set; } = string.Empty;

        /// <inheritdoc/>
        public int RequestedWidth { get; set; } = 1280;

        /// <inheritdoc/>
        public int RequestedHeight { get; set; } = 720;

        /// <inheritdoc/>
        public float RequestedFPS { get; set; } = 30f;

        /// <inheritdoc/>
        public float FPS => _hlGrabber.FPS;

        /// <inheritdoc/>
        public IReadOnlyList<CameraDeviceInfo> SupportedDevices => Array.Empty<CameraDeviceInfo>();

        /// <inheritdoc/>
        /// <remarks>
        /// Shortcut for <see cref="GetSupportedResolutions(string)"/>. HoloLens locatable-camera
        /// resolutions do not depend on <see cref="RequestedDeviceName"/>.
        /// </remarks>
        public IReadOnlyList<CameraResolution> SupportedResolutions => GetSupportedResolutions(RequestedDeviceName);

        /// <inheritdoc/>
        /// <remarks>
        /// Returns the locatable-camera camera-to-world matrix from
        /// <see cref="HLCameraStreamFrameGrabber"/>, not <c>Camera.main</c>.
        /// </remarks>
        public Matrix4x4 CameraToWorldMatrix => _hlGrabber.CameraToWorldMatrix;

        /// <inheritdoc/>
        /// <remarks>
        /// Returns the locatable-camera projection matrix from
        /// <see cref="HLCameraStreamFrameGrabber"/>, not <c>Camera.main</c>.
        /// </remarks>
        public Matrix4x4 ProjectionMatrix => _hlGrabber.ProjectionMatrix;

        /// <summary>
        /// Gets the latest locatable-camera intrinsics from <see cref="HLCameraStreamFrameGrabber"/>.
        /// </summary>
        public CameraIntrinsics Intrinsics => _hlGrabber.Intrinsics;

        // Public Events
        /// <summary>
        /// Raised on the camera callback thread when a new converted frame is available.
        /// </summary>
        /// <remarks>
        /// Fires only while <see cref="MatSourceBase.IsPlaying"/> is <see langword="true"/>
        /// (<see cref="MatSourceState.Paused"/>, <see cref="MatSourceState.Ready"/>, and
        /// uninitialized states do not raise).
        /// <see cref="FrameMatDeliveredEventArgs.Mat"/> is a newly allocated buffer; the subscriber
        /// must dispose it. <see cref="MatSourceBase.FrameMat"/> is not passed.
        /// Color conversion, 90-degree rotation, and flip use the settings snapshotted at the start
        /// of conversion. The event is not marshaled to the Unity main thread.
        /// </remarks>
        public event EventHandler<FrameMatDeliveredEventArgs> FrameMatDelivered;

        // Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="HLCameraStreamMatSource"/> class.
        /// </summary>
        /// <param name="grabber">HoloLens CameraStream grabber that supplies raw frames and pose metadata.</param>
        /// <exception cref="ArgumentNullException"><paramref name="grabber"/> is <see langword="null"/>.</exception>
        public HLCameraStreamMatSource(HLCameraStreamFrameGrabber grabber)
            : base(grabber, orientationCorrector: null)
        {
            _hlGrabber = grabber;
            _hlGrabber.SetDeliveryMatSource(this);
            _hlGrabber.ErrorOccurred += OnGrabberError;
            OnDisposed += OnMatSourceDisposed;
        }

        // Public Methods
        /// <inheritdoc/>
        public IReadOnlyList<CameraResolution> GetSupportedResolutions(string deviceNameOrIndex)
        {
            _ = deviceNameOrIndex;

            HoloLensCameraStream.Resolution[] resolutions = _hlGrabber.GetSupportedResolutions();
            if (resolutions == null || resolutions.Length == 0)
            {
                return Array.Empty<CameraResolution>();
            }

            var result = new CameraResolution[resolutions.Length];
            for (int i = 0; i < resolutions.Length; i++)
            {
                HoloLensCameraStream.Resolution resolution = resolutions[i];
                result[i] = new CameraResolution(resolution.width, resolution.height, 0f);
            }

            return result;
        }

        /// <inheritdoc/>
        public override async Task PlayAsync(CancellationToken cancellationToken = default)
        {
            if (IsInitializing)
            {
                await base.PlayAsync(cancellationToken);
                return;
            }

            if (State == MatSourceState.Playing)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await BeginGrabberPlaybackAsync(cancellationToken);
                return;
            }

            bool startFromReady = State == MatSourceState.Ready;
            bool resumeFromPause = State == MatSourceState.Paused;
            await base.PlayAsync(cancellationToken);

            if (startFromReady || resumeFromPause)
            {
                await BeginGrabberPlaybackAsync(cancellationToken);
            }
        }

        /// <inheritdoc/>
        public override async Task PauseAsync(CancellationToken cancellationToken = default)
        {
            if (IsInitializing)
            {
                await base.PauseAsync(cancellationToken);
                return;
            }

            await base.PauseAsync(cancellationToken);
            await PauseGrabberPlaybackAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (IsInitializing)
            {
                await base.StopAsync(cancellationToken);
                return;
            }

            await base.StopAsync(cancellationToken);
            await StopGrabberPlaybackAsync(cancellationToken);
        }

        // Protected Methods
        /// <inheritdoc/>
        protected override void SyncGrabberBeforeOpen()
        {
            _hlGrabber.RequestedWidth = RequestedWidth;
            _hlGrabber.RequestedHeight = RequestedHeight;
            _hlGrabber.RequestedFPS = RequestedFPS;
            _hlGrabber.RequestedColorFormat = OutputColorFormat == SourceToMatColorFormat.GRAY
                ? SourceToMatColorFormat.GRAY
                : SourceToMatColorFormat.BGRA;
        }

        /// <inheritdoc/>
        protected override SourceToMatColorFormat InferGrabberColorFormat(Mat workingMat)
        {
            _ = workingMat;

            return OutputColorFormat == SourceToMatColorFormat.GRAY
                ? SourceToMatColorFormat.GRAY
                : SourceToMatColorFormat.BGRA;
        }

        // Internal Methods
        /// <summary>
        /// Produces a delivered mat from the grabber-owned image bytes. Does not raise
        /// <see cref="FrameMatDelivered"/>.
        /// </summary>
        /// <remarks>
        /// Must be called while the grabber image-bytes lock is held. Does not retain
        /// <paramref name="imageBytesUnderLock"/>. When there is no <see cref="FrameMatDelivered"/>
        /// subscriber or this source is not playing, returns <see langword="false"/> without producing.
        /// Color conversion, 90-degree rotation, and flip use the settings snapshotted at the start
        /// of this call.
        /// </remarks>
        /// <param name="imageBytesUnderLock">Grabber-owned image buffer. Read-only when a copy is not required.</param>
        /// <param name="frameWidth">Frame width in pixels.</param>
        /// <param name="frameHeight">Frame height in pixels.</param>
        /// <param name="operationGeneration">Grabber operation generation captured with this frame.</param>
        /// <param name="deliveredMat">
        /// Newly produced mat when this method returns <see langword="true"/>; otherwise
        /// <see langword="null"/>. The caller must raise <see cref="FrameMatDelivered"/> or dispose it.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when <paramref name="deliveredMat"/> was produced; otherwise
        /// <see langword="false"/>.
        /// </returns>
        internal bool TryProduceDeliveredMatUnderLock(
            byte[] imageBytesUnderLock,
            int frameWidth,
            int frameHeight,
            int operationGeneration,
            out Mat deliveredMat)
        {
            deliveredMat = null;
            _usedUnsupportedColorFallback = false;
            _produceOperationGeneration = operationGeneration;

            if (!IsPlaying || FrameMatDelivered == null)
            {
                return false;
            }

            if (imageBytesUnderLock == null || frameWidth <= 0 || frameHeight <= 0)
            {
                return false;
            }

            SourceToMatColorFormat outputColorFormat = OutputColorFormat;
            bool rotate90Degree = Rotate90Degree;
            bool flipVertical = FlipVertical;
            bool flipHorizontal = FlipHorizontal;
            SourceToMatColorFormat sourceColorFormat = _hlGrabber.RequestedColorFormat == SourceToMatColorFormat.GRAY
                ? SourceToMatColorFormat.GRAY
                : SourceToMatColorFormat.BGRA;

            int sourceChannels = SourceToMatUtils.Channels(sourceColorFormat);
            int requiredLength = frameWidth * frameHeight * sourceChannels;
            if (imageBytesUnderLock.Length < requiredLength)
            {
                return false;
            }

            bool canReadSourceWithoutCopy = SourceToMatUtils.CanReadSourceWithoutCopy(
                sourceColorFormat,
                outputColorFormat,
                rotate90Degree,
                flipVertical,
                flipHorizontal);

            GCHandle pinnedBytes = default;
            bool pinnedBytesAllocated = false;
            Mat wrappedSourceMat = null;
            Mat ownedSourceMat = null;
            Mat producedMat = null;
            bool ownershipTransferred = false;

            try
            {
                Mat produceInputMat;
                if (canReadSourceWithoutCopy)
                {
                    pinnedBytes = GCHandle.Alloc(imageBytesUnderLock, GCHandleType.Pinned);
                    pinnedBytesAllocated = true;
                    wrappedSourceMat = new Mat(
                        frameHeight,
                        frameWidth,
                        CvType.CV_8UC(sourceChannels),
                        pinnedBytes.AddrOfPinnedObject());
                    produceInputMat = wrappedSourceMat;
                }
                else
                {
                    ownedSourceMat = new Mat(frameHeight, frameWidth, CvType.CV_8UC(sourceChannels));
                    MatBufferUtils.CopyToMat(imageBytesUnderLock, ownedSourceMat);
                    if (ownedSourceMat.IsDisposed || ownedSourceMat.empty())
                    {
                        return false;
                    }

                    produceInputMat = ownedSourceMat;
                }

                if (rotate90Degree
                    && outputColorFormat != sourceColorFormat
                    && (_colorConversionScratch == null || _colorConversionScratch.IsDisposed))
                {
                    _colorConversionScratch = new Mat();
                }

                producedMat = SourceToMatUtils.ProduceOutputMat(
                    produceInputMat,
                    sourceColorFormat,
                    outputColorFormat,
                    rotate90Degree,
                    flipVertical,
                    flipHorizontal,
                    out bool usedUnsupportedColorFallback,
                    _colorConversionScratch);

                _usedUnsupportedColorFallback = usedUnsupportedColorFallback;

                if (ownedSourceMat != null && !ReferenceEquals(producedMat, ownedSourceMat))
                {
                    ownedSourceMat.Dispose();
                }

                ownedSourceMat = null;

                if (producedMat == null
                    || producedMat.IsDisposed
                    || ReferenceEquals(producedMat, wrappedSourceMat))
                {
                    return false;
                }

                deliveredMat = producedMat;
                ownershipTransferred = true;
                return true;
            }
            finally
            {
                wrappedSourceMat?.Dispose();
                if (pinnedBytesAllocated)
                {
                    pinnedBytes.Free();
                }

                ownedSourceMat?.Dispose();
                if (!ownershipTransferred && !ReferenceEquals(producedMat, wrappedSourceMat))
                {
                    producedMat?.Dispose();
                }
            }
        }

        /// <summary>
        /// Raises <see cref="FrameMatDelivered"/> after the grabber image-bytes lock has been released.
        /// </summary>
        /// <remarks>
        /// Rechecks playing state, operation generation, and whether a subscriber is present.
        /// On failure, disposes <paramref name="deliveredMat"/> and returns <see langword="false"/>.
        /// On success, ownership of <paramref name="deliveredMat"/> transfers to the subscriber.
        /// Must not be called while the grabber image-bytes lock is held.
        /// </remarks>
        /// <param name="deliveredMat">Mat produced by <see cref="TryProduceDeliveredMatUnderLock"/>.</param>
        /// <param name="projectionMatrix">Projection matrix snapshotted with this frame.</param>
        /// <param name="cameraToWorldMatrix">Camera-to-world matrix snapshotted with this frame.</param>
        /// <param name="intrinsics">Camera intrinsics snapshotted with this frame.</param>
        /// <param name="operationGeneration">Current grabber operation generation.</param>
        /// <returns>
        /// <see langword="true"/> when <see cref="FrameMatDelivered"/> was raised; otherwise
        /// <see langword="false"/>.
        /// </returns>
        internal bool RaiseFrameMatDeliveredAfterLock(
            Mat deliveredMat,
            Matrix4x4 projectionMatrix,
            Matrix4x4 cameraToWorldMatrix,
            CameraIntrinsics intrinsics,
            int operationGeneration)
        {
            bool ownershipTransferred = false;
            try
            {
                if (_usedUnsupportedColorFallback)
                {
                    _usedUnsupportedColorFallback = false;
                    RaiseError(SourceToMatErrorCode.UNKNOWN, "Unsupported color conversion.");
                }

                if (deliveredMat == null || deliveredMat.IsDisposed)
                {
                    return false;
                }

                if (!IsPlaying || operationGeneration != _produceOperationGeneration)
                {
                    return false;
                }

                EventHandler<FrameMatDeliveredEventArgs> handler = FrameMatDelivered;
                if (handler == null)
                {
                    return false;
                }

                handler(
                    this,
                    new FrameMatDeliveredEventArgs(
                        deliveredMat,
                        projectionMatrix,
                        cameraToWorldMatrix,
                        intrinsics));
                ownershipTransferred = true;
                return true;
            }
            finally
            {
                if (!ownershipTransferred)
                {
                    deliveredMat?.Dispose();
                }
            }
        }

        // Private Methods
        private Task BeginGrabberPlaybackAsync(CancellationToken cancellationToken)
        {
            return _hlGrabber.BeginPlaybackAsync(cancellationToken);
        }

        private Task PauseGrabberPlaybackAsync(CancellationToken cancellationToken)
        {
            return _hlGrabber.PausePlaybackAsync(cancellationToken);
        }

        private Task StopGrabberPlaybackAsync(CancellationToken cancellationToken)
        {
            return _hlGrabber.StopPlaybackAsync(cancellationToken);
        }

        private void OnGrabberError(SourceToMatErrorCode errorCode, string message)
        {
            RaiseError(errorCode, message);
        }

        private void OnMatSourceDisposed()
        {
            OnDisposed -= OnMatSourceDisposed;
            _hlGrabber.SetDeliveryMatSource(null);
            _hlGrabber.ErrorOccurred -= OnGrabberError;
            _colorConversionScratch?.Dispose();
            _colorConversionScratch = null;
        }
    }
}

#endif

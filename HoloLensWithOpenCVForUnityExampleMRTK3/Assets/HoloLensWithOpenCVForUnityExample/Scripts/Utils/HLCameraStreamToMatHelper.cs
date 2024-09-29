#pragma warning disable 0067
#pragma warning disable 0618

#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API
#if !(PLATFORM_LUMIN && !UNITY_EDITOR)

using HoloLensCameraStream;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.UnityUtils.Helper;
using OpenCVForUnity.UtilsModule;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API && XR_PLUGIN_OPENXR
using Windows.Perception.Spatial;
#endif

namespace HoloLensWithOpenCVForUnity.UnityUtils.Helper
{
    /// <summary>
    /// This is called every time there is a new frame image mat available.
    /// The Mat object's type is 'CV_8UC4' or 'CV_8UC3' or 'CV_8UC1' (ColorFormat is determined by the outputColorFormat setting).
    /// </summary>
    /// <param name="videoCaptureSample">The recently captured frame image mat.</param>
    /// <param name="projectionMatrix">The projection matrices.</param>
    /// <param name="cameraToWorldMatrix">The camera to world matrices.</param>
    /// <param name="cameraIntrinsics">The camera intrinsics.</param>
    public delegate void FrameMatAcquiredCallback(Mat mat, Matrix4x4 projectionMatrix, Matrix4x4 cameraToWorldMatrix, CameraIntrinsics cameraIntrinsics);

    /// <summary>
    /// Hololens camera stream to mat helper.
    /// v 1.0.7
    /// Depends on EnoxSoftware/HoloLensCameraStream (https://github.com/EnoxSoftware/HoloLensCameraStream).
    /// Depends on OpenCVForUnity version 2.4.1 (WebCamTextureToMatHelper v 1.1.2) or later.
    /// 
    /// By setting outputColorFormat to BGRA or GRAY, processing that does not include extra color conversion is performed.
    /// 
    /// Usage:
    /// Add Define Symbols: "Open File > Build Settings > Player Settings > Other Settings" and add the following to Scripting Define Symbols depending on the XR system used in your project.
    /// This is the setup needed to get the correct values from the TryGetCameraToWorldMatrix method.
    ///    Legacy built-in XR                          ; "BUILTIN_XR"
    ///    XR Plugin Management(Windows Mixed Reality) : "XR_PLUGIN_WINDOWSMR"
    ///    XR Plugin Management(OpenXR)                : "XR_PLUGIN_OPENXR"
    /// 
    /// 
    /// Combination of camera frame size and frame rate that can be acquired on Hololens. (width x height : framerate)
    /// (See https://learn.microsoft.com/en-us/windows/mixed-reality/develop/advanced-concepts/locatable-camera-overview)
    /// 
    /// Hololens1
    /// 
    /// [1280x720] - Suggested usage : (default mode with video stabilization)
    /// 1280 x 720 : 30
    /// 1280 x 720 : 24
    /// 1280 x 720 : 20
    /// 1280 x 720 : 15
    /// 1280 x 720 : 5
    /// 
    /// [1408x792] - Suggested usage : Overscan (padding) resolution before video stabilization
    /// 1408 x 792 : 30
    /// 1408 x 792 : 24
    /// 1408 x 792 : 20
    /// 1408 x 792 : 15
    /// 1408 x 792 : 5
    /// 
    /// [1344x756] - Suggested usage : Large FOV video mode with overscan
    /// 1344 x 756 : 30
    /// 1344 x 756 : 24
    /// 1344 x 756 : 20
    /// 1344 x 756 : 15
    /// 1344 x 756 : 5
    /// 
    /// [896x504] - Suggested usage : Low power / Low resolution mode for image processing tasks
    /// 896 x 504 : 30
    /// 896 x 504 : 24
    /// 896 x 504 : 20
    /// 896 x 504 : 15
    /// 896 x 504 : 5
    /// 
    /// 
    /// Hololens2
    /// 
    /// [1952x1100] - Suggested usage : Video conferencing, long duration scenarios
    /// 1952x1100 : 60
    /// 1952x1100 : 30
    /// 1952x1100 : 15
    /// 
    /// [1504x846] - Suggested usage : Video conferencing, long duration scenarios
    /// 1504x846 : 60
    /// 1504x846 : 30
    /// 1504x846 : 15
    /// 1504x846 : 5
    /// 
    /// [1920x1080] - Suggested usage : Video conferencing, long duration scenarios
    /// 1920x1080 : 30
    /// 1920x1080 : 15
    /// 
    /// [1280x720] - Suggested usage : Video conferencing, long duration scenarios
    /// 1280x720 : 30
    /// 1280x720 : 15
    /// 
    /// [1128x636] - Suggested usage : Video conferencing, long duration scenarios
    /// 1128x636 : 30
    /// 1128x636 : 15
    /// 
    /// [960x540] - Suggested usage : Video conferencing, long duration scenarios
    /// 960x540 : 30
    /// 960x540 : 15
    /// 
    /// [760x428] - Suggested usage : Video conferencing, long duration scenarios
    /// 760x428 : 30
    /// 760x428 : 15
    /// 
    /// [640x360] - Suggested usage : Video conferencing, long duration scenarios
    /// 640x360 : 30
    /// 640x360 : 15
    /// 
    /// [500x282] - Suggested usage : Video conferencing, long duration scenarios
    /// 500x282 : 30
    /// 500x282 : 15
    /// 
    /// [424x240] - Suggested usage : Video conferencing, long duration scenarios
    /// 424x240 : 30
    /// 424x240 : 15
    /// 
    /// </summary>
    public class HLCameraStreamToMatHelper : WebCamTextureToMatHelper
    {
        /// <summary>
        /// This will be called whenever a new camera frame image available is converted to Mat.
        /// The Mat object's type is 'CV_8UC4' or 'CV_8UC3' or 'CV_8UC1' (ColorFormat is determined by the outputColorFormat setting).
        /// You must properly initialize the HLCameraStreamToMatHelper, 
        /// including calling Play() before this event will begin firing.
        /// </summary>
        public virtual event FrameMatAcquiredCallback frameMatAcquired;

        protected CameraIntrinsics cameraIntrinsics;

        /// <summary>
        /// Returns the camera intrinsics.
        /// </summary>
        /// <returns>The camera intrinsics.</returns>
        public virtual CameraIntrinsics GetCameraIntrinsics()
        {
            return cameraIntrinsics;
        }

#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API

        public override string requestedDeviceName
        {
            get { return _requestedDeviceName; }
            set
            {
                if (_requestedDeviceName != value)
                {
                    _requestedDeviceName = value;
                    if (hasInitDone)
                        Initialize();
                }
            }
        }

        public override int requestedWidth
        {
            get { return _requestedWidth; }
            set
            {
                int _value = (int)Mathf.Clamp(value, 0f, float.MaxValue);
                if (_requestedWidth != _value)
                {
                    _requestedWidth = _value;
                    if (hasInitDone)
                        Initialize();
                }
            }
        }

        public override int requestedHeight
        {
            get { return _requestedHeight; }
            set
            {
                int _value = (int)Mathf.Clamp(value, 0f, float.MaxValue);
                if (_requestedHeight != _value)
                {
                    _requestedHeight = _value;
                    if (hasInitDone)
                        Initialize();
                }
            }
        }

        public override bool requestedIsFrontFacing
        {
            get { return _requestedIsFrontFacing; }
            set
            {
                if (_requestedIsFrontFacing != value)
                {
                    _requestedIsFrontFacing = value;
                    if (hasInitDone)
                        Initialize(_requestedIsFrontFacing, requestedFPS, rotate90Degree);
                }
            }
        }

        public override bool rotate90Degree
        {
            get { return _rotate90Degree; }
            set
            {
                if (_rotate90Degree != value)
                {
                    _rotate90Degree = value;
                    if (hasInitDone)
                        Initialize();
                }
            }
        }

        public override ColorFormat outputColorFormat
        {
            get { return _outputColorFormat; }
            set
            {
                if (_outputColorFormat != value)
                {
                    _outputColorFormat = value;
                    if (hasInitDone)
                        Initialize();
                }
            }
        }

        public override float requestedFPS
        {
            get { return _requestedFPS; }
            set
            {
                _requestedFPS = Mathf.Clamp(value, -1f, float.MaxValue);
                if (hasInitDone)
                {
                    Initialize();
                }
            }
        }

        new protected ColorFormat baseColorFormat = ColorFormat.BGRA;

        protected System.Object lockObject = new System.Object();
        protected System.Object matrixLockObject = new System.Object();
        protected System.Object latestImageBytesLockObject = new System.Object();

        protected HoloLensCameraStream.VideoCapture videoCapture;
        protected HoloLensCameraStream.CameraParameters cameraParams;

        protected int frameSampleWidth;
        protected int frameSampleHeight;

        protected bool wasPlayingBeforeSuspended;

        protected Matrix4x4 _cameraToWorldMatrix = Matrix4x4.identity;
        protected Matrix4x4 cameraToWorldMatrix
        {
            get
            {
                lock (matrixLockObject)
                    return _cameraToWorldMatrix;
            }
            set
            {
                lock (matrixLockObject)
                    _cameraToWorldMatrix = value;
            }
        }

        protected Matrix4x4 _projectionMatrix = Matrix4x4.identity;
        protected Matrix4x4 projectionMatrix
        {
            get
            {
                lock (matrixLockObject)
                    return _projectionMatrix;
            }
            set
            {
                lock (matrixLockObject)
                    _projectionMatrix = value;
            }
        }

        protected bool _didUpdateThisFrame = false;
        protected bool didUpdateThisFrame
        {
            get
            {
                lock (lockObject)
                    return _didUpdateThisFrame;
            }
            set
            {
                lock (lockObject)
                    _didUpdateThisFrame = value;
            }
        }

        protected bool _didUpdateImageBufferInCurrentFrame = false;
        protected bool didUpdateImageBufferInCurrentFrame
        {
            get
            {
                lock (lockObject)
                    return _didUpdateImageBufferInCurrentFrame;
            }
            set
            {
                lock (lockObject)
                    _didUpdateImageBufferInCurrentFrame = value;
            }
        }

        protected byte[] _latestImageBytes;
        protected byte[] latestImageBytes
        {
            get
            {
                lock (latestImageBytesLockObject)
                    return _latestImageBytes;
            }
            set
            {
                lock (latestImageBytesLockObject)
                    _latestImageBytes = value;
            }
        }

#if XR_PLUGIN_OPENXR
        protected SpatialCoordinateSystem spatialCoordinateSystem;
#elif XR_PLUGIN_WINDOWSMR || BUILTIN_XR
        protected IntPtr spatialCoordinateSystemPtr;
#endif

        protected bool isChangeVideoModeWaiting = false;

        protected bool _hasInitDone = false;
        new protected bool hasInitDone
        {
            get
            {
                lock (lockObject)
                    return _hasInitDone;
            }
            set
            {
                lock (lockObject)
                    _hasInitDone = value;
            }
        }

        protected bool _hasInitEventCompleted = false;
        protected bool hasInitEventCompleted
        {
            get
            {
                lock (lockObject)
                    return _hasInitEventCompleted;
            }
            set
            {
                lock (lockObject)
                    _hasInitEventCompleted = value;
            }
        }

        protected virtual void LateUpdate()
        {
            if (didUpdateThisFrame && !didUpdateImageBufferInCurrentFrame)
                didUpdateThisFrame = false;

            didUpdateImageBufferInCurrentFrame = false;
        }

        protected virtual void OnFrameSampleAcquired(VideoCaptureSample sample)
        {
            lock (latestImageBytesLockObject)
            {
                //When copying the bytes out of the buffer, you must supply a byte[] that is appropriately sized.
                //You can reuse this byte[] until you need to resize it (for whatever reason).
                if (_latestImageBytes == null || _latestImageBytes.Length < sample.dataLength)
                {
                    _latestImageBytes = new byte[sample.dataLength];
                }
                sample.CopyRawImageDataIntoBuffer(_latestImageBytes);
            }

            float[] cameraToWorldMatrixAsFloat;
            if (sample.TryGetCameraToWorldMatrix(out cameraToWorldMatrixAsFloat) == false)
            {
                //sample.Dispose();
                //return;
            }

            float[] projectionMatrixAsFloat;
            if (sample.TryGetProjectionMatrix(out projectionMatrixAsFloat) == false)
            {
                //sample.Dispose();
                //return;
            }

            // Right now we pass things across the pipe as a float array then convert them back into UnityEngine.Matrix using a utility method
            projectionMatrix = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(projectionMatrixAsFloat);
            cameraToWorldMatrix = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(cameraToWorldMatrixAsFloat);

            cameraIntrinsics = sample.cameraIntrinsics;
            frameSampleWidth = sample.FrameWidth;
            frameSampleHeight = sample.FrameHeight;

            sample.Dispose();

            didUpdateThisFrame = true;
            didUpdateImageBufferInCurrentFrame = true;

            if (hasInitEventCompleted && frameMatAcquired != null)
            {
                Mat mat = new Mat(frameSampleHeight, frameSampleWidth, CvType.CV_8UC(Channels(outputColorFormat)));

                if (baseColorFormat == outputColorFormat)
                {
                    MatUtils.copyToMat<byte>(latestImageBytes, mat);
                }
                else
                {
                    Mat baseMat = new Mat(frameSampleHeight, frameSampleWidth, CvType.CV_8UC(Channels(baseColorFormat)));
                    MatUtils.copyToMat<byte>(latestImageBytes, baseMat);
                    Imgproc.cvtColor(baseMat, mat, ColorConversionCodes(baseColorFormat, outputColorFormat));
                }

                if (_rotate90Degree)
                {
                    Mat rotatedFrameMat = new Mat(frameSampleWidth, frameSampleHeight, CvType.CV_8UC(Channels(outputColorFormat)));
                    Core.rotate(mat, rotatedFrameMat, Core.ROTATE_90_CLOCKWISE);
                    mat.Dispose();

                    FlipMat(rotatedFrameMat, _flipVertical, _flipHorizontal);

                    frameMatAcquired.Invoke(rotatedFrameMat, projectionMatrix, cameraToWorldMatrix, cameraIntrinsics);
                }
                else
                {
                    FlipMat(mat, _flipVertical, _flipHorizontal);

                    frameMatAcquired.Invoke(mat, projectionMatrix, cameraToWorldMatrix, cameraIntrinsics);
                }
            }
        }

        protected virtual HoloLensCameraStream.CameraParameters CreateCameraParams(HoloLensCameraStream.VideoCapture videoCapture)
        {
            int min1 = videoCapture.GetSupportedResolutions().Min(r => Mathf.Abs((r.width * r.height) - (_requestedWidth * _requestedHeight)));
            HoloLensCameraStream.Resolution resolution = videoCapture.GetSupportedResolutions().First(r => Mathf.Abs((r.width * r.height) - (_requestedWidth * _requestedHeight)) == min1);

            float min2 = videoCapture.GetSupportedFrameRatesForResolution(resolution).Min(f => Mathf.Abs(f - _requestedFPS));
            float frameRate = videoCapture.GetSupportedFrameRatesForResolution(resolution).First(f => Mathf.Abs(f - _requestedFPS) == min2);

            HoloLensCameraStream.CameraParameters cameraParams = new HoloLensCameraStream.CameraParameters();
            cameraParams.cameraResolutionHeight = resolution.height;
            cameraParams.cameraResolutionWidth = resolution.width;
            cameraParams.frameRate = Mathf.RoundToInt(frameRate);
            cameraParams.pixelFormat = (outputColorFormat == ColorFormat.GRAY) ? CapturePixelFormat.NV12 : CapturePixelFormat.BGRA32;
            cameraParams.rotateImage180Degrees = false;
            cameraParams.enableHolograms = false;
            cameraParams.enableVideoStabilization = false;
            cameraParams.recordingIndicatorVisible = false;

            return cameraParams;
        }
#endif

        /// <summary>
        /// Returns the video capture.
        /// </summary>
        /// <returns>The video capture.</returns>
        public virtual HoloLensCameraStream.VideoCapture GetVideoCapture()
        {
#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            return videoCapture;
#else
            return null;
#endif
        }

#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
        // Update is called once per frame
        protected override void Update() { }

        protected override IEnumerator OnApplicationFocus(bool hasFocus)
        {
            while (isChangeVideoModeWaiting)
            {
                yield return null;
            }

            if (!hasInitDone) yield break;

            if (hasFocus)
            {
                HoloLensCameraStream.VideoCapture.CreateAync(videoCapture =>
                {
                    this.videoCapture = videoCapture;

                    //Request the spatial coordinate ptr if you want fetch the camera and set it if you need to 
#if XR_PLUGIN_OPENXR
                    videoCapture.WorldOrigin = spatialCoordinateSystem;
#elif XR_PLUGIN_WINDOWSMR || BUILTIN_XR
                    videoCapture.WorldOriginPtr = spatialCoordinateSystemPtr;
#endif

                    videoCapture.FrameSampleAcquired -= OnFrameSampleAcquired;
                    videoCapture.FrameSampleAcquired += OnFrameSampleAcquired;

                    if (wasPlayingBeforeSuspended)
                    {
                        isChangeVideoModeWaiting = true;
                        videoCapture.StartVideoModeAsync(cameraParams, result =>
                        {
                            isChangeVideoModeWaiting = false;
                        });
                    }
                });
            }
            else
            {
                wasPlayingBeforeSuspended = IsPlaying();

                isChangeVideoModeWaiting = true;
                videoCapture.StopVideoModeAsync(result =>
                {
                    videoCapture.Dispose();
                    videoCapture = null;
                    isChangeVideoModeWaiting = false;
                });
            }

            yield break;
        }

        /// <summary>
        /// Raises the destroy event.
        /// </summary>
        protected override void OnDestroy()
        {
            Dispose();

            if (videoCapture != null)
            {
                if (videoCapture.IsStreaming)
                {
                    videoCapture.StopVideoModeAsync(result =>
                    {
                        videoCapture.Dispose();
                        videoCapture = null;
                    });
                }
                else
                {
                    videoCapture.Dispose();
                    videoCapture = null;
                }
            }
        }

        /// <summary>
        /// Initializes this instance by coroutine.
        /// </summary>
        protected override IEnumerator _Initialize()
        {
            if (hasInitDone)
            {
                StartCoroutine(_Stop());

                while (isChangeVideoModeWaiting)
                {
                    yield return null;
                }

                ReleaseResources();

                if (onDisposed != null)
                    onDisposed.Invoke();
            }

            isInitWaiting = true;

            while (isChangeVideoModeWaiting)
            {
                yield return null;
            }

            isChangeVideoModeWaiting = true;
            if (videoCapture != null)
            {
                videoCapture.StopVideoModeAsync(result1 =>
                {
                    cameraParams = CreateCameraParams(videoCapture);
                    videoCapture.StartVideoModeAsync(cameraParams, result2 =>
                    {
                        if (!result2.success)
                        {
                            isChangeVideoModeWaiting = false;
                            isInitWaiting = false;
                            CancelInitCoroutine();

                            if (onErrorOccurred != null)
                                onErrorOccurred.Invoke(ErrorCode.UNKNOWN);
                        }
                        else
                        {
                            isChangeVideoModeWaiting = false;
                        }
                    });
                });
            }
            else
            {

                //Fetch a pointer to Unity's spatial coordinate system if you need pixel mapping
#if XR_PLUGIN_WINDOWSMR

                spatialCoordinateSystemPtr = UnityEngine.XR.WindowsMR.WindowsMREnvironment.OriginSpatialCoordinateSystem;

#elif XR_PLUGIN_OPENXR

                spatialCoordinateSystem = Microsoft.MixedReality.OpenXR.PerceptionInterop.GetSceneCoordinateSystem(UnityEngine.Pose.identity) as SpatialCoordinateSystem;

#elif BUILTIN_XR

#if UNITY_2017_2_OR_NEWER
                spatialCoordinateSystemPtr = UnityEngine.XR.WSA.WorldManager.GetNativeISpatialCoordinateSystemPtr();
#else
                spatialCoordinateSystemPtr = UnityEngine.VR.WSA.WorldManager.GetNativeISpatialCoordinateSystemPtr();
#endif

#endif

                HoloLensCameraStream.VideoCapture.CreateAync(videoCapture =>
                {

                    if (initCoroutine == null) return;

                    if (videoCapture == null)
                    {
                        Debug.LogError("Did not find a video capture object. You may not be using the HoloLens.");

                        isChangeVideoModeWaiting = false;
                        isInitWaiting = false;
                        CancelInitCoroutine();

                        if (onErrorOccurred != null)
                            onErrorOccurred.Invoke(ErrorCode.CAMERA_DEVICE_NOT_EXIST);

                        return;
                    }

                    this.videoCapture = videoCapture;

                    //Request the spatial coordinate ptr if you want fetch the camera and set it if you need to 
#if XR_PLUGIN_OPENXR
                    videoCapture.WorldOrigin = spatialCoordinateSystem;
#elif XR_PLUGIN_WINDOWSMR || BUILTIN_XR
                    videoCapture.WorldOriginPtr = spatialCoordinateSystemPtr;
#endif

                    cameraParams = CreateCameraParams(videoCapture);

                    videoCapture.FrameSampleAcquired -= OnFrameSampleAcquired;
                    videoCapture.FrameSampleAcquired += OnFrameSampleAcquired;
                    videoCapture.StartVideoModeAsync(cameraParams, result =>
                    {
                        if (!result.success)
                        {
                            isChangeVideoModeWaiting = false;
                            isInitWaiting = false;
                            CancelInitCoroutine();

                            if (onErrorOccurred != null)
                                onErrorOccurred.Invoke(ErrorCode.UNKNOWN);
                        }
                        else
                        {
                            isChangeVideoModeWaiting = false;
                        }
                    });
                });
            }

            int initFrameCount = 0;
            bool isTimeout = false;

            while (true)
            {
                if (initFrameCount > _timeoutFrameCount)
                {
                    isTimeout = true;
                    break;
                }
                else if (didUpdateThisFrame)
                {
                    Debug.Log("HololensCameraStreamToMatHelper:: " + "name:" + "" + " width:" + frameSampleWidth + " height:" + frameSampleHeight + " fps:" + cameraParams.frameRate);

                    baseColorFormat = (outputColorFormat == ColorFormat.GRAY) ? ColorFormat.GRAY : ColorFormat.BGRA;

                    baseMat = new Mat(frameSampleHeight, frameSampleWidth, CvType.CV_8UC(Channels(baseColorFormat)));

                    if (baseColorFormat == outputColorFormat)
                    {
                        frameMat = baseMat;
                    }
                    else
                    {
                        frameMat = new Mat(baseMat.rows(), baseMat.cols(), CvType.CV_8UC(Channels(outputColorFormat)));
                    }

                    if (_rotate90Degree)
                        rotatedFrameMat = new Mat(frameMat.cols(), frameMat.rows(), CvType.CV_8UC(Channels(outputColorFormat)));

                    isInitWaiting = false;
                    hasInitDone = true;
                    initCoroutine = null;

                    if (onInitialized != null)
                        onInitialized.Invoke();

                    hasInitEventCompleted = true;

                    break;
                }
                else
                {
                    initFrameCount++;
                    yield return null;
                }
            }

            if (isTimeout)
            {
                if (videoCapture != null)
                {
                    videoCapture.FrameSampleAcquired -= OnFrameSampleAcquired;

                    isChangeVideoModeWaiting = true;
                    videoCapture.StopVideoModeAsync(result =>
                    {
                        videoCapture.Dispose();
                        videoCapture = null;
                        isChangeVideoModeWaiting = false;
                    });

                    isInitWaiting = false;
                    initCoroutine = null;

                    if (onErrorOccurred != null)
                        onErrorOccurred.Invoke(ErrorCode.TIMEOUT);
                }
                else
                {
                    isInitWaiting = false;
                    initCoroutine = null;

                    if (onErrorOccurred != null)
                        onErrorOccurred.Invoke(ErrorCode.TIMEOUT);
                }
            }
        }

        /// <summary>
        /// Indicates whether this instance has been initialized.
        /// </summary>
        /// <returns><c>true</c>, if this instance has been initialized, <c>false</c> otherwise.</returns>
        public override bool IsInitialized()
        {
            return hasInitDone;
        }

        /// <summary>
        /// Starts the camera.
        /// </summary>
        public override void Play()
        {
            if (hasInitDone)
                StartCoroutine(_Play());
        }

        protected virtual IEnumerator _Play()
        {
            while (isChangeVideoModeWaiting)
            {
                yield return null;
            }

            if (!hasInitDone || videoCapture.IsStreaming) yield break;

            isChangeVideoModeWaiting = true;
            videoCapture.StartVideoModeAsync(cameraParams, result =>
            {
                isChangeVideoModeWaiting = false;
            });
        }

        /// <summary>
        /// Pauses the active camera.
        /// </summary>
        public override void Pause()
        {
            if (hasInitDone)
                StartCoroutine(_Stop());
        }

        /// <summary>
        /// Stops the active camera.
        /// </summary>
        public override void Stop()
        {
            if (hasInitDone)
                StartCoroutine(_Stop());
        }

        protected virtual IEnumerator _Stop()
        {
            while (isChangeVideoModeWaiting)
            {
                yield return null;
            }

            if (!hasInitDone || !videoCapture.IsStreaming) yield break;

            isChangeVideoModeWaiting = true;
            videoCapture.StopVideoModeAsync(result =>
            {
                isChangeVideoModeWaiting = false;
            });
        }

        /// <summary>
        /// Indicates whether the active camera is currently playing.
        /// </summary>
        /// <returns><c>true</c>, if the active camera is playing, <c>false</c> otherwise.</returns>
        public override bool IsPlaying()
        {
            if (!hasInitDone)
                return false;

            return videoCapture.IsStreaming;
        }

        /// <summary>
        /// Indicates whether the active camera device is currently front facng.
        /// </summary>
        /// <returns><c>true</c>, if the active camera device is front facng, <c>false</c> otherwise.</returns>
        public override bool IsFrontFacing()
        {
            return false;
        }

        /// <summary>
        /// Returns the active camera device name.
        /// </summary>
        /// <returns>The active camera device name.</returns>
        public override string GetDeviceName()
        {
            return "";
        }

        /// <summary>
        /// Returns the active camera width.
        /// </summary>
        /// <returns>The active camera width.</returns>
        public override int GetWidth()
        {
            if (!hasInitDone)
                return -1;
            return (rotatedFrameMat != null) ? frameMat.height() : frameMat.width();
        }

        /// <summary>
        /// Returns the active camera height.
        /// </summary>
        /// <returns>The active camera height.</returns>
        public override int GetHeight()
        {
            if (!hasInitDone)
                return -1;
            return (rotatedFrameMat != null) ? frameMat.width() : frameMat.height();
        }

        /// <summary>
        /// Returns the active camera framerate.
        /// </summary>
        /// <returns>The active camera framerate.</returns>
        public override float GetFPS()
        {
            return hasInitDone ? cameraParams.frameRate : -1f;
        }

        /// <summary>
        /// Returns the webcam texture.
        /// </summary>
        /// <returns>The webcam texture.</returns>
        public override WebCamTexture GetWebCamTexture()
        {
            return null;
        }

        /// <summary>
        /// Returns the camera to world matrix.
        /// </summary>
        /// <returns>The camera to world matrix.</returns>
        public override Matrix4x4 GetCameraToWorldMatrix()
        {
            return cameraToWorldMatrix;
        }

        /// <summary>
        /// Returns the projection matrix matrix.
        /// </summary>
        /// <returns>The projection matrix.</returns>
        public override Matrix4x4 GetProjectionMatrix()
        {
            return projectionMatrix;
        }

        /// <summary>
        /// Indicates whether the video buffer of the frame has been updated.
        /// </summary>
        /// <returns><c>true</c>, if the video buffer has been updated <c>false</c> otherwise.</returns>
        public override bool DidUpdateThisFrame()
        {
            if (!hasInitDone)
                return false;

            return didUpdateThisFrame;
        }

        /// <summary>
        /// Gets the mat of the current frame.
        /// The Mat object's type is 'CV_8UC4' or 'CV_8UC3' or 'CV_8UC1' (ColorFormat is determined by the outputColorFormat setting).
        /// Please do not dispose of the returned mat as it will be reused.
        /// </summary>
        /// <returns>The mat of the current frame.</returns>
        public override Mat GetMat()
        {
            if (!hasInitDone || !videoCapture.IsStreaming || latestImageBytes == null)
            {
                return (rotatedFrameMat != null) ? rotatedFrameMat : frameMat;
            }

            if (baseColorFormat == outputColorFormat)
            {
                MatUtils.copyToMat<byte>(latestImageBytes, frameMat);
            }
            else
            {
                MatUtils.copyToMat<byte>(latestImageBytes, baseMat);
                Imgproc.cvtColor(baseMat, frameMat, ColorConversionCodes(baseColorFormat, outputColorFormat));
            }

            if (rotatedFrameMat != null)
            {
                Core.rotate(frameMat, rotatedFrameMat, Core.ROTATE_90_CLOCKWISE);
                FlipMat(rotatedFrameMat, _flipVertical, _flipHorizontal);

                return rotatedFrameMat;
            }
            else
            {
                FlipMat(frameMat, _flipVertical, _flipHorizontal);

                return frameMat;
            }
        }

        /// <summary>
        /// Flips the mat.
        /// </summary>
        /// <param name="mat">Mat.</param>
        protected override void FlipMat(Mat mat, bool flipVertical, bool flipHorizontal)
        {
            int flipCode = int.MinValue;

            if (_flipVertical)
            {
                if (flipCode == int.MinValue)
                {
                    flipCode = 0;
                }
                else if (flipCode == 0)
                {
                    flipCode = int.MinValue;
                }
                else if (flipCode == 1)
                {
                    flipCode = -1;
                }
                else if (flipCode == -1)
                {
                    flipCode = 1;
                }
            }

            if (_flipHorizontal)
            {
                if (flipCode == int.MinValue)
                {
                    flipCode = 1;
                }
                else if (flipCode == 0)
                {
                    flipCode = -1;
                }
                else if (flipCode == 1)
                {
                    flipCode = int.MinValue;
                }
                else if (flipCode == -1)
                {
                    flipCode = 0;
                }
            }

            if (flipCode > int.MinValue)
            {
                Core.flip(mat, mat, flipCode);
            }
        }

        /// <summary>
        /// To release the resources.
        /// </summary>
        protected override void ReleaseResources()
        {
            isInitWaiting = false;
            hasInitDone = false;
            hasInitEventCompleted = false;

            latestImageBytes = null;
            didUpdateThisFrame = false;
            didUpdateImageBufferInCurrentFrame = false;

            if (frameMat != null)
            {
                frameMat.Dispose();
                frameMat = null;
            }
            if (baseMat != null)
            {
                baseMat.Dispose();
                baseMat = null;
            }
            if (rotatedFrameMat != null)
            {
                rotatedFrameMat.Dispose();
                rotatedFrameMat = null;
            }
        }

        /// <summary>
        /// Releases all resource used by the <see cref="WebCamTextureToMatHelper"/> object.
        /// </summary>
        /// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="WebCamTextureToMatHelper"/>. The
        /// <see cref="Dispose"/> method leaves the <see cref="WebCamTextureToMatHelper"/> in an unusable state. After
        /// calling <see cref="Dispose"/>, you must release all references to the <see cref="WebCamTextureToMatHelper"/> so
        /// the garbage collector can reclaim the memory that the <see cref="WebCamTextureToMatHelper"/> was occupying.</remarks>
        public override void Dispose()
        {
            if (colors != null)
                colors = null;

            if (isInitWaiting)
            {
                CancelInitCoroutine();

                frameMatAcquired = null;

                if (videoCapture != null)
                {
                    videoCapture.FrameSampleAcquired -= OnFrameSampleAcquired;

                    StartCoroutine(_Dispose());
                }

                ReleaseResources();
            }
            else if (hasInitDone)
            {
                frameMatAcquired = null;

                videoCapture.FrameSampleAcquired -= OnFrameSampleAcquired;

                StartCoroutine(_Dispose());

                ReleaseResources();

                if (onDisposed != null)
                    onDisposed.Invoke();
            }
        }

        protected virtual IEnumerator _Dispose()
        {
            while (isChangeVideoModeWaiting)
            {
                yield return null;
            }

            isChangeVideoModeWaiting = true;
            videoCapture.StopVideoModeAsync(result =>
            {
                videoCapture.Dispose();
                videoCapture = null;
                isChangeVideoModeWaiting = false;
            });
        }
#endif
    }
}

#endif
#endif
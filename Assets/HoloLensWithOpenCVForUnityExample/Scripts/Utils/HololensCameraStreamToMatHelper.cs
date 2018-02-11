#pragma warning disable 0067
using HoloLensCameraStream;
using OpenCVForUnity;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace HoloLensWithOpenCVForUnityExample
{
    /// <summary>
    /// This is called every time there is a new frame image mat available.
    /// The Mat object's type is 'CV_8UC4' (BGRA).
    /// </summary>
    /// <param name="videoCaptureSample">The recently captured frame image mat.</param>
    /// <param name="projectionMatrix">projection matrices.</param>
    /// <param name="cameraToWorldMatrix">camera to world matrices.</param>
    public delegate void FrameMatAcquiredCallback (Mat mat, Matrix4x4 projectionMatrix, Matrix4x4 cameraToWorldMatrix);

    /// <summary>
    /// Hololens camera stream to mat helper.
    /// v 1.0.0
    /// 
    /// Combination of camera frame size and frame rate that can be acquired on Hololens. (width x height : framerate)
    /// 1280 x 720 : 30
    /// 1280 x 720 : 24
    /// 1280 x 720 : 20
    /// 1280 x 720 : 15
    /// 1280 x 720 : 5
    /// 
    /// 896 x 504 : 29.97003
    /// 896 x 504 : 24
    /// 896 x 504 : 20
    /// 896 x 504 : 15
    /// 896 x 504 : 5
    /// 
    /// 1344 x 756 : 29.97003
    /// 1344 x 756 : 24
    /// 1344 x 756 : 20
    /// 1344 x 756 : 15
    /// 1344 x 756 : 5
    /// 
    /// 1408 x 792 : 29.97003
    /// 1408 x 792 : 24
    /// 1408 x 792 : 20
    /// 1408 x 792 : 15
    /// 1408 x 792 : 5
    /// </summary>
    public class HololensCameraStreamToMatHelper : WebCamTextureToMatHelper
    {
        /// <summary>
        /// This will be called whenever a new camera frame image available is converted to Mat.
        /// The Mat object's type is 'CV_8UC4' (BGRA).
        /// You must properly initialize the HololensCameraStreamToMatHelper, 
        /// including calling Play() before this event will begin firing.
        /// </summary>
        public event FrameMatAcquiredCallback frameMatAcquired;

        #if NETFX_CORE
        protected System.Object lockObject = new System.Object ();
        protected System.Object matrixLockObject = new System.Object ();
        protected System.Object latestImageBytesLockObject = new System.Object ();

        protected HoloLensCameraStream.VideoCapture videoCapture;
        protected CameraParameters cameraParams;

        protected Matrix4x4 _cameraToWorldMatrix = Matrix4x4.identity;
        protected Matrix4x4 cameraToWorldMatrix {
            get { lock (matrixLockObject)
                return _cameraToWorldMatrix; }
            set { lock (matrixLockObject)
                _cameraToWorldMatrix = value; }
        }

        protected Matrix4x4 _projectionMatrix = Matrix4x4.identity;
        protected Matrix4x4 projectionMatrix {
            get { lock (matrixLockObject)
                return _projectionMatrix; }
            set { lock (matrixLockObject)
                _projectionMatrix = value; }
        }
            
        protected bool _didUpdateThisFrame = false;
        protected bool didUpdateThisFrame {
            get { lock (lockObject)
                return _didUpdateThisFrame; }
            set { lock (lockObject)
                _didUpdateThisFrame = value; }
        }
            
        protected bool _didUpdateImageBufferInCurrentFrame = false;
        protected bool didUpdateImageBufferInCurrentFrame {
            get { lock (lockObject)
                return _didUpdateImageBufferInCurrentFrame; }
            set { lock (lockObject)
                _didUpdateImageBufferInCurrentFrame = value; }
        }
            
        protected byte[] _latestImageBytes;
        protected byte[] latestImageBytes {
            get { lock (latestImageBytesLockObject)
                    return _latestImageBytes; }
            set { lock (latestImageBytesLockObject)
                    _latestImageBytes = value; }
        }

        protected IntPtr spatialCoordinateSystemPtr;

        protected bool isChangeVideoModeWaiting = false;

        protected bool _hasInitDone = false;
        new protected bool hasInitDone {
            get { lock (lockObject)
                return _hasInitDone; }
            set { lock (lockObject)
                _hasInitDone = value; }
        }

        protected virtual void LateUpdate ()
        {
            if (didUpdateThisFrame && !didUpdateImageBufferInCurrentFrame)
                didUpdateThisFrame = false;

            didUpdateImageBufferInCurrentFrame = false;
        }
            
        protected virtual void OnFrameSampleAcquired(VideoCaptureSample sample)
        {
            lock (latestImageBytesLockObject){
                //When copying the bytes out of the buffer, you must supply a byte[] that is appropriately sized.
                //You can reuse this byte[] until you need to resize it (for whatever reason).
                if (_latestImageBytes == null || _latestImageBytes.Length < sample.dataLength) {
                    _latestImageBytes = new byte[sample.dataLength];
                }
                sample.CopyRawImageDataIntoBuffer (_latestImageBytes);
            }

            float[] cameraToWorldMatrixAsFloat;
            if (sample.TryGetCameraToWorldMatrix(out cameraToWorldMatrixAsFloat) == false)
            {
                sample.Dispose();
                return;
            }
                
            float[] projectionMatrixAsFloat;
            if (sample.TryGetProjectionMatrix(out projectionMatrixAsFloat) == false)
            {
                sample.Dispose();
                return;
            }

            // Right now we pass things across the pipe as a float array then convert them back into UnityEngine.Matrix using a utility method
            projectionMatrix = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4 (projectionMatrixAsFloat);
            cameraToWorldMatrix = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4 (cameraToWorldMatrixAsFloat);

            sample.Dispose();

            didUpdateThisFrame = true;
            didUpdateImageBufferInCurrentFrame = true;

            if (hasInitDone && frameMatAcquired != null)
            {
                Mat mat = new Mat (cameraParams.cameraResolutionHeight, cameraParams.cameraResolutionWidth, CvType.CV_8UC4);
                OpenCVForUnity.Utils.copyToMat<byte> (latestImageBytes, mat);

                if (_requestedRotate90Degree) {
                    Mat rotatedFrameMat = new Mat (cameraParams.cameraResolutionWidth, cameraParams.cameraResolutionHeight, CvType.CV_8UC4);
                    Core.rotate (mat, rotatedFrameMat, Core.ROTATE_90_CLOCKWISE);
                    mat.Dispose();

                    FlipMat (rotatedFrameMat);

                    frameMatAcquired.Invoke (rotatedFrameMat, projectionMatrix, cameraToWorldMatrix);
                }else{

                    FlipMat (mat);

                    frameMatAcquired.Invoke (mat, projectionMatrix, cameraToWorldMatrix);
                }
            }
        }

        protected virtual CameraParameters CreateCameraParams (HoloLensCameraStream.VideoCapture videoCapture)
        {
            int min1 = videoCapture.GetSupportedResolutions().Min (r => Mathf.Abs((r.width * r.height) - (_requestedWidth * _requestedHeight)));
            HoloLensCameraStream.Resolution resolution = videoCapture.GetSupportedResolutions().First (r => Mathf.Abs((r.width * r.height) - (_requestedWidth * _requestedHeight)) == min1);

            float min2 = videoCapture.GetSupportedFrameRatesForResolution(resolution).Min (f => Mathf.Abs(f - _requestedFPS));
            float frameRate = videoCapture.GetSupportedFrameRatesForResolution(resolution).First (f => Mathf.Abs(f - _requestedFPS) == min2);

            CameraParameters cameraParams = new CameraParameters();
            cameraParams.cameraResolutionHeight = resolution.height;
            cameraParams.cameraResolutionWidth = resolution.width;
            cameraParams.frameRate = Mathf.RoundToInt(frameRate);
            cameraParams.pixelFormat = CapturePixelFormat.BGRA32;
            cameraParams.enableHolograms = false;

            return cameraParams;
        }
        #endif

        /// <summary>
        /// Returns the video capture.
        /// </summary>
        /// <returns>The video capture.</returns>
        public virtual HoloLensCameraStream.VideoCapture GetVideoCapture ()
        {
            #if NETFX_CORE
            return videoCapture;
            #else
            return null;
            #endif
        }

        #if NETFX_CORE
        /// <summary>
        /// Raises the destroy event.
        /// </summary>
        protected override void OnDestroy ()
        {
            Dispose ();

            if (videoCapture != null) {
                if (videoCapture.IsStreaming) {
                    videoCapture.StopVideoModeAsync (result => {
                        videoCapture.Dispose ();
                        videoCapture = null;
                    });
                } else {
                    videoCapture.Dispose ();
                    videoCapture = null;
                }
            }
        }

        /// <summary>
        /// Initializes this instance by coroutine.
        /// </summary>
        protected override IEnumerator _Initialize ()
        {
            if (hasInitDone)
            {
                ReleaseResources ();

                if (onDisposed != null)
                    onDisposed.Invoke ();
            }

            isInitWaiting = true;

            while(isChangeVideoModeWaiting){
                yield return null;
            }

            isChangeVideoModeWaiting = true;
            if (videoCapture != null) {

                videoCapture.StopVideoModeAsync (result1 => {

                        cameraParams = CreateCameraParams (videoCapture);
                        videoCapture.StartVideoModeAsync(cameraParams, result2 => {
                            if (!result2.success){

                                    isChangeVideoModeWaiting = false;
                                    isInitWaiting = false;
                                    CancelInitCoroutine ();

                                    if (onErrorOccurred != null)
                                        onErrorOccurred.Invoke (ErrorCode.UNKNOWN);

                            } else {
                                isChangeVideoModeWaiting = false;
                            }
                        });

                });

            } else {                

                //Fetch a pointer to Unity's spatial coordinate system if you need pixel mapping
                spatialCoordinateSystemPtr = UnityEngine.VR.WSA.WorldManager.GetNativeISpatialCoordinateSystemPtr ();

                HoloLensCameraStream.VideoCapture.CreateAync (videoCapture => {

                        if (initCoroutine == null) return;
                        
                        if (videoCapture == null)
                        {
                            Debug.LogError("Did not find a video capture object. You may not be using the HoloLens.");

                            isChangeVideoModeWaiting = false;
                            isInitWaiting = false;
                            CancelInitCoroutine ();

                            if (onErrorOccurred != null)
                                onErrorOccurred.Invoke (ErrorCode.CAMERA_DEVICE_NOT_EXIST);

                            return;
                        }

                        this.videoCapture = videoCapture;

                        //Request the spatial coordinate ptr if you want fetch the camera and set it if you need to 
                        videoCapture.WorldOriginPtr = spatialCoordinateSystemPtr;

                        cameraParams = CreateCameraParams (videoCapture);

                        videoCapture.FrameSampleAcquired -= OnFrameSampleAcquired;
                        videoCapture.FrameSampleAcquired += OnFrameSampleAcquired;
                        videoCapture.StartVideoModeAsync(cameraParams, result => {

                            if (!result.success){

                                    isChangeVideoModeWaiting = false;
                                    isInitWaiting = false;
                                    CancelInitCoroutine ();

                                    if (onErrorOccurred != null)
                                        onErrorOccurred.Invoke (ErrorCode.UNKNOWN);

                            } else {
                                isChangeVideoModeWaiting = false;
                            }
                        });                        
                });
            }

            int initFrameCount = 0;
            bool isTimeout = false;

            while (true) {
                if (initFrameCount > _timeoutFrameCount) {
                    isTimeout = true;
                    break;
                } else if (didUpdateThisFrame) {

                    Debug.Log ("HololensCameraStreamToMatHelper:: " + "name:" + "" + " width:" + cameraParams.cameraResolutionWidth + " height:" + cameraParams.cameraResolutionHeight + " fps:" + cameraParams.frameRate);

                    if (colors == null || colors.Length != cameraParams.cameraResolutionWidth * cameraParams.cameraResolutionHeight)
                        colors = new Color32[cameraParams.cameraResolutionWidth * cameraParams.cameraResolutionHeight];

                    frameMat = new Mat (cameraParams.cameraResolutionHeight, cameraParams.cameraResolutionWidth, CvType.CV_8UC4);
                    screenOrientation = Screen.orientation;

                    if (_requestedRotate90Degree) {
                        rotatedFrameMat = new Mat (cameraParams.cameraResolutionWidth, cameraParams.cameraResolutionHeight, CvType.CV_8UC4);
                    }

                    isInitWaiting = false;
                    initCoroutine = null;

                    if (onInitialized != null)
                        onInitialized.Invoke ();

                    hasInitDone = true;

                    break;
                } else {
                    initFrameCount++;
                    yield return null;
                }
            }

            if (isTimeout) {
                if (videoCapture != null) {
                    
                    videoCapture.FrameSampleAcquired -= OnFrameSampleAcquired;

                    isChangeVideoModeWaiting = true;
                    videoCapture.StopVideoModeAsync (result => {
                        videoCapture.Dispose ();
                        videoCapture = null;
                        isChangeVideoModeWaiting = false;
                    });

                    isInitWaiting = false;
                    initCoroutine = null;

                    if (onErrorOccurred != null)
                        onErrorOccurred.Invoke (ErrorCode.TIMEOUT);
                } else {

                    isInitWaiting = false;
                    initCoroutine = null;

                    if (onErrorOccurred != null)
                        onErrorOccurred.Invoke (ErrorCode.TIMEOUT);
                }
            }
        }

        /// <summary>
        /// Starts the webcam texture.
        /// </summary>
        public override void Play ()
        {
            if (hasInitDone)
                StartCoroutine (_Play());
        }

        protected virtual IEnumerator _Play ()
        {
            while (isChangeVideoModeWaiting) {
                yield return null;
            }

            if(!hasInitDone || videoCapture.IsStreaming) yield break;

            isChangeVideoModeWaiting = true;
            videoCapture.StartVideoModeAsync (cameraParams, result => {
                isChangeVideoModeWaiting = false;
            });
        }

        /// <summary>
        /// Pauses the webcam texture
        /// </summary>
        public override void Pause ()
        {
            if (hasInitDone)
                StartCoroutine (_Stop());
        }

        /// <summary>
        /// Stops the webcam texture.
        /// </summary>
        public override void Stop ()
        {
            if (hasInitDone)
                StartCoroutine (_Stop());
        }

        protected virtual IEnumerator _Stop ()
        {
            while (isChangeVideoModeWaiting) {
                yield return null;
            }

            if(!hasInitDone || !videoCapture.IsStreaming) yield break;

            isChangeVideoModeWaiting = true;
            videoCapture.StopVideoModeAsync(result => {
                isChangeVideoModeWaiting = false;
            });
        }

        /// <summary>
        /// Indicates whether the webcam texture is currently playing.
        /// </summary>
        /// <returns><c>true</c>, if the webcam texture is playing, <c>false</c> otherwise.</returns>
        public override bool IsPlaying ()
        {
            if (!hasInitDone)
                return false;
            
            return videoCapture.IsStreaming;
        }

        /// <summary>
        /// Returns the webcam texture.
        /// </summary>
        /// <returns>The webcam texture.</returns>
        public override WebCamTexture GetWebCamTexture ()
        {
            return null;
        }

        /// <summary>
        /// Returns the camera to world matrix.
        /// </summary>
        /// <returns>The camera to world matrix.</returns>
        public override Matrix4x4 GetCameraToWorldMatrix ()
        {
            return cameraToWorldMatrix;
        }

        /// <summary>
        /// Returns the projection matrix matrix.
        /// </summary>
        /// <returns>The projection matrix.</returns>
        public override Matrix4x4 GetProjectionMatrix ()
        {
            return projectionMatrix;
        }

        /// <summary>
        /// Indicates whether the video buffer of the frame has been updated.
        /// </summary>
        /// <returns><c>true</c>, if the video buffer has been updated <c>false</c> otherwise.</returns>
        public override bool DidUpdateThisFrame ()
        {
            if (!hasInitDone)
                return false;

            return didUpdateThisFrame;
        }

        /// <summary>
        /// Gets the mat of the current frame.
        /// The Mat object's type is 'CV_8UC4' (BGRA).
        /// </summary>
        /// <returns>The mat of the current frame.</returns>
        public override Mat GetMat ()
        {
            if (!hasInitDone || !videoCapture.IsStreaming || latestImageBytes == null) {
                if (rotatedFrameMat != null) {
                    return rotatedFrameMat;
                } else {
                    return frameMat;
                }
            }

            OpenCVForUnity.Utils.copyToMat<byte> (latestImageBytes, frameMat);

            if (rotatedFrameMat != null) {

                Core.rotate (frameMat, rotatedFrameMat, Core.ROTATE_90_CLOCKWISE);

                FlipMat (rotatedFrameMat);
                    
                return rotatedFrameMat;
            } else {

                FlipMat (frameMat);

                return frameMat;
            }
        }

        /// <summary>
        /// Flips the mat.
        /// </summary>
        /// <param name="mat">Mat.</param>
        protected override void FlipMat (Mat mat)
        {
            int flipCode = int.MinValue;

            if (_flipVertical) {
                if (flipCode == int.MinValue) {
                    flipCode = 0;
                } else if (flipCode == 0) {
                    flipCode = int.MinValue;
                } else if (flipCode == 1) {
                    flipCode = -1;
                } else if (flipCode == -1) {
                    flipCode = 1;
                }
            }

            if (_flipHorizontal) {
                if (flipCode == int.MinValue) {
                    flipCode = 1;
                } else if (flipCode == 0) {
                    flipCode = -1;
                } else if (flipCode == 1) {
                    flipCode = int.MinValue;
                } else if (flipCode == -1) {
                    flipCode = 0;
                }
            }

            if (flipCode > int.MinValue) {
                Core.flip (mat, mat, flipCode);
            }
        }

        /// <summary>
        /// To release the resources.
        /// </summary>
        protected override void ReleaseResources ()
        {
            isInitWaiting = false;
            hasInitDone = false;

            latestImageBytes = null;
            didUpdateThisFrame = false;
            didUpdateImageBufferInCurrentFrame = false;

            if (frameMat != null) {
                frameMat.Dispose ();
                frameMat = null;
            }
            if (rotatedFrameMat != null) {
                rotatedFrameMat.Dispose ();
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
        public override void Dispose ()
        {
            if (colors != null)
                colors = null;

            if (isInitWaiting) {
                
                CancelInitCoroutine ();

                frameMatAcquired = null;

                if (videoCapture != null) {
                    videoCapture.FrameSampleAcquired -= OnFrameSampleAcquired;

                    StartCoroutine (_Dispose ());
                }

                ReleaseResources ();
            }
            else if (hasInitDone)
            {

                frameMatAcquired = null;

                videoCapture.FrameSampleAcquired -= OnFrameSampleAcquired;

                StartCoroutine (_Dispose());

                ReleaseResources ();

                if (onDisposed != null)
                    onDisposed.Invoke ();
            }
        }

        protected virtual IEnumerator _Dispose ()
        {
            while (isChangeVideoModeWaiting) {
                yield return null;
            }
                
            isChangeVideoModeWaiting = true;
            videoCapture.StopVideoModeAsync (result => {
                videoCapture.Dispose ();
                videoCapture = null;
                isChangeVideoModeWaiting = false;
            });
        }
        #endif
    }
}
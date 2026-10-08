using System.Linq;
using System.Threading;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityIntegration;
using OpenCVForUnity.XobjdetectModule;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

#if UNITY_2018_2_OR_NEWER
using UnityEngine.Windows.WebCam;
using WSAWebCamCameraParameters = UnityEngine.Windows.WebCam.CameraParameters;
#else
using UnityEngine.XR.WSA.WebCam;
using WSAWebCamCameraParameters = UnityEngine.XR.WSA.WebCam.CameraParameters;
#endif

namespace HoloLensWithOpenCVForUnityExample
{
    /// <summary>
    /// HoloLens PhotoCapture Example
    /// An example of holographic photo blending using the PhotocCapture class on Hololens.
    /// Referring to https://github.com/microsoft/OpenXR-Unity-MixedReality-Samples/blob/main/BasicSample/Assets/LocatableCamera/Scripts/LocatableCamera.cs
    /// </summary>
    public class HLPhotoCaptureExample : MonoBehaviour
    {
        // Private Fields
        [FormerlySerializedAs("textureShader")]
        [SerializeField]
        private Shader _textureShader = null;

        [FormerlySerializedAs("text")]
        [SerializeField]
        private Text _text = null;

        private PhotoCapture _photoCaptureObject = null;
        private Resolution _cameraResolution = default(Resolution);
        private bool _isCapturingPhoto;
        private bool _isReadyToCapturePhoto = false;
        private uint _numPhotos = 0;
        private CascadeClassifier _cascade;
        private MatOfRect _faces;
        private XROrigin _xrOrigin;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        // Unity Lifecycle Methods
        private async void Start()
        {
            _xrOrigin = FindFirstObjectByType<XROrigin>();

            _faces = new MatOfRect();

            // Asynchronously retrieves the readable file path from the StreamingAssets directory.
            if (_text != null)
            {
                _text.text = "Preparing file access...";
            }

            string cascadeFilepath = await OpenCVForUnityEnv.GetFilePathAsync("OpenCVForUnityExample/objdetect/haarcascade_frontalface_alt.xml", cancellationToken: _cts.Token);

            if (_text != null)
            {
                _text.text = "";
            }

            _cascade = new CascadeClassifier();
            _cascade.load(cascadeFilepath);

            var resolutions = PhotoCapture.SupportedResolutions;
            if (resolutions == null || resolutions.Count() == 0)
            {
                if (_text != null)
                {
                    _text.text = "Resolutions not available. Did you provide web cam access?";
                }
                return;
            }

            _cameraResolution = resolutions.OrderByDescending((res) => res.width * res.height).First();
            PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);

            if (_text != null)
            {
                _text.text = "Starting camera...";
            }
        }

        private void OnDestroy()
        {
            _isReadyToCapturePhoto = false;

            if (_photoCaptureObject != null)
            {
                _photoCaptureObject.StopPhotoModeAsync(OnPhotoCaptureStopped);

                if (_text != null)
                {
                    _text.text = "Stopping camera...";
                }
            }

            _cts?.Cancel();

            _cascade?.Dispose();
            _cascade = null;
            _faces?.Dispose();
            _faces = null;
            _cts?.Dispose();
            _cts = null;
        }

        // Public Methods
        /// <summary>
        /// Takes a photo and attempts to load it into the scene using its location data.
        /// </summary>
        public void TakePhoto()
        {
            if (!_isReadyToCapturePhoto || _isCapturingPhoto)
            {
                return;
            }

            _isCapturingPhoto = true;

            if (_text != null)
            {
                _text.text = "Taking picture...";
            }

            _photoCaptureObject.TakePhotoAsync(OnPhotoCaptured);
        }

        /// <summary>
        /// Raises the back button click event.
        /// </summary>
        public void OnBackButtonClick()
        {
            SceneManager.LoadScene("HoloLensWithOpenCVForUnityExample");
        }

        // Private Methods
        private void OnPhotoCaptureCreated(PhotoCapture captureObject)
        {
            if (_text != null)
            {
                _text.text += "\nPhotoCapture created...";
            }

            _photoCaptureObject = captureObject;

            WSAWebCamCameraParameters cameraParameters = new WSAWebCamCameraParameters(WebCamMode.PhotoMode)
            {
                hologramOpacity = 0.0f,
                cameraResolutionWidth = _cameraResolution.width,
                cameraResolutionHeight = _cameraResolution.height,
                pixelFormat = CapturePixelFormat.BGRA32
            };

            captureObject.StartPhotoModeAsync(cameraParameters, OnPhotoModeStarted);
        }

        private void OnPhotoModeStarted(PhotoCapture.PhotoCaptureResult result)
        {
            if (result.success)
            {
                _isReadyToCapturePhoto = true;

                if (_text != null)
                {
                    _text.text = "Ready!\nPress button to take a picture.";
                }
            }
            else
            {
                _isReadyToCapturePhoto = false;

                if (_text != null)
                {
                    _text.text = "Unable to start photo mode!";
                }
            }
        }

        private void OnPhotoCaptured(PhotoCapture.PhotoCaptureResult result, PhotoCaptureFrame photoCaptureFrame)
        {
            if (result.success)
            {
                if (_text != null)
                {
                    _text.text += "\nTook picture!";
                }

                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = $"Photo{_numPhotos++}";
                quad.transform.parent = transform;

                float ratio = _cameraResolution.height / (float)_cameraResolution.width;
                quad.transform.localScale = new Vector3(2f, 2f * ratio, 1);

                Renderer quadRenderer = quad.GetComponent<Renderer>();
                quadRenderer.material = new Material(_textureShader);
                Texture2D targetTexture = new Texture2D(_cameraResolution.width, _cameraResolution.height, TextureFormat.BGRA32, false);
                photoCaptureFrame.UploadImageDataToTexture(targetTexture);

                Mat bgraMat = new Mat(targetTexture.height, targetTexture.width, CvType.CV_8UC4);
                Mat grayMat = new Mat(bgraMat.rows(), bgraMat.cols(), CvType.CV_8UC1);

                // For BGRA or BGR format, use the texture2DToMatRaw method.
                OpenCVMatUnityUtils.Texture2DToMatRaw(targetTexture, bgraMat);

                Imgproc.cvtColor(bgraMat, grayMat, Imgproc.COLOR_BGRA2GRAY);
                Imgproc.equalizeHist(grayMat, grayMat);

                if (_cascade != null)
                {
                    _cascade.detectMultiScale(grayMat, _faces, 1.1, 2, Xobjdetect.CASCADE_SCALE_IMAGE,
                        new Size(grayMat.cols() * 0.05, grayMat.rows() * 0.05), new Size());
                }

                OpenCVForUnity.CoreModule.Rect[] rects = _faces.toArray();
                for (int i = 0; i < rects.Length; i++)
                {
                    //Debug.Log ("detect faces " + rects [i]);
                    Imgproc.rectangle(bgraMat, new Point(rects[i].x, rects[i].y), new Point(rects[i].x + rects[i].width, rects[i].y + rects[i].height), new Scalar(255, 0, 0, 255), 4);
                }

                // draw an edge lines.
                Imgproc.rectangle(bgraMat, new Point(0, 0), new Point(bgraMat.width(), bgraMat.height()), new Scalar(255, 0, 0, 255), 2);

                Imgproc.putText(bgraMat, targetTexture.format + " W:" + bgraMat.width() + " H:" + bgraMat.height(), new Point(5, bgraMat.rows() - 10), Imgproc.FONT_HERSHEY_SIMPLEX, 1.5, new Scalar(255, 0, 0, 255), 2, Imgproc.LINE_AA, false);

                // For BGRA or BGR format, use the matToTexture2DRaw method.
                OpenCVMatUnityUtils.MatToTexture2DRaw(bgraMat, targetTexture);
                bgraMat.Dispose();
                grayMat.Dispose();

                quadRenderer.sharedMaterial.SetTexture("_MainTex", targetTexture);

                if (photoCaptureFrame.hasLocationData)
                {
                    photoCaptureFrame.TryGetCameraToWorldMatrix(out Matrix4x4 cameraToWorldMatrix);

                    // MRTK's XROrigin (Camera Offset) can apply an additional transform,
                    // such as the Camera Y Offset depending on the Tracking Origin Mode.
                    // Therefore, first convert the camera transform into the XROrigin world space.
                    cameraToWorldMatrix = _xrOrigin.transform.localToWorldMatrix * cameraToWorldMatrix;

                    Vector3 position = cameraToWorldMatrix.GetColumn(3) - cameraToWorldMatrix.GetColumn(2);
                    Quaternion rotation = Quaternion.LookRotation(-cameraToWorldMatrix.GetColumn(2), cameraToWorldMatrix.GetColumn(1));

                    photoCaptureFrame.TryGetProjectionMatrix(Camera.main.nearClipPlane, Camera.main.farClipPlane, out Matrix4x4 projectionMatrix);

                    targetTexture.wrapMode = TextureWrapMode.Clamp;

                    quadRenderer.sharedMaterial.SetMatrix("_WorldToCameraMatrix", cameraToWorldMatrix.inverse);
                    quadRenderer.sharedMaterial.SetMatrix("_CameraProjectionMatrix", projectionMatrix);

                    quad.transform.position = position;
                    quad.transform.rotation = rotation;

                    if (_text != null)
                    {
                        _text.text += $"\nPosition: ({position.x}, {position.y}, {position.z})";
                        _text.text += $"\nRotation: ({rotation.x}, {rotation.y}, {rotation.z}, {rotation.w})";
                    }
                }
                else
                {
                    if (_text != null)
                    {
                        _text.text += "\nNo location data :(";
                    }
                }
            }
            else
            {
                if (_text != null)
                {
                    _text.text += "\nPicture taking failed: " + result.hResult;
                }
            }

            _isCapturingPhoto = false;
        }

        private void OnPhotoCaptureStopped(PhotoCapture.PhotoCaptureResult result)
        {
            if (_text != null)
            {
                _text.text = result.success ? "Photo mode stopped." : "Unable to stop photo mode.";
            }

            _photoCaptureObject.Dispose();
            _photoCaptureObject = null;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils.Helper;
using OpenCVForUnity.UnityUtils;
using HoloLensWithOpenCVForUnity.UnityUtils.Helper;
using HoloLensCameraStream;

namespace HoloLensWithOpenCVForUnityExample
{
    /// <summary>
    /// HLCameraStreamToMatHelper Example
    /// An example of image processing (comic filter) using OpenCVForUnity on Hololens.
    /// Referring to http://dev.classmethod.jp/smartphone/opencv-manga-2/.
    /// </summary>
    [RequireComponent(typeof(HLCameraStreamToMatHelper))]
    public class HLCameraStreamToMatHelperExample : MonoBehaviour
    {
        /// <summary>
        /// The rotate 90 degree toggle.
        /// </summary>
        public Toggle rotate90DegreeToggle;

        /// <summary>
        /// The flip vertical toggle.
        /// </summary>
        public Toggle flipVerticalToggle;

        /// <summary>
        /// The flip horizontal toggle.
        /// </summary>
        public Toggle flipHorizontalToggle;

        /// <summary>
        /// Determines if applys comic filter.
        /// </summary>
        public bool applyComicFilter = false;

        /// <summary>
        /// The apply comic filter toggle.
        /// </summary>
        public Toggle applyComicFilterToggle;

        /// <summary>
        /// The vignette scale.
        /// </summary>
        public float vignetteScale = 0f;

        /// <summary>
        /// The vignette scale slider.
        /// </summary>
        public Slider VignetteScaleSlider;

        /// <summary>
        /// The comic filter.
        /// </summary>
        ComicFilter comicFilter;

        /// <summary>
        /// The texture.
        /// </summary>
        Texture2D texture;

        /// <summary>
        /// The quad renderer.
        /// </summary>
        Renderer quad_renderer;

        /// <summary>
        /// The web cam texture to mat helper.
        /// </summary>
        HLCameraStreamToMatHelper webCamTextureToMatHelper;

        readonly static Queue<Action> ExecuteOnMainThread = new Queue<Action>();

        [Space(10)]

        [HeaderAttribute("Debug")]

        public Text renderFPS;
        public Text videoFPS;
        public Text trackFPS;
        public Text debugStr;

        // Use this for initialization
        protected void Start()
        {
            webCamTextureToMatHelper = gameObject.GetComponent<HLCameraStreamToMatHelper>();
#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            webCamTextureToMatHelper.frameMatAcquired += OnFrameMatAcquired;
#endif
            webCamTextureToMatHelper.outputColorFormat = WebCamTextureToMatHelper.ColorFormat.BGRA;
            webCamTextureToMatHelper.Initialize();

            // Update GUI state
            rotate90DegreeToggle.isOn = webCamTextureToMatHelper.rotate90Degree;
            flipVerticalToggle.isOn = webCamTextureToMatHelper.flipVertical;
            flipHorizontalToggle.isOn = webCamTextureToMatHelper.flipHorizontal;
            applyComicFilterToggle.isOn = applyComicFilter;
            VignetteScaleSlider.value = vignetteScale;
        }

        /// <summary>
        /// Raises the web cam texture to mat helper initialized event.
        /// </summary>
        public void OnWebCamTextureToMatHelperInitialized()
        {
            Debug.Log("OnWebCamTextureToMatHelperInitialized");

            Mat bgraMat = webCamTextureToMatHelper.GetMat();

            texture = new Texture2D(bgraMat.cols(), bgraMat.rows(), TextureFormat.BGRA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            quad_renderer = gameObject.GetComponent<Renderer>() as Renderer;
            quad_renderer.sharedMaterial.SetTexture("_MainTex", texture);


            //Debug.Log("Screen.width " + Screen.width + " Screen.height " + Screen.height + " Screen.orientation " + Screen.orientation);

            DebugUtils.AddDebugStr(webCamTextureToMatHelper.outputColorFormat.ToString() + " "+ webCamTextureToMatHelper.GetWidth() + " x " + webCamTextureToMatHelper.GetHeight() + " : " + webCamTextureToMatHelper.GetFPS());


            Matrix4x4 projectionMatrix;
#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            projectionMatrix = webCamTextureToMatHelper.GetProjectionMatrix();
            quad_renderer.sharedMaterial.SetMatrix("_CameraProjectionMatrix", projectionMatrix);
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
            quad_renderer.sharedMaterial.SetMatrix("_CameraProjectionMatrix", projectionMatrix);
#endif

            quad_renderer.sharedMaterial.SetFloat("_VignetteScale", vignetteScale);


            float halfOfVerticalFov = Mathf.Atan(1.0f / projectionMatrix.m11);
            float aspectRatio = (1.0f / Mathf.Tan(halfOfVerticalFov)) / projectionMatrix.m00;
            Debug.Log("halfOfVerticalFov " + halfOfVerticalFov);
            Debug.Log("aspectRatio " + aspectRatio);

            comicFilter = new ComicFilter(60, 120, 3);
        }

        /// <summary>
        /// Raises the web cam texture to mat helper disposed event.
        /// </summary>
        public void OnWebCamTextureToMatHelperDisposed()
        {
            Debug.Log("OnWebCamTextureToMatHelperDisposed");

            lock (ExecuteOnMainThread)
            {
                ExecuteOnMainThread.Clear();
            }

            comicFilter.Dispose();

            if (debugStr != null)
            {
                debugStr.text = string.Empty;
            }
            DebugUtils.ClearDebugStr();
        }

        /// <summary>
        /// Raises the web cam texture to mat helper error occurred event.
        /// </summary>
        /// <param name="errorCode">Error code.</param>
        public void OnWebCamTextureToMatHelperErrorOccurred(WebCamTextureToMatHelper.ErrorCode errorCode)
        {
            Debug.Log("OnWebCamTextureToMatHelperErrorOccurred " + errorCode);
        }

#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
        public void OnFrameMatAcquired(Mat bgraMat, Matrix4x4 projectionMatrix, Matrix4x4 cameraToWorldMatrix, CameraIntrinsics cameraIntrinsics)
        {
            DebugUtils.VideoTick();

            if (applyComicFilter)
            {
                comicFilter.Process(bgraMat, bgraMat, true);
            }
            else
            {
                Imgproc.rectangle(bgraMat, new Point(0, 0), new Point(bgraMat.width(), bgraMat.height()), new Scalar(255, 0, 0, 255), 2);
                Imgproc.putText(bgraMat, "W:" + bgraMat.width() + " H:" + bgraMat.height(), new Point(5, bgraMat.rows() - 10), Imgproc.FONT_HERSHEY_SIMPLEX, 1.0, new Scalar(255, 0, 0, 255), 2, Imgproc.LINE_AA, false);
            }

            DebugUtils.TrackTick();

            Enqueue(() =>
            {

                if (!webCamTextureToMatHelper.IsPlaying()) return;

                // For BGRA or BGR format, use the fastMatToTexture2D method.
                Utils.fastMatToTexture2D(bgraMat, texture);
                bgraMat.Dispose();

                Matrix4x4 worldToCameraMatrix = cameraToWorldMatrix.inverse;

                quad_renderer.sharedMaterial.SetMatrix("_WorldToCameraMatrix", worldToCameraMatrix);
                quad_renderer.sharedMaterial.SetMatrix("_CameraProjectionMatrix", projectionMatrix);

                // Position the canvas object slightly in front
                // of the real world web camera.
                Vector3 position = cameraToWorldMatrix.GetColumn(3) - cameraToWorldMatrix.GetColumn(2) * 2.2f;

                // Rotate the canvas object so that it faces the user.
                Quaternion rotation = Quaternion.LookRotation(-cameraToWorldMatrix.GetColumn(2), cameraToWorldMatrix.GetColumn(1));

                gameObject.transform.position = position;
                gameObject.transform.rotation = rotation;

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
        void Update()
        {
            if (webCamTextureToMatHelper.IsPlaying() && webCamTextureToMatHelper.DidUpdateThisFrame())
            {
                DebugUtils.VideoTick();

                Mat bgraMat = webCamTextureToMatHelper.GetMat();

                if (applyComicFilter)
                {
                    comicFilter.Process(bgraMat, bgraMat, false);
                }
                else
                {
                    Imgproc.rectangle(bgraMat, new Point(0, 0), new Point(bgraMat.width(), bgraMat.height()), new Scalar(255, 0, 0, 255), 2);
                    Imgproc.putText(bgraMat, "W:" + bgraMat.width() + " H:" + bgraMat.height(), new Point(5, bgraMat.rows() - 10), Imgproc.FONT_HERSHEY_SIMPLEX, 1.0, new Scalar(255, 0, 0, 255), 2, Imgproc.LINE_AA, false);
                }

                DebugUtils.TrackTick();

                // For BGRA or BGR format, use the fastMatToTexture2D method.
                Utils.fastMatToTexture2D(bgraMat, texture);
            }

            if (webCamTextureToMatHelper.IsPlaying())
            {

                Matrix4x4 cameraToWorldMatrix = webCamTextureToMatHelper.GetCameraToWorldMatrix();
                Matrix4x4 worldToCameraMatrix = cameraToWorldMatrix.inverse;

                quad_renderer.sharedMaterial.SetMatrix("_WorldToCameraMatrix", worldToCameraMatrix);

                // Position the canvas object slightly in front
                // of the real world web camera.
                Vector3 position = cameraToWorldMatrix.GetColumn(3) - cameraToWorldMatrix.GetColumn(2) * 2.2f;

                // Rotate the canvas object so that it faces the user.
                Quaternion rotation = Quaternion.LookRotation(-cameraToWorldMatrix.GetColumn(2), cameraToWorldMatrix.GetColumn(1));

                gameObject.transform.position = position;
                gameObject.transform.rotation = rotation;
            }
        }
#endif

        void LateUpdate()
        {
            DebugUtils.RenderTick();
            float renderDeltaTime = DebugUtils.GetRenderDeltaTime();
            float videoDeltaTime = DebugUtils.GetVideoDeltaTime();
            float trackDeltaTime = DebugUtils.GetTrackDeltaTime();

            if (renderFPS != null)
            {
                renderFPS.text = string.Format("Render: {0:0.0} ms ({1:0.} fps)", renderDeltaTime, 1000.0f / renderDeltaTime);
            }
            if (videoFPS != null)
            {
                videoFPS.text = string.Format("Video: {0:0.0} ms ({1:0.} fps)", videoDeltaTime, 1000.0f / videoDeltaTime);
            }
            if (trackFPS != null)
            {
                trackFPS.text = string.Format("Track:   {0:0.0} ms ({1:0.} fps)", trackDeltaTime, 1000.0f / trackDeltaTime);
            }
            if (debugStr != null)
            {
                if (DebugUtils.GetDebugStrLength() > 0)
                {
                    if (debugStr.preferredHeight >= debugStr.rectTransform.rect.height)
                        debugStr.text = string.Empty;

                    debugStr.text += DebugUtils.GetDebugStr();
                    DebugUtils.ClearDebugStr();
                }
            }
        }

        /// <summary>
        /// Raises the destroy event.
        /// </summary>
        void OnDestroy()
        {
#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            webCamTextureToMatHelper.frameMatAcquired -= OnFrameMatAcquired;
#endif
            webCamTextureToMatHelper.Dispose();
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
            webCamTextureToMatHelper.Play();
        }

        /// <summary>
        /// Raises the pause button click event.
        /// </summary>
        public void OnPauseButtonClick()
        {
            webCamTextureToMatHelper.Pause();
        }

        /// <summary>
        /// Raises the stop button click event.
        /// </summary>
        public void OnStopButtonClick()
        {
            webCamTextureToMatHelper.Stop();
        }

        /// <summary>
        /// Raises the change camera button click event.
        /// </summary>
        public void OnChangeCameraButtonClick()
        {
            webCamTextureToMatHelper.requestedIsFrontFacing = !webCamTextureToMatHelper.IsFrontFacing();
        }

        /// <summary>
        /// Raises the rotate 90 degree toggle value changed event.
        /// </summary>
        public void OnRotate90DegreeToggleValueChanged()
        {
            if (rotate90DegreeToggle.isOn != webCamTextureToMatHelper.rotate90Degree)
            {
                webCamTextureToMatHelper.rotate90Degree = rotate90DegreeToggle.isOn;
            }
        }

        /// <summary>
        /// Raises the flip vertical toggle value changed event.
        /// </summary>
        public void OnFlipVerticalToggleValueChanged()
        {
            if (flipVerticalToggle.isOn != webCamTextureToMatHelper.flipVertical)
            {
                webCamTextureToMatHelper.flipVertical = flipVerticalToggle.isOn;
            }
        }

        /// <summary>
        /// Raises the flip horizontal toggle value changed event.
        /// </summary>
        public void OnFlipHorizontalToggleValueChanged()
        {
            if (flipHorizontalToggle.isOn != webCamTextureToMatHelper.flipHorizontal)
            {
                webCamTextureToMatHelper.flipHorizontal = flipHorizontalToggle.isOn;
            }
        }

        /// <summary>
        /// Raises the apply comic filter toggle value changed event.
        /// </summary>
        public void OnApplyComicFilterToggleValueChanged()
        {
            applyComicFilter = applyComicFilterToggle.isOn;
        }

        /// <summary>
        /// Raises the vignette scale slider value changed event.
        /// </summary>
        public void OnVignetteScaleSliderValueChanged()
        {
            vignetteScale = VignetteScaleSlider.value;

            if (quad_renderer != null)
                quad_renderer.sharedMaterial.SetFloat("_VignetteScale", vignetteScale);
        }
    }
}
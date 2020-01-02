using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils.Helper;
using OpenCVForUnity.UnityUtils;
using Rect = OpenCVForUnity.CoreModule.Rect;
using HoloLensWithOpenCVForUnity.UnityUtils.Helper;

namespace HoloLensWithOpenCVForUnityExample
{
    /// <summary>
    /// HoloLens Comic Filter Example
    /// An example of image processing (comic filter) using OpenCVForUnity on Hololens.
    /// Referring to http://dev.classmethod.jp/smartphone/opencv-manga-2/.
    /// </summary>
    [RequireComponent(typeof(HololensCameraStreamToMatHelper))]
    public class HoloLensComicFilterExample : MonoBehaviour
    {
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
        HololensCameraStreamToMatHelper webCamTextureToMatHelper;

        public float vignetteScale = 1.5f;
        public Slider VignetteScaleSlider;

        readonly static Queue<Action> ExecuteOnMainThread = new Queue<Action>();

        // Use this for initialization
        protected void Start ()
        {
            webCamTextureToMatHelper = gameObject.GetComponent<HololensCameraStreamToMatHelper> ();
            #if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            webCamTextureToMatHelper.frameMatAcquired += OnFrameMatAcquired;
            #endif
            webCamTextureToMatHelper.Initialize ();

            VignetteScaleSlider.value = vignetteScale;
        }

        /// <summary>
        /// Raises the web cam texture to mat helper initialized event.
        /// </summary>
        public void OnWebCamTextureToMatHelperInitialized ()
        {
            Debug.Log ("OnWebCamTextureToMatHelperInitialized");
        
            Mat webCamTextureMat = webCamTextureToMatHelper.GetMat ();


            #if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            // HololensCameraStream always returns image data in BGRA format.
            texture = new Texture2D (webCamTextureMat.cols (), webCamTextureMat.rows (), TextureFormat.BGRA32, false);
            #else
            texture = new Texture2D (webCamTextureMat.cols (), webCamTextureMat.rows (), TextureFormat.RGBA32, false);
            #endif

            texture.wrapMode = TextureWrapMode.Clamp;

            Debug.Log ("Screen.width " + Screen.width + " Screen.height " + Screen.height + " Screen.orientation " + Screen.orientation);
        
            comicFilter = new ComicFilter(60, 120, 3);

            quad_renderer = gameObject.GetComponent<Renderer> () as Renderer;
            quad_renderer.sharedMaterial.SetTexture ("_MainTex", texture);

            Matrix4x4 projectionMatrix;
            #if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            projectionMatrix = webCamTextureToMatHelper.GetProjectionMatrix ();
            quad_renderer.sharedMaterial.SetMatrix ("_CameraProjectionMatrix", projectionMatrix);
            #else
            //This value is obtained from PhotoCapture's TryGetProjectionMatrix() method.I do not know whether this method is good.
            //Please see the discussion of this thread.Https://forums.hololens.com/discussion/782/live-stream-of-locatable-camera-webcam-in-unity
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
            quad_renderer.sharedMaterial.SetMatrix ("_CameraProjectionMatrix", projectionMatrix);
            #endif

            quad_renderer.sharedMaterial.SetFloat ("_VignetteScale", vignetteScale);


            float halfOfVerticalFov = Mathf.Atan (1.0f / projectionMatrix.m11);
            float aspectRatio = (1.0f / Mathf.Tan (halfOfVerticalFov)) / projectionMatrix.m00;
            Debug.Log ("halfOfVerticalFov " + halfOfVerticalFov);
            Debug.Log ("aspectRatio " + aspectRatio);
        }

        /// <summary>
        /// Raises the web cam texture to mat helper disposed event.
        /// </summary>
        public void OnWebCamTextureToMatHelperDisposed ()
        {
            Debug.Log ("OnWebCamTextureToMatHelperDisposed");

            lock (ExecuteOnMainThread)
            {
                ExecuteOnMainThread.Clear();
            }

            comicFilter.Dispose();
        }

        /// <summary>
        /// Raises the web cam texture to mat helper error occurred event.
        /// </summary>
        /// <param name="errorCode">Error code.</param>
        public void OnWebCamTextureToMatHelperErrorOccurred(WebCamTextureToMatHelper.ErrorCode errorCode){
            Debug.Log ("OnWebCamTextureToMatHelperErrorOccurred " + errorCode);
        }

#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
        public void OnFrameMatAcquired (Mat bgraMat, Matrix4x4 projectionMatrix, Matrix4x4 cameraToWorldMatrix)
        {
            comicFilter.Process(bgraMat, bgraMat, true);

            //Imgproc.putText (bgraMat, "W:" + bgraMat.width () + " H:" + bgraMat.height () + " SO:" + Screen.orientation, new Point (5, bgraMat.rows () - 10), Imgproc.FONT_HERSHEY_SIMPLEX, 1.0, new Scalar (0), 2, Imgproc.LINE_AA, false);


            Enqueue(() => {

                if (!webCamTextureToMatHelper.IsPlaying ()) return;

                Utils.fastMatToTexture2D(bgraMat, texture);
                bgraMat.Dispose ();

                Matrix4x4 worldToCameraMatrix = cameraToWorldMatrix.inverse;

                quad_renderer.sharedMaterial.SetMatrix ("_WorldToCameraMatrix", worldToCameraMatrix);

                // Position the canvas object slightly in front
                // of the real world web camera.
                Vector3 position = cameraToWorldMatrix.GetColumn(3) - cameraToWorldMatrix.GetColumn(2) * 2.2f;

                // Rotate the canvas object so that it faces the user.
                Quaternion rotation = Quaternion.LookRotation (-cameraToWorldMatrix.GetColumn (2), cameraToWorldMatrix.GetColumn (1));

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
        void Update ()
        {
            if (webCamTextureToMatHelper.IsPlaying () && webCamTextureToMatHelper.DidUpdateThisFrame ()) {

                Mat rgbaMat = webCamTextureToMatHelper.GetMat ();

                comicFilter.Process(rgbaMat, rgbaMat, false);

                //Imgproc.putText (rgbaMat, "W:" + rgbaMat.width () + " H:" + rgbaMat.height () + " SO:" + Screen.orientation, new Point (5, rgbaMat.rows () - 10), Imgproc.FONT_HERSHEY_SIMPLEX, 1.0, new Scalar (0), 2, Imgproc.LINE_AA, false);

                Utils.fastMatToTexture2D(rgbaMat, texture);
            }

            if (webCamTextureToMatHelper.IsPlaying ()) {

                Matrix4x4 cameraToWorldMatrix = webCamTextureToMatHelper.GetCameraToWorldMatrix();
                Matrix4x4 worldToCameraMatrix = cameraToWorldMatrix.inverse;

                quad_renderer.sharedMaterial.SetMatrix ("_WorldToCameraMatrix", worldToCameraMatrix);

                // Position the canvas object slightly in front
                // of the real world web camera.
                Vector3 position = cameraToWorldMatrix.GetColumn(3) - cameraToWorldMatrix.GetColumn(2) * 2.2f;

                // Rotate the canvas object so that it faces the user.
                Quaternion rotation = Quaternion.LookRotation (-cameraToWorldMatrix.GetColumn (2), cameraToWorldMatrix.GetColumn (1));

                gameObject.transform.position = position;
                gameObject.transform.rotation = rotation;
            }
        }
        #endif

        /// <summary>
        /// Raises the destroy event.
        /// </summary>
        void OnDestroy ()
        {
            #if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            webCamTextureToMatHelper.frameMatAcquired -= OnFrameMatAcquired;
            #endif
            webCamTextureToMatHelper.Dispose ();
        }

        /// <summary>
        /// Raises the back button click event.
        /// </summary>
        public void OnBackButtonClick ()
        {
            SceneManager.LoadScene("HoloLensWithOpenCVForUnityExample");
        }

        /// <summary>
        /// Raises the play button click event.
        /// </summary>
        public void OnPlayButtonClick ()
        {
            webCamTextureToMatHelper.Play ();
        }

        /// <summary>
        /// Raises the pause button click event.
        /// </summary>
        public void OnPauseButtonClick ()
        {
            webCamTextureToMatHelper.Pause ();
        }

        /// <summary>
        /// Raises the stop button click event.
        /// </summary>
        public void OnStopButtonClick ()
        {
            webCamTextureToMatHelper.Stop ();
        }

        /// <summary>
        /// Raises the change camera button click event.
        /// </summary>
        public void OnChangeCameraButtonClick ()
        {
            webCamTextureToMatHelper.requestedIsFrontFacing = !webCamTextureToMatHelper.IsFrontFacing();
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
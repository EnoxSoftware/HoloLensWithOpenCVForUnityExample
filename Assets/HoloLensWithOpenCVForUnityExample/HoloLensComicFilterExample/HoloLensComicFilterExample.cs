using UnityEngine;
using System.Collections;
using UnityEngine.UI;

#if UNITY_5_3 || UNITY_5_3_OR_NEWER
using UnityEngine.SceneManagement;
#endif
using OpenCVForUnity;

namespace HoloLensWithOpenCVForUnityExample
{
    /// <summary>
    /// HoloLens ComicFilter example.
    /// An example of image processing (comic filter) using OpenCVForUnity on Hololens.
    /// Referring to http://dev.classmethod.jp/smartphone/opencv-manga-2/.
    /// </summary>
    [RequireComponent(typeof(OptimizationWebCamTextureToMatHelper))]
    public class HoloLensComicFilterExample : MonoBehaviour
    {
        /// <summary>
        /// The gray mat.
        /// </summary>
        Mat grayMat;

        /// <summary>
        /// The line mat.
        /// </summary>
        Mat lineMat;

        /// <summary>
        /// The mask mat.
        /// </summary>
        Mat maskMat;

        /// <summary>
        /// The background mat.
        /// </summary>
        Mat bgMat;

        /// <summary>
        /// The dst mat.
        /// </summary>
        Mat dstMat;

        /// <summary>
        /// The gray pixels.
        /// </summary>
        byte[] grayPixels;

        /// <summary>
        /// The mask pixels.
        /// </summary>
        byte[] maskPixels;

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
        OptimizationWebCamTextureToMatHelper webCamTextureToMatHelper;

        OpenCVForUnity.Rect processingAreaRect;
        public Vector2 outsideClippingRatio = new Vector2(0.17f, 0.19f);
        public Vector2 clippingOffset = new Vector2(0.043f, -0.041f);
        public float vignetteScale = 1.8f;

        // Debug
//        public Vector2 outsideClippingRatio = new Vector2(0.0f, 0.0f);
//        public Vector2 clippingOffset = new Vector2(0.0f, 0.0f);
//        public float vignetteScale = 0.3f;

        Mat dstMatClippingROI;

        // Use this for initialization
        void Start ()
        {
            webCamTextureToMatHelper = gameObject.GetComponent<OptimizationWebCamTextureToMatHelper> ();
            webCamTextureToMatHelper.Initialize ();
        }

        /// <summary>
        /// Raises the web cam texture to mat helper initialized event.
        /// </summary>
        public void OnWebCamTextureToMatHelperInitialized ()
        {
            Debug.Log ("OnWebCamTextureToMatHelperInitialized");
        
            Mat webCamTextureMat = webCamTextureToMatHelper.GetDownScaleMat( webCamTextureToMatHelper.GetMat ());
        
            texture = new Texture2D (webCamTextureMat.cols (), webCamTextureMat.rows (), TextureFormat.RGBA32, false);

            gameObject.GetComponent<Renderer> ().material.mainTexture = texture;
        

            Debug.Log ("Screen.width " + Screen.width + " Screen.height " + Screen.height + " Screen.orientation " + Screen.orientation);
        

            processingAreaRect = new OpenCVForUnity.Rect ((int)(webCamTextureMat.cols ()*(outsideClippingRatio.x - clippingOffset.x)), (int)(webCamTextureMat.rows ()*(outsideClippingRatio.y + clippingOffset.y)),
                (int)(webCamTextureMat.cols ()*(1f-outsideClippingRatio.x*2)), (int)(webCamTextureMat.rows ()*(1f-outsideClippingRatio.y*2)));
            processingAreaRect = processingAreaRect.intersect (new OpenCVForUnity.Rect(0,0,webCamTextureMat.cols (),webCamTextureMat.rows ()));
            
            dstMat = new Mat (webCamTextureMat.rows (), webCamTextureMat.cols (), CvType.CV_8UC1);
            dstMatClippingROI = new Mat (dstMat, processingAreaRect);

            // fill all black.
            //Imgproc.rectangle (dstMat, new Point (0, 0), new Point (dstMat.width (), dstMat.height ()), new Scalar (0, 0, 0, 0), -1);


            grayMat = new Mat (dstMatClippingROI.rows (), dstMatClippingROI.cols (), CvType.CV_8UC1);
            lineMat = new Mat (dstMatClippingROI.rows (), dstMatClippingROI.cols (), CvType.CV_8UC1);
            maskMat = new Mat (dstMatClippingROI.rows (), dstMatClippingROI.cols (), CvType.CV_8UC1);

            //create a striped background.
            bgMat = new Mat (dstMatClippingROI.rows (), dstMatClippingROI.cols (), CvType.CV_8UC1, new Scalar (255));
            for (int i = 0; i < bgMat.rows ()*2.5f; i=i+4) {
                Imgproc.line (bgMat, new Point (0, 0 + i), new Point (bgMat.cols (), -bgMat.cols () + i), new Scalar (0), 1);
            }

            grayPixels = new byte[grayMat.cols () * grayMat.rows () * grayMat.channels ()];
            maskPixels = new byte[maskMat.cols () * maskMat.rows () * maskMat.channels ()];
             

            quad_renderer = gameObject.GetComponent<Renderer> () as Renderer;
            quad_renderer.sharedMaterial.SetTexture ("_MainTex", texture);
            quad_renderer.sharedMaterial.SetVector ("_VignetteOffset", new Vector4(clippingOffset.x, clippingOffset.y));

            //This value is obtained from PhotoCapture's TryGetProjectionMatrix() method.I do not know whether this method is good.
            //Please see the discussion of this thread.Https://forums.hololens.com/discussion/782/live-stream-of-locatable-camera-webcam-in-unity
            Matrix4x4 projectionMatrix = Matrix4x4.identity;
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
            quad_renderer.sharedMaterial.SetFloat ("_VignetteScale", vignetteScale);


            float halfOfVerticalFov = Mathf.Atan (1.0f / projectionMatrix.m11);
            float aspectRatio = (1.0f / Mathf.Tan (halfOfVerticalFov)) / projectionMatrix.m00;
            Debug.Log ("halfOfVerticalFov " + halfOfVerticalFov);
            Debug.Log ("aspectRatio " + aspectRatio);

            //
            //Imgproc.rectangle (dstMat, new Point (0, 0), new Point (webCamTextureMat.width (), webCamTextureMat.height ()), new Scalar (126, 126, 126, 255), -1);
            //
        }

        /// <summary>
        /// Raises the web cam texture to mat helper disposed event.
        /// </summary>
        public void OnWebCamTextureToMatHelperDisposed ()
        {
            Debug.Log ("OnWebCamTextureToMatHelperDisposed");

            grayMat.Dispose ();
            lineMat.Dispose ();
            maskMat.Dispose ();
        
            bgMat.Dispose ();
            dstMat.Dispose ();
            dstMatClippingROI.Dispose ();

            grayPixels = null;
            maskPixels = null;
        }

        /// <summary>
        /// Raises the web cam texture to mat helper error occurred event.
        /// </summary>
        /// <param name="errorCode">Error code.</param>
        public void OnWebCamTextureToMatHelperErrorOccurred(WebCamTextureToMatHelper.ErrorCode errorCode){
            Debug.Log ("OnWebCamTextureToMatHelperErrorOccurred " + errorCode);
        }

        // Update is called once per frame
        void Update ()
        {
            if (webCamTextureToMatHelper.IsPlaying () && webCamTextureToMatHelper.DidUpdateThisFrame ()) {
            
                Mat rgbaMat = webCamTextureToMatHelper.GetDownScaleMat(webCamTextureToMatHelper.GetMat ());


                Mat rgbaMatClipROI = new Mat(rgbaMat, processingAreaRect);

                Imgproc.cvtColor (rgbaMatClipROI, grayMat, Imgproc.COLOR_RGBA2GRAY);

                bgMat.copyTo (dstMatClippingROI);

                Imgproc.GaussianBlur (grayMat, lineMat, new Size (3, 3), 0);


                grayMat.get (0, 0, grayPixels);

                for (int i = 0; i < grayPixels.Length; i++) {

                    maskPixels [i] = 0;

                    if (grayPixels [i] < 70) {
                        grayPixels [i] = 0;
                        maskPixels [i] = 1;
                    } else if (70 <= grayPixels [i] && grayPixels [i] < 120) {
                        grayPixels [i] = 100;

                    } else {
                        grayPixels [i] = 255;
                        maskPixels [i] = 1;
                    }
                }

                grayMat.put (0, 0, grayPixels);
                maskMat.put (0, 0, maskPixels);
                grayMat.copyTo (dstMatClippingROI, maskMat);


                Imgproc.Canny (lineMat, lineMat, 20, 120);

                lineMat.copyTo (maskMat);

                Core.bitwise_not (lineMat, lineMat);

                lineMat.copyTo (dstMatClippingROI, maskMat);



                //          Imgproc.putText (dstMat, "W:" + dstMat.width () + " H:" + dstMat.height () + " SO:" + Screen.orientation, new Point (5, dstMat.rows () - 10), Core.FONT_HERSHEY_SIMPLEX, 1.0, new Scalar (0), 2, Imgproc.LINE_AA, false);

                Imgproc.cvtColor(dstMat, rgbaMat, Imgproc.COLOR_GRAY2RGBA);

                //
                //Imgproc.rectangle (rgbaMat, new Point (0, 0), new Point (rgbaMat.width (), rgbaMat.height ()), new Scalar (255, 0, 0, 255), 2);
                //Imgproc.rectangle (rgbaMat, processingAreaRect.tl(), processingAreaRect.br(), new Scalar (255, 0, 0, 255), 2);
                //

                Utils.fastMatToTexture2D(rgbaMat, texture);

                rgbaMatClipROI.Dispose ();
            }

            if (webCamTextureToMatHelper.IsPlaying ()) {

                Matrix4x4 cameraToWorldMatrix = Camera.main.cameraToWorldMatrix;;
                Matrix4x4 worldToCameraMatrix = cameraToWorldMatrix.inverse;

                texture.wrapMode = TextureWrapMode.Clamp;

                quad_renderer.sharedMaterial.SetMatrix ("_WorldToCameraMatrix", worldToCameraMatrix);

                // Position the canvas object slightly in front
                // of the real world web camera.
                Vector3 position = cameraToWorldMatrix.GetColumn (3) - cameraToWorldMatrix.GetColumn (2);

                // Rotate the canvas object so that it faces the user.
                Quaternion rotation = Quaternion.LookRotation (-cameraToWorldMatrix.GetColumn (2), cameraToWorldMatrix.GetColumn (1));

                gameObject.transform.position = position;
                gameObject.transform.rotation = rotation;
            }
        }

        /// <summary>
        /// Raises the destroy event.
        /// </summary>
        void OnDestroy ()
        {
            webCamTextureToMatHelper.Dispose ();
        }

        /// <summary>
        /// Raises the back button click event.
        /// </summary>
        public void OnBackButtonClick ()
        {
            #if UNITY_5_3 || UNITY_5_3_OR_NEWER
            SceneManager.LoadScene ("HoloLensWithOpenCVForUnityExample");
            #else
            Application.LoadLevel ("HoloLensWithOpenCVForUnityExample");
            #endif
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
            webCamTextureToMatHelper.Initialize (null, webCamTextureToMatHelper.requestedWidth, webCamTextureToMatHelper.requestedHeight, !webCamTextureToMatHelper.requestedIsFrontFacing);
        }
    }
}
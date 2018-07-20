using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using HoloToolkit.Unity.InputModule;

#if UNITY_2017_2_OR_NEWER
using UnityEngine.XR.WSA.WebCam;
using UnityEngine.XR.WSA.Input;
using WSAWebCamCameraParameters = UnityEngine.XR.WSA.WebCam.CameraParameters;
#else
using UnityEngine.VR.WSA.WebCam;
using UnityEngine.VR.WSA.Input;
using WSAWebCamCameraParameters = UnityEngine.VR.WSA.WebCam.CameraParameters;
#endif

using OpenCVForUnity;

namespace HoloLensWithOpenCVForUnityExample
{
    /// <summary>
    /// HoloLens PhotoCapture Example
    /// An example of holographic photo blending using the PhotocCapture class on Hololens. (Air Tap to take a picture)
    /// Referring to https://forum.unity3d.com/threads/holographic-photo-blending-with-photocapture.416023/.
    /// </summary>
    public class HoloLensPhotoCaptureExample : ExampleSceneBase
    {
        GestureRecognizer m_GestureRecognizer;
        GameObject m_Canvas = null;
        Renderer m_CanvasRenderer = null;
        PhotoCapture m_PhotoCaptureObj;
        WSAWebCamCameraParameters m_CameraParameters;
        bool m_CapturingPhoto = false;
        Texture2D m_Texture = null;

        /// <summary>
        /// The rgba mat.
        /// </summary>
        Mat rgbaMat;

        /// <summary>
        /// The gray mat.
        /// </summary>
        Mat grayMat;

        /// <summary>
        /// The cascade.
        /// </summary>
        CascadeClassifier cascade;

        /// <summary>
        /// The faces.
        /// </summary>
        MatOfRect faces;

        /// <summary>
        /// The colors.
        /// </summary>
        Color32[] colors;

        protected override void Start ()
        {
            base.Start ();

            m_Canvas = GameObject.Find ("PhotoCaptureCanvas");
            m_CanvasRenderer = m_Canvas.GetComponent<Renderer> () as Renderer;
            m_CanvasRenderer.enabled = false;

            Initialize ();
        }

        void SetupGestureRecognizer ()
        {
            m_GestureRecognizer = new GestureRecognizer ();
            m_GestureRecognizer.SetRecognizableGestures (GestureSettings.Tap);
            #if UNITY_2017_2_OR_NEWER
            m_GestureRecognizer.Tapped += OnTappedEvent;
            #else
            m_GestureRecognizer.TappedEvent += OnTappedEvent;
            #endif
            m_GestureRecognizer.StartCapturingGestures ();

            m_CapturingPhoto = false;
        }

        void Initialize ()
        {
            Debug.Log ("Initializing...");
            List<Resolution> resolutions = new List<Resolution> (PhotoCapture.SupportedResolutions);
            Resolution selectedResolution = resolutions [1];

            foreach (var item in resolutions) {
                Debug.Log ("resolution width " + item.width + " height " + item.height);
            }

            m_CameraParameters = new WSAWebCamCameraParameters (WebCamMode.PhotoMode);
            m_CameraParameters.cameraResolutionWidth = selectedResolution.width;
            m_CameraParameters.cameraResolutionHeight = selectedResolution.height;
            m_CameraParameters.hologramOpacity = 0.0f;
            m_CameraParameters.pixelFormat = CapturePixelFormat.BGRA32;

            m_Texture = new Texture2D (selectedResolution.width, selectedResolution.height, TextureFormat.BGRA32, false);


            rgbaMat = new Mat (m_Texture.height, m_Texture.width, CvType.CV_8UC4);
            colors = new Color32[rgbaMat.cols () * rgbaMat.rows ()];
            grayMat = new Mat (rgbaMat.rows (), rgbaMat.cols (), CvType.CV_8UC1);

            faces = new MatOfRect ();

            cascade = new CascadeClassifier ();
            cascade.load (Utils.getFilePath ("haarcascade_frontalface_alt.xml"));


            PhotoCapture.CreateAsync (false, OnCreatedPhotoCaptureObject);
        }

        void OnCreatedPhotoCaptureObject (PhotoCapture captureObject)
        {
            m_PhotoCaptureObj = captureObject;
            m_PhotoCaptureObj.StartPhotoModeAsync (m_CameraParameters, OnStartPhotoMode);
        }

        void OnStartPhotoMode (PhotoCapture.PhotoCaptureResult result)
        {
            SetupGestureRecognizer ();

            Debug.Log ("Ready!");
            Debug.Log ("Air Tap to take a picture.");
        }

        #if UNITY_2017_2_OR_NEWER
        void OnTappedEvent (TappedEventArgs args)
        #else
        void OnTappedEvent (InteractionSourceKind source, int tapCount, Ray headRay)
        #endif
        {
            // Determine if a Gaze pointer is over a GUI.
            if (GazeManager.Instance.HitObject != null && GazeManager.Instance.HitObject.transform.name == "Text") {
                return;
            }

            if (m_CapturingPhoto) {
                return;
            }

            m_CanvasRenderer.enabled = false;
            m_CapturingPhoto = true;
            Debug.Log ("Taking picture...");
            m_PhotoCaptureObj.TakePhotoAsync (OnPhotoCaptured);
        }

        void OnPhotoCaptured (PhotoCapture.PhotoCaptureResult result, PhotoCaptureFrame photoCaptureFrame)
        {

            Matrix4x4 cameraToWorldMatrix;
            photoCaptureFrame.TryGetCameraToWorldMatrix (out cameraToWorldMatrix);
            Matrix4x4 worldToCameraMatrix = cameraToWorldMatrix.inverse;

            Matrix4x4 projectionMatrix;
            photoCaptureFrame.TryGetProjectionMatrix (out projectionMatrix);

            photoCaptureFrame.UploadImageDataToTexture (m_Texture);


            Utils.texture2DToMat (m_Texture, rgbaMat);

            Imgproc.cvtColor (rgbaMat, grayMat, Imgproc.COLOR_RGBA2GRAY);
            Imgproc.equalizeHist (grayMat, grayMat);

            // fill all black.
            //Imgproc.rectangle (rgbaMat, new Point (0, 0), new Point (rgbaMat.width (), rgbaMat.height ()), new Scalar (0, 0, 0, 0), -1);
            // draw an edge lines.
            Imgproc.rectangle (rgbaMat, new Point (0, 0), new Point (rgbaMat.width (), rgbaMat.height ()), new Scalar (255, 0, 0, 255), 2);
            // draw a diagonal line.
            //Imgproc.line (rgbaMat, new Point (0, 0), new Point (rgbaMat.cols (), rgbaMat.rows ()), new Scalar (255, 0, 0, 255));

            if (cascade != null)
                cascade.detectMultiScale (grayMat, faces, 1.1, 2, 2, // TODO: objdetect.CV_HAAR_SCALE_IMAGE
                    new Size (grayMat.cols () * 0.05, grayMat.rows () * 0.05), new Size ());

            OpenCVForUnity.Rect[] rects = faces.toArray ();
            for (int i = 0; i < rects.Length; i++) {
                //Debug.Log ("detect faces " + rects [i]);
                Imgproc.rectangle (rgbaMat, new Point (rects [i].x, rects [i].y), new Point (rects [i].x + rects [i].width, rects [i].y + rects [i].height), new Scalar (255, 0, 0, 255), 2);
            }

            Imgproc.putText (rgbaMat, "W:" + rgbaMat.width () + " H:" + rgbaMat.height () + " SO:" + Screen.orientation, new Point (5, rgbaMat.rows () - 10), Core.FONT_HERSHEY_SIMPLEX, 1.5, new Scalar (0, 255, 0, 255), 2, Imgproc.LINE_AA, false);

            Utils.matToTexture2D (rgbaMat, m_Texture, colors);


            m_Texture.wrapMode = TextureWrapMode.Clamp;

            m_CanvasRenderer.enabled = true;
            m_CanvasRenderer.sharedMaterial.SetTexture ("_MainTex", m_Texture);
            m_CanvasRenderer.sharedMaterial.SetMatrix ("_WorldToCameraMatrix", worldToCameraMatrix);
            m_CanvasRenderer.sharedMaterial.SetMatrix ("_CameraProjectionMatrix", projectionMatrix);
            m_CanvasRenderer.sharedMaterial.SetVector ("_VignetteOffset", new Vector4(0, 0));
            m_CanvasRenderer.sharedMaterial.SetFloat ("_VignetteScale", 0.0f);

            // Position the canvas object slightly in front
            // of the real world web camera.
            Vector3 position = cameraToWorldMatrix.GetColumn (3) - cameraToWorldMatrix.GetColumn (2);
            position *= 1.2f;

            // Rotate the canvas object so that it faces the user.
            Quaternion rotation = Quaternion.LookRotation (-cameraToWorldMatrix.GetColumn (2), cameraToWorldMatrix.GetColumn (1));

            m_Canvas.transform.position = position;
            m_Canvas.transform.rotation = rotation;

            Debug.Log ("Took picture!");
            Debug.Log ("projectionMatrix:\n" + projectionMatrix.ToString());
            m_CapturingPhoto = false;
        }

        void OnStopPhotoMode (PhotoCapture.PhotoCaptureResult result)
        {
            Debug.Log ("StopPhotoMode!");
            m_PhotoCaptureObj.Dispose ();
        }

        /// <summary>
        /// Raises the destroy event.
        /// </summary>
        void OnDestroy ()
        {
            if (m_PhotoCaptureObj != null)
                m_PhotoCaptureObj.StopPhotoModeAsync (OnStopPhotoMode);

            if (m_GestureRecognizer != null && m_GestureRecognizer.IsCapturingGestures()) {
                m_GestureRecognizer.StopCapturingGestures ();
                #if UNITY_2017_2_OR_NEWER
                m_GestureRecognizer.Tapped -= OnTappedEvent;
                #else
                m_GestureRecognizer.TappedEvent -= OnTappedEvent;
                #endif
                m_GestureRecognizer.Dispose ();
            }

            if (rgbaMat != null)
                rgbaMat.Dispose ();

            if (grayMat != null)
                grayMat.Dispose ();

            if (cascade != null)
                cascade.Dispose ();
        }

        /// <summary>
        /// Raises the back button click event.
        /// </summary>
        public void OnBackButtonClick ()
        {
            LoadScene ("HoloLensWithOpenCVForUnityExample");
        }
    }
}
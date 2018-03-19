using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using OpenCVForUnity.RectangleTrack;
using System.Threading;

#if UNITY_5_3 || UNITY_5_3_OR_NEWER
using UnityEngine.SceneManagement;
#endif
using OpenCVForUnity;
using Rect = OpenCVForUnity.Rect;

namespace HoloLensWithOpenCVForUnityExample
{
    /// <summary>
    /// HoloLens Face Detection Overlay Example
    /// An example of overlay display of face area rectangles using OpenCVForUnity on Hololens.
    /// Referring to https://github.com/Itseez/opencv/blob/master/modules/objdetect/src/detection_based_tracker.cpp.
    /// </summary>
    [RequireComponent(typeof(HololensCameraStreamToMatHelper))]
    public class HoloLensFaceDetectionOverlayExample : MonoBehaviour
    {
        /// <summary>
        /// Determines if enables the detection.
        /// </summary>
        public bool enableDetection = true;

        /// <summary>
        /// Determines if uses separate detection.
        /// </summary>
        public bool useSeparateDetection = false;

        /// <summary>
        /// The use separate detection toggle.
        /// </summary>
        public Toggle useSeparateDetectionToggle;

        /// <summary>
        /// The min detection size ratio.
        /// </summary>
        public float minDetectionSizeRatio = 0.07f;

        /// <summary>
        /// The overlay Distance.
        /// </summary>
        public float overlayDistance = 1;

        /// <summary>
        /// The webcam texture to mat helper.
        /// </summary>
        HololensCameraStreamToMatHelper webCamTextureToMatHelper;

        /// <summary>
        /// The gray mat.
        /// </summary>
        Mat grayMat;

        /// <summary>
        /// The cascade.
        /// </summary>
        CascadeClassifier cascade;

        /// <summary>
        /// The detection result.
        /// </summary>
        MatOfRect detectionResult;

        Mat grayMat4Thread;
        CascadeClassifier cascade4Thread;
        readonly static Queue<Action> ExecuteOnMainThread = new Queue<Action>();
        System.Object sync = new System.Object ();

        bool _isThreadRunning = false;
        bool isThreadRunning {
            get { lock (sync)
                return _isThreadRunning; }
            set { lock (sync)
                _isThreadRunning = value; }
        }

        RectangleTracker rectangleTracker;
        float coeffTrackingWindowSize = 2.0f;
        float coeffObjectSizeToTrack = 0.85f;
        Rect[] rectsWhereRegions;
        List<Rect> detectedObjectsInRegions = new List<Rect> ();
        List<Rect> resultObjects = new List<Rect> ();

        bool _isDetecting = false;
        bool isDetecting {
            get { lock (sync)
                return _isDetecting; }
            set { lock (sync)
                _isDetecting = value; }
        }

        bool _hasUpdatedDetectionResult = false;
        bool hasUpdatedDetectionResult {
            get { lock (sync)
                return _hasUpdatedDetectionResult; }
            set { lock (sync)
                _hasUpdatedDetectionResult = value; }
        }

        Matrix4x4 projectionMatrix;
        RectOverlay rectOverlay;

        // Use this for initialization
        void Start ()
        {
            useSeparateDetectionToggle.isOn = useSeparateDetection;

            webCamTextureToMatHelper = gameObject.GetComponent<HololensCameraStreamToMatHelper> ();
            #if NETFX_CORE
            webCamTextureToMatHelper.frameMatAcquired += OnFrameMatAcquired;
            #endif
            webCamTextureToMatHelper.Initialize ();

            rectangleTracker = new RectangleTracker ();
            rectOverlay = gameObject.GetComponent<RectOverlay> ();
        }

        /// <summary>
        /// Raises the web cam texture to mat helper initialized event.
        /// </summary>
        public void OnWebCamTextureToMatHelperInitialized ()
        {
            Debug.Log ("OnWebCamTextureToMatHelperInitialized");

            Mat webCamTextureMat = webCamTextureToMatHelper.GetMat ();

            Debug.Log ("Screen.width " + Screen.width + " Screen.height " + Screen.height + " Screen.orientation " + Screen.orientation);


            #if NETFX_CORE
            projectionMatrix = webCamTextureToMatHelper.GetProjectionMatrix ();
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
            #endif

            grayMat = new Mat (webCamTextureMat.rows (), webCamTextureMat.cols (), CvType.CV_8UC1);
            cascade = new CascadeClassifier ();
            cascade.load (Utils.getFilePath ("lbpcascade_frontalface.xml"));

            // "empty" method is not working on the UWP platform.
            //            if (cascade.empty ()) {
            //                Debug.LogError ("cascade file is not loaded.Please copy from “OpenCVForUnity/StreamingAssets/” to “Assets/StreamingAssets/” folder. ");
            //            }

            grayMat4Thread = new Mat ();
            cascade4Thread = new CascadeClassifier ();
            cascade4Thread.load (Utils.getFilePath ("haarcascade_frontalface_alt.xml"));

            // "empty" method is not working on the UWP platform.
            //            if (cascade4Thread.empty ()) {
            //                Debug.LogError ("cascade file is not loaded.Please copy from “OpenCVForUnity/StreamingAssets/” to “Assets/StreamingAssets/” folder. ");
            //            }

            detectionResult = new MatOfRect ();
        }

        /// <summary>
        /// Raises the web cam texture to mat helper disposed event.
        /// </summary>
        public void OnWebCamTextureToMatHelperDisposed ()
        {
            Debug.Log ("OnWebCamTextureToMatHelperDisposed");

            StopThread ();
            lock (sync) {
                ExecuteOnMainThread.Clear ();
            }
            hasUpdatedDetectionResult = false;
            isDetecting = false;

            if (grayMat != null)
                grayMat.Dispose ();

            if (cascade != null)
                cascade.Dispose ();

            if (grayMat4Thread != null)
                grayMat4Thread.Dispose ();

            if (cascade4Thread != null)
                cascade4Thread.Dispose ();

            rectangleTracker.Reset ();
        }

        /// <summary>
        /// Raises the web cam texture to mat helper error occurred event.
        /// </summary>
        /// <param name="errorCode">Error code.</param>
        public void OnWebCamTextureToMatHelperErrorOccurred(WebCamTextureToMatHelper.ErrorCode errorCode){
            Debug.Log ("OnWebCamTextureToMatHelperErrorOccurred " + errorCode);
        }


        #if NETFX_CORE
        public void OnFrameMatAcquired (Mat bgraMat, Matrix4x4 projectionMatrix, Matrix4x4 cameraToWorldMatrix)
        {            
            Imgproc.cvtColor (bgraMat, grayMat, Imgproc.COLOR_BGRA2GRAY);
            Imgproc.equalizeHist (grayMat, grayMat);

            if (enableDetection && !isDetecting ) {

                isDetecting = true;

                grayMat.copyTo (grayMat4Thread);

                System.Threading.Tasks.Task.Run(() => {

                    isThreadRunning = true;
                    DetectObject ();
                    isThreadRunning = false;
                    OnDetectionDone ();
                });
            }


            Rect[] rects;
            if (!useSeparateDetection) {
                if (hasUpdatedDetectionResult) {
                    hasUpdatedDetectionResult = false;

                    lock (rectangleTracker) {
                        rectangleTracker.UpdateTrackedObjects (detectionResult.toList ());
                    }
                }

                lock (rectangleTracker) {
                    rectangleTracker.GetObjects (resultObjects, true);
                }
                rects = resultObjects.ToArray ();

            }else {

                if (hasUpdatedDetectionResult) {
                    hasUpdatedDetectionResult = false;

                    //UnityEngine.WSA.Application.InvokeOnAppThread (() => {
                    //    Debug.Log("process: get rectsWhereRegions were got from detectionResult");
                    //}, true);

                    lock (rectangleTracker) {
                        rectsWhereRegions = detectionResult.toArray ();
                    }

                    rects = rectsWhereRegions;

                } else {
                    //UnityEngine.WSA.Application.InvokeOnAppThread (() => {
                    //    Debug.Log("process: get rectsWhereRegions from previous positions");
                    //}, true);

                    lock (rectangleTracker) {
                        rectsWhereRegions = rectangleTracker.CreateCorrectionBySpeedOfRects ();
                    }

                    rects = rectsWhereRegions;
                }

                detectedObjectsInRegions.Clear ();
                if (rectsWhereRegions.Length > 0) {
                    int len = rectsWhereRegions.Length;
                    for (int i = 0; i < len; i++) {
                        DetectInRegion (grayMat, rectsWhereRegions [i], detectedObjectsInRegions);
                    }
                }

                lock (rectangleTracker) {
                    rectangleTracker.UpdateTrackedObjects (detectedObjectsInRegions);
                    rectangleTracker.GetObjects (resultObjects, true);
                }

                rects = resultObjects.ToArray ();
            }


            UnityEngine.WSA.Application.InvokeOnAppThread(() => {

                if (!webCamTextureToMatHelper.IsPlaying ()) return;

                DrawRects (rects, bgraMat.width(), bgraMat.height());
                bgraMat.Dispose ();

                Vector3 ccCameraSpacePos = UnProjectVector(projectionMatrix, new Vector3(0.0f, 0.0f, overlayDistance));
                Vector3 tlCameraSpacePos = UnProjectVector(projectionMatrix, new Vector3(-overlayDistance, overlayDistance, overlayDistance));

                //position
                Vector3 position = cameraToWorldMatrix.MultiplyPoint3x4(ccCameraSpacePos);
                gameObject.transform.position = position;

                //scale
                Vector3 scale = new Vector3(Mathf.Abs(tlCameraSpacePos.x - ccCameraSpacePos.x)*2, Mathf.Abs(tlCameraSpacePos.y - ccCameraSpacePos.y)*2, 1);
                gameObject.transform.localScale = scale;

                // Rotate the canvas object so that it faces the user.
                Quaternion rotation = Quaternion.LookRotation (-cameraToWorldMatrix.GetColumn (2), cameraToWorldMatrix.GetColumn (1));
                gameObject.transform.rotation = rotation;

                rectOverlay.UpdateOverlayTransform(gameObject.transform);

            }, false);
        }

        #else

        // Update is called once per frame
        void Update ()
        {
            lock (sync) {
                while (ExecuteOnMainThread.Count > 0) {
                    ExecuteOnMainThread.Dequeue ().Invoke ();
                }
            }

            if (webCamTextureToMatHelper.IsPlaying () && webCamTextureToMatHelper.DidUpdateThisFrame ()) { 

                Mat rgbaMat = webCamTextureToMatHelper.GetMat ();

                Imgproc.cvtColor (rgbaMat, grayMat, Imgproc.COLOR_RGBA2GRAY);
                Imgproc.equalizeHist (grayMat, grayMat);

                if (enableDetection && !isDetecting ) {
                    isDetecting = true;

                    grayMat.copyTo (grayMat4Thread);

                    StartThread (ThreadWorker);
                }

                Rect[] rects;
                if (!useSeparateDetection) {
                    if (hasUpdatedDetectionResult) 
                    {
                        hasUpdatedDetectionResult = false;

                        rectangleTracker.UpdateTrackedObjects (detectionResult.toList());
                    }

                    rectangleTracker.GetObjects (resultObjects, true);
                    rects = resultObjects.ToArray ();

                    DrawRects (rects, grayMat.width(), grayMat.height());

                } else {

                    if (hasUpdatedDetectionResult) {
                        hasUpdatedDetectionResult = false;

                        //Debug.Log("process: get rectsWhereRegions were got from detectionResult");
                        rectsWhereRegions = detectionResult.toArray ();

                    } else {
                        //Debug.Log("process: get rectsWhereRegions from previous positions");
                        rectsWhereRegions = rectangleTracker.CreateCorrectionBySpeedOfRects ();

                    }

                    detectedObjectsInRegions.Clear ();
                    if (rectsWhereRegions.Length > 0) {
                        int len = rectsWhereRegions.Length;
                        for (int i = 0; i < len; i++) {
                            DetectInRegion (grayMat, rectsWhereRegions [i], detectedObjectsInRegions);
                        }
                    }

                    rectangleTracker.UpdateTrackedObjects (detectedObjectsInRegions);
                    rectangleTracker.GetObjects (resultObjects, true);

                    rects = resultObjects.ToArray ();
                    DrawRects (rects, grayMat.width(), grayMat.height());
                }
            }

            if (webCamTextureToMatHelper.IsPlaying ()) {

                Matrix4x4 cameraToWorldMatrix = Camera.main.cameraToWorldMatrix;;

                Vector3 ccCameraSpacePos = UnProjectVector(projectionMatrix, new Vector3(0.0f, 0.0f, overlayDistance));
                Vector3 tlCameraSpacePos = UnProjectVector(projectionMatrix, new Vector3(-overlayDistance, overlayDistance, overlayDistance));
 
                //position
                Vector3 position = cameraToWorldMatrix.MultiplyPoint3x4(ccCameraSpacePos);
                gameObject.transform.position = position;

                //scale
                Vector3 scale = new Vector3(Mathf.Abs(tlCameraSpacePos.x - ccCameraSpacePos.x)*2, Mathf.Abs(tlCameraSpacePos.y - ccCameraSpacePos.y)*2, 1);
                gameObject.transform.localScale = scale;

                // Rotate the canvas object so that it faces the user.
                Quaternion rotation = Quaternion.LookRotation (-cameraToWorldMatrix.GetColumn (2), cameraToWorldMatrix.GetColumn (1));
                gameObject.transform.rotation = rotation;

                rectOverlay.UpdateOverlayTransform(gameObject.transform);
            }
        }

        #endif

        private Vector3 UnProjectVector(Matrix4x4 proj, Vector3 to)
        {
            Vector3 from = new Vector3(0, 0, 0);
            var axsX = proj.GetRow(0);
            var axsY = proj.GetRow(1);
            var axsZ = proj.GetRow(2);
            from.z = to.z / axsZ.z;
            from.y = (to.y - (from.z * axsY.z)) / axsY.y;
            from.x = (to.x - (from.z * axsX.z)) / axsX.x;
            return from;
        }

        private void DrawRects(Rect[] rects, float imageWidth, float imageHeight)
        {
            UnityEngine.Rect[] overlayRects = new UnityEngine.Rect[rects.Length];

            for (int i = 0; i < rects.Length; i++) {
                overlayRects[i] = new UnityEngine.Rect(rects[i].x/imageWidth
                    , rects[i].y/imageHeight
                    , rects[i].width/imageWidth
                    , rects[i].height/imageHeight);
            }
            rectOverlay.DrawRects(overlayRects);
        }

        private void StartThread(Action action)
        {
            #if UNITY_METRO && NETFX_CORE
            System.Threading.Tasks.Task.Run(() => action());
            #elif UNITY_METRO
            action.BeginInvoke(ar => action.EndInvoke(ar), null);
            #else
            ThreadPool.QueueUserWorkItem (_ => action());
            #endif
        }

        private void StopThread ()
        {
            if (!isThreadRunning)
                return;

            while (isThreadRunning) {
                //Wait threading stop
            } 
        }

        private void ThreadWorker()
        {
            isThreadRunning = true;

            DetectObject ();

            lock (sync) {
                if (ExecuteOnMainThread.Count == 0) {
                    ExecuteOnMainThread.Enqueue (() => {
                        OnDetectionDone ();
                    });
                }
            }

            isThreadRunning = false;
        }

        private void DetectObject()
        {
            MatOfRect objects = new MatOfRect ();
            if (cascade4Thread != null)
                cascade4Thread.detectMultiScale (grayMat4Thread, objects, 1.1, 2, Objdetect.CASCADE_SCALE_IMAGE, // TODO: objdetect.CV_HAAR_SCALE_IMAGE
                    new Size (grayMat4Thread.cols () * minDetectionSizeRatio, grayMat4Thread.rows () * minDetectionSizeRatio), new Size ());

            detectionResult = objects;
        }

        private void OnDetectionDone()
        {
            hasUpdatedDetectionResult = true;

            isDetecting = false;
        }

        private void DetectInRegion (Mat img, Rect r, List<Rect> detectedObjectsInRegions)
        {
            Rect r0 = new Rect (new Point (), img.size ());
            Rect r1 = new Rect (r.x, r.y, r.width, r.height);
            Rect.inflate (r1, (int)((r1.width * coeffTrackingWindowSize) - r1.width) / 2,
                (int)((r1.height * coeffTrackingWindowSize) - r1.height) / 2);
            r1 = Rect.intersect (r0, r1);

            if ((r1.width <= 0) || (r1.height <= 0)) {
                Debug.Log ("DetectionBasedTracker::detectInRegion: Empty intersection");
                return;
            }

            int d = Math.Min (r.width, r.height);
            d = (int)Math.Round (d * coeffObjectSizeToTrack);

            MatOfRect tmpobjects = new MatOfRect ();

            Mat img1 = new Mat (img, r1);//subimage for rectangle -- without data copying

            cascade.detectMultiScale (img1, tmpobjects, 1.1, 2, 0 | Objdetect.CASCADE_DO_CANNY_PRUNING | Objdetect.CASCADE_SCALE_IMAGE | Objdetect.CASCADE_FIND_BIGGEST_OBJECT, new Size (d, d), new Size ());


            Rect[] tmpobjectsArray = tmpobjects.toArray ();
            int len = tmpobjectsArray.Length;
            for (int i = 0; i < len; i++) {
                Rect tmp = tmpobjectsArray [i];
                Rect curres = new Rect (new Point (tmp.x + r1.x, tmp.y + r1.y), tmp.size ());
                detectedObjectsInRegions.Add (curres);
            }
        }

        /// <summary>
        /// Raises the destroy event.
        /// </summary>
        void OnDestroy ()
        {
            #if NETFX_CORE
            webCamTextureToMatHelper.frameMatAcquired -= OnFrameMatAcquired;
            #endif
            webCamTextureToMatHelper.Dispose ();

            if (rectangleTracker != null)
                rectangleTracker.Dispose ();
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

        /// <summary>
        /// Raises the use separate detection toggle value changed event.
        /// </summary>
        public void OnUseSeparateDetectionToggleValueChanged ()
        {
            if (useSeparateDetectionToggle.isOn) {
                useSeparateDetection = true;
            } else {
                useSeparateDetection = false;
            }

            lock (rectangleTracker) {
                if (rectangleTracker != null)
                    rectangleTracker.Reset ();
            }
        }
    }
}
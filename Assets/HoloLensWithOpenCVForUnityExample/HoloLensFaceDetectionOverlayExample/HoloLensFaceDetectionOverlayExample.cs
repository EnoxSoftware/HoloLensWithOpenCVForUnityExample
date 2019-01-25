using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using OpenCVForUnity.RectangleTrack;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.UnityUtils.Helper;
using OpenCVForUnity.ImgprocModule;
using Rect = OpenCVForUnity.CoreModule.Rect;
using HoloLensWithOpenCVForUnity.UnityUtils.Helper;

namespace HoloLensWithOpenCVForUnityExample
{
    /// <summary>
    /// HoloLens Face Detection Overlay Example
    /// An example of overlay display of face area rectangles using OpenCVForUnity on Hololens.
    /// Referring to https://github.com/Itseez/opencv/blob/master/modules/objdetect/src/detection_based_tracker.cpp.
    /// </summary>
    [RequireComponent(typeof(HololensCameraStreamToMatHelper))]
    public class HoloLensFaceDetectionOverlayExample : ExampleSceneBase
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
        /// Determines if enable downscale.
        /// </summary>
        public bool enableDownScale;

        /// <summary>
        /// The enable downscale toggle.
        /// </summary>
        public Toggle enableDownScaleToggle;

        /// <summary>
        /// The min detection size ratio.
        /// </summary>
        public float minDetectionSizeRatio = 0.07f;

        /// <summary>
        /// The overlay Distance.
        /// </summary>
        public float overlayDistance = 1.2f;

        /// <summary>
        /// The webcam texture to mat helper.
        /// </summary>
        HololensCameraStreamToMatHelper webCamTextureToMatHelper;

        /// <summary>
        /// The image optimization helper.
        /// </summary>
        ImageOptimizationHelper imageOptimizationHelper;

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
        List<Rect> detectionResult = new List<Rect>();

        #if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
        int CVTCOLOR_CODE = Imgproc.COLOR_BGRA2GRAY;
        Scalar COLOR_RED = new Scalar(0, 0, 255, 255);
        Scalar COLOR_GREEN = new Scalar(0, 255, 0, 255);
        Scalar COLOR_BLUE = new Scalar(255, 0, 0, 255);
        #else
        int CVTCOLOR_CODE = Imgproc.COLOR_RGBA2GRAY;
        Scalar COLOR_RED = new Scalar(255, 0, 0, 255);
        Scalar COLOR_GREEN = new Scalar(0, 255, 0, 255);
        Scalar COLOR_BLUE = new Scalar(0, 0, 255, 255);
        #endif

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
        protected override void Start ()
        {
            base.Start ();

            imageOptimizationHelper = gameObject.GetComponent<ImageOptimizationHelper>();
            webCamTextureToMatHelper = gameObject.GetComponent<HololensCameraStreamToMatHelper> ();
            #if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            webCamTextureToMatHelper.frameMatAcquired += OnFrameMatAcquired;
            #endif
            webCamTextureToMatHelper.Initialize ();

            rectangleTracker = new RectangleTracker ();
            rectOverlay = gameObject.GetComponent<RectOverlay> ();

            useSeparateDetectionToggle.isOn = useSeparateDetection;
            enableDownScaleToggle.isOn = enableDownScale;
        }

        /// <summary>
        /// Raises the web cam texture to mat helper initialized event.
        /// </summary>
        public void OnWebCamTextureToMatHelperInitialized ()
        {
            Debug.Log ("OnWebCamTextureToMatHelperInitialized");

            Mat webCamTextureMat = webCamTextureToMatHelper.GetMat ();

            Debug.Log ("Screen.width " + Screen.width + " Screen.height " + Screen.height + " Screen.orientation " + Screen.orientation);


            #if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
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

            grayMat = new Mat ();
            cascade = new CascadeClassifier ();
            cascade.load (Utils.getFilePath ("lbpcascade_frontalface.xml"));
            #if !UNITY_WSA_10_0 || UNITY_EDITOR
            // "empty" method is not working on the UWP platform.
            if (cascade.empty())
            {
                Debug.LogError("cascade file is not loaded. Please copy from “OpenCVForUnity/StreamingAssets/” to “Assets/StreamingAssets/” folder. ");
            }
            #endif

            grayMat4Thread = new Mat ();
            cascade4Thread = new CascadeClassifier ();
            cascade4Thread.load (Utils.getFilePath ("haarcascade_frontalface_alt.xml"));
            #if !UNITY_WSA_10_0 || UNITY_EDITOR
            // "empty" method is not working on the UWP platform.
            if (cascade4Thread.empty())
            {
                Debug.LogError("cascade file is not loaded. Please copy from “OpenCVForUnity/StreamingAssets/” to “Assets/StreamingAssets/” folder. ");
            }
            #endif
        }

        /// <summary>
        /// Raises the web cam texture to mat helper disposed event.
        /// </summary>
        public void OnWebCamTextureToMatHelperDisposed ()
        {
            Debug.Log ("OnWebCamTextureToMatHelperDisposed");

            StopThread ();
            lock (ExecuteOnMainThread) {
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


        #if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
        public void OnFrameMatAcquired (Mat bgraMat, Matrix4x4 projectionMatrix, Matrix4x4 cameraToWorldMatrix)
        {
            Mat downScaleMat = null;
            float DOWNSCALE_RATIO;
            if (enableDownScale)
            {
                downScaleMat = imageOptimizationHelper.GetDownScaleMat(bgraMat);
                DOWNSCALE_RATIO = imageOptimizationHelper.downscaleRatio;
            }
            else
            {
                downScaleMat = bgraMat;
                DOWNSCALE_RATIO = 1.0f;
            }

            Imgproc.cvtColor (downScaleMat, grayMat, CVTCOLOR_CODE);
            Imgproc.equalizeHist (grayMat, grayMat);

            if (enableDetection && !isDetecting ) {

                isDetecting = true;

                grayMat.copyTo (grayMat4Thread);

                System.Threading.Tasks.Task.Run(() => {

                    isThreadRunning = true;

                    DetectObject(grayMat4Thread, out detectionResult, cascade4Thread);

                    isThreadRunning = false;
                    OnDetectionDone ();
                });
            }


            Rect[] rects;
            if (!useSeparateDetection) {
                if (hasUpdatedDetectionResult) {
                    hasUpdatedDetectionResult = false;

                    lock (rectangleTracker) {
                        rectangleTracker.UpdateTrackedObjects (detectionResult);
                    }
                }

                lock (rectangleTracker) {
                    rectangleTracker.GetObjects (resultObjects, true);
                }
                rects = resultObjects.ToArray ();

            }else {

                Rect[] rectsWhereRegions;

                if (hasUpdatedDetectionResult) {
                    hasUpdatedDetectionResult = false;

                    //UnityEngine.WSA.Application.InvokeOnAppThread (() => {
                    //    Debug.Log("process: get rectsWhereRegions were got from detectionResult");
                    //}, true);

                    lock (rectangleTracker) {
                        rectsWhereRegions = detectionResult.ToArray ();
                    }
                } else {
                    //UnityEngine.WSA.Application.InvokeOnAppThread (() => {
                    //    Debug.Log("process: get rectsWhereRegions from previous positions");
                    //}, true);

                    lock (rectangleTracker) {
                        rectsWhereRegions = rectangleTracker.CreateCorrectionBySpeedOfRects ();
                    }
                }

                detectedObjectsInRegions.Clear ();
                int len = rectsWhereRegions.Length;
                for (int i = 0; i < len; i++)
                {
                    DetectInRegion(grayMat, rectsWhereRegions[i], detectedObjectsInRegions, cascade);
                }

                lock (rectangleTracker) {
                    rectangleTracker.UpdateTrackedObjects (detectedObjectsInRegions);
                    rectangleTracker.GetObjects (resultObjects, true);
                }

                rects = resultObjects.ToArray ();
            }

            if (enableDownScale)
            {
                int len = rects.Length;
                for (int i = 0; i < len; i++)
                {
                    Rect rect = rects[i];

                    // restore to original size rect
                    rect.x = (int)(rect.x * DOWNSCALE_RATIO);
                    rect.y = (int)(rect.y * DOWNSCALE_RATIO);
                    rect.width = (int)(rect.width * DOWNSCALE_RATIO);
                    rect.height = (int)(rect.height * DOWNSCALE_RATIO);
                }
            }

            Enqueue(() => {

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
            lock (ExecuteOnMainThread) {
                while (ExecuteOnMainThread.Count > 0) {
                    ExecuteOnMainThread.Dequeue ().Invoke ();
                }
            }

            if (webCamTextureToMatHelper.IsPlaying () && webCamTextureToMatHelper.DidUpdateThisFrame ()) { 

                Mat rgbaMat = webCamTextureToMatHelper.GetMat ();

                Mat downScaleMat = null;
                float DOWNSCALE_RATIO;
                if (enableDownScale)
                {
                    downScaleMat = imageOptimizationHelper.GetDownScaleMat(rgbaMat);
                    DOWNSCALE_RATIO = imageOptimizationHelper.downscaleRatio;
                }
                else
                {
                    downScaleMat = rgbaMat;
                    DOWNSCALE_RATIO = 1.0f;
                }

                Imgproc.cvtColor (downScaleMat, grayMat, CVTCOLOR_CODE);
                Imgproc.equalizeHist (grayMat, grayMat);

                if (enableDetection && !isDetecting ) {
                    isDetecting = true;

                    grayMat.copyTo (grayMat4Thread);

                    StartThread (ThreadWorker);
                }

                if (!useSeparateDetection) {
                    if (hasUpdatedDetectionResult) 
                    {
                        hasUpdatedDetectionResult = false;

                        rectangleTracker.UpdateTrackedObjects (detectionResult);
                    }

                    rectangleTracker.GetObjects (resultObjects, true);

                    Rect[] rects = resultObjects.ToArray ();

                    if (enableDownScale)
                    {
                        int len = rects.Length;
                        for (int i = 0; i < len; i++)
                        {
                            Rect rect = rects[i];

                            // restore to original size rect
                            rect.x = (int)(rect.x * DOWNSCALE_RATIO);
                            rect.y = (int)(rect.y * DOWNSCALE_RATIO);
                            rect.width = (int)(rect.width * DOWNSCALE_RATIO);
                            rect.height = (int)(rect.height * DOWNSCALE_RATIO);
                        }
                    }

                    // draw face rect
                    DrawRects(rects, rgbaMat.width(), rgbaMat.height());

                } else {

                    Rect[] rectsWhereRegions;

                    if (hasUpdatedDetectionResult) {
                        hasUpdatedDetectionResult = false;

                        //Debug.Log("process: get rectsWhereRegions were got from detectionResult");
                        rectsWhereRegions = detectionResult.ToArray ();
                    } else {
                        //Debug.Log("process: get rectsWhereRegions from previous positions");
                        rectsWhereRegions = rectangleTracker.CreateCorrectionBySpeedOfRects ();
                    }

                    detectedObjectsInRegions.Clear ();
                    int len = rectsWhereRegions.Length;
                    for (int i = 0; i < len; i++)
                    {
                        DetectInRegion (grayMat, rectsWhereRegions[i], detectedObjectsInRegions, cascade);
                    }

                    rectangleTracker.UpdateTrackedObjects (detectedObjectsInRegions);
                    rectangleTracker.GetObjects (resultObjects, true);

                    Rect[] rects = resultObjects.ToArray ();

                    if (enableDownScale)
                    {
                        len = rects.Length;
                        for (int i = 0; i < len; i++)
                        {
                            Rect rect = rects[i];

                            // restore to original size rect
                            rect.x = (int)(rect.x * DOWNSCALE_RATIO);
                            rect.y = (int)(rect.y * DOWNSCALE_RATIO);
                            rect.width = (int)(rect.width * DOWNSCALE_RATIO);
                            rect.height = (int)(rect.height * DOWNSCALE_RATIO);
                        }
                    }

                    // draw face rect
                    DrawRects(rects, rgbaMat.width(), rgbaMat.height());
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
            #if WINDOWS_UWP || (!UNITY_WSA_10_0 && (NET_4_6 || NET_STANDARD_2_0))
            System.Threading.Tasks.Task.Run(() => action());
            #else
            ThreadPool.QueueUserWorkItem(_ => action());
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

            DetectObject(grayMat4Thread, out detectionResult, cascade4Thread);

            lock (ExecuteOnMainThread) {
                if (ExecuteOnMainThread.Count == 0) {
                    ExecuteOnMainThread.Enqueue (() => {
                        OnDetectionDone ();
                    });
                }
            }

            isThreadRunning = false;
        }

        private void DetectObject(Mat img, out List<Rect> detectedObjects, CascadeClassifier cascade)
        {
            int d = Mathf.Min(img.width(), img.height());
            d = (int)Mathf.Round(d * minDetectionSizeRatio);

            MatOfRect objects = new MatOfRect();
            if (cascade != null)
                cascade.detectMultiScale(img, objects, 1.1, 2, Objdetect.CASCADE_SCALE_IMAGE, new Size(d, d), new Size());

            detectedObjects = objects.toList();
        }

        private void OnDetectionDone()
        {
            hasUpdatedDetectionResult = true;

            isDetecting = false;
        }

        private void DetectInRegion(Mat img, Rect region, List<Rect> detectedObjectsInRegions, CascadeClassifier cascade)
        {
            Rect r0 = new Rect(new Point(), img.size());
            Rect r1 = new Rect(region.x, region.y, region.width, region.height);
            Rect.inflate(r1, (int)((r1.width * coeffTrackingWindowSize) - r1.width) / 2,
                (int)((r1.height * coeffTrackingWindowSize) - r1.height) / 2);
            r1 = Rect.intersect(r0, r1);

            if ((r1.width <= 0) || (r1.height <= 0))
            {
                Debug.Log("detectInRegion: Empty intersection");
                return;
            }

            int d = Math.Min(region.width, region.height);
            d = (int)Math.Round(d * coeffObjectSizeToTrack);

            using (MatOfRect tmpobjects = new MatOfRect())
            using (Mat img1 = new Mat(img, r1)) //subimage for rectangle -- without data copying
            {
                cascade.detectMultiScale(img1, tmpobjects, 1.1, 2, 0 | Objdetect.CASCADE_DO_CANNY_PRUNING | Objdetect.CASCADE_SCALE_IMAGE | Objdetect.CASCADE_FIND_BIGGEST_OBJECT, new Size(d, d), new Size());

                Rect[] tmpobjectsArray = tmpobjects.toArray();
                int len = tmpobjectsArray.Length;
                for (int i = 0; i < len; i++)
                {
                    Rect tmp = tmpobjectsArray[i];
                    Rect r = new Rect(new Point(tmp.x + r1.x, tmp.y + r1.y), tmp.size());

                    detectedObjectsInRegions.Add(r);
                }
            }
        }

        /// <summary>
        /// Raises the destroy event.
        /// </summary>
        void OnDestroy ()
        {
            imageOptimizationHelper.Dispose();
            #if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
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
            LoadScene ("HoloLensWithOpenCVForUnityExample");
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
        /// Raises the use separate detection toggle value changed event.
        /// </summary>
        public void OnUseSeparateDetectionToggleValueChanged ()
        {
            useSeparateDetection = useSeparateDetectionToggle.isOn;

            lock (rectangleTracker) {
                if (rectangleTracker != null)
                    rectangleTracker.Reset ();
            }
        }

        /// <summary>
        /// Raises the enable downscale toggle value changed event.
        /// </summary>
        public void OnEnableDownScaleToggleValueChanged()
        {
            enableDownScale = enableDownScaleToggle.isOn;

            lock (rectangleTracker)
            {
                if (rectangleTracker != null)
                    rectangleTracker.Reset();
            }
        }
    }
}
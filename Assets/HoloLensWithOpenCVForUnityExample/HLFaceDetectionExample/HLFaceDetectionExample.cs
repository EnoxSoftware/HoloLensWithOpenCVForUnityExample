using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using OpenCVForUnity.RectangleTrack;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.UnityUtils.Helper;
using OpenCVForUnity.ImgprocModule;
using Rect = OpenCVForUnity.CoreModule.Rect;
using HoloLensWithOpenCVForUnity.UnityUtils.Helper;
using HoloLensCameraStream;

namespace HoloLensWithOpenCVForUnityExample
{
    /// <summary>
    /// HoloLens Face Detection Example
    /// An example of detecting face using OpenCVForUnity on Hololens.
    /// Referring to https://github.com/Itseez/opencv/blob/master/modules/objdetect/src/detection_based_tracker.cpp.
    /// </summary>
    [RequireComponent(typeof(HLCameraStreamToMatHelper), typeof(ImageOptimizationHelper))]
    public class HLFaceDetectionExample : MonoBehaviour
    {
        /// <summary>
        /// Determines if enables the detection.
        /// </summary>
        public bool enableDetection = true;

        /// <summary>
        /// Determines if enable downscale.
        /// </summary>
        public bool enableDownScale;

        /// <summary>
        /// The enable downscale toggle.
        /// </summary>
        public Toggle enableDownScaleToggle;

        /// <summary>
        /// Determines if uses separate detection.
        /// </summary>
        public bool useSeparateDetection = false;

        /// <summary>
        /// The use separate detection toggle.
        /// </summary>
        public Toggle useSeparateDetectionToggle;

        /// <summary>
        /// Determines if displays camera image.
        /// </summary>
        public bool displayCameraImage = false;

        /// <summary>
        /// The display camera image toggle.
        /// </summary>
        public Toggle displayCameraImageToggle;

        /// <summary>
        /// The min detection size ratio.
        /// </summary>
        public float minDetectionSizeRatio = 0.07f;

        /// <summary>
        /// The webcam texture to mat helper.
        /// </summary>
        HLCameraStreamToMatHelper webCamTextureToMatHelper;

        /// <summary>
        /// The image optimization helper.
        /// </summary>
        ImageOptimizationHelper imageOptimizationHelper;

        /// <summary>
        /// The texture.
        /// </summary>
        Texture2D texture;

        /// <summary>
        /// The cascade.
        /// </summary>
        CascadeClassifier cascade;

        /// <summary>
        /// The quad renderer.
        /// </summary>
        Renderer quad_renderer;

        /// <summary>
        /// The detection result.
        /// </summary>
        List<Rect> detectionResult = new List<Rect>();

        Scalar COLOR_WHITE = new Scalar(255, 255, 255, 255);
        Scalar COLOR_GRAY = new Scalar(128, 128, 128, 255);

        Mat grayMat4Thread;
        CascadeClassifier cascade4Thread;
        readonly static Queue<Action> ExecuteOnMainThread = new Queue<Action>();
        System.Object sync = new System.Object();

        bool _isThreadRunning = false;
        bool isThreadRunning
        {
            get
            {
                lock (sync)
                    return _isThreadRunning;
            }
            set
            {
                lock (sync)
                    _isThreadRunning = value;
            }
        }

        RectangleTracker rectangleTracker;
        float coeffTrackingWindowSize = 2.0f;
        float coeffObjectSizeToTrack = 0.85f;
        List<Rect> detectedObjectsInRegions = new List<Rect>();
        List<Rect> resultObjects = new List<Rect>();

        bool _isDetecting = false;
        bool isDetecting
        {
            get
            {
                lock (sync)
                    return _isDetecting;
            }
            set
            {
                lock (sync)
                    _isDetecting = value;
            }
        }

        bool _hasUpdatedDetectionResult = false;
        bool hasUpdatedDetectionResult
        {
            get
            {
                lock (sync)
                    return _hasUpdatedDetectionResult;
            }
            set
            {
                lock (sync)
                    _hasUpdatedDetectionResult = value;
            }
        }

        bool _isDetectingInFrameArrivedThread = false;
        bool isDetectingInFrameArrivedThread
        {
            get
            {
                lock (sync)
                    return _isDetectingInFrameArrivedThread;
            }
            set
            {
                lock (sync)
                    _isDetectingInFrameArrivedThread = value;
            }
        }


        [HeaderAttribute("Debug")]

        public Text renderFPS;
        public Text videoFPS;
        public Text trackFPS;
        public Text debugStr;


        // Use this for initialization
        protected void Start()
        {
            enableDownScaleToggle.isOn = enableDownScale;
            useSeparateDetectionToggle.isOn = useSeparateDetection;
            displayCameraImageToggle.isOn = displayCameraImage;

            imageOptimizationHelper = gameObject.GetComponent<ImageOptimizationHelper>();
            webCamTextureToMatHelper = gameObject.GetComponent<HLCameraStreamToMatHelper>();
#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API
            webCamTextureToMatHelper.frameMatAcquired += OnFrameMatAcquired;
#endif
            webCamTextureToMatHelper.outputColorFormat = WebCamTextureToMatHelper.ColorFormat.GRAY;
            webCamTextureToMatHelper.Initialize();

            rectangleTracker = new RectangleTracker();
        }

        /// <summary>
        /// Raises the web cam texture to mat helper initialized event.
        /// </summary>
        public void OnWebCamTextureToMatHelperInitialized()
        {
            Debug.Log("OnWebCamTextureToMatHelperInitialized");

            Mat grayMat = webCamTextureToMatHelper.GetMat();

            texture = new Texture2D(grayMat.cols(), grayMat.rows(), TextureFormat.Alpha8, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            quad_renderer = gameObject.GetComponent<Renderer>() as Renderer;
            quad_renderer.sharedMaterial.SetTexture("_MainTex", texture);
            quad_renderer.sharedMaterial.SetVector("_VignetteOffset", new Vector4(0, 0));


            //Debug.Log("Screen.width " + Screen.width + " Screen.height " + Screen.height + " Screen.orientation " + Screen.orientation);


            DebugUtils.AddDebugStr(webCamTextureToMatHelper.outputColorFormat.ToString() + " " + webCamTextureToMatHelper.GetWidth() + " x " + webCamTextureToMatHelper.GetHeight() + " : " + webCamTextureToMatHelper.GetFPS());
            if (enableDownScale)
                DebugUtils.AddDebugStr("enableDownScale = true: " + imageOptimizationHelper.downscaleRatio + " / " + webCamTextureToMatHelper.GetWidth() / imageOptimizationHelper.downscaleRatio + " x " + webCamTextureToMatHelper.GetHeight() / imageOptimizationHelper.downscaleRatio);


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

            quad_renderer.sharedMaterial.SetFloat("_VignetteScale", 0.0f);


            cascade = new CascadeClassifier();
            cascade.load(Utils.getFilePath("objdetect/lbpcascade_frontalface.xml"));
#if !WINDOWS_UWP || UNITY_EDITOR
            // "empty" method is not working on the UWP platform.
            if (cascade.empty())
            {
                Debug.LogError("cascade file is not loaded. Please copy from “OpenCVForUnity/StreamingAssets/objdetect/” to “Assets/StreamingAssets/objdetect/” folder. ");
            }
#endif

            grayMat4Thread = new Mat();
            cascade4Thread = new CascadeClassifier();
            //cascade4Thread.load(Utils.getFilePath("objdetect/haarcascade_frontalface_alt.xml"));
            cascade4Thread.load(Utils.getFilePath("objdetect/lbpcascade_frontalface.xml"));
#if !WINDOWS_UWP || UNITY_EDITOR
            // "empty" method is not working on the UWP platform.
            if (cascade4Thread.empty())
            {
                Debug.LogError("cascade file is not loaded. Please copy from “OpenCVForUnity/StreamingAssets/objdetect/” to “Assets/StreamingAssets/objdetect/” folder. ");
            }
#endif
        }

        /// <summary>
        /// Raises the web cam texture to mat helper disposed event.
        /// </summary>
        public void OnWebCamTextureToMatHelperDisposed()
        {
            Debug.Log("OnWebCamTextureToMatHelperDisposed");

#if WINDOWS_UWP && !DISABLE_HOLOLENSCAMSTREAM_API

            while (isDetectingInFrameArrivedThread)
            {
                //Wait detecting stop
            }
#endif

            StopThread();
            lock (ExecuteOnMainThread)
            {
                ExecuteOnMainThread.Clear();
            }
            hasUpdatedDetectionResult = false;
            isDetecting = false;

            if (cascade != null)
                cascade.Dispose();

            if (grayMat4Thread != null)
                grayMat4Thread.Dispose();

            if (cascade4Thread != null)
                cascade4Thread.Dispose();

            rectangleTracker.Reset();

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
        public void OnFrameMatAcquired(Mat grayMat, Matrix4x4 projectionMatrix, Matrix4x4 cameraToWorldMatrix, CameraIntrinsics cameraIntrinsics)
        {
            isDetectingInFrameArrivedThread = true;

            DebugUtils.VideoTick();

            Mat downScaleMat = null;
            float DOWNSCALE_RATIO;
            if (enableDownScale)
            {
                downScaleMat = imageOptimizationHelper.GetDownScaleMat(grayMat);
                DOWNSCALE_RATIO = imageOptimizationHelper.downscaleRatio;
            }
            else
            {
                downScaleMat = grayMat;
                DOWNSCALE_RATIO = 1.0f;
            }

            Imgproc.equalizeHist(downScaleMat, downScaleMat);

            if (enableDetection && !isDetecting)
            {
                isDetecting = true;

                downScaleMat.copyTo(grayMat4Thread);

                System.Threading.Tasks.Task.Run(() =>
                {

                    isThreadRunning = true;

                    DetectObject(grayMat4Thread, out detectionResult, cascade4Thread);

                    isThreadRunning = false;
                    OnDetectionDone();
                });
            }

            if (!useSeparateDetection)
            {
                if (hasUpdatedDetectionResult)
                {
                    hasUpdatedDetectionResult = false;

                    lock (rectangleTracker)
                    {
                        rectangleTracker.UpdateTrackedObjects(detectionResult);
                    }
                }

                lock (rectangleTracker)
                {
                    rectangleTracker.GetObjects(resultObjects, true);
                }

                if (displayCameraImage)
                {
                    Imgproc.putText(grayMat, "W:" + grayMat.width() + " H:" + grayMat.height(), new Point(5, grayMat.rows() - 10), Imgproc.FONT_HERSHEY_SIMPLEX, 1.0, new Scalar(255, 255, 255, 255), 2, Imgproc.LINE_AA, false);
                }
                else
                {
                    // fill all black.
                    Imgproc.rectangle(grayMat, new Point(0, 0), new Point(grayMat.width(), grayMat.height()), new Scalar(0, 0, 0, 0), -1);
                }

                // draw face rect.
                DrawDownScaleFaceRects(grayMat, resultObjects.ToArray(), DOWNSCALE_RATIO, COLOR_WHITE, 6);
            }
            else
            {

                Rect[] rectsWhereRegions;

                if (hasUpdatedDetectionResult)
                {
                    hasUpdatedDetectionResult = false;

                    //Enqueue(() =>
                    //{
                    //    Debug.Log("process: get rectsWhereRegions were got from detectionResult");
                    //});

                    lock (rectangleTracker)
                    {
                        rectsWhereRegions = detectionResult.ToArray();
                    }
                }
                else
                {
                    //Enqueue(() =>
                    //{
                    //    Debug.Log("process: get rectsWhereRegions from previous positions");
                    //});

                    lock (rectangleTracker)
                    {
                        rectsWhereRegions = rectangleTracker.CreateCorrectionBySpeedOfRects();
                    }
                }

                detectedObjectsInRegions.Clear();
                int len = rectsWhereRegions.Length;
                for (int i = 0; i < len; i++)
                {
                    DetectInRegion(downScaleMat, rectsWhereRegions[i], detectedObjectsInRegions, cascade);
                }

                lock (rectangleTracker)
                {
                    rectangleTracker.UpdateTrackedObjects(detectedObjectsInRegions);
                    rectangleTracker.GetObjects(resultObjects, true);
                }

                if (displayCameraImage)
                {
                    Imgproc.putText(grayMat, "W:" + grayMat.width() + " H:" + grayMat.height(), new Point(5, grayMat.rows() - 10), Imgproc.FONT_HERSHEY_SIMPLEX, 1.0, new Scalar(255, 255, 255, 255), 2, Imgproc.LINE_AA, false);
                }
                else
                {
                    // fill all black.
                    Imgproc.rectangle(grayMat, new Point(0, 0), new Point(grayMat.width(), grayMat.height()), new Scalar(0, 0, 0, 0), -1);
                }

                // draw previous rect.
                DrawDownScaleFaceRects(grayMat, rectsWhereRegions, DOWNSCALE_RATIO, COLOR_GRAY, 1);

                // draw face rect.
                DrawDownScaleFaceRects(grayMat, resultObjects.ToArray(), DOWNSCALE_RATIO, COLOR_WHITE, 6);
            }

            DebugUtils.TrackTick();

            Enqueue(() =>
            {
                if (!webCamTextureToMatHelper.IsPlaying()) return;

                Utils.matToTexture2D(grayMat, texture);
                grayMat.Dispose();

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

            isDetectingInFrameArrivedThread = false;
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
            lock (ExecuteOnMainThread)
            {
                while (ExecuteOnMainThread.Count > 0)
                {
                    ExecuteOnMainThread.Dequeue().Invoke();
                }
            }

            if (webCamTextureToMatHelper.IsPlaying() && webCamTextureToMatHelper.DidUpdateThisFrame())
            {

                DebugUtils.VideoTick();

                Mat grayMat = webCamTextureToMatHelper.GetMat();

                Mat downScaleMat = null;
                float DOWNSCALE_RATIO;
                if (enableDownScale)
                {
                    downScaleMat = imageOptimizationHelper.GetDownScaleMat(grayMat);
                    DOWNSCALE_RATIO = imageOptimizationHelper.downscaleRatio;
                }
                else
                {
                    downScaleMat = grayMat;
                    DOWNSCALE_RATIO = 1.0f;
                }

                Imgproc.equalizeHist(downScaleMat, downScaleMat);

                if (enableDetection && !isDetecting)
                {
                    isDetecting = true;

                    downScaleMat.copyTo(grayMat4Thread);

                    StartThread(ThreadWorker);
                }

                if (!useSeparateDetection)
                {
                    if (hasUpdatedDetectionResult)
                    {
                        hasUpdatedDetectionResult = false;

                        rectangleTracker.UpdateTrackedObjects(detectionResult);
                    }

                    rectangleTracker.GetObjects(resultObjects, true);

                    if (displayCameraImage)
                    {
                        Imgproc.putText(grayMat, "W:" + grayMat.width() + " H:" + grayMat.height(), new Point(5, grayMat.rows() - 10), Imgproc.FONT_HERSHEY_SIMPLEX, 1.0, new Scalar(255, 255, 255, 255), 2, Imgproc.LINE_AA, false);
                    }
                    else
                    {
                        // fill all black.
                        Imgproc.rectangle(grayMat, new Point(0, 0), new Point(grayMat.width(), grayMat.height()), new Scalar(0, 0, 0, 0), -1);
                    }

                    // draw face rect.
                    DrawDownScaleFaceRects(grayMat, resultObjects.ToArray(), DOWNSCALE_RATIO, COLOR_WHITE, 6);
                }
                else
                {

                    Rect[] rectsWhereRegions;

                    if (hasUpdatedDetectionResult)
                    {
                        hasUpdatedDetectionResult = false;

                        //Debug.Log("process: get rectsWhereRegions were got from detectionResult");
                        rectsWhereRegions = detectionResult.ToArray();
                    }
                    else
                    {
                        //Debug.Log("process: get rectsWhereRegions from previous positions");
                        rectsWhereRegions = rectangleTracker.CreateCorrectionBySpeedOfRects();
                    }

                    detectedObjectsInRegions.Clear();
                    int len = rectsWhereRegions.Length;
                    for (int i = 0; i < len; i++)
                    {
                        DetectInRegion(downScaleMat, rectsWhereRegions[i], detectedObjectsInRegions, cascade);
                    }

                    rectangleTracker.UpdateTrackedObjects(detectedObjectsInRegions);
                    rectangleTracker.GetObjects(resultObjects, true);

                    if (displayCameraImage)
                    {
                        Imgproc.putText(grayMat, "W:" + grayMat.width() + " H:" + grayMat.height(), new Point(5, grayMat.rows() - 10), Imgproc.FONT_HERSHEY_SIMPLEX, 1.0, new Scalar(255, 255, 255, 255), 2, Imgproc.LINE_AA, false);
                    }
                    else
                    {
                        // fill all black.
                        Imgproc.rectangle(grayMat, new Point(0, 0), new Point(grayMat.width(), grayMat.height()), new Scalar(0, 0, 0, 0), -1);
                    }

                    // draw previous rect.
                    DrawDownScaleFaceRects(grayMat, rectsWhereRegions, DOWNSCALE_RATIO, COLOR_GRAY, 1);

                    // draw face rect.
                    DrawDownScaleFaceRects(grayMat, resultObjects.ToArray(), DOWNSCALE_RATIO, COLOR_WHITE, 6);
                }

                DebugUtils.TrackTick();

                Utils.matToTexture2D(grayMat, texture);
            }

            if (webCamTextureToMatHelper.IsPlaying())
            {

                Matrix4x4 cameraToWorldMatrix = Camera.main.cameraToWorldMatrix; ;
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

        private void StartThread(Action action)
        {
#if WINDOWS_UWP || (!UNITY_WSA_10_0 && (NET_4_6 || NET_STANDARD_2_0))
            System.Threading.Tasks.Task.Run(() => action());
#else
            ThreadPool.QueueUserWorkItem(_ => action());
#endif
        }

        private void StopThread()
        {
            if (!isThreadRunning)
                return;

            while (isThreadRunning)
            {
                //Wait threading stop
            }
        }

        private void ThreadWorker()
        {
            isThreadRunning = true;

            DetectObject(grayMat4Thread, out detectionResult, cascade4Thread);

            lock (ExecuteOnMainThread)
            {
                if (ExecuteOnMainThread.Count == 0)
                {
                    ExecuteOnMainThread.Enqueue(() =>
                    {
                        OnDetectionDone();
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

        private void DrawDownScaleFaceRects(Mat img, Rect[] rects, float downscaleRatio, Scalar color, int thickness)
        {
            int len = rects.Length;
            for (int i = 0; i < len; i++)
            {
                Rect rect = new Rect(
                    (int)(rects[i].x * downscaleRatio),
                    (int)(rects[i].y * downscaleRatio),
                    (int)(rects[i].width * downscaleRatio),
                    (int)(rects[i].height * downscaleRatio)
                );
                Imgproc.rectangle(img, rect, color, thickness);
            }
        }

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
            imageOptimizationHelper.Dispose();

            if (rectangleTracker != null)
                rectangleTracker.Dispose();
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
        /// Raises the enable downscale toggle value changed event.
        /// </summary>
        public void OnEnableDownScaleToggleValueChanged()
        {
            enableDownScale = enableDownScaleToggle.isOn;

            if (rectangleTracker != null)
            {
                lock (rectangleTracker)
                {
                    rectangleTracker.Reset();
                }
            }
        }

        /// <summary>
        /// Raises the use separate detection toggle value changed event.
        /// </summary>
        public void OnUseSeparateDetectionToggleValueChanged()
        {
            useSeparateDetection = useSeparateDetectionToggle.isOn;

            if (rectangleTracker != null)
            {
                lock (rectangleTracker)
                {
                    rectangleTracker.Reset();
                }
            }
        }

        /// <summary>
        /// Raises the display camera image toggle value changed event.
        /// </summary>
        public void OnDisplayCameraImageToggleValueChanged()
        {
            displayCameraImage = displayCameraImageToggle.isOn;
        }
    }
}
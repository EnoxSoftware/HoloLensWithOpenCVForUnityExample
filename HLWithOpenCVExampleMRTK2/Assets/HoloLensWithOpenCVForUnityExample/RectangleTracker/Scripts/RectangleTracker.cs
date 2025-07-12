using System;
using System.Collections.Generic;
using System.Linq;
using OpenCVForUnity.CoreModule;
using UnityEngine;
using Rect = OpenCVForUnity.CoreModule.Rect;

namespace HoloLensWithOpenCVForUnityExample.RectangleTrack
{
    /// <summary>
    /// Rectangle tracker for tracking detected face rectangles across frames.
    /// v2.0.0
    /// This class manages multiple face rectangles, handles object correspondence,
    /// and provides smoothing and state management for tracked objects.
    /// Referring to https://github.com/Itseez/opencv/blob/master/modules/objdetect/src/detection_based_tracker.cpp.
    /// </summary>
    public class RectangleTracker
    {
        // Enums
        /// <summary>
        /// States for rectangle correspondence during tracking.
        /// </summary>
        public enum TrackedRectState : int
        {
            /// <summary>
            /// New rectangle that hasn't been assigned to any tracked object.
            /// </summary>
            NEW_RECTANGLE = -1,

            /// <summary>
            /// Rectangle that intersects with another rectangle and is excluded from tracking.
            /// </summary>
            INTERSECTED_RECTANGLE = -2
        }

        // Private Fields
        private int _nextId = 0;
        private List<TrackedObject> _trackedObjects;
        private TrackerParameters _trackerParameters;
        private List<float> _weightsPositionsSmoothing = new List<float>();
        private List<float> _weightsSizesSmoothing = new List<float>();


        // Public Properties
        /// <summary>
        /// Gets the list of currently tracked objects.
        /// </summary>
        public List<TrackedObject> TrackedObjects
        {
            get { return _trackedObjects; }
        }

        /// <summary>
        /// Gets or sets the tracker parameters for controlling tracking behavior.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when setting null value.</exception>
        public TrackerParameters TrackerParameters
        {
            get { return _trackerParameters; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }
                _trackerParameters = value;
            }
        }

        /// <summary>
        /// Gets or sets the weights for position smoothing calculation.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when setting null value.</exception>
        public List<float> WeightsPositionsSmoothing
        {
            get { return _weightsPositionsSmoothing; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }
                _weightsPositionsSmoothing = value;
            }
        }

        /// <summary>
        /// Gets or sets the weights for size smoothing calculation.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when setting null value.</exception>
        public List<float> WeightsSizesSmoothing
        {
            get { return _weightsSizesSmoothing; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }
                _weightsSizesSmoothing = value;
            }
        }

        // Public Methods
        /// <summary>
        /// Initializes a new instance of the RectangleTracker class.
        /// </summary>
        /// <param name="trackerParamerers">Optional tracker parameters. If null, default parameters will be used.</param>
        public RectangleTracker(TrackerParameters trackerParamerers = null)
        {
            _trackedObjects = new List<TrackedObject>();

            if (trackerParamerers != null)
            {
                this._trackerParameters = trackerParamerers;
            }
            else
            {
                this._trackerParameters = new TrackerParameters();
            }

            _weightsPositionsSmoothing.Add(1);
            _weightsSizesSmoothing.Add(0.5f);
            _weightsSizesSmoothing.Add(0.3f);
            _weightsSizesSmoothing.Add(0.2f);
        }

        /// <summary>
        /// Gets the current tracked objects as rectangles.
        /// </summary>
        /// <param name="result">List to store the tracked rectangles.</param>
        /// <param name="smoothing">Whether to apply smoothing to the rectangle positions and sizes.</param>
        public void GetObjects(List<Rect> result, bool smoothing = true)
        {
            result.Clear();

            int count = _trackedObjects.Count;
            for (int i = 0; i < count; i++)
            {
                Rect r;
                if (smoothing)
                {
                    r = GetSmoothingRect(i);
                }
                else
                {
                    r = _trackedObjects[i].Position;
                }

                if (_trackedObjects[i].State > TrackedState.NEW_DISPLAYED && _trackedObjects[i].State < TrackedState.NEW_HIDED)
                    result.Add(r);

                //LOGD("DetectionBasedTracker::process: found a object with SIZE %d x %d, rect={%d, %d, %d x %d}", r.width, r.height, r.x, r.y, r.width, r.height);
                //Debug.Log("GetObjects" + r.width + " " + r.height + " " + r.x + " " + r.y + " " + r.width + " " + r.height + " " + trackedObjects[i].state + " " + trackedObjects[i].numDetectedFrames + " " + trackedObjects[i].numFramesNotDetected);
            }
        }

        /// <summary>
        /// Gets the current tracked objects with detailed tracking information.
        /// </summary>
        /// <param name="result">List to store the tracked rectangles with tracking metadata.</param>
        /// <param name="smoothing">Whether to apply smoothing to the rectangle positions and sizes.</param>
        public void GetObjects(List<TrackedRect> result, bool smoothing = true)
        {
            result.Clear();

            int count = _trackedObjects.Count;
            for (int i = 0; i < count; i++)
            {
                Rect r;
                if (smoothing)
                {
                    r = GetSmoothingRect(i);
                }
                else
                {
                    r = _trackedObjects[i].Position;
                }

                result.Add(new TrackedRect(_trackedObjects[i].Id, r, _trackedObjects[i].State, _trackedObjects[i].NumDetectedFrames, _trackedObjects[i].NumFramesNotDetected));

                //LOGD("DetectionBasedTracker::process: found a object with SIZE %d x %d, rect={%d, %d, %d x %d}", r.width, r.height, r.x, r.y, r.width, r.height);
                //Debug.Log("GetObjects" + r.width + " " + r.height + " " + r.x + " " + r.y + " " + r.width + " " + r.height + " " + trackedObjects[i].state + " " + trackedObjects[i].numDetectedFrames + " " + trackedObjects[i].numFramesNotDetected);
            }
        }

        /// <summary>
        /// Updates the tracked objects with newly detected rectangles.
        /// This method handles object correspondence, state management, and tracking lifecycle.
        /// </summary>
        /// <param name="detectedObjects">List of rectangles detected in the current frame.</param>
        /// <exception cref="ArgumentNullException">Thrown when detectedObjects is null.</exception>
        public void UpdateTrackedObjects(List<Rect> detectedObjects)
        {
            if (detectedObjects == null)
                throw new ArgumentNullException("detectedObjects");

            Rect[] correctionRects = CreateCorrectionBySpeedOfRects();

            int N1 = (int)_trackedObjects.Count;
            int N2 = (int)detectedObjects.Count;

            for (int i = 0; i < N1; i++)
            {
                _trackedObjects[i].NumDetectedFrames++;
            }

            int[] correspondence = Enumerable.Repeat<int>((int)TrackedRectState.NEW_RECTANGLE, N2).ToArray();


            for (int i = 0; i < N1; i++)
            {
                TrackedObject curObject = _trackedObjects[i];

                int bestIndex = -1;
                int bestArea = -1;

                Rect prevRect = correctionRects[i];

                for (int j = 0; j < N2; j++)
                {
                    if (correspondence[j] >= 0)
                    {
                        //Debug.Log("DetectionBasedTracker::updateTrackedObjects: j=" + j + " is rejected, because it has correspondence=" + correspondence[j]);
                        continue;
                    }

                    if (correspondence[j] != (int)TrackedRectState.NEW_RECTANGLE)
                    {
                        //Debug.Log("DetectionBasedTracker::updateTrackedObjects: j=" + j + " is rejected, because it is intersected with another rectangle");

                        continue;
                    }

                    if (IsCollideByRectangle(prevRect, detectedObjects[j], _trackerParameters.CoeffRectangleOverlap))
                    {
                        Rect r = Intersect(prevRect, detectedObjects[j]);
                        if ((r.width > 0) && (r.height > 0))
                        {
                            //Debug.Log("DetectionBasedTracker::updateTrackedObjects: There is intersection between prevRect and detectedRect r={" + r.x + ", " + r.y + ", " + r.width + ", " + r.height + "]");

                            correspondence[j] = (int)TrackedRectState.INTERSECTED_RECTANGLE;

                            if (r.area() > bestArea)
                            {
                                //Debug.Log("DetectionBasedTracker::updateTrackedObjects: The area of intersection is " + r.area() + " it is better than bestArea= " + bestArea);

                                bestIndex = j;
                                bestArea = (int)r.area();
                            }
                        }
                    }
                }

                if (bestIndex >= 0)
                {
                    //Debug.Log("DetectionBasedTracker::updateTrackedObjects: The best correspondence for i=" + i + " is j=" + bestIndex);

                    correspondence[bestIndex] = i;

                    Rect bestRect = detectedObjects[bestIndex];

                    for (int j = 0; j < N2; j++)
                    {
                        if (correspondence[j] >= 0)
                            continue;

                        if (IsCollideByRectangle(detectedObjects[j], bestRect, _trackerParameters.CoeffRectangleOverlap))
                        {
                            Rect r = Intersect(detectedObjects[j], bestRect);

                            if ((r.width > 0) && (r.height > 0))
                            {
                                //Debug.Log("DetectionBasedTracker::updateTrackedObjects: Found intersection between rectangles j= " + j + " and bestIndex= " + bestIndex + " rectangle j= " + j + " is marked as intersected");

                                correspondence[j] = (int)TrackedRectState.INTERSECTED_RECTANGLE;
                            }
                        }
                    }
                }
                else
                {
                    //Debug.Log("DetectionBasedTracker::updateTrackedObjects: There is no correspondence for i= " + i);
                    curObject.NumFramesNotDetected++;
                }
            }

            //Debug.Log("DetectionBasedTracker::updateTrackedObjects: start second cycle");
            for (int j = 0; j < N2; j++)
            {
                int i = correspondence[j];
                if (i >= 0)
                {
                    //add position
                    //Debug.Log("DetectionBasedTracker::updateTrackedObjects: add position");

                    _trackedObjects[i].LastPositions.Add(detectedObjects[j]);
                    while ((int)_trackedObjects[i].LastPositions.Count > (int)_trackerParameters.NumLastPositionsToTrack)
                    {
                        _trackedObjects[i].LastPositions.Remove(_trackedObjects[i].LastPositions[0]);
                    }
                    _trackedObjects[i].NumFramesNotDetected = 0;
                    if (_trackedObjects[i].State != TrackedState.DELETED)
                        _trackedObjects[i].State = TrackedState.DISPLAYED;
                }
                else if (i == (int)TrackedRectState.NEW_RECTANGLE)
                {
                    //new object
                    //Debug.Log("DetectionBasedTracker::updateTrackedObjects: new object");

                    _trackedObjects.Add(new TrackedObject(detectedObjects[j], _nextId++));
                }
                else
                {
                    //Debug.Log("DetectionBasedTracker::updateTrackedObjects: was auxiliary intersection");
                }
            }

            int t = 0;
            TrackedObject it;
            while (t < _trackedObjects.Count)
            {
                it = _trackedObjects[t];

                if (it.State == TrackedState.DELETED)
                {
                    _trackedObjects.Remove(it);
                }
                else if ((it.NumFramesNotDetected > _trackerParameters.MaxTrackLifetime)//ALL
                         ||
                         ((it.NumDetectedFrames <= _trackerParameters.NumStepsToWaitBeforeFirstShow)
                         &&
                         (it.NumFramesNotDetected > _trackerParameters.NumStepsToTrackWithoutDetectingIfObjectHasNotBeenShown)))
                {
                    it.State = TrackedState.DELETED;
                    t++;
                }
                else if (it.State >= TrackedState.DISPLAYED)
                {
                    //DISPLAYED, NEW_DISPLAYED, HIDED

                    if (it.NumDetectedFrames < _trackerParameters.NumStepsToWaitBeforeFirstShow)
                    {
                        it.State = TrackedState.PENDING;
                    }
                    else if (it.NumDetectedFrames == _trackerParameters.NumStepsToWaitBeforeFirstShow)
                    {
                        //i, trackedObjects[i].numDetectedFrames, innerParameters.numStepsToWaitBeforeFirstShow);
                        it.State = TrackedState.NEW_DISPLAYED;
                    }
                    else if (it.NumFramesNotDetected == _trackerParameters.NumStepsToShowWithoutDetecting)
                    {
                        it.State = TrackedState.NEW_HIDED;
                    }
                    else if (it.NumFramesNotDetected > _trackerParameters.NumStepsToShowWithoutDetecting)
                    {
                        it.State = TrackedState.HIDED;
                    }

                    t++;
                }
                else
                {
                    //NEW
                    t++;
                }
            }
        }

        /// <summary>
        /// Creates predicted rectangles based on the speed of tracked objects.
        /// Uses the last two positions to predict where objects should be in the current frame.
        /// </summary>
        /// <returns>Array of predicted rectangles for each tracked object.</returns>
        public Rect[] CreateCorrectionBySpeedOfRects()
        {
            //Debug.Log("DetectionBasedTracker::process: get _rectsWhereRegions from previous positions");
            Rect[] rectsWhereRegions = new Rect[_trackedObjects.Count];

            int count = _trackedObjects.Count;
            for (int i = 0; i < count; i++)
            {
                int n = _trackedObjects[i].LastPositions.Count;

                Rect r = _trackedObjects[i].LastPositions[n - 1].clone();

                //correction by speed of rectangle
                if (n > 1)
                {
                    Point center = CenterRect(r);
                    Point _centerPrev = CenterRect(_trackedObjects[i].LastPositions[n - 2]);
                    Point shift = new Point((center.x - _centerPrev.x) * _trackerParameters.CoeffObjectSpeedUsingInPrediction,
                                      (center.y - _centerPrev.y) * _trackerParameters.CoeffObjectSpeedUsingInPrediction);

                    r.x += (int)Math.Round(shift.x);
                    r.y += (int)Math.Round(shift.y);
                }

                rectsWhereRegions[i] = r;
            }

            return rectsWhereRegions;
        }

        /// <summary>
        /// Creates raw rectangles from the current positions of tracked objects without prediction.
        /// </summary>
        /// <returns>Array of current rectangles for each tracked object.</returns>
        public Rect[] CreateRawRects()
        {
            Rect[] rectsWhereRegions = new Rect[_trackedObjects.Count];

            int count = _trackedObjects.Count;
            for (int i = 0; i < count; i++)
            {
                rectsWhereRegions[i] = _trackedObjects[i].Position;
            }

            return rectsWhereRegions;
        }

        /// <summary>
        /// Resets the tracker by clearing all tracked objects and resetting the ID counter.
        /// </summary>
        public void Reset()
        {
            _trackedObjects.Clear();
            _nextId = 0;
        }

        /// <summary>
        /// Disposes the tracker by calling Reset().
        /// </summary>
        public void Dispose()
        {
            Reset();
        }

        // Private Methods
        private Point CenterRect(Rect r)
        {
            return new Point(r.x + (r.width / 2), r.y + (r.height / 2));
        }

        private Rect GetSmoothingRect(int i)
        {
            //Debug.Log("trackedObjects[i].numFramesNotDetected: " + trackedObjects[i].numFramesNotDetected);

            List<float> weightsSizesSmoothing = _weightsSizesSmoothing;
            List<float> weightsPositionsSmoothing = _weightsPositionsSmoothing;

            List<Rect> lastPositions = _trackedObjects[i].LastPositions;

            int N = lastPositions.Count;
            if (N <= 0)
            {
                Debug.Log("DetectionBasedTracker::calcTrackedObjectPositionToShow: ERROR: no positions for i=" + i);
                return new Rect();
            }

            int Nsize = Math.Min(N, (int)weightsSizesSmoothing.Count);
            int Ncenter = Math.Min(N, (int)weightsPositionsSmoothing.Count);

            Point center = new Point();
            double w = 0, h = 0;
            if (Nsize > 0)
            {
                double sum = 0;
                for (int j = 0; j < Nsize; j++)
                {
                    int k = N - j - 1;
                    w += lastPositions[k].width * weightsSizesSmoothing[j];
                    h += lastPositions[k].height * weightsSizesSmoothing[j];
                    sum += weightsSizesSmoothing[j];
                }
                w /= sum;
                h /= sum;
            }
            else
            {
                w = lastPositions[N - 1].width;
                h = lastPositions[N - 1].height;
            }

            if (Ncenter > 0)
            {
                double sum = 0;
                for (int j = 0; j < Ncenter; j++)
                {
                    int k = N - j - 1;
                    Point tl = lastPositions[k].tl();
                    Point br = lastPositions[k].br();
                    Point c1;

                    c1 = new Point(tl.x * 0.5f, tl.y * 0.5f);
                    Point c2;

                    c2 = new Point(br.x * 0.5f, br.y * 0.5f);
                    c1 = new Point(c1.x + c2.x, c1.y + c2.y);

                    center = new Point(center.x + (c1.x * weightsPositionsSmoothing[j]), center.y + (c1.y * weightsPositionsSmoothing[j]));
                    sum += weightsPositionsSmoothing[j];
                }
                center = new Point(center.x * (1 / sum), center.y * (1 / sum));
            }
            else
            {
                int k = N - 1;
                Point tl = lastPositions[k].tl();
                Point br = lastPositions[k].br();
                Point c1;

                c1 = new Point(tl.x * 0.5f, tl.y * 0.5f);
                Point c2;

                c2 = new Point(br.x * 0.5f, br.y * 0.5f);

                center = new Point(c1.x + c2.x, c1.y + c2.y);
            }
            Point tl2 = new Point(center.x - (w * 0.5f), center.y - (h * 0.5f));
            Rect res = new Rect((int)Math.Round(tl2.x), (int)Math.Round(tl2.y), (int)Math.Round(w), (int)Math.Round(h));

            //Debug.Log("DetectionBasedTracker::calcTrackedObjectPositionToShow: Result for i=" + i + ": {" + res.x + ", " + res.y + ", " + res.width + ", " + res.height + "}");

            return res;
        }

        private Rect Intersect(Rect a, Rect b)
        {
            int x1 = Math.Max(a.x, b.x);
            int x2 = Math.Min(a.x + a.width, b.x + b.width);
            int y1 = Math.Max(a.y, b.y);
            int y2 = Math.Min(a.y + a.height, b.y + b.height);

            if (x2 >= x1 && y2 >= y1)
                return new Rect(x1, y1, x2 - x1, y2 - y1);
            else
                return new Rect();
        }

        private bool IsCollideByRectangle(Rect a, Rect b, float coeffRectangleOverlap)
        {
            int mw = (int)(a.width * coeffRectangleOverlap);
            int mh = (int)(a.height * coeffRectangleOverlap);
            int mx1 = (int)(a.x + (a.width - mw) / 2.0f);
            int my1 = (int)(a.y + (a.height - mh) / 2.0f);
            int mx2 = (int)(mx1 + mw);
            int my2 = (int)(my1 + mh);

            int ew = (int)(b.width * coeffRectangleOverlap);
            int eh = (int)(b.height * coeffRectangleOverlap);
            int ex1 = (int)(b.x + (b.width - ew) / 2.0f);
            int ey1 = (int)(b.y + (b.height - eh) / 2.0f);
            int ex2 = (int)(ex1 + ew);
            int ey2 = (int)(ey1 + eh);

            if (mx1 <= ex2 && ex1 <= mx2 && my1 <= ey2 && ey1 <= my2)
                return true;
            else
                return false;
        }
    }
}

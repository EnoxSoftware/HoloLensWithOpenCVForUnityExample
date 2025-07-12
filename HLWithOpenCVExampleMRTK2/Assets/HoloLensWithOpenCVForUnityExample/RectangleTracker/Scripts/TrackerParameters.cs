namespace HoloLensWithOpenCVForUnityExample.RectangleTrack
{
    /// <summary>
    /// Parameters for controlling the behavior of the RectangleTracker.
    /// </summary>
    public class TrackerParameters
    {
        // Public Fields
        /// <summary>
        /// Number of last positions to keep for each tracked object.
        /// </summary>
        public int NumLastPositionsToTrack = 4;

        /// <summary>
        /// Number of frames to wait before showing a newly detected object.
        /// </summary>
        public int NumStepsToWaitBeforeFirstShow = 6;

        /// <summary>
        /// Number of frames to track an object without detection if it hasn't been shown yet.
        /// </summary>
        public int NumStepsToTrackWithoutDetectingIfObjectHasNotBeenShown = 3;

        /// <summary>
        /// Number of frames to show an object without detection before hiding it.
        /// </summary>
        public int NumStepsToShowWithoutDetecting = 3;

        /// <summary>
        /// Maximum number of frames to track an object without detection before deletion.
        /// </summary>
        public int MaxTrackLifetime = 5;

        /// <summary>
        /// Coefficient for using object speed in position prediction (0.0 to 1.0).
        /// </summary>
        public float CoeffObjectSpeedUsingInPrediction = 0.8f;

        /// <summary>
        /// Coefficient for rectangle overlap detection (0.0 to 1.0).
        /// </summary>
        public float CoeffRectangleOverlap = 0.7f;

        // Public Methods
        /// <summary>
        /// Initializes a new instance of the TrackerParameters class with default values.
        /// </summary>
        public TrackerParameters()
        {
        }

        /// <summary>
        /// Creates a deep copy of this TrackerParameters instance.
        /// </summary>
        /// <returns>A new TrackerParameters instance with the same values.</returns>
        public TrackerParameters Clone()
        {
            TrackerParameters trackerParameters = new TrackerParameters();
            trackerParameters.NumLastPositionsToTrack = NumLastPositionsToTrack;
            trackerParameters.NumStepsToWaitBeforeFirstShow = NumStepsToWaitBeforeFirstShow;
            trackerParameters.NumStepsToTrackWithoutDetectingIfObjectHasNotBeenShown = NumStepsToTrackWithoutDetectingIfObjectHasNotBeenShown;
            trackerParameters.NumStepsToShowWithoutDetecting = NumStepsToShowWithoutDetecting;
            trackerParameters.MaxTrackLifetime = MaxTrackLifetime;
            trackerParameters.CoeffObjectSpeedUsingInPrediction = CoeffObjectSpeedUsingInPrediction;
            trackerParameters.CoeffRectangleOverlap = CoeffRectangleOverlap;

            return trackerParameters;
        }
    }
}

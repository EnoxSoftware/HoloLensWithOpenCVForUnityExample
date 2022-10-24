namespace OpenCVForUnity.RectangleTrack
{
    public class TrackerParameters
    {
        public int numLastPositionsToTrack = 4;
        public int numStepsToWaitBeforeFirstShow = 6;
        public int numStepsToTrackWithoutDetectingIfObjectHasNotBeenShown = 3;
        public int numStepsToShowWithoutDetecting = 3;

        public int maxTrackLifetime = 5;

        public float coeffObjectSpeedUsingInPrediction = 0.8f;
        public float coeffRectangleOverlap = 0.7f;

        public TrackerParameters()
        {
        }

        public TrackerParameters Clone()
        {
            TrackerParameters trackerParameters = new TrackerParameters();
            trackerParameters.numLastPositionsToTrack = numLastPositionsToTrack;
            trackerParameters.numStepsToWaitBeforeFirstShow = numStepsToWaitBeforeFirstShow;
            trackerParameters.numStepsToTrackWithoutDetectingIfObjectHasNotBeenShown = numStepsToTrackWithoutDetectingIfObjectHasNotBeenShown;
            trackerParameters.numStepsToShowWithoutDetecting = numStepsToShowWithoutDetecting;
            trackerParameters.maxTrackLifetime = maxTrackLifetime;
            trackerParameters.coeffObjectSpeedUsingInPrediction = coeffObjectSpeedUsingInPrediction;
            trackerParameters.coeffRectangleOverlap = coeffRectangleOverlap;

            return trackerParameters;
        }
    }
}

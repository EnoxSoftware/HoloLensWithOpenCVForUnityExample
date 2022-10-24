using OpenCVForUnity.CoreModule;

namespace OpenCVForUnity.RectangleTrack
{
    public class TrackedRect : Rect
    {
        public int numDetectedFrames;
        public int numFramesNotDetected;
        public int id;
        public TrackedState state;

        public TrackedRect(int id, Rect rect, TrackedState state, int numDetectedFrames, int numFramesNotDetected)
            : base(rect.x, rect.y, rect.width, rect.height)
        {
            this.numDetectedFrames = numDetectedFrames;
            this.numFramesNotDetected = numFramesNotDetected;
            this.id = id;
            this.state = state;
        }
    }
}
using OpenCVForUnity.CoreModule;
using PositionsVector = System.Collections.Generic.List<OpenCVForUnity.CoreModule.Rect>;

namespace OpenCVForUnity.RectangleTrack
{
    public enum TrackedState : int
    {
        NEW = 0,
        PENDING = 1,
        NEW_DISPLAYED = 2,
        DISPLAYED = 3,
        NEW_HIDED = 4,
        HIDED = 5,
        DELETED = 6
    }

    public class TrackedObject
    {
        public PositionsVector lastPositions;
        public int numDetectedFrames;
        public int numFramesNotDetected;
        public int id;
        public TrackedState state;

        public Rect position
        {
            get { return lastPositions[lastPositions.Count - 1].clone(); }
        }

        static private int _id = 0;

        public TrackedObject(Rect rect)
        {
            lastPositions = new PositionsVector();

            numDetectedFrames = 1;
            numFramesNotDetected = 0;
            state = TrackedState.NEW;

            lastPositions.Add(rect.clone());

            _id = GetNextId();
            id = _id;
        }

        static int GetNextId()
        {
            _id++;
            return _id;
        }
    }
}
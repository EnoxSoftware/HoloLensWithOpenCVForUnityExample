using OpenCVForUnity.CoreModule;

namespace HoloLensWithOpenCVForUnityExample.RectangleTrack
{
    /// <summary>
    /// Represents a tracked rectangle with additional tracking metadata.
    /// Extends OpenCV Rect with tracking information.
    /// </summary>
    public class TrackedRect : Rect
    {
        // Public Fields
        /// <summary>
        /// Number of frames where this object was detected.
        /// </summary>
        public int NumDetectedFrames;

        /// <summary>
        /// Number of consecutive frames where this object was not detected.
        /// </summary>
        public int NumFramesNotDetected;

        /// <summary>
        /// Unique identifier for this tracked object.
        /// </summary>
        public int Id;

        /// <summary>
        /// Current state of this tracked object.
        /// </summary>
        public TrackedState State;

        // Public Methods
        /// <summary>
        /// Initializes a new instance of the TrackedRect class.
        /// </summary>
        /// <param name="id">Unique identifier for this tracked object.</param>
        /// <param name="rect">Rectangle position and size.</param>
        /// <param name="state">Current state of this tracked object.</param>
        /// <param name="numDetectedFrames">Number of frames where this object was detected.</param>
        /// <param name="numFramesNotDetected">Number of consecutive frames where this object was not detected.</param>
        public TrackedRect(int id, Rect rect, TrackedState state, int numDetectedFrames, int numFramesNotDetected)
            : base(rect.x, rect.y, rect.width, rect.height)
        {
            this.NumDetectedFrames = numDetectedFrames;
            this.NumFramesNotDetected = numFramesNotDetected;
            this.Id = id;
            this.State = state;
        }
    }
}

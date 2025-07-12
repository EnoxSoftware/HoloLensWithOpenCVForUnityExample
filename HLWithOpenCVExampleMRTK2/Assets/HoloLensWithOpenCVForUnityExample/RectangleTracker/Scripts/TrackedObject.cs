using OpenCVForUnity.CoreModule;
using PositionsVector = System.Collections.Generic.List<OpenCVForUnity.CoreModule.Rect>;

namespace HoloLensWithOpenCVForUnityExample.RectangleTrack
{
    // Enums
    /// <summary>
    /// States for tracked objects during their lifecycle.
    /// </summary>
    public enum TrackedState : int
    {
        /// <summary>
        /// New object that was just detected.
        /// </summary>
        NEW = 0,

        /// <summary>
        /// Object is pending display (waiting for minimum detection frames).
        /// </summary>
        PENDING = 1,

        /// <summary>
        /// Object is newly displayed (just met the minimum detection requirement).
        /// </summary>
        NEW_DISPLAYED = 2,

        /// <summary>
        /// Object is currently being displayed.
        /// </summary>
        DISPLAYED = 3,

        /// <summary>
        /// Object is newly hidden (just exceeded the maximum non-detection frames).
        /// </summary>
        NEW_HIDED = 4,

        /// <summary>
        /// Object is currently hidden (not detected for several frames).
        /// </summary>
        HIDED = 5,

        /// <summary>
        /// Object is marked for deletion.
        /// </summary>
        DELETED = 6
    }

    /// <summary>
    /// Represents a tracked object with position history and state management.
    /// </summary>
    public class TrackedObject
    {
        // Public Fields
        /// <summary>
        /// History of positions for this tracked object.
        /// </summary>
        public PositionsVector LastPositions;

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

        // Public Properties
        /// <summary>
        /// Gets the current position of this tracked object.
        /// </summary>
        public Rect Position
        {
            get { return LastPositions[LastPositions.Count - 1].clone(); }
        }

        // Public Methods
        /// <summary>
        /// Initializes a new instance of the TrackedObject class.
        /// </summary>
        /// <param name="rect">Initial rectangle position.</param>
        /// <param name="id">Unique identifier for this tracked object.</param>
        public TrackedObject(Rect rect, int id)
        {
            LastPositions = new PositionsVector();

            NumDetectedFrames = 1;
            NumFramesNotDetected = 0;
            State = TrackedState.NEW;

            LastPositions.Add(rect.clone());

            this.Id = id;
        }
    }
}

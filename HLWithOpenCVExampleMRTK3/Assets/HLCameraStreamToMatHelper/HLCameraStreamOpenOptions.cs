namespace HoloLensWithOpenCVForUnity.UnityIntegration.Helper.SourceToMat
{
    /// <summary>
    /// One-shot open options for HoloLens CameraStream-backed camera sources.
    /// </summary>
    /// <remarks>
    /// Projects only <see cref="Width"/>, <see cref="Height"/>, and <see cref="FPS"/> onto
    /// <see cref="HLCameraStreamToMatHelper"/> before initialization.
    /// WebCam-only requests (device name, facing, kind, and AsyncGPU readback) are not included;
    /// Editor / WebCam fallback uses Inspector source settings for those values.
    /// </remarks>
    public sealed class HLCameraStreamOpenOptions
    {
        // Public Properties
        /// <summary>
        /// Gets or sets the requested frame width in pixels.
        /// </summary>
        public int Width { get; set; } = 1280;

        /// <summary>
        /// Gets or sets the requested frame height in pixels.
        /// </summary>
        public int Height { get; set; } = 720;

        /// <summary>
        /// Gets or sets the requested frame rate in frames per second.
        /// </summary>
        public float FPS { get; set; } = 30f;
    }
}

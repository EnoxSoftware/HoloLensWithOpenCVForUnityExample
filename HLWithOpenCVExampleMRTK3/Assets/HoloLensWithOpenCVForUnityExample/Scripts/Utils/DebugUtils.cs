using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace HoloLensWithOpenCVForUnityExample
{
    /// <summary>
    /// Debug HUD helpers for render-loop timing and event-rate FPS (video / track).
    /// </summary>
    /// <remarks>
    /// Render rate is an EMA of <see cref="Time.unscaledDeltaTime"/> sampled from <see cref="RenderTick"/>.
    /// Video and track rates count <see cref="VideoTick"/> / <see cref="TrackTick"/> events in a 1-second window
    /// and fall to 0 after about one second with no events (Stop / Pause need no extra Example code).
    /// Video and track ticks may run off the Unity main thread; timestamps use <see cref="System.Diagnostics.Stopwatch"/>.
    /// </remarks>
    public static class DebugUtils
    {
        // Constants
        private const float WINDOW_SECONDS = 1f;
        private const float RENDER_EMA_WEIGHT = 0.1f;

        // Private Fields
#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        private static readonly EventRateMeter VIDEO_METER = new EventRateMeter();

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        private static readonly EventRateMeter TRACK_METER = new EventRateMeter();

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        private static readonly StringBuilder SB = new StringBuilder(1000);

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        private static float _renderEmaMs;

        // Public Methods
        /// <summary>
        /// Records one Unity render-loop sample. Call from <c>LateUpdate</c>.
        /// </summary>
        public static void RenderTick()
        {
            float deltaTimeMs = Time.unscaledDeltaTime * 1000f;
            if (deltaTimeMs <= 0f)
            {
                return;
            }

            if (_renderEmaMs <= 0f)
            {
                _renderEmaMs = deltaTimeMs;
                return;
            }

            _renderEmaMs = Mathf.Lerp(_renderEmaMs, deltaTimeMs, RENDER_EMA_WEIGHT);
        }

        /// <summary>
        /// Tries to read the smoothed render-loop interval and FPS.
        /// </summary>
        /// <param name="intervalMs">Smoothed frame interval in milliseconds, or 0 when no sample exists.</param>
        /// <param name="fps">Smoothed frames per second, or 0 when no sample exists.</param>
        /// <returns><see langword="true"/> when at least one <see cref="RenderTick"/> has been recorded.</returns>
        public static bool TryGetRenderRate(out float intervalMs, out float fps)
        {
            intervalMs = _renderEmaMs;
            if (intervalMs <= 0f)
            {
                fps = 0f;
                return false;
            }

            fps = 1000f / intervalMs;
            return true;
        }

        /// <summary>
        /// Records one video-frame event (delivery or poll update).
        /// </summary>
        public static void VideoTick()
        {
            VIDEO_METER.Record();
        }

        /// <summary>
        /// Tries to read the video event rate over the last 1 second.
        /// </summary>
        /// <param name="intervalMs">Mean interval in milliseconds implied by the windowed rate, or 0 when inactive.</param>
        /// <param name="fps">Events per second in the window, or 0 when inactive.</param>
        /// <param name="isActive"><see langword="true"/> when at least one event remains in the window.</param>
        /// <returns>Always <see langword="true"/>.</returns>
        public static bool TryGetVideoRate(out float intervalMs, out float fps, out bool isActive)
        {
            VIDEO_METER.GetRate(out intervalMs, out fps, out isActive);
            return true;
        }

        /// <summary>
        /// Records one track / processing-complete event.
        /// </summary>
        public static void TrackTick()
        {
            TRACK_METER.Record();
        }

        /// <summary>
        /// Tries to read the track event rate over the last 1 second.
        /// </summary>
        /// <param name="intervalMs">Mean interval in milliseconds implied by the windowed rate, or 0 when inactive.</param>
        /// <param name="fps">Events per second in the window, or 0 when inactive.</param>
        /// <param name="isActive"><see langword="true"/> when at least one event remains in the window.</param>
        /// <returns>Always <see langword="true"/>.</returns>
        public static bool TryGetTrackRate(out float intervalMs, out float fps, out bool isActive)
        {
            TRACK_METER.GetRate(out intervalMs, out fps, out isActive);
            return true;
        }

        /// <summary>
        /// Clears video and track event windows.
        /// </summary>
        public static void ResetVideoAndTrack()
        {
            VIDEO_METER.Reset();
            TRACK_METER.Reset();
        }

        /// <summary>
        /// Clears render EMA and video / track event windows.
        /// </summary>
        public static void ResetAll()
        {
            _renderEmaMs = 0f;
            ResetVideoAndTrack();
        }

        /// <summary>
        /// Formats a HUD line. When <paramref name="isActive"/> is <see langword="false"/>, interval and FPS are 0.
        /// </summary>
        /// <param name="label">Metric name, for example <c>Render</c> or <c>Video</c>.</param>
        /// <param name="intervalMs">Frame or event interval in milliseconds.</param>
        /// <param name="fps">Frames or events per second.</param>
        /// <param name="isActive">Whether the metric currently has samples.</param>
        /// <returns>A string such as <c>Video: 20.0 ms (50 fps)</c>.</returns>
        public static string FormatRateLine(string label, float intervalMs, float fps, bool isActive)
        {
            if (string.IsNullOrEmpty(label))
            {
                label = string.Empty;
            }

            if (!isActive)
            {
                intervalMs = 0f;
                fps = 0f;
            }

            return string.Format("{0}: {1:0.0} ms ({2:0.} fps)", label, intervalMs, fps);
        }

        public static void AddDebugStr(string str)
        {
            SB.AppendLine(str);
        }

        public static void ClearDebugStr()
        {
            SB.Clear();
        }

        public static string GetDebugStr()
        {
            return SB.ToString();
        }

        public static int GetDebugStrLength()
        {
            return SB.Length;
        }

        private sealed class EventRateMeter
        {
            private readonly Queue<long> _timestamps = new Queue<long>();
            private readonly object _sync = new object();

            public void Record()
            {
                long now = Stopwatch.GetTimestamp();
                lock (_sync)
                {
                    _timestamps.Enqueue(now);
                    TrimUnlocked(now);
                }
            }

            public void Reset()
            {
                lock (_sync)
                {
                    _timestamps.Clear();
                }
            }

            public void GetRate(out float intervalMs, out float fps, out bool isActive)
            {
                long now = Stopwatch.GetTimestamp();
                lock (_sync)
                {
                    TrimUnlocked(now);
                    int count = _timestamps.Count;
                    isActive = count > 0;
                    fps = count / WINDOW_SECONDS;
                    intervalMs = fps > 0f ? 1000f / fps : 0f;
                }
            }

            private void TrimUnlocked(long now)
            {
                long windowTicks = (long)(WINDOW_SECONDS * Stopwatch.Frequency);
                long cutoff = now - windowTicks;
                while (_timestamps.Count > 0 && _timestamps.Peek() < cutoff)
                {
                    _timestamps.Dequeue();
                }
            }
        }
    }
}

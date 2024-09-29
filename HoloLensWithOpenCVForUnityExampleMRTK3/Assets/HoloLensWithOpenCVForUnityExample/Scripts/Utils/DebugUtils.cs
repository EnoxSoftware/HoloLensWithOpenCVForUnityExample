using System;
using System.Collections.Generic;
using System.Text;

namespace HoloLensWithOpenCVForUnityExample
{
    public static class DebugUtils
    {
        private static Queue<long> qRenderTick = new Queue<long>();

        private static Queue<long> qVideoTick = new Queue<long>();

        private static Queue<long> qTrackTick = new Queue<long>();

        private static StringBuilder sb = new StringBuilder(1000);

        public static void RenderTick()
        {
            while (qRenderTick.Count > 49)
            {
                qRenderTick.Dequeue();
            }
            qRenderTick.Enqueue(DateTime.Now.Ticks);
        }

        public static float GetRenderDeltaTime()
        {
            if (qRenderTick.Count == 0)
            {
                return float.PositiveInfinity;
            }
            return (DateTime.Now.Ticks - qRenderTick.Peek()) / 500000.0f;
        }

        public static void VideoTick()
        {
            while (qVideoTick.Count > 49)
            {
                qVideoTick.Dequeue();
            }
            qVideoTick.Enqueue(DateTime.Now.Ticks);
        }

        public static float GetVideoDeltaTime()
        {
            if (qVideoTick.Count == 0)
            {
                return float.PositiveInfinity;
            }
            return (DateTime.Now.Ticks - qVideoTick.Peek()) / 500000.0f;
        }

        public static void TrackTick()
        {
            while (qTrackTick.Count > 49)
            {
                qTrackTick.Dequeue();
            }
            qTrackTick.Enqueue(DateTime.Now.Ticks);
        }

        public static float GetTrackDeltaTime()
        {
            if (qTrackTick.Count == 0)
            {
                return float.PositiveInfinity;
            }
            return (DateTime.Now.Ticks - qTrackTick.Peek()) / 500000.0f;
        }

        public static void AddDebugStr(string str)
        {
            sb.AppendLine(str);
        }

        public static void ClearDebugStr()
        {
            sb.Clear();
        }

        public static string GetDebugStr()
        {
            return sb.ToString();
        }

        public static int GetDebugStrLength()
        {
            return sb.Length;
        }
    }
}
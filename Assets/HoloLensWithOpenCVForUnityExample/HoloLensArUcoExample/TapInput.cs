using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_2017_2_OR_NEWER
using UnityEngine.XR.WSA.Input;
#else
using UnityEngine.VR.WSA.Input;
#endif

namespace HoloLensWithOpenCVForUnityExample
{
    public class TapInput : MonoBehaviour
    {
        public UnityEvent OnTappedEvent;

        GestureRecognizer recognizer;

        void Awake ()
        {
            recognizer = new GestureRecognizer ();
            #if UNITY_2017_2_OR_NEWER
            recognizer.Tapped += (args) => {
            #else
            recognizer.TappedEvent += (source, tapCount, ray) => {
            #endif
                OnTappedEvent.Invoke ();
            };
            recognizer.StartCapturingGestures ();
        }
    }
}
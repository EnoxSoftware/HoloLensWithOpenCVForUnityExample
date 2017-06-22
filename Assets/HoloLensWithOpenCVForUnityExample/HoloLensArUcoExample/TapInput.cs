using System.Collections;
using System.Collections.Generic;
using UnityEngine.VR.WSA.Input;
using UnityEngine;
using UnityEngine.Events;

namespace HoloLensWithOpenCVForUnityExample
{
    public class TapInput : MonoBehaviour
    {

        public UnityEvent OnTappedEvent;

        GestureRecognizer recognizer;

        void Awake ()
        {
            recognizer = new GestureRecognizer ();
            recognizer.TappedEvent += (source, tapCount, ray) => {
                OnTappedEvent.Invoke ();
            };
            recognizer.StartCapturingGestures ();
        }
    }
}
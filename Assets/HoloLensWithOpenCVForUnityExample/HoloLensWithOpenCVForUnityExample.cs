using UnityEngine;
using System.Collections;

#if UNITY_5_3 || UNITY_5_3_OR_NEWER
using UnityEngine.SceneManagement;
#endif

namespace HoloLensWithOpenCVForUnityExample
{
    public class HoloLensWithOpenCVForUnityExample : MonoBehaviour
    {
        // Use this for initialization
        void Start ()
        {
            
        }
        
        // Update is called once per frame
        void Update ()
        {
            
        }

        public void OnShowLicenseButtonClick ()
        {
            #if UNITY_5_3 || UNITY_5_3_OR_NEWER
            SceneManager.LoadScene ("ShowLicense");
            #else
            Application.LoadLevel ("ShowLicense");
            #endif
        }

        public void OnHoloLensPhotoCaptureExampleButtonClick ()
        {
            #if UNITY_5_3 || UNITY_5_3_OR_NEWER
            SceneManager.LoadScene ("HoloLensPhotoCaptureExample");
            #else
            Application.LoadLevel ("HoloLensPhotoCaptureExample");
            #endif
        }

        public void OnHoloLensComicFilterExampleButtonClick ()
        {
            #if UNITY_5_3 || UNITY_5_3_OR_NEWER
            SceneManager.LoadScene ("HoloLensComicFilterExample");
            #else
            Application.LoadLevel ("HoloLensComicFilterExample");
            #endif
        }
        
        public void OnHoloLensFaceDetectionExampleButtonClick ()
        {
            #if UNITY_5_3 || UNITY_5_3_OR_NEWER
            SceneManager.LoadScene ("HoloLensFaceDetectionExample");
            #else
            Application.LoadLevel ("HoloLensFaceDetectionExample");
            #endif
        }

        public void OnHoloLensFaceDetectionOverlayExampleButtonClick ()
        {
            #if UNITY_5_3 || UNITY_5_3_OR_NEWER
            SceneManager.LoadScene ("HoloLensFaceDetectionOverlayExample");
            #else
            Application.LoadLevel ("HoloLensFaceDetectionOverlayExample");
            #endif
        }

        public void OnHoloLensArUcoExampleButtonClick ()
        {
            #if UNITY_5_3 || UNITY_5_3_OR_NEWER
            SceneManager.LoadScene ("HoloLensArUcoExample");
            #else
            Application.LoadLevel ("HoloLensArUcoExample");
            #endif
        }
    }
}
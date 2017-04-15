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

        public void OnShowLicenseButton ()
        {
            #if UNITY_5_3 || UNITY_5_3_OR_NEWER
            SceneManager.LoadScene ("ShowLicense");
            #else
            Application.LoadLevel ("ShowLicense");
            #endif
        }

        public void OnHoloLensPhotoCaptureExample ()
        {
            #if UNITY_5_3 || UNITY_5_3_OR_NEWER
            SceneManager.LoadScene ("HoloLensPhotoCaptureExample");
            #else
            Application.LoadLevel ("HoloLensPhotoCaptureExample");
            #endif
        }

        public void OnHoloLensComicFilterExample ()
        {
            #if UNITY_5_3 || UNITY_5_3_OR_NEWER
            SceneManager.LoadScene ("HoloLensComicFilterExample");
            #else
            Application.LoadLevel ("HoloLensComicFilterExample");
            #endif
        }
        
        public void OnHoloLensWebCamTextureAsyncDetectFaceExample ()
        {
            #if UNITY_5_3 || UNITY_5_3_OR_NEWER
            SceneManager.LoadScene ("HoloLensWebCamTextureAsyncDetectFaceExample");
            #else
            Application.LoadLevel ("HoloLensWebCamTextureAsyncDetectFaceExample");
            #endif
        }

        public void OnHoloLensWebCamTextureAsyncDetectFaceOverlayExample ()
        {
            #if UNITY_5_3 || UNITY_5_3_OR_NEWER
            SceneManager.LoadScene ("HoloLensWebCamTextureAsyncDetectFaceOverlayExample");
            #else
            Application.LoadLevel ("HoloLensWebCamTextureAsyncDetectFaceOverlayExample");
            #endif
        }

        public void OnHoloLensAnonymousFaceExample ()
        {
            #if UNITY_5_3 || UNITY_5_3_OR_NEWER
            SceneManager.LoadScene ("HoloLensAnonymousFaceExample");
            #else
            Application.LoadLevel ("HoloLensAnonymousFaceExample");
            #endif
        }


        public void OnHoloLensArUcoWebCamTextureExample ()
        {
            #if UNITY_5_3 || UNITY_5_3_OR_NEWER
            SceneManager.LoadScene ("HoloLensArUcoWebCamTextureExample");
            #else
            Application.LoadLevel ("HoloLensArUcoWebCamTextureExample");
            #endif
        }
    }
}
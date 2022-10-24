using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using OpenCVForUnity.CoreModule;

namespace HoloLensWithOpenCVForUnityExample
{
    /// <summary>
    /// HoloLensWithOpenCVForUnity Example
    /// </summary>
    public class HoloLensWithOpenCVForUnityExample : MonoBehaviour
    {
        public Text exampleTitle;
        public Text versionInfo;
        public ScrollRect scrollRect;
        static float verticalNormalizedPosition = 1f;

        // Use this for initialization
        protected void Start()
        {
            exampleTitle.text = "HoloLensWithOpenCVForUnity Example " + Application.version;

            versionInfo.text = Core.NATIVE_LIBRARY_NAME + " " + OpenCVForUnity.UnityUtils.Utils.getVersion() + " (" + Core.VERSION + ")";
            versionInfo.text += " / UnityEditor " + Application.unityVersion;
            versionInfo.text += " / ";

#if UNITY_EDITOR
            versionInfo.text += "Editor";
#elif UNITY_STANDALONE_WIN
            versionInfo.text += "Windows";
#elif UNITY_STANDALONE_OSX
            versionInfo.text += "Mac OSX";
#elif UNITY_STANDALONE_LINUX
            versionInfo.text += "Linux";
#elif UNITY_ANDROID
            versionInfo.text += "Android";
#elif UNITY_IOS
            versionInfo.text += "iOS";
#elif UNITY_WSA
            versionInfo.text += "WSA";
#elif UNITY_WEBGL
            versionInfo.text += "WebGL";
#endif
            versionInfo.text += " ";
#if ENABLE_MONO
            versionInfo.text += "Mono";
#elif ENABLE_IL2CPP
            versionInfo.text += "IL2CPP";
#elif ENABLE_DOTNET
            versionInfo.text += ".NET";
#endif

            versionInfo.text += " / ";

#if XR_PLUGIN_WINDOWSMR
            versionInfo.text += "XR_PLUGIN_WINDOWSMR";
#elif XR_PLUGIN_OPENXR
            versionInfo.text += "XR_PLUGIN_OPENXR";
#elif BUILTIN_XR
            versionInfo.text += "BUILTIN_XR";
#else
            versionInfo.text += "XR system unknown";
#endif

            scrollRect.verticalNormalizedPosition = verticalNormalizedPosition;
        }

        // Update is called once per frame
        void Update()
        {

        }

        public void OnScrollRectValueChanged()
        {
            verticalNormalizedPosition = scrollRect.verticalNormalizedPosition;
        }


        public void OnShowLicenseButtonClick()
        {
            SceneManager.LoadScene("ShowLicense");
        }

        public void OnHLPhotoCaptureExampleButtonClick()
        {
            SceneManager.LoadScene("HLPhotoCaptureExample");
        }

        public void OnHLCameraStreamToMatHelperExampleButtonClick()
        {
            SceneManager.LoadScene("HLCameraStreamToMatHelperExample");
        }

        public void OnHLFaceDetectionExampleButtonClick()
        {
            SceneManager.LoadScene("HLFaceDetectionExample");
        }

        public void OnHLArUcoExampleButtonClick()
        {
            SceneManager.LoadScene("HLArUcoExample");
        }

        public void OnHLArUcoCameraCalibrationExampleButtonClick()
        {
            SceneManager.LoadScene("HLArUcoCameraCalibrationExample");
        }
        public void OnHLCameraIntrinsicsCheckerButtonClick()
        {
            SceneManager.LoadScene("HLCameraIntrinsicsChecker");
        }
    }
}
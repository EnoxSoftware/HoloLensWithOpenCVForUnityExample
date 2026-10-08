using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityIntegration;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HoloLensWithOpenCVForUnityExample
{
    /// <summary>
    /// HoloLensWithOpenCVForUnity Example
    /// </summary>
    public class HoloLensWithOpenCVForUnityExample : MonoBehaviour
    {
        // Constants
#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        private static float _verticalNormalizedPosition = 1f;

        // Public Fields
        public Text ExampleTitle;
        public Text VersionInfo;
        public ScrollRect ScrollRect;

        // Unity Lifecycle Methods
        private void Start()
        {
            ExampleTitle.text = "HoloLensWithOpenCVForUnity Example " + Application.version;

            VersionInfo.text = Core.NATIVE_LIBRARY_NAME + " " + OpenCVForUnityEnv.GetVersion() + " (" + Core.VERSION + ")";
            VersionInfo.text += " / UnityEditor " + Application.unityVersion;
            VersionInfo.text += " / ";

#if UNITY_EDITOR
            VersionInfo.text += "Editor";
#elif UNITY_STANDALONE_WIN
            VersionInfo.text += "Windows";
#elif UNITY_STANDALONE_OSX
            VersionInfo.text += "Mac OSX";
#elif UNITY_STANDALONE_LINUX
            VersionInfo.text += "Linux";
#elif UNITY_ANDROID
            VersionInfo.text += "Android";
#elif UNITY_IOS
            VersionInfo.text += "iOS";
#elif UNITY_WSA
            VersionInfo.text += "WSA";
#elif UNITY_WEBGL
            VersionInfo.text += "WebGL";
#endif
            VersionInfo.text += " ";
#if ENABLE_MONO
            VersionInfo.text += "Mono";
#elif ENABLE_IL2CPP
            VersionInfo.text += "IL2CPP";
#elif ENABLE_DOTNET
            VersionInfo.text += ".NET";
#endif

            VersionInfo.text += " / ";

#if XR_PLUGIN_WINDOWSMR
            VersionInfo.text += "XR_PLUGIN_WINDOWSMR";
#elif XR_PLUGIN_OPENXR
            VersionInfo.text += "XR_PLUGIN_OPENXR";
#elif BUILTIN_XR
            VersionInfo.text += "BUILTIN_XR";
#else
            VersionInfo.text += "XR system unknown";
#endif

            ScrollRect.verticalNormalizedPosition = _verticalNormalizedPosition;
        }

        // Public Methods
        public void OnScrollRectValueChanged()
        {
            _verticalNormalizedPosition = ScrollRect.verticalNormalizedPosition;
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

        public void OnHLCameraIntrinsicsCheckerButtonClick()
        {
            SceneManager.LoadScene("HLCameraIntrinsicsChecker");
        }
    }
}

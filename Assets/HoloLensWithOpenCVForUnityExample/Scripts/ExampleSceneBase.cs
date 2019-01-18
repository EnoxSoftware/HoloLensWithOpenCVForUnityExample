#pragma warning disable 0109
using UnityEngine;
using UnityEngine.SceneManagement;
using HoloToolkit.Unity.InputModule;
using HoloToolkit.Unity;

namespace HoloLensWithOpenCVForUnityExample
{
    // Prevent HoloToolKit InputManager Singletons from working multiple scenes.
    public class ExampleSceneBase : MonoBehaviour {

        protected new MixedRealityCameraManager camera;
        protected HoloToolkit.Unity.InputModule.Cursor cursor;
        protected InputManager input;

	    // Use this for initialization
        protected virtual void Start () {
            camera = FindObjectOfType<MixedRealityCameraManager>();
            cursor = FindObjectOfType<HoloToolkit.Unity.InputModule.Cursor>();
            input = FindObjectOfType<InputManager>();
	    }        
	
        protected virtual void LoadScene(string sceneName) {
            Destroy(camera.transform.parent.gameObject);
            Destroy(cursor.gameObject);
            Destroy(input.gameObject);

            // Avoids the error that is caused by the absence of the camera in the scene.
           Camera cam = gameObject.AddComponent<Camera> ();
            cam.backgroundColor = Color.black;
            CameraCache.Refresh (cam);

            SceneManager.LoadScene (sceneName);
        }
    }
}

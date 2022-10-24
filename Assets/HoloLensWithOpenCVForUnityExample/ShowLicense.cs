using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace HoloLensWithOpenCVForUnityExample
{
    public class ShowLicense : MonoBehaviour
    {
        // Use this for initialization
        protected void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        /// <summary>
        /// Raises the back button click event.
        /// </summary>
        public void OnBackButtonClick()
        {
            SceneManager.LoadScene("HoloLensWithOpenCVForUnityExample");
        }
    }
}

using UnityEngine;
using System.Collections;

namespace HoloLensWithOpenCVForUnityExample
{
    public class HoloLensWithOpenCVForUnityExample : ExampleSceneBase
    {
        // Use this for initialization
        protected override void Start ()
        {
            base.Start ();
        }
        
        // Update is called once per frame
        void Update ()
        {
            
        }

        public void OnShowLicenseButtonClick ()
        {
            LoadScene ("ShowLicense");
        }

        public void OnHoloLensPhotoCaptureExampleButtonClick ()
        {
            LoadScene ("HoloLensPhotoCaptureExample");
        }

        public void OnHoloLensComicFilterExampleButtonClick ()
        {
            LoadScene ("HoloLensComicFilterExample");
        }
        
        public void OnHoloLensFaceDetectionExampleButtonClick ()
        {
            LoadScene ("HoloLensFaceDetectionExample");
        }

        public void OnHoloLensFaceDetectionOverlayExampleButtonClick ()
        {
            LoadScene ("HoloLensFaceDetectionOverlayExample");
        }

        public void OnHoloLensArUcoExampleButtonClick ()
        {
            LoadScene ("HoloLensArUcoExample");
        }

        public void OnHoloLensArUcoCameraCalibrationExampleButtonClick ()
        {
            LoadScene ("HoloLensArUcoCameraCalibrationExample");
        }
    }
}
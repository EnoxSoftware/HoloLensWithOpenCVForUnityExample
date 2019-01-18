/*
 * Hololens Camera Intrinsics Checker Helper
*/

using UnityEngine;

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.UI;

#if WINDOWS_UWP
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Media.Devices.Core;
#endif

namespace HololensCameraIntrinsics
{
    public class CameraIntrinsicsCheckerHelper : MonoBehaviour
    {
        public Text ResultText;

        #if WINDOWS_UWP
        CameraIntrinsicsChecker cameraIntrinsicsChecker;

        // Use this for initialization
        void Start ()
        {
            CameraIntrinsicsChecker.CreateAync (OnCameraIntrinsicsCheckerInstanceCreated);
        }

        void OnDestroy()
        {
            if (cameraIntrinsicsChecker != null)
                cameraIntrinsicsChecker.Dispose ();
        }

        private void OnCameraIntrinsicsCheckerInstanceCreated(CameraIntrinsicsChecker checker)
        {
            if (checker == null)
            {
                Debug.LogError("Creating the CameraIntrinsicsChecker object failed.");
                return;
            }

            this.cameraIntrinsicsChecker = checker;

            checker.GetCameraIntrinsicsAync (OnCameraIntrinsicsGot);
        }

        private void OnCameraIntrinsicsGot(CameraIntrinsics cameraIntrinsics, VideoEncodingProperties property)
        {
            if (cameraIntrinsics == null)
            {
                Debug.LogError("Getting the CameraIntrinsics object failed.");
                return;
            }


            double calculatedFrameRate = (double)property.FrameRate.Numerator / (double)property.FrameRate.Denominator;

            String result = "\n" + "=============================================";
            result += "\n" + "==== Size: " + property.Width + "x" + property.Height + " FrameRate: " + (int)Math.Round(calculatedFrameRate) + "====";
            result += "\n" + "FocalLength: " + cameraIntrinsics.FocalLength;
            result += "\n" + "ImageHeight: " + cameraIntrinsics.ImageHeight;
            result += "\n" + "ImageWidth: " + cameraIntrinsics.ImageWidth;
            result += "\n" + "PrincipalPoint: " + cameraIntrinsics.PrincipalPoint;
            result += "\n" + "RadialDistortion: " + cameraIntrinsics.RadialDistortion;
            result += "\n" + "TangentialDistortion: " + cameraIntrinsics.TangentialDistortion;
            result += "\n" + "=============================================";

            Debug.Log(result);

            UnityEngine.WSA.Application.InvokeOnAppThread(() =>
            {
                ResultText.text += result;
            }, false);
            

        }
        #endif
    }

    #if WINDOWS_UWP
    public class CameraIntrinsicsChecker
    {
        public delegate void OnVideoCaptureResourceCreatedCallback(CameraIntrinsicsChecker chakerObject);

        public delegate void OnCameraIntrinsicsGotCallback(CameraIntrinsics cameraIntrinsics, VideoEncodingProperties property);

        public bool IsStreaming
        {
            get
            {
                return _frameReader != null;
            }
        }

        static readonly MediaStreamType STREAM_TYPE = MediaStreamType.VideoPreview;

        MediaFrameSourceGroup _frameSourceGroup;
        MediaFrameSourceInfo _frameSourceInfo;
        DeviceInformation _deviceInfo;
        MediaCapture _mediaCapture;
        MediaFrameReader _frameReader;

        CameraIntrinsicsChecker(MediaFrameSourceGroup frameSourceGroup, MediaFrameSourceInfo frameSourceInfo, DeviceInformation deviceInfo)
        {
            _frameSourceGroup = frameSourceGroup;
            _frameSourceInfo = frameSourceInfo;
            _deviceInfo = deviceInfo;
        }

        public static async void CreateAync(OnVideoCaptureResourceCreatedCallback onCreatedCallback)
        {
            var allFrameSourceGroups = await MediaFrameSourceGroup.FindAllAsync();                                              //Returns IReadOnlyList<MediaFrameSourceGroup>
            var candidateFrameSourceGroups = allFrameSourceGroups.Where(group => group.SourceInfos.Any(IsColorVideo));   //Returns IEnumerable<MediaFrameSourceGroup>
            var selectedFrameSourceGroup = candidateFrameSourceGroups.FirstOrDefault();                                         //Returns a single MediaFrameSourceGroup

            if (selectedFrameSourceGroup == null)
            {
                onCreatedCallback?.Invoke(null);
                return;
            }

            var selectedFrameSourceInfo = selectedFrameSourceGroup.SourceInfos.FirstOrDefault(); //Returns a MediaFrameSourceInfo

            if (selectedFrameSourceInfo == null)
            {
                onCreatedCallback?.Invoke(null);
                return;
            }

            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);   //Returns DeviceCollection
            var deviceInformation = devices.FirstOrDefault();                               //Returns a single DeviceInformation

            if (deviceInformation == null)
            {
                onCreatedCallback(null);
                return;
            }

            var cameraIntrinsicsChecker = new CameraIntrinsicsChecker(selectedFrameSourceGroup, selectedFrameSourceInfo, deviceInformation);
            await cameraIntrinsicsChecker.CreateMediaCaptureAsync();
            onCreatedCallback?.Invoke(cameraIntrinsicsChecker);
        }

        public async void GetCameraIntrinsicsAync(OnCameraIntrinsicsGotCallback onGotCallback)
        {
            Camera​Intrinsics cameraIntrinsics = null;

            // Start video
            var mediaFrameSource = _mediaCapture.FrameSources[_frameSourceInfo.Id]; //Returns a MediaFrameSource

            if (mediaFrameSource == null)
            {
                onGotCallback?.Invoke(null, null);
                return;
            }

            var pixelFormat = MediaEncodingSubtypes.Bgra8;
            _frameReader = await _mediaCapture.CreateFrameReaderAsync(mediaFrameSource, pixelFormat);

            await _frameReader.StartAsync();

            IEnumerable<VideoEncodingProperties> allProperties = _mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(STREAM_TYPE).Select(x => x as VideoEncodingProperties);

            foreach (var property in allProperties)
            {
                await _mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(STREAM_TYPE, property);
                
                // Get CameraIntrinsics
                var taskCompletionSource = new TaskCompletionSource<bool>();

                TypedEventHandler<MediaFrameReader, MediaFrameArrivedEventArgs> handler = null;
                handler = (MediaFrameReader sender, MediaFrameArrivedEventArgs args) =>
                {
                    using (var frameReference = _frameReader.TryAcquireLatestFrame()) //frame: MediaFrameReference
                    {
                        if (frameReference != null)
                        {
                            cameraIntrinsics = frameReference.VideoMediaFrame.CameraIntrinsics;

                            taskCompletionSource.SetResult(true);
                        }
                        else
                        {
                            taskCompletionSource.SetResult(false);
                        }
                    }
                    _frameReader.FrameArrived -= handler;
                };
                _frameReader.FrameArrived += handler;

                var result = await taskCompletionSource.Task;

                if (result == false)
                {
                    onGotCallback?.Invoke(null, null);
                    return;
                }

                onGotCallback?.Invoke(cameraIntrinsics, property);
            }

            // Stop video
            await _frameReader.StopAsync();
            _frameReader.Dispose();
            _frameReader = null;
        }

        public void Dispose()
        {
            if (IsStreaming)
            {
                throw new Exception("Please make sure StopVideoModeAsync() is called before displosing the VideoCapture object.");
            }

            _mediaCapture?.Dispose();
        }

        async Task CreateMediaCaptureAsync()
        {
            if (_mediaCapture != null)
            {
                throw new Exception("The MediaCapture object has already been created.");
            }

            _mediaCapture = new MediaCapture();
            await _mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings()
                {
                    VideoDeviceId = _deviceInfo.Id,
                    SourceGroup = _frameSourceGroup,
                    MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                    StreamingCaptureMode = StreamingCaptureMode.Video
                });
            _mediaCapture.VideoDeviceController.Focus.TrySetAuto(true);
        }

        static bool IsColorVideo(MediaFrameSourceInfo sourceInfo)
        {
            return (sourceInfo.MediaStreamType == STREAM_TYPE &&
                sourceInfo.SourceKind == MediaFrameSourceKind.Color);
        }
    }
    #endif
}

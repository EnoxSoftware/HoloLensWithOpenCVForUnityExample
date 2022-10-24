using UnityEngine;
using UnityEngine.UI;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

#if WINDOWS_UWP
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Media.Devices.Core;
#endif

namespace HoloLensWithOpenCVForUnityExample
{
    /// <summary>
    /// Hololens Camera Intrinsics Checker Helper
    /// An example for displaying camera resolutions and camera Intrinsics available on the hololens device.
    /// </summary>
    public class HLCameraIntrinsicsCheckerHelper : MonoBehaviour
    {
        public Text ResultText;

#if WINDOWS_UWP
        CameraIntrinsicsChecker cameraIntrinsicsChecker;

        readonly static Queue<Action> ExecuteOnMainThread = new Queue<Action>();

        // Use this for initialization
        void Start()
        {
            CameraIntrinsicsChecker.CreateAync(OnCameraIntrinsicsCheckerInstanceCreated);
        }

        void OnDestroy()
        {
            if (cameraIntrinsicsChecker != null)
                cameraIntrinsicsChecker.Dispose();
        }

        private void OnCameraIntrinsicsCheckerInstanceCreated(CameraIntrinsicsChecker checker)
        {
            if (checker == null)
            {
                Debug.LogError("Creating the CameraIntrinsicsChecker object failed.");
                return;
            }

            Enqueue(() =>
            {
                if (CameraIntrinsicsChecker._hololensDevice == 1)
                {
                    ResultText.text += "\n" + "Device: HoloLens 1";
                }
                else if (CameraIntrinsicsChecker._hololensDevice == 2)
                {
                    ResultText.text += "\n" + "Device: HoloLens 2";
                }
            });

            this.cameraIntrinsicsChecker = checker;

            checker.GetCameraIntrinsicsAync(OnCameraIntrinsicsGot);
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

            Enqueue(() =>
            {
                ResultText.text += result;
            });

        }
        private void Update()
        {
            lock (ExecuteOnMainThread)
            {
                while (ExecuteOnMainThread.Count > 0)
                {
                    ExecuteOnMainThread.Dequeue().Invoke();
                }
            }
        }

        private void Enqueue(Action action)
        {
            lock (ExecuteOnMainThread)
            {
                ExecuteOnMainThread.Enqueue(action);
            }
        }
#endif

        /// <summary>
        /// Raises the back button click event.
        /// </summary>
        public void OnBackButtonClick()
        {
            SceneManager.LoadScene("HoloLensWithOpenCVForUnityExample");
        }
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

        static public int _hololensDevice = 0;
        static public MediaStreamType _mediaStreamType = MediaStreamType.VideoPreview;

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
            // Whether it is running on HoloLens 1 or HoloLens 2.
            // from https://github.com/qian256/HoloLensARToolKit/blob/bef36a89f191ab7d389d977c46639376069bbed6/HoloLensARToolKit/Assets/ARToolKitUWP/Scripts/ARUWPVideo.cs#L279
            var allGroups = await MediaFrameSourceGroup.FindAllAsync();
            int selectedGroupIndex = -1;
            for (int i = 0; i < allGroups.Count; i++)
            {
                var group = allGroups[i];

                if (group.DisplayName == "MN34150")
                {
                    _hololensDevice = 1;
                    _mediaStreamType = MediaStreamType.VideoPreview;
                    selectedGroupIndex = i;
                    break;
                }
                else if (group.DisplayName == "QC Back Camera")
                {
                    _hololensDevice = 2;
                    _mediaStreamType = MediaStreamType.VideoRecord;
                    selectedGroupIndex = i;
                    break;
                }
            }

            MediaFrameSourceGroup selectedFrameSourceGroup = null;

            if (selectedGroupIndex != -1)
            {
                selectedFrameSourceGroup = allGroups[selectedGroupIndex];
            }
            else
            {
                var candidateFrameSourceGroups = allGroups.Where(group => group.SourceInfos.Any(IsColorVideo));          //Returns IEnumerable<MediaFrameSourceGroup>
                selectedFrameSourceGroup = candidateFrameSourceGroups.FirstOrDefault();                                         //Returns a single MediaFrameSourceGroup
            }

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
                onCreatedCallback?.Invoke(null);
                return;
            }

            var cameraIntrinsicsChecker = new CameraIntrinsicsChecker(selectedFrameSourceGroup, selectedFrameSourceInfo, deviceInformation);
            await cameraIntrinsicsChecker.CreateMediaCaptureAsync();
            onCreatedCallback?.Invoke(cameraIntrinsicsChecker);
        }

        public async void GetCameraIntrinsicsAync(OnCameraIntrinsicsGotCallback onGotCallback)
        {
            CameraIntrinsics cameraIntrinsics = null;

            // Start video
            MediaFrameSource mediaFrameSource = _mediaCapture.FrameSources.Values.Single(x => x.Info.MediaStreamType == _mediaStreamType);

            if (mediaFrameSource == null)
            {
                onGotCallback?.Invoke(null, null);
                return;
            }

            var pixelFormat = MediaEncodingSubtypes.Bgra8;
            _frameReader = await _mediaCapture.CreateFrameReaderAsync(mediaFrameSource, pixelFormat);

            await _frameReader.StartAsync();

            IEnumerable<VideoEncodingProperties> allProperties = _mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(_mediaStreamType).Select(x => x as VideoEncodingProperties);

            foreach (var property in allProperties)
            {
                await _mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(_mediaStreamType, property);

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

            
            // from https://github.com/qian256/HoloLensARToolKit/blob/bef36a89f191ab7d389d977c46639376069bbed6/HoloLensARToolKit/Assets/ARToolKitUWP/Scripts/ARUWPVideo.cs#L301
            _mediaCapture = new MediaCapture();
            if (_hololensDevice == 1 || _hololensDevice == 0)
            {
                var settings = new MediaCaptureInitializationSettings
                {
                    SourceGroup = _frameSourceGroup,
                    // This media capture can share streaming with other apps.
                    //SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                    SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                    // Only stream video and don't initialize audio capture devices.
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                    // Set to CPU to ensure frames always contain CPU SoftwareBitmap images
                    // instead of preferring GPU D3DSurface images.
                    MemoryPreference = MediaCaptureMemoryPreference.Cpu
                };
                await _mediaCapture.InitializeAsync(settings);
            }
            else if (_hololensDevice == 2)
            {
                string deviceId = _frameSourceGroup.Id;
                // Look up for all video profiles
                IReadOnlyList<MediaCaptureVideoProfile> profileList = MediaCapture.FindKnownVideoProfiles(deviceId, KnownVideoProfile.VideoConferencing);

                // Initialize mediacapture with the source group.
                var settings = new MediaCaptureInitializationSettings
                {
                    VideoDeviceId = deviceId,
                    VideoProfile = profileList[0],
                    // This media capture can share streaming with other apps.
                    SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                    // Only stream video and don't initialize audio capture devices.
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                    // Set to CPU to ensure frames always contain CPU SoftwareBitmap images
                    // instead of preferring GPU D3DSurface images.
                    MemoryPreference = MediaCaptureMemoryPreference.Cpu
                };
                await _mediaCapture.InitializeAsync(settings);
            }

            _mediaCapture.VideoDeviceController.Focus.TrySetAuto(true);
        }

        static bool IsColorVideo(MediaFrameSourceInfo sourceInfo)
        {
            return (sourceInfo.MediaStreamType == _mediaStreamType &&
                sourceInfo.SourceKind == MediaFrameSourceKind.Color);
        }
    }
#endif
}

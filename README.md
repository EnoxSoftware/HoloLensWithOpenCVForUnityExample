# HoloLens With OpenCVForUnity Example


## Demo Video (old version)
[![](http://img.youtube.com/vi/SdzsedkTpCI/0.jpg)](https://youtu.be/SdzsedkTpCI)


## Environment
* HoloLens2 22621.1399
* Windows 10 SDK 10.0.19041.0 / 10.0.22621.0
* Unity 2023.3.62f3 (Built-in Render Pipeline / OpenXR 1.11.2 / MRTK 3.3.0 / DirectX 11 / Visual Studio 2022 MSVC v143)
* [OpenCV for Unity](https://assetstore.unity.com/packages/tools/integration/opencv-for-unity-21088?aid=1011l4ehR) 3.0.4+ 
* [EnoxSoftware/HoloLensCameraStream](https://github.com/EnoxSoftware/HoloLensCameraStream)


---

## **:warning:IMPORTANT**: Development Environment Setup
The setup procedure for HoloLens projects using Unity and MRTK has changed significantly. The legacy Mixed Reality Feature Tool no longer functions on the latest Windows 11 environments.


### Prerequisites (OpenXR Stack)

To run this project, you must import the following assets:

* Unity OpenXR Plugin 1.14.3
* [Microsoft Mixed Reality OpenXR Plugin](https://github.com/microsoft/OpenXR-Unity-MixedReality-Samples) 1.11.2
* [Mixed Reality Graphics Tools](https://github.com/microsoft/MixedReality-GraphicsTools-Unity) 0.8.1
* [MRTK3](https://github.com/MixedRealityToolkit/MixedRealityToolkit-Unity) packages listed below

The MRTK3 package versions used by this project are based on the following GitHub release:

* [MRTK3 GitHub Release: Core v3.3.0](https://github.com/MixedRealityToolkit/MixedRealityToolkit-Unity/releases/tag/core-v3.3.0)

> **Note:** MRTK3 packages are independently versioned. There is no single unified MRTK3 version number. The versions below correspond to the packages included in the above release.

| Package                                          | Version |
| ------------------------------------------------ | ------: |
| `org.mixedrealitytoolkit.core`                   |   3.3.0 |
| `org.mixedrealitytoolkit.input`                  |   3.3.0 |
| `org.mixedrealitytoolkit.uxcore`                 |   3.3.0 |
| `org.mixedrealitytoolkit.spatialmanipulation`    |   3.4.0 |
| `org.mixedrealitytoolkit.uxcomponents`           |   3.4.0 |
| `org.mixedrealitytoolkit.uxcomponents.noncanvas` |   3.1.5 |
| `org.mixedrealitytoolkit.standardassets`         |   3.2.1 |

### Installation Order

Please install the packages in the following specific order to ensure all dependencies are resolved correctly:

1. **Unity OpenXR Plugin**

   * Add the following line to the `dependencies` section of the `manifest.json` file:

     ```json
     "com.unity.xr.openxr": "1.14.3",
     ```

2. **Microsoft Mixed Reality OpenXR Plugin**

   * Add the following line to the `dependencies` section of the `manifest.json` file:

     ```json
     "com.microsoft.mixedreality.openxr": "1.11.2",
     ```

3. **MRTK Graphics Tools**

   * Add the following line to the `dependencies` section of the `manifest.json` file:

     ```json
     "com.microsoft.mrtk.graphicstools.unity": "https://github.com/microsoft/MixedReality-GraphicsTools-Unity.git?path=/com.microsoft.mrtk.graphicstools.unity#v0.8.1",
     ```

4. **MRTK Core Definitions (`org.mixedrealitytoolkit.core` 3.3.0)**

   * Download `org.mixedrealitytoolkit.core-3.3.0.tgz` from the [MRTK3 Core v3.3.0 GitHub release](https://github.com/MixedRealityToolkit/MixedRealityToolkit-Unity/releases/tag/core-v3.3.0).
   * Import it using UPM's **Add package from tarball...** option.

5. **MRTK Input (`org.mixedrealitytoolkit.input` 3.3.0)**

   * Download `org.mixedrealitytoolkit.input-3.3.0.tgz` from the same [MRTK3 GitHub release](https://github.com/MixedRealityToolkit/MixedRealityToolkit-Unity/releases/tag/core-v3.3.0).
   * Import it using UPM's **Add package from tarball...** option.

6. **MRTK UX Core (`org.mixedrealitytoolkit.uxcore` 3.3.0)**

   * Download `org.mixedrealitytoolkit.uxcore-3.3.0.tgz` from the same [MRTK3 GitHub release](https://github.com/MixedRealityToolkit/MixedRealityToolkit-Unity/releases/tag/core-v3.3.0).
   * Import it using UPM's **Add package from tarball...** option.

7. **MRTK Spatial Manipulation (`org.mixedrealitytoolkit.spatialmanipulation` 3.4.0)**

   * Download `org.mixedrealitytoolkit.spatialmanipulation-3.4.0.tgz` from the same [MRTK3 GitHub release](https://github.com/MixedRealityToolkit/MixedRealityToolkit-Unity/releases/tag/core-v3.3.0).
   * Import it using UPM's **Add package from tarball...** option.

8. **MRTK Standard Assets (`org.mixedrealitytoolkit.standardassets` 3.2.1)**

   * Download `org.mixedrealitytoolkit.standardassets-3.2.1.tgz` from the same [MRTK3 GitHub release](https://github.com/MixedRealityToolkit/MixedRealityToolkit-Unity/releases/tag/core-v3.3.0).
   * Import it using UPM's **Add package from tarball...** option.

9. **MRTK UX Components (`org.mixedrealitytoolkit.uxcomponents` 3.4.0)**

   * Download `org.mixedrealitytoolkit.uxcomponents-3.4.0.tgz` from the same [MRTK3 GitHub release](https://github.com/MixedRealityToolkit/MixedRealityToolkit-Unity/releases/tag/core-v3.3.0).
   * Import it using UPM's **Add package from tarball...** option.

10. **MRTK UX Components (Non-Canvas) (`org.mixedrealitytoolkit.uxcomponents.noncanvas` 3.1.5)**

    * Download `org.mixedrealitytoolkit.uxcomponents.noncanvas-3.1.5.tgz` from the same [MRTK3 GitHub release](https://github.com/MixedRealityToolkit/MixedRealityToolkit-Unity/releases/tag/core-v3.3.0).
    * Import it using UPM's **Add package from tarball...** option.

* [Choosing a Unity version and XR plugin](https://learn.microsoft.com/en-us/windows/mixed-reality/develop/unity/choosing-unity-version)

---


## Setup (Unity 2022 / Built-in Render Pipeline / OpenXR / MRTK 3 / DirectX 11 / Visual Studio 2022)

1. **Download the latest release `.unitypackage`.**

   * [HoloLensWithOpenCVForUnityExampleMRTK3.unitypackage](https://github.com/EnoxSoftware/HoloLensWithOpenCVForUnityExample/releases)

2. **Create a new Unity project.**

   * Use `HoloLensWithOpenCVForUnityExample` as the project name.
   * Change the platform to `UWP` in the **Build Settings** window.

3. **Import and configure the Unity OpenXR Plugin, Microsoft Mixed Reality OpenXR Plugin, Mixed Reality Graphics Tools, and MRTK3.**

4. **Import OpenCVForUnity.**

   * Select **Tools > OpenCV for Unity > Open Setup Tools**.
   * Click the **Move StreamingAssets Folder** button.
   * Keep the following files and delete the rest:

     * `StreamingAssets/OpenCVForUnity/objdetect/haarcascade_frontalface_alt.xml`
     * `StreamingAssets/OpenCVForUnity/objdetect/lbpcascade_frontalface.xml`

5. **Clone the HoloLensCameraStream repository.**

   * Copy the `HoloLensCameraStream/HoloLensVideoCaptureExample/Assets/CamStream/` folder into the project's `Assets/` folder.

6. **Import `HoloLensWithOpenCVForUnityExampleMRTK3.unitypackage`.**

7. **Add the Unity scene files to the build list.**

   * In the **Build Settings** window, add all `Assets/HoloLensWithOpenCVForUnityExample/*.unity` files to the **Scenes In Build** list.

8. **Configure the project settings.**

   * Add `XR_PLUGIN_OPENXR` to the **Scripting Define Symbols** list.
   * Enable the **WebCam** capability in the **Publishing Settings** tab.

9. **(Optional) Configure performance settings for HoloLens.**

   * See Microsoft's [recommended settings for Unity](https://docs.microsoft.com/en-us/windows/mixed-reality/develop/unity/recommended-settings-for-unity).

10. **Build the project.**

    * In the **Build Settings** window, click **Build**.
    * In the folder selection dialog, create a new folder named `App` next to the `Assets` folder.
    * Select the `App` folder as the build destination.
    * Unity will generate a Visual Studio solution in this folder.

11. **Open the Visual Studio solution.**

    * Open the generated solution:
      `App/HoloLensWithOpenCVForUnityExample.sln`
    * This Visual Studio solution is used to deploy the application to your HoloLens.

12. **Configure the deployment settings.**

    * In the Visual Studio toolbar, select the appropriate solution platform:

      * `x86` for HoloLens 1
      * `ARM64` for HoloLens 2
    * Set the deployment target (the green **Start** button) to:

      * `Device` if the HoloLens is connected to your computer via USB.
      * `Remote Machine` if the HoloLens is connected via Wi-Fi.

13. **Deploy and run the application.**

    * Select **Debug > Start Debugging** in Visual Studio.
    * Once the application is deployed to the HoloLens, you can check the deployment status and application output in the **Output** window.
    * Print the AR marker `CanonicalMarker-d10-i1-sp500-bb1.pdf` and `ArUcoMarkers_DICT_4X4_50_0-8.pdf` on A4-size paper for use with the sample.

### Additional Resources

* [Choosing a Unity version and XR plugin](https://learn.microsoft.com/en-us/windows/mixed-reality/develop/unity/choosing-unity-version)
* [Known issues in Unity versions and packages](https://learn.microsoft.com/en-us/windows/mixed-reality/develop/unity/known-issues)
* [Setting up a new Unity project with MRTK3](https://learn.microsoft.com/en-us/windows/mixed-reality/mrtk-unity/mrtk3-overview/getting-started/setting-up/setup-new-project)

---


|Project Assets|Build Settings|
|---|---|
|![ProjectAssets.jpg](ProjectAssets.jpg)|![BuildSettings.jpg](BuildSettings.jpg)|

|Player Settings|
|---|
|![PlayerSettings.jpg](PlayerSettings.jpg)|


## ScreenShot (old version)
![screenshot01.jpg](screenshot01.jpg) 

![screenshot02.jpg](screenshot02.jpg) 

![screenshot03.jpg](screenshot03.jpg) 

![screenshot04.jpg](screenshot04.jpg) 

![screenshot05.jpg](screenshot05.jpg) 



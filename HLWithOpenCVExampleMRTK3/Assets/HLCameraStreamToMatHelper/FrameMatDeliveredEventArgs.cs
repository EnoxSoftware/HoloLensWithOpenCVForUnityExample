using System;
using HoloLensCameraStream;
using OpenCVForUnity.CoreModule;
using UnityEngine;

namespace HoloLensWithOpenCVForUnity.UnityIntegration.Helper.SourceToMat
{
    /// <summary>
    /// Payload for a newly delivered camera frame <see cref="Mat"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Mat"/> is a newly allocated buffer. The subscriber must dispose it when finished.
    /// Do not use <see cref="OpenCVForUnity.Extensions.SourceToMat.IMatSource.FrameMat"/> as this payload.
    /// On HoloLens, delivery runs on the camera callback thread. On WebCam fallback, delivery runs on the Unity main thread.
    /// </remarks>
    public sealed class FrameMatDeliveredEventArgs : EventArgs
    {
        // Public Properties
        /// <summary>
        /// Gets the newly allocated frame image. The subscriber must dispose this <see cref="Mat"/>.
        /// </summary>
        public Mat Mat { get; }

        /// <summary>
        /// Gets the locatable-camera projection matrix.
        /// </summary>
        public Matrix4x4 ProjectionMatrix { get; }

        /// <summary>
        /// Gets the locatable-camera camera-to-world matrix.
        /// </summary>
        public Matrix4x4 CameraToWorldMatrix { get; }

        /// <summary>
        /// Gets the locatable-camera intrinsics. WebCam fallback uses <see langword="default"/>.
        /// </summary>
        public CameraIntrinsics Intrinsics { get; }

        // Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="FrameMatDeliveredEventArgs"/> class.
        /// </summary>
        /// <param name="mat">Newly allocated frame image. The subscriber owns this <see cref="Mat"/>.</param>
        /// <param name="projectionMatrix">Locatable-camera projection matrix, or identity on WebCam fallback.</param>
        /// <param name="cameraToWorldMatrix">Locatable-camera camera-to-world matrix, or identity on WebCam fallback.</param>
        /// <param name="intrinsics">Locatable-camera intrinsics, or <see langword="default"/> on WebCam fallback.</param>
        /// <exception cref="ArgumentNullException"><paramref name="mat"/> is <see langword="null"/>.</exception>
        public FrameMatDeliveredEventArgs(
            Mat mat,
            Matrix4x4 projectionMatrix,
            Matrix4x4 cameraToWorldMatrix,
            CameraIntrinsics intrinsics)
        {
            if (mat == null)
            {
                throw new ArgumentNullException(nameof(mat), "Parameter cannot be null.");
            }

            Mat = mat;
            ProjectionMatrix = projectionMatrix;
            CameraToWorldMatrix = cameraToWorldMatrix;
            Intrinsics = intrinsics;
        }
    }
}

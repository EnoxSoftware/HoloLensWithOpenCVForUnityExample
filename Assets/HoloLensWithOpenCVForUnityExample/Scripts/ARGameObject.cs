using OpenCVForUnity.UnityUtils;
using System.Collections.Generic;
using UnityEngine;

namespace HoloLensWithOpenCVForUnityExample
{
    /// <summary>
    /// AR Game Object
    /// Referring to https://github.com/qian256/HoloLensARToolKit/blob/master/HoloLensARToolKit/Assets/ARToolKitUWP/Scripts/ARUWPTarget.cs.
    /// </summary>
    public class ARGameObject : MonoBehaviour
    {
        public bool smoothing = true;
        public float lerp = 0.15f;

        private static float positionJumpThreshold = 0.08f;
        private static float rotationJumpThreshold = 24f;
        private static float positionRecoverThreshold = 0.04f;
        private static float rotationRecoverThreshold = 12f;
        private static int maxPendingList = 15;
        private List<Vector3> pendingPositionList = new List<Vector3>();
        private List<Quaternion> pendingRotationList = new List<Quaternion>();

        /// <summary>
        /// When smoothing is enabled, the new pose will be filtered with current pose using lerp. Big sudden change of 6-DOF pose will be prohibited.
        /// </summary>
        /// <param name="localToWorldMatrix">The localToWorldMatrix.</param>
        public void SetMatrix4x4(Matrix4x4 localToWorldMatrix)
        {
            Vector3 previousPosition = transform.localPosition;
            Quaternion previousRotation = transform.localRotation;

            Vector3 targetPosition = ARUtils.ExtractTranslationFromMatrix(ref localToWorldMatrix);
            Quaternion targetRotation = ARUtils.ExtractRotationFromMatrix(ref localToWorldMatrix);

            if (!smoothing)
            {
                transform.localRotation = targetRotation;
                transform.localPosition = targetPosition;
            }
            else
            {
                float positionDiff = Vector3.Distance(targetPosition, previousPosition);
                float rotationDiff = Quaternion.Angle(targetRotation, previousRotation);

                if (Mathf.Abs(positionDiff) < positionJumpThreshold && Mathf.Abs(rotationDiff) < rotationJumpThreshold)
                {
                    transform.localRotation = Quaternion.Slerp(previousRotation, targetRotation, lerp);
                    transform.localPosition = Vector3.Lerp(previousPosition, targetPosition, lerp);
                    pendingPositionList.Clear();
                    pendingRotationList.Clear();
                }
                else
                {
                    // maybe there is a jump
                    pendingPositionList.Add(targetPosition);
                    pendingRotationList.Add(targetRotation);
                    bool confirmJump = true;
                    if (pendingPositionList.Count > maxPendingList)
                    {
                        for (int i = 0; i < maxPendingList - 1; i++)
                        {
                            float tempPositionDiff = Vector3.Distance(pendingPositionList[pendingPositionList.Count - i - 1], pendingPositionList[pendingPositionList.Count - i - 2]);
                            float tempRotationDiff = Quaternion.Angle(pendingRotationList[pendingRotationList.Count - i - 1], pendingRotationList[pendingRotationList.Count - i - 2]);
                            if (Mathf.Abs(tempPositionDiff) > positionRecoverThreshold || Mathf.Abs(tempRotationDiff) > rotationRecoverThreshold)
                            {
                                confirmJump = false;
                                break;
                            }
                        }
                        if (confirmJump)
                        {
                            transform.localRotation = targetRotation;
                            transform.localPosition = targetPosition;
                            pendingPositionList.Clear();
                            pendingRotationList.Clear();
                        }
                    }
                }
            }
        }
    }
}
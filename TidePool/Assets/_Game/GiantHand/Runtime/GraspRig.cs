using UnityEngine;

namespace TidePool.GiantHand.AutoGrasp
{
    public class GraspRig : MonoBehaviour
    {
        public Transform armRoot;
        public Transform wrist;
        public Transform palmFrame;

        [Tooltip("食指、中指、无名指、小指、拇指的 Tip")]
        public Transform[] fingertips = new Transform[5];

        public bool TryCaptureReference(
            out GraspRigSnapshot snapshot,
            out string message)
        {
            snapshot = null;

            if (armRoot == null
                || wrist == null
                || palmFrame == null)
            {
                message = "手臂根部、手腕或 PalmFrame 引用缺失。";
                return false;
            }

            if (palmFrame.parent != wrist)
            {
                message = "PalmFrame 必须直接放在 Wrist 下面。";
                return false;
            }

            if (fingertips == null
                || fingertips.Length != 5)
            {
                message = "Fingertips 必须正好填写五个 Tip。";
                return false;
            }

            if (!HasUnitScale(armRoot)
                || !HasUnitScale(palmFrame))
            {
                message = "骨架及 PalmFrame 的层级缩放需要为 1。";
                return false;
            }

            Vector3[] tipPositions = new Vector3[5];

            for (int i = 0; i < 5; i++)
            {
                if (fingertips[i] == null
                    || !HasUnitScale(fingertips[i]))
                {
                    message = $"第 {i + 1} 个 Tip 引用或缩放无效。";
                    return false;
                }

                tipPositions[i] =
                    palmFrame.InverseTransformPoint(
                        fingertips[i].position
                    );
            }

            snapshot = new GraspRigSnapshot
            {
                rootPosition = armRoot.position,

                startPalmPosition = palmFrame.position,
                startPalmRotation = palmFrame.rotation,

                palmPositionInWrist = palmFrame.localPosition,
                palmRotationInWrist = palmFrame.localRotation,

                openTipPositionsInPalm = tipPositions
            };

            message = "手部参考姿态采样完成。";
            return true;
        }

        private static bool HasUnitScale(Transform item)
        {
            for (Transform current = item;
                 current != null;
                 current = current.parent)
            {
                Vector3 scale = current.localScale;

                if (!CapsuleGeometry.IsFinite(scale)
                    || (scale - Vector3.one).sqrMagnitude
                        > 0.000001f)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
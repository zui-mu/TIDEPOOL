using UnityEngine;

namespace TidePool.GiantHand.AutoGrasp
{
    public class PlayerCapsuleSampler : MonoBehaviour
    {
        public CharacterController source;

        public bool TryCapture(
            out CapsuleSnapshot snapshot,
            out string message)
        {
            snapshot = default;

            if (source == null)
            {
                message = "尚未指定玩家的 Character Controller。";
                return false;
            }

            Transform sourceTransform = source.transform;

            for (Transform current = sourceTransform;
                 current != null;
                 current = current.parent)
            {
                Vector3 scale = current.localScale;

                if (!CapsuleGeometry.IsFinite(scale)
                    || (scale - Vector3.one).sqrMagnitude
                    > 0.000001f)
                {
                    message =
                        "首轮要求 Player 及其父物体的缩放均为 1。";
                    return false;
                }
            }

            if (Vector3.Dot(
                    sourceTransform.up,
                    Vector3.up) < 0.999f)
            {
                message = "首轮先使用竖直的玩家胶囊。";
                return false;
            }

            snapshot = new CapsuleSnapshot
            {
                center = sourceTransform.TransformPoint(
                    source.center
                ),

                axis = sourceTransform.up.normalized,

                radius = source.radius,
                height = source.height
            };

            if (!CapsuleGeometry.IsValid(snapshot))
            {
                message = "胶囊参数无效，请检查半径和高度。";
                return false;
            }

            message = "胶囊采样完成。";
            return true;
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TidePool.GiantHand
{
    public static class SerialChainFk
    {
        // Entry i describes joint i + 1 in joint i's frame. Rotations are unit quaternions.
        public static void Evaluate(
            Pose rootWorld,
            IReadOnlyList<Pose> childLocalPoses,
            Pose[] jointWorldPoses)
        {
            if (childLocalPoses == null)
                throw new ArgumentNullException(nameof(childLocalPoses));
            if (jointWorldPoses == null)
                throw new ArgumentNullException(nameof(jointWorldPoses));
            if (jointWorldPoses.Length != childLocalPoses.Count + 1)
                throw new ArgumentException("The result needs one pose per joint, including the root.",
                    nameof(jointWorldPoses));

            jointWorldPoses[0] = rootWorld;
            for (int i = 0; i < childLocalPoses.Count; i++)
            {
                Pose parent = jointWorldPoses[i];
                Pose child = childLocalPoses[i];
                jointWorldPoses[i + 1] = new Pose(
                    parent.position + parent.rotation * child.position,
                    parent.rotation * child.rotation
                );
            }
        }
    }
}

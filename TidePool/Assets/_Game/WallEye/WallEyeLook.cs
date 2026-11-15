using UnityEngine;

namespace TidePool
{
    [DisallowMultipleComponent]
    public sealed class WallEyeLook : MonoBehaviour
    {
        [SerializeField] Transform pivot;
        [SerializeField] Transform target;
        [SerializeField, Min(0)] float engageDistance = 12f;
        [SerializeField, Min(0)] float releaseDistance = 14f;
        [SerializeField, Min(0)] float reactionDelay = .28f;
        [SerializeField, Range(0, 89)] float horizontalLimit = 32f;
        [SerializeField, Range(0, 89)] float verticalLimit = 23f;
        [SerializeField, Min(0)] float turnSpeed = 54f;
        Quaternion restRotation;
        float approachTime;

        public Transform Pivot => pivot;
        public Transform Target => target;
        public bool IsTracking { get; private set; }

        void Awake()
        {
            if (pivot) restRotation = pivot.localRotation;
        }

        void OnDisable()
        {
            approachTime = 0f;
            IsTracking = false;
            if (pivot) pivot.localRotation = restRotation;
        }

        void LateUpdate()
        {
            if (!pivot || !target) return;

            var aimAt = target.position + Vector3.up * .55f;
            var offset = transform.InverseTransformDirection(aimAt - pivot.position);
            float distance = offset.magnitude;
            float threshold = IsTracking ? Mathf.Max(engageDistance, releaseDistance) : engageDistance;
            if (offset.z <= 0f || distance > threshold)
            {
                IsTracking = false;
                approachTime = 0f;
            }
            else if (!IsTracking)
            {
                approachTime += Time.deltaTime;
                IsTracking = approachTime >= reactionDelay;
            }

            var desired = restRotation;
            if (IsTracking)
            {
                float yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
                float horizontal = Mathf.Sqrt(offset.x * offset.x + offset.z * offset.z);
                float pitch = -Mathf.Atan2(offset.y, horizontal) * Mathf.Rad2Deg;
                desired = Quaternion.Euler(Mathf.Clamp(pitch, -verticalLimit, verticalLimit),
                                            Mathf.Clamp(yaw, -horizontalLimit, horizontalLimit), 0f);
            }

            pivot.localRotation = Quaternion.RotateTowards(pivot.localRotation, desired, turnSpeed * Time.deltaTime);
        }
    }
}

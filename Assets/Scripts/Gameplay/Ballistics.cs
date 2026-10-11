using UnityEngine;

namespace UmdJam.Gameplay
{
    public static class Ballistics
    {
        public static bool TryCalculateVelocity(
            Vector3 origin, Vector3 target, float apexHeight, out Vector3 velocity)
        {
            velocity = Vector3.zero;
            Vector3 gravity = Physics.gravity;
            if (!IsFinite(origin) || !IsFinite(target) || !IsFinite(gravity) ||
                !IsFinite(apexHeight) || apexHeight < 0f || gravity.y >= 0f ||
                gravity.x != 0f || gravity.z != 0f)
            {
                return false;
            }

            float gravityMagnitude = -gravity.y;
            float apexY = origin.y + apexHeight;
            if (!IsFinite(apexY) || target.y > apexY)
            {
                return false;
            }

            float verticalSpeed = Mathf.Sqrt(2f * gravityMagnitude * apexHeight);
            float riseTime = verticalSpeed / gravityMagnitude;
            float fallTime = Mathf.Sqrt(2f * (apexY - target.y) / gravityMagnitude);
            float flightTime = riseTime + fallTime;
            if (!IsFinite(flightTime) || flightTime <= 0f)
            {
                return false;
            }

            Vector3 horizontalDisplacement = target - origin;
            horizontalDisplacement.y = 0f;
            Vector3 calculatedVelocity = horizontalDisplacement / flightTime + Vector3.up * verticalSpeed;
            if (!IsFinite(calculatedVelocity))
            {
                return false;
            }

            velocity = calculatedVelocity;
            return true;
        }

        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

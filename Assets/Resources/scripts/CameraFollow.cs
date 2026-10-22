using UnityEngine;
using CarContollingScripts;

namespace CameraControlsforcar
{
    public class CameraFollow : MonoBehaviour
    {
        public float moveSmoothness;
        public float rotSmoothness;

        public Vector3 moveOffset;
        public Vector3 rotOffset;

        public Transform carTarget;

        [Header("Drift Camera")]
        public CarController car;                 // drag the car here
        public float driftSideOffset = 2.5f;      // how far the camera slides sideways
        public float driftLookShift = 1.5f;       // how far the aim point shifts sideways
        public float maxSlipForFullOffset = 35f;  // slip angle (degrees) that gives the full shift
        public float driftSmoothness = 4f;        // higher = reacts faster

        float driftShift; // -1 = slide to the left, +1 = slide to the right

        void LateUpdate()
        {
            UpdateDriftShift();
            HandleMovement();
            HandleRotation();
        }

        public float slipDeadzone = 8f; // degrees of slide to ignore (normal cornering)

        void UpdateDriftShift()
        {
            float target = 0f;
            if (car != null)
            {
                float slip = car.SlipAngle;
                float excess = Mathf.Max(0f, Mathf.Abs(slip) - slipDeadzone);
                float range = Mathf.Max(1f, maxSlipForFullOffset - slipDeadzone);
                target = Mathf.Sign(slip) * Mathf.Clamp01(excess / range);
            }

            float t = 1f - Mathf.Exp(-driftSmoothness * Time.deltaTime);
            driftShift = Mathf.Lerp(driftShift, target, t);
        }

        void HandleMovement()
        {
            Vector3 offset = moveOffset + Vector3.right * (-driftShift * driftSideOffset);
            Vector3 targetPos = carTarget.TransformPoint(offset);
            transform.position = Vector3.Lerp(transform.position, targetPos, moveSmoothness * Time.deltaTime);
        }

        void HandleRotation()
        {
            Vector3 lookPoint = carTarget.position + carTarget.right * (-driftShift * driftLookShift);
            Vector3 direction = lookPoint - transform.position;
            Quaternion rotation = Quaternion.LookRotation(direction + rotOffset, Vector3.up);
            transform.rotation = Quaternion.Lerp(transform.rotation, rotation, rotSmoothness * Time.deltaTime);
        }
    }
}
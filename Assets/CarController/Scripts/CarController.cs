using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarContollingScripts
{
    public class CarController : MonoBehaviour
    {
        public enum ControlMode
        {
            Keyboard,
            Buttons
        }

        public enum Axel
        {
            Front,
            Rear
        }

        [Serializable]
        public struct Wheel
        {
            public GameObject wheelModel;
            public WheelCollider wheelCollider;
            public Axel axel;
            public TrailRenderer trailRenderer;
        }

        [Header("Control")]
        public ControlMode control = ControlMode.Keyboard;

        [Header("Engine")]
        public float maxAcceleration = 30f;
        public float maxSpeed = 40f;
        public float reverseSpeed = 10f;
        public float minimumDrivingSpeed = 10f;
        public bool maintainMinimumSpeed = false;
        public float brakeAcceleration = 50f;

        [Header("Steering")]
        public float turnSensitivity = 1f;
        public float maxSteerAngle = 30f;
        public float backwheelturn = 2f;
        public float steeringSmoothness = 5f;
        public float highSpeedSteeringReduction = 0.45f;

        [Header("Drifting")]
        public bool enableDrifting = true;
        public float driftStartSpeed = 15f;
        [Range(0.1f, 1f)]
        public float rearDriftGrip = 0.65f;
        public float driftGripTransition = 4f;
        public float driftSteeringAssist = 0.5f;

        [Header("Stability")]
        public Vector3 _centerOfMass = new Vector3(0f, -0.4f, 0f);
        public float downforce = 30f;
        public float lateralGrip = 1.5f;
        public float stabilityStrength = 2f;

        [Header("Wheel Setup")]
        public List<Wheel> wheels = new List<Wheel>();
        public Transform body;

        private Rigidbody carRb;

        private float moveInput;
        private float steerInput;
        private float currentSteer;
        private bool isBraking;
        private float mobilesteer;

        private float driftAmount;
        private WheelFrictionCurve[] originalSidewaysFriction;

        void Start()
        {
            carRb = GetComponent<Rigidbody>();

            if (carRb == null)
            {
                Debug.LogError("CarController requires a Rigidbody.", this);
                enabled = false;
                return;
            }

            carRb.centerOfMass = _centerOfMass;

            originalSidewaysFriction =
                new WheelFrictionCurve[wheels.Count];

            for (int i = 0; i < wheels.Count; i++)
            {
                Wheel wheel = wheels[i];

                if (wheel.wheelCollider != null)
                {
                    originalSidewaysFriction[i] =
                        wheel.wheelCollider.sidewaysFriction;
                }

                if (wheel.trailRenderer != null)
                {
                    wheel.trailRenderer.emitting = false;
                }
            }
        }

        void Update()
        {
            GetInputs();
            AnimateWheels();
            WheelEffects();
        }

        void FixedUpdate()
        {
            if (carRb == null) return;

            Move();
            Steer();
            Brake();
            ApplyStability();
            UpdateDriftGrip();
        }

        public void MoveInput(float input)
        {
            moveInput = Mathf.Clamp(input, -1f, 1f);
        }

        public void SteerInput(float input)
        {
            steerInput = Mathf.Clamp(input, -1f, 1f);
        }

        public void BrakeInput(bool input)
        {
            isBraking = input;
        }

        void GetInputs()
        {
            if (control == ControlMode.Keyboard)
            {

                steerInput = Input.GetAxis("Horizontal");


            }
            // Buttons mode uses the public input methods above.
           
        }

      
       


        void Move()
        {
            float forwardSpeed =
                Vector3.Dot(carRb.linearVelocity, transform.forward) * 3.6f;

            float speed = carRb.linearVelocity.magnitude * 3.6f;

            bool reversing = 1 < -0.05f;

            for (int i = 0; i < wheels.Count; i++)
            {
                Wheel wheel = wheels[i];

                if (wheel.wheelCollider == null) continue;

                WheelCollider collider = wheel.wheelCollider;

                collider.motorTorque = 0f;

                // Apply drive torque only when the car can accelerate
                // in the requested direction.
                if (isBraking) continue;

                if (reversing)
                {
                    if (forwardSpeed > 1f) continue;

                    if (forwardSpeed > -reverseSpeed)
                    {
                        collider.motorTorque =
                            1 * maxAcceleration * 100f;
                    }
                }
                else if (1 > 0.05f)
                {
                    if (forwardSpeed < maxSpeed)
                    {
                        float acceleration = maxAcceleration;

                        // Optional minimum driving speed while throttle
                        // is held. This is a soft acceleration assist.
                        if (maintainMinimumSpeed &&
                            forwardSpeed >= 0f &&
                            forwardSpeed < minimumDrivingSpeed)
                        {
                            acceleration *= 1.5f;
                        }

                        collider.motorTorque =
                            1 * acceleration * 100f;
                    }
                }
            }

            // Smoothly prevent excessive forward and reverse speed.
            if (forwardSpeed > maxSpeed)
            {
                carRb.AddForce(
                    -transform.forward *
                    ((forwardSpeed - maxSpeed) * 0.5f),
                    ForceMode.Acceleration);
            }
            else if (forwardSpeed < -reverseSpeed)
            {
                carRb.AddForce(
                    transform.forward *
                    ((-reverseSpeed - forwardSpeed) * 0.5f),
                    ForceMode.Acceleration);
            }
        }

        void Steer()
        {
            print(steerInput);
            float speed = carRb.linearVelocity.magnitude * 3.6f;

            // Reduce steering sensitivity at higher speeds.
            float speedFactor = Mathf.Lerp(
                1f,
                highSpeedSteeringReduction,
                Mathf.InverseLerp(10f, maxSpeed, speed));

            float targetSteer =
                steerInput * turnSensitivity *
                maxSteerAngle * speedFactor;

            currentSteer = Mathf.Lerp(
                currentSteer,
                targetSteer,
                1f - Mathf.Exp(-steeringSmoothness *
                               Time.fixedDeltaTime));

            for (int i = 0; i < wheels.Count; i++)
            {
                Wheel wheel = wheels[i];

                if (wheel.wheelCollider == null) continue;

                float angle;

                if (wheel.axel == Axel.Front)
                {
                    angle = currentSteer;
                }
                else
                {
                    // Counter-steer rear wheels gently for stability.
                    angle = -currentSteer /
                            Mathf.Max(1f, backwheelturn);

                    // Reduce rear steering when moving quickly.
                    angle *= 1f - driftAmount * driftSteeringAssist;
                }

                wheel.wheelCollider.steerAngle = angle;
            }
        }

        void Brake()
        {
            float speed = carRb.linearVelocity.magnitude * 3.6f;

            for (int i = 0; i < wheels.Count; i++)
            {
                Wheel wheel = wheels[i];

                if (wheel.wheelCollider == null) continue;

                // Always reset brake torque when not braking.
                if (!isBraking)
                {
                    wheel.wheelCollider.brakeTorque = 0f;
                    continue;
                }

                float brakePower = brakeAcceleration * 100f;

                // Rear braking helps initiate a drift.
                if (enableDrifting &&
                    speed >= driftStartSpeed &&
                    wheel.axel == Axel.Rear)
                {
                    brakePower *= 1.25f;
                }

                wheel.wheelCollider.brakeTorque = brakePower;
            }
        }

        void UpdateDriftGrip()
        {
            float speed = carRb.linearVelocity.magnitude * 3.6f;

            float targetDrift = 0f;

            if (enableDrifting && isBraking && speed >= driftStartSpeed)
            {
                targetDrift = 1f;
            }

            driftAmount = Mathf.MoveTowards(
                driftAmount,
                targetDrift,
                driftGripTransition * Time.fixedDeltaTime);

            for (int i = 0; i < wheels.Count; i++)
            {
                Wheel wheel = wheels[i];

                if (wheel.wheelCollider == null) continue;

                WheelFrictionCurve friction =
                    originalSidewaysFriction[i];

                if (wheel.axel == Axel.Rear && enableDrifting)
                {
                    friction.stiffness = Mathf.Lerp(
                        originalSidewaysFriction[i].stiffness,
                        rearDriftGrip,
                        driftAmount);
                }

                wheel.wheelCollider.sidewaysFriction = friction;
            }
        }

        void ApplyStability()
        {
            float speed = carRb.linearVelocity.magnitude;

            // Downforce keeps the car planted at speed.
            carRb.AddForce(
                -transform.up * downforce * speed,
                ForceMode.Force);

            // Dampen unwanted sideways motion without removing
            // all lateral velocity needed for drifting.
            Vector3 localVelocity =
                transform.InverseTransformDirection(carRb.linearVelocity);

            float grip = lateralGrip;

            if (enableDrifting)
            {
                grip *= Mathf.Lerp(1f, 0.35f, driftAmount);
            }

            Vector3 correction =
                -transform.right * localVelocity.x * grip;

            carRb.AddForce(correction, ForceMode.Acceleration);

            // Mildly resist unwanted yaw spin at higher speeds.
            Vector3 localAngularVelocity =
                transform.InverseTransformDirection(carRb.angularVelocity);

            float yawDamping = stabilityStrength *
                               Mathf.Lerp(0.5f, 1f,
                                   Mathf.InverseLerp(5f, maxSpeed, speed));

            carRb.AddTorque(
                -transform.up * localAngularVelocity.y * yawDamping,
                ForceMode.Acceleration);
        }

        void AnimateWheels()
        {
            foreach (Wheel wheel in wheels)
            {
                if (wheel.axel == Axel.Front)
                {
                    if (wheel.wheelCollider == null ||
                    wheel.wheelModel == null) continue;

                    wheel.wheelCollider.GetWorldPose(
                        out Vector3 position,
                        out Quaternion rotation);

                    wheel.wheelModel.transform.SetPositionAndRotation(
                        position, rotation);
                }
            }
        }

        void WheelEffects()
        {
            foreach (Wheel wheel in wheels)
            {
                if (wheel.trailRenderer == null) continue;

                float speed = carRb.linearVelocity.magnitude * 3.6f;

                bool shouldEmit =
                    isBraking &&
                    wheel.axel == Axel.Rear &&
                    wheel.wheelCollider != null &&
                    wheel.wheelCollider.isGrounded &&
                    speed >= driftStartSpeed;

                wheel.trailRenderer.emitting = shouldEmit;
            }
        }
    }
}


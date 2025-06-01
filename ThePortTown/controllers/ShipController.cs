using UnityEngine;

public class ShipController : MonoBehaviour
{
    // Enum to select the rotation axis for visual lean
    public enum RotationAxis
    {
        X_Pitch,
        Y_Yaw, // Not typically used for visual lean, but included for completeness
        Z_Roll
    }

    [Header("Ship References")]
    [Tooltip("Drag the actual visual mesh GameObject here (the child of this GameObject).")]
    public Transform shipVisualTransform; // Reference to the GameObject holding the visual mesh

    private Rigidbody rb; // Rigidbody for the 'ShipRoot' (this GameObject)
    private Vector3 originalVisualLocalPosition;
    private Quaternion originalVisualLocalRotation;
    private float uniqueBobOffset; // To make each ship bob out of sync

    [Header("Movement Settings")]
    [Tooltip("The force applied to move the ship forward.")]
    public float forwardThrust = 100.0f; // Changed to thrust force
    [Tooltip("The force applied to move the ship backward (reverse).")]
    public float reverseThrust = 50.0f; // Changed to thrust force
    [Tooltip("The maximum forward speed the ship can reach.")]
    public float maxForwardSpeed = 10.0f;
    [Tooltip("The maximum backward (reverse) speed the ship can reach.")]
    public float maxBackwardSpeed = 5.0f;
    [Tooltip("Resistance to linear movement applied as a force.")]
    public float linearDrag = 2.0f; // Still useful for general damping
    [Tooltip("The rate at which the ship decelerates when no forward/backward input is given.")]
    public float decelerationRate = 50.0f; 

    [Header("Turning Settings (Physics-based)")] 
    [Tooltip("Minimum turning speed (degrees per second) when at or below min speed.")]
    public float minTurnSpeed = 0.0f; // Minimum turn speed when moving
    [Tooltip("Maximum turning speed (degrees per second) when at or above min speed for max turn.")]
    public float maxTurnSpeed = 90.0f; // Maximum turn speed when moving
    [Tooltip("The forward speed at which the ship achieves its maximum turning speed. Below this, turn speed scales down.")]
    public float minSpeedForMaxTurn = 5.0f; // Speed at which maxTurnSpeed is reached
    [Tooltip("Turning speed (degrees per second) when the ship is effectively stationary (speed below rotationSpeedThreshold).")]
    public float stationaryTurnSpeed = 30.0f; // NEW: Turn speed when not moving forward
    [Tooltip("Resistance to rotation applied as a force.")]
    public float angularDrag = 3.0f;
    [Tooltip("How smoothly the ship's actual turning (Yaw) responds to input. Higher value = slower turn response.")]
    public float turnInputSmoothTime = 0.2f; // Time in seconds to reach target turn input
    private float currentTurnInput; // The smoothed turning input value applied to Rigidbody
    private float turnInputSmoothVelocity; // Used by Mathf.SmoothDamp for internal calculations
    
    [Header("Movement Options")]
    [Tooltip("If true, the ship can only rotate when moving forward above a certain speed.")]
    public bool goForwardtoRotate = false; 
    [Tooltip("Minimum forward speed required to rotate if 'Go Forward to Rotate' is enabled. Also defines 'stationary' for 'Stationary Turn Speed'.")]
    public float rotationSpeedThreshold = 0.5f; // Speed needed to rotate

    [Tooltip("If true, the ship can go backward (reverse) with the 'S' key.")]
    public bool canGoBackward = true; 

    [Header("Fake Buoyancy Settings (Visual Only)")]
    [Tooltip("How high and low the visual mesh bobs.")]
    public float bobHeight = 0.1f;
    [Tooltip("How fast the visual mesh bobs.")]
    public float bobSpeed = 1.0f;
    [Tooltip("How much the visual mesh pitches forward/backward (X-axis rotation) due to waves.")]
    public float pitchAmount = 1.0f;
    [Tooltip("How much the visual mesh rolls side-to-side (Z-axis rotation) due to waves.")]
    public float waveRollAmount = 0.5f; // Roll from wave motion
    [Tooltip("How fast the visual mesh pitches and rolls due to waves.")]
    public float pitchRollSpeed = 1.0f;
    [Tooltip("Phase shift for pitch relative to bobbing.")]
    public float pitchPhaseShift = 0.5f;
    [Tooltip("Phase shift for roll relative to bobbing.")]
    public float rollPhaseShift = 1.2f;

    [Header("Turning Visual Lean")]
    [Tooltip("The axis around which the visual mesh will lean when turning.")]
    public RotationAxis turnLeanAxis = RotationAxis.Z_Roll; // Selectable axis
    [Tooltip("The minimum lean amount when turning at low speeds (e.g., minSpeed).")]
    public float minTurnLeanAmount = 1.0f; // Min lean amount, scales with speed
    [Tooltip("The maximum lean amount when turning at high speeds (e.g., maxSpeed).")]
    public float maxTurnLeanAmount = 15.0f; // Max lean amount, scales with speed
    [Tooltip("How smoothly the visual lean responds to turning input.")]
    public float turnLeanSmoothTime = 0.1f;
    private float currentTurnLean; // The smoothed value for visual lean
    private float turnLeanSmoothVelocity; // Used by Mathf.SmoothDamp for internal calculations


    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("ShipController requires a Rigidbody component on the root GameObject.");
            enabled = false;
            return;
        }

        if (shipVisualTransform == null)
        {
            Debug.LogError("Ship Visual Transform is not assigned! Please assign the visual mesh GameObject in the Inspector.");
            enabled = false;
            return;
        }

        originalVisualLocalPosition = shipVisualTransform.localPosition;
        originalVisualLocalRotation = shipVisualTransform.localRotation;

        uniqueBobOffset = Random.Range(0f, 2f * Mathf.PI);

        currentTurnInput = 0f;
        currentTurnLean = 0f; // Initialize new variable

        // --- THE FIX: Initialize the smooth velocity to zero ---
        turnInputSmoothVelocity = 0f; 
        // -----------------------------------------------------
    }

    void FixedUpdate()
    {
        // --- Ship Movement (Physics-based on the Rigidbody) ---

        float verticalInput = Input.GetAxis("Vertical"); // W for forward (+1), S for backward (-1), 0 for no input

        // Get current speed along the ship's forward axis
        // Using rb.linearVelocity for more accurate current movement direction than rb.velocity for this purpose
        float currentForwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        // Apply thrust based on input, but only if not exceeding max speed in that direction
        if (verticalInput > 0) // W key (Forward)
        {
            if (currentForwardSpeed < maxForwardSpeed)
            {
                rb.AddForce(transform.forward * forwardThrust, ForceMode.Acceleration); 
            }
            // If already at max speed, or slightly over (due to other forces), gently push back towards max speed
            if (currentForwardSpeed > maxForwardSpeed)
            {
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, transform.forward * maxForwardSpeed, Time.fixedDeltaTime * 5f); 
            }
        }
        else if (verticalInput < 0) // S key (Backward)
        {
            // Check if backward movement is allowed
            if (canGoBackward) 
            {
                if (currentForwardSpeed > -maxBackwardSpeed)
                {
                    rb.AddForce(-transform.forward * reverseThrust, ForceMode.Acceleration); 
                }
                // If already at max reverse speed, or slightly under, gently push back towards max reverse speed
                if (currentForwardSpeed < -maxBackwardSpeed)
                {
                    rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, -transform.forward * maxBackwardSpeed, Time.fixedDeltaTime * 5f); 
                }
            }
            else 
            {
                 verticalInput = 0; // Effectively cancel input for deceleration logic below if backward is disallowed
            }
        }
        
        // Deceleration when no vertical input, or if backward input is given but not allowed
        if (verticalInput == 0 || (verticalInput < 0 && !canGoBackward)) 
        {
            // Only apply deceleration if moving, to prevent constant force at 0 speed
            if (Mathf.Abs(currentForwardSpeed) > 0.01f) // Check for small speed to avoid jitter at rest
            {
                // Apply a braking force opposite to current movement direction
                rb.AddForce(-rb.linearVelocity.normalized * decelerationRate, ForceMode.Acceleration);
            }

            // Stop completely if very slow to prevent infinite coasting due to low drag
            if (Mathf.Abs(currentForwardSpeed) < 0.1f) 
            {
                rb.linearVelocity = Vector3.zero;
            }
        }
        
        // Always apply linear drag to counteract other forces and provide general water resistance
        rb.AddForce(-rb.linearVelocity * linearDrag, ForceMode.Force);

        // --- Ship Turning (Physics-based) ---
        float rawHorizontalInput = Input.GetAxis("Horizontal");
        currentTurnInput = Mathf.SmoothDamp(currentTurnInput, rawHorizontalInput, ref turnInputSmoothVelocity, turnInputSmoothTime);
        
        float appliedTurnSpeed;

        // Determine if the ship is "stationary" for turning purposes
        if (Mathf.Abs(currentForwardSpeed) <= rotationSpeedThreshold)
        {
            // If stationary, use the dedicated stationary turn speed
            appliedTurnSpeed = stationaryTurnSpeed;
        }
        else
        {
            // If moving, calculate turn speed based on forward velocity
            float speedRatio = Mathf.InverseLerp(0f, minSpeedForMaxTurn, Mathf.Abs(currentForwardSpeed));
            appliedTurnSpeed = Mathf.Lerp(minTurnSpeed, maxTurnSpeed, speedRatio);
        }

        // Check if rotation is allowed based on 'goForwardtoRotate' toggle
        // If goForwardtoRotate is true, we only allow rotation if moving AND above threshold
        // If goForwardtoRotate is false, rotation is always allowed (using either calculated or stationary speed)
        bool canRotate = !goForwardtoRotate || (goForwardtoRotate && Mathf.Abs(currentForwardSpeed) > rotationSpeedThreshold);

        if (canRotate)
        {
            rb.AddTorque(Vector3.up * currentTurnInput * appliedTurnSpeed, ForceMode.Force); 
        }
        else
        {
            // If cannot rotate, dampen angular velocity to stop turning quickly
            rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, Time.fixedDeltaTime * 10f);
        }

        // Apply custom angular drag (to stop turning)
        rb.AddTorque(-rb.angularVelocity * angularDrag, ForceMode.Force);
    }

    void Update()
    {
        // --- Fake Buoyancy (Visual Only, applied to the shipVisualTransform) ---

        float timeValue = Time.time * bobSpeed + uniqueBobOffset;
        float pitchRollTimeValue = Time.time * pitchRollSpeed + uniqueBobOffset;

        // Apply Bobbing (Y-axis movement)
        float yOffset = Mathf.Sin(timeValue) * bobHeight;
        shipVisualTransform.localPosition = new Vector3(
            originalVisualLocalPosition.x,
            originalVisualLocalPosition.y + yOffset,
            originalVisualLocalPosition.z
        );

        // Apply Pitch (X-axis rotation, forward/backward tilt due to waves)
        float pitchAngle = Mathf.Sin(pitchRollTimeValue + pitchPhaseShift) * pitchAmount;

        // Apply Wave-induced Roll (Z-axis rotation)
        float waveRoll = Mathf.Sin(pitchRollTimeValue + rollPhaseShift) * waveRollAmount;


        // --- Turning Visual Lean ---
        // Get current speed magnitude for visual lean scaling
        float currentSpeedMagnitudeForVisuals = Mathf.Abs(Vector3.Dot(rb.linearVelocity, transform.forward));

        // Calculate dynamic lean amount based on current absolute speed
        float speedRatioVisualLean = Mathf.InverseLerp(0, maxForwardSpeed, currentSpeedMagnitudeForVisuals);
        float currentCalculatedLeanAmount = Mathf.Lerp(minTurnLeanAmount, maxTurnLeanAmount, speedRatioVisualLean);

        // Smoothly adjust currentTurnLean based on the *raw* horizontal input and calculated lean amount
        // Note: We use -Input.GetAxis("Horizontal") because turning right (positive horizontal input)
        // should cause a lean in the opposite direction of the turn itself (e.g., negative Z rotation for roll).
        currentTurnLean = Mathf.SmoothDamp(currentTurnLean, -Input.GetAxis("Horizontal") * currentCalculatedLeanAmount, ref turnLeanSmoothVelocity, turnLeanSmoothTime);


        // Combine all visual rotations for pitch, wave roll, and turning lean
        float finalPitch = pitchAngle;
        float finalYaw = 0f; // Rigidbody handles actual yaw for the root
        float finalRoll = waveRoll;

        // Apply turning lean based on selected axis
        switch (turnLeanAxis)
        {
            case RotationAxis.X_Pitch:
                finalPitch += currentTurnLean;
                break;
            case RotationAxis.Y_Yaw:
                finalYaw += currentTurnLean;
                break;
            case RotationAxis.Z_Roll:
                finalRoll += currentTurnLean;
                break;
        }

        Quaternion combinedVisualRotation = Quaternion.Euler(finalPitch, finalYaw, finalRoll);
        
        // Apply combined visual rotation relative to the original local rotation
        shipVisualTransform.localRotation = originalVisualLocalRotation * combinedVisualRotation;
    }
}

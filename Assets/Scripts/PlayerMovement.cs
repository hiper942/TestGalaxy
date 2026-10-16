using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Transform visual;
    [SerializeField] private Animator animator;
    [SerializeField] private GravitySource defaultGravitySource;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float acceleration = 25f;
    [SerializeField] private float bodyRotationSpeed = 12f;
    [SerializeField] private float visualRotationSpeed = 18f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 2f;

    [Header("Ground Check")]
    [SerializeField] private float bodyCenter = 0.8f;
    [SerializeField] private float groundRayMargin = 0.1f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float stickToGroundForce = 5f;

    [Header("Landing VFX")]
    [SerializeField] private VisualEffect landVfxPrefab;
    [SerializeField] private float landVfxOffset = 0.02f;
    [SerializeField] private string landNormalProperty = "SurfaceNormal";
    [SerializeField] private string landForwardProperty = "Forward";
    [SerializeField] private string landStrengthProperty = "Strength";
    [SerializeField] private float landVfxLifetime = 3f;

    [Header("Run VFX")]
    [SerializeField] private VisualEffect stepVfxPrefab;
    [SerializeField] private float stepVfxOffset = 0.02f;
    [SerializeField] private float minStepSpeed = 0.3f;
    [SerializeField] private float stepIntervalWalk = 0.35f;
    [SerializeField] private float stepIntervalRun = 0.18f;
    [SerializeField] private string stepNormalProperty = "SurfaceNormal";
    [SerializeField] private string stepForwardProperty = "Forward";
    [SerializeField] private string stepStrengthProperty = "Strength";
    [SerializeField] private float stepVfxLifetime = 2f;

    private float stepTimer;
    private bool leftFootNext = true;

    private Rigidbody rb;

    private readonly List<GravitySource> activeGravitySources = new();

    private GravitySource currentGravitySource;
    private Vector3 gravityVector;
    private Vector3 upAxis = Vector3.up;

    private bool isGrounded;
    private bool wasGrounded;
    private bool jumpQueued;

    private float previousVerticalVelocity;
    private Vector3 lastMoveDirection = Vector3.forward;

    private RaycastHit groundHit;

    private bool runVfxPlaying;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Update()
    {
        if (Input.GetButtonDown("Jump"))
            jumpQueued = true;
    }

    private void FixedUpdate()
    {
        UpdateGravityData();
        CheckGrounded();
        ApplyGravity();
        HandleMovement();
        HandleJump();
        AlignBodyToGravity();
        RotateVisual();
        UpdateAnimator();
        UpdateRunVfx();
    }

    private void UpdateGravityData()
    {
        UpdateCurrentGravitySource();

        if (currentGravitySource == null)
        {
            gravityVector = Vector3.zero;
            upAxis = transform.up;
            return;
        }

        gravityVector = currentGravitySource.GetGravity(transform.position);
        upAxis = currentGravitySource.GetUpAxis(transform.position);
    }

    private void UpdateCurrentGravitySource()
    {
        RemoveNullGravitySources();

        if (activeGravitySources.Count == 0)
        {
            currentGravitySource = defaultGravitySource;
            return;
        }

        GravitySource bestSource = null;
        int bestPriority = int.MinValue;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < activeGravitySources.Count; i++)
        {
            GravitySource candidate = activeGravitySources[i];
            if (candidate == null)
                continue;

            int candidatePriority = candidate.Priority;
            float candidateDistance = candidate.GetPriorityDistance(transform.position);

            if (bestSource == null ||
                candidatePriority > bestPriority ||
                (candidatePriority == bestPriority && candidateDistance < bestDistance))
            {
                bestSource = candidate;
                bestPriority = candidatePriority;
                bestDistance = candidateDistance;
            }
        }

        currentGravitySource = bestSource != null ? bestSource : defaultGravitySource;
    }

    private void RemoveNullGravitySources()
    {
        for (int i = activeGravitySources.Count - 1; i >= 0; i--)
        {
            if (activeGravitySources[i] == null)
                activeGravitySources.RemoveAt(i);
        }
    }

    private void CheckGrounded()
    {
        Vector3 rayOrigin = transform.position + upAxis * bodyCenter;
        float rayDistance = bodyCenter + groundRayMargin;

        isGrounded = Physics.Raycast(
            rayOrigin,
            -upAxis,
            out groundHit,
            rayDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );
    }

    private void ApplyGravity()
    {
        rb.AddForce(gravityVector, ForceMode.Acceleration);

        if (isGrounded)
            rb.AddForce(-upAxis * stickToGroundForce, ForceMode.Acceleration);
    }

    private void HandleMovement()
    {
        if (cameraTarget == null)
            return;

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTarget.forward, upAxis).normalized;
        Vector3 cameraRight = Vector3.ProjectOnPlane(cameraTarget.right, upAxis).normalized;

        Vector3 moveDirection = cameraForward * vertical + cameraRight * horizontal;

        if (moveDirection.sqrMagnitude > 1f)
            moveDirection.Normalize();

        if (moveDirection.sqrMagnitude > 0.001f)
            lastMoveDirection = moveDirection;

        Vector3 currentVelocity = rb.velocity;
        Vector3 lateralVelocity = Vector3.ProjectOnPlane(currentVelocity, upAxis);
        Vector3 targetVelocity = moveDirection * moveSpeed;

        Vector3 velocityDelta = targetVelocity - lateralVelocity;
        Vector3 neededAcceleration = velocityDelta / Time.fixedDeltaTime;
        neededAcceleration = Vector3.ClampMagnitude(neededAcceleration, acceleration);

        rb.AddForce(neededAcceleration, ForceMode.Acceleration);
    }

    private void HandleJump()
    {
        if (!jumpQueued)
            return;

        jumpQueued = false;

        if (!isGrounded)
            return;

        float gravityMagnitude = gravityVector.magnitude;
        if (gravityMagnitude <= 0.0001f)
            return;

        float jumpSpeed = Mathf.Sqrt(2f * gravityMagnitude * jumpHeight);

        Vector3 verticalVelocity = Vector3.Project(rb.velocity, upAxis);
        rb.velocity -= verticalVelocity;

        rb.AddForce(upAxis * jumpSpeed, ForceMode.VelocityChange);
    }

    private void AlignBodyToGravity()
    {
        Quaternion targetRotation =
            Quaternion.FromToRotation(transform.up, upAxis) * transform.rotation;

        rb.MoveRotation(
            Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                bodyRotationSpeed * Time.fixedDeltaTime
            )
        );
    }

    private void RotateVisual()
    {
        if (visual == null)
            return;

        if (lastMoveDirection.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(-lastMoveDirection, upAxis);

        visual.rotation = Quaternion.Slerp(
            visual.rotation,
            targetRotation,
            visualRotationSpeed * Time.fixedDeltaTime
        );
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        Vector3 lateralVelocity = Vector3.ProjectOnPlane(rb.velocity, upAxis);
        float speed = lateralVelocity.magnitude;

        if (speed < 0.05f)
            speed = 0f;

        float verticalVelocity = Vector3.Dot(rb.velocity, upAxis);

        animator.SetFloat("Speed", speed, 0.1f, Time.fixedDeltaTime);
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetFloat("VerticalVelocity", verticalVelocity, 0.1f, Time.fixedDeltaTime);

        bool landedThisFrame = !wasGrounded && isGrounded;
        bool wasActuallyFalling = previousVerticalVelocity < -2f;

        if (landedThisFrame && wasActuallyFalling)
        {
            animator.SetTrigger("Land");
            SpawnLandVfx();
        }

        wasGrounded = isGrounded;
        previousVerticalVelocity = verticalVelocity;
    }

    private void SpawnLandVfx()
    {
        if (landVfxPrefab == null || !isGrounded)
            return;

        Vector3 spawnPosition = groundHit.point + groundHit.normal * landVfxOffset;

        Vector3 forwardOnPlane = Vector3.ProjectOnPlane(transform.forward, groundHit.normal);
        if (forwardOnPlane.sqrMagnitude < 0.001f)
            forwardOnPlane = Vector3.ProjectOnPlane(lastMoveDirection, groundHit.normal);

        if (forwardOnPlane.sqrMagnitude < 0.001f)
            forwardOnPlane = Vector3.Cross(groundHit.normal, transform.right);

        if (forwardOnPlane.sqrMagnitude < 0.001f)
            forwardOnPlane = Vector3.forward;

        Quaternion spawnRotation = Quaternion.LookRotation(forwardOnPlane.normalized, groundHit.normal);

        VisualEffect spawnedVfx = Instantiate(landVfxPrefab, spawnPosition, spawnRotation);

        if (spawnedVfx.HasVector3(landNormalProperty))
            spawnedVfx.SetVector3(landNormalProperty, groundHit.normal);

        if (spawnedVfx.HasVector3(landForwardProperty))
            spawnedVfx.SetVector3(landForwardProperty, forwardOnPlane.normalized);

        float landingStrength = Mathf.Abs(previousVerticalVelocity);
        if (spawnedVfx.HasFloat(landStrengthProperty))
            spawnedVfx.SetFloat(landStrengthProperty, landingStrength);

        spawnedVfx.Reinit();

        Destroy(spawnedVfx.gameObject, landVfxLifetime);
    }

    private void UpdateRunVfx()
    {
        if (stepVfxPrefab == null || !isGrounded)
        {
            stepTimer = 0f;
            return;
        }

        Vector3 lateralVelocity = Vector3.ProjectOnPlane(rb.velocity, upAxis);
        float speed = lateralVelocity.magnitude;

        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        bool hasInput = input.sqrMagnitude > 0.01f;

        if (!hasInput || speed < minStepSpeed)
        {
            stepTimer = 0f;
            return;
        }

        float speed01 = Mathf.Clamp01(speed / moveSpeed);
        float currentStepInterval = Mathf.Lerp(stepIntervalWalk, stepIntervalRun, speed01);

        stepTimer += Time.fixedDeltaTime;

        if (stepTimer >= currentStepInterval)
        {
            stepTimer = 0f;
            SpawnStepVfx(speed, leftFootNext);
            leftFootNext = !leftFootNext;
        }
    }

    private void SpawnStepVfx(float speed, bool leftFoot)
    {
        if (stepVfxPrefab == null || !isGrounded)
            return;

        Vector3 forwardOnPlane = Vector3.ProjectOnPlane(lastMoveDirection, groundHit.normal);
        if (forwardOnPlane.sqrMagnitude < 0.001f)
            forwardOnPlane = Vector3.ProjectOnPlane(transform.forward, groundHit.normal);

        if (forwardOnPlane.sqrMagnitude < 0.001f)
            forwardOnPlane = Vector3.Cross(groundHit.normal, transform.right);

        forwardOnPlane.Normalize();

        Vector3 rightOnPlane = Vector3.Cross(groundHit.normal, forwardOnPlane).normalized;

        float footOffset = 0.12f;
        Vector3 lateralOffset = (leftFoot ? -rightOnPlane : rightOnPlane) * footOffset;

        Vector3 spawnPosition = groundHit.point + groundHit.normal * stepVfxOffset + lateralOffset;
        Quaternion spawnRotation = Quaternion.LookRotation(forwardOnPlane, groundHit.normal);

        VisualEffect spawnedVfx = Instantiate(stepVfxPrefab, spawnPosition, spawnRotation);

        if (spawnedVfx.HasVector3(stepNormalProperty))
            spawnedVfx.SetVector3(stepNormalProperty, groundHit.normal);

        if (spawnedVfx.HasVector3(stepForwardProperty))
            spawnedVfx.SetVector3(stepForwardProperty, forwardOnPlane);

        if (spawnedVfx.HasFloat(stepStrengthProperty))
            spawnedVfx.SetFloat(stepStrengthProperty, speed);

        spawnedVfx.Reinit();

        Destroy(spawnedVfx.gameObject, stepVfxLifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        GravitySource gravitySource = other.GetComponent<GravitySource>();
        if (gravitySource == null)
            return;

        if (!activeGravitySources.Contains(gravitySource))
            activeGravitySources.Add(gravitySource);
    }

    private void OnTriggerExit(Collider other)
    {
        GravitySource gravitySource = other.GetComponent<GravitySource>();
        if (gravitySource == null)
            return;

        activeGravitySources.Remove(gravitySource);

        if (currentGravitySource == gravitySource)
            currentGravitySource = null;
    }

    public Vector3 GetUpAxis()
    {
        return upAxis;
    }

    public Vector3 GetGravityVector()
    {
        return gravityVector;
    }

    public GravitySource GetCurrentGravitySource()
    {
        return currentGravitySource;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 debugUp = Application.isPlaying ? upAxis : transform.up;
        Vector3 rayOrigin = transform.position + debugUp * bodyCenter;
        float rayDistance = bodyCenter + groundRayMargin;
        Vector3 rayEnd = rayOrigin - debugUp * rayDistance;

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(rayOrigin, rayEnd);
        Gizmos.DrawSphere(rayOrigin, 0.03f);
        Gizmos.DrawSphere(rayEnd, 0.03f);

        if (Application.isPlaying && currentGravitySource != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, gravityVector.normalized);
        }
    }
}
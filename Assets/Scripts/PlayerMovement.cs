using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Core Movement")] 
    [SerializeField] private float walkSpeed = 7f;
    [SerializeField] private float runSpeed = 15f;
    [SerializeField] private float crouchSpeed = 4f;
    [SerializeField] private float speedTransitionTime = 5f;
    [SerializeField] private float gravity = -30f;
    [SerializeField] private float jumpHeight = 2.5f;
    [SerializeField] private int maxJumps = 2;

    [Header("Slide Settings")] 
    [SerializeField] private float slideSpeed = 18f;
    [SerializeField] private float slideDrag = 5f;
    [SerializeField] private float crouchScaleY = 0.5f;
    [SerializeField] private float crouchTransitionSpeed = 10f;
    private Vector3 slideDirection;

    [Header("Dash Settings")] 
    [SerializeField] private KeyCode dashKey = KeyCode.Q;
    [SerializeField] private float dashSpeed = 25f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("Ledge Climbing & Vaulting")]
    [SerializeField] private float ledgeClimbSpeed = 10f;
    [SerializeField] private float forwardRayLength = 1f;
    [SerializeField] private float downwardRayHeight = 1.5f;
    [SerializeField] private float ledgeVaultBoost = 1.5f;

    [Header("Wall Run & Slide")] 
    [SerializeField] private float wallCheckDistance = 0.6f;
    [SerializeField] private float wallSlideSpeed = -2f;
    [SerializeField] private float wallRunGravity = -1f; 
    [SerializeField] private float wallJumpPushForce = 18f;
    [SerializeField] private float wallRunCameraTilt = 15f;
    // ปรับ Cooldown เพิ่มขึ้นเป็น 0.4 วินาที เพื่อให้มีเวลาลอยตัวหนีกำแพง
    [SerializeField] private float wallJumpCooldown = 0.4f; 

    [Header("Game Feel - Particle Speed Lines")] 
    [SerializeField] private ParticleSystem speedLinesParticle;
    [SerializeField] private float minSpeedToShowLines = 14f;
    [SerializeField] private float maxParticleEmission = 100f;
    [SerializeField] private float fadeSpeed = 5f;

    [Header("Game Feel - FOV & Shake")] 
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float normalFOV = 60f;
    [SerializeField] private float runFOV = 80f;
    [SerializeField] private float dashFOV = 95f;
    [SerializeField] private float fovTransitionSpeed = 10f;
    [SerializeField] private float landShakeDuration = 0.15f;
    [SerializeField] private float landShakeMagnitude = 0.2f;

    [Header("Game Feel - Camera Tilt")] 
    [SerializeField] private float maxTiltAngle = 4f;
    [SerializeField] private float tiltTransitionSpeed = 8f;

    [Header("Game Feel - Head Bob & Impact")] 
    [SerializeField] private float idleBobSpeed = 2f;
    [SerializeField] private float idleBobAmount = 0.02f;
    [SerializeField] private float walkBobSpeed = 12f;
    [SerializeField] private float walkBobAmount = 0.05f;
    [SerializeField] private float runBobSpeed = 18f;
    [SerializeField] private float runBobAmount = 0.1f;
    [SerializeField] private float jumpDipAmount = 0.15f;
    [SerializeField] private float landDipAmount = 0.25f;

    [Header("Camera & Input")]
    [SerializeField] private float mouseSensitivity = 100f;

    [Header("Ground Check")] 
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance = 0.4f;
    [SerializeField] private LayerMask groundMask;

    // Internal Variables
    private CharacterController controller;
    private Vector3 velocity;
    private Vector3 moveDirection;
    private bool isGrounded;
    private bool wasGrounded;
    private bool isClimbing;
    private bool isSliding;
    private bool isDashing;
    private bool isWallSliding;
    private bool isWallRunning;
    private int jumpsRemaining;
    private float currentSpeed;
    private float xRotation = 0f;
    private float originalHeight;
    private float nextDashTime;
    private float wallJumpTimer;
    
    // Camera Effect Variables
    private Vector3 cameraBaseLocalPos;
    private Vector3 currentShakeOffset;
    private float headBobTimer;
    private float currentTilt;
    private float currentDipY;
    private float currentParticleEmission = 0f;

    private RaycastHit currentWallHit;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        originalHeight = controller.height;
        currentSpeed = walkSpeed;
        cameraBaseLocalPos = playerCamera.transform.localPosition;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        if(playerCamera != null) playerCamera.fieldOfView = normalFOV;
        
        if (speedLinesParticle != null)
        {
            var emission = speedLinesParticle.emission;
            emission.rateOverTime = 0f;
            speedLinesParticle.Play();
        }
    }

    void Update()
    {
        if (isClimbing) return; 
        
        if (wallJumpTimer > 0) wallJumpTimer -= Time.deltaTime;

        HandleGroundCheck();
        HandleLook();
        
        if (!isDashing) 
        {
            HandleWallMovement(); 
            
            if (!isWallRunning)
            {
                HandleMovement();
                HandleCrouchAndSlide();
            }
            
            HandleJump();
        }

        HandleDash();
        HandleGameFeel();
        ApplyGravity();
        HandleSpeedLines();
    }

    void HandleGroundCheck()
    {
        wasGrounded = isGrounded;
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (isGrounded && !wasGrounded)
        {
            if (velocity.y < -5f) 
            {
                currentDipY = -landDipAmount; 
            }
            if (velocity.y < -7f)
            {
                StartCoroutine(CameraShakeRoutine(landShakeDuration, landShakeMagnitude));
            }
        }

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
            jumpsRemaining = maxJumps; 
        }
    }

    void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, currentTilt);
        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleWallMovement()
    {
        if (isGrounded || isClimbing || isDashing || wallJumpTimer > 0) 
        {
            isWallSliding = false;
            isWallRunning = false;
            return;
        }

        Vector3 chestPos = transform.position + Vector3.up * 0.5f;
        
        bool wallFront = Physics.Raycast(chestPos, transform.forward, out RaycastHit frontHit, wallCheckDistance, groundMask);
        bool wallRight = Physics.Raycast(chestPos, transform.right, out RaycastHit rightHit, wallCheckDistance, groundMask);
        bool wallLeft = Physics.Raycast(chestPos, -transform.right, out RaycastHit leftHit, wallCheckDistance, groundMask);

        if ((wallLeft || wallRight) && Input.GetAxisRaw("Vertical") > 0)
        {
            isWallRunning = true;
            isWallSliding = false;
            currentWallHit = wallRight ? rightHit : leftHit;
            
            Vector3 wallNormal = currentWallHit.normal;
            Vector3 wallForward = Vector3.Cross(wallNormal, transform.up);
            
            if ((transform.forward - wallForward).magnitude > (transform.forward - -wallForward).magnitude)
            {
                wallForward = -wallForward;
            }

            moveDirection = wallForward;
            currentSpeed = runSpeed;
            
            controller.Move((moveDirection * currentSpeed + -wallNormal * 2f) * Time.deltaTime);
            return;
        }

        isWallRunning = false;
        if (wallFront || wallRight || wallLeft)
        {
            if (velocity.y < 0)
            {
                isWallSliding = true;
                if (wallFront) currentWallHit = frontHit;
                else if (wallRight) currentWallHit = rightHit;
                else currentWallHit = leftHit;
            }
            else
            {
                isWallSliding = false;
            }
        }
        else
        {
            isWallSliding = false;
        }
    }

    void HandleMovement()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 inputDir = (transform.right * x + transform.forward * z).normalized;

        if (isSliding)
        {
            currentSpeed = Mathf.Lerp(currentSpeed, crouchSpeed, slideDrag * Time.deltaTime);
            if (currentSpeed <= crouchSpeed + 0.5f) isSliding = false;
            
            moveDirection = slideDirection;
        }
        else
        {
            if (isGrounded)
            {
                float targetSpeed = 0f;

                if (inputDir.magnitude > 0.1f)
                {
                    targetSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
                    if (Input.GetKey(KeyCode.LeftControl)) targetSpeed = crouchSpeed;
                    
                    moveDirection = inputDir;
                }

                currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, speedTransitionTime * Time.deltaTime);
                if (currentSpeed < 0.1f) currentSpeed = 0f;
            }
            else
            {
                // [THE FIX] - Limit air control drastically if we just wall jumped
                if (inputDir.magnitude > 0.1f)
                {
                    // Use a very slow lerp (0.5f) during cooldown so player can't instantly turn back into the wall
                    float airControlSpeed = (wallJumpTimer > 0) ? 0.5f : 5f; 
                    moveDirection = Vector3.Lerp(moveDirection, inputDir, airControlSpeed * Time.deltaTime).normalized;
                }
            }
        }

        controller.Move(moveDirection * currentSpeed * Time.deltaTime);
    }

    void HandleCrouchAndSlide()
    {
        float targetHeight = Input.GetKey(KeyCode.LeftControl) ? originalHeight * crouchScaleY : originalHeight;
        controller.height = Mathf.Lerp(controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);

        if (Input.GetKey(KeyCode.LeftControl) && isGrounded && !isSliding)
        {
            if (currentSpeed > walkSpeed + 0.5f) 
            {
                isSliding = true;
                currentSpeed = Mathf.Max(currentSpeed, slideSpeed); 

                float x = Input.GetAxisRaw("Horizontal");
                float z = Input.GetAxisRaw("Vertical");
                Vector3 inputDir = (transform.right * x + transform.forward * z).normalized;
                
                slideDirection = inputDir.magnitude > 0.1f ? inputDir : transform.forward; 
            }
        }

        if (Input.GetKeyUp(KeyCode.LeftControl))
        {
            isSliding = false;
        }
    }

    void HandleDash()
    {
        if (Input.GetKeyDown(dashKey) && Time.time >= nextDashTime && !isDashing)
        {
            StartCoroutine(DashRoutine());
        }
    }

    IEnumerator DashRoutine()
    {
        isDashing = true;
        nextDashTime = Time.time + dashCooldown;

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector3 dashDir = (transform.right * x + transform.forward * z).normalized;
        
        if (dashDir.magnitude < 0.1f) dashDir = transform.forward;

        velocity.y = 0f; 

        float startTime = Time.time;
        while (Time.time < startTime + dashDuration)
        {
            controller.Move(dashDir * dashSpeed * Time.deltaTime);
            yield return null;
        }

        currentSpeed = runSpeed;
        moveDirection = dashDir;
        isDashing = false;
    }

    void HandleJump()
    {
        if (Input.GetButtonDown("Jump"))
        {
            if (TryClimbLedge()) return;

            if (isWallSliding || isWallRunning)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                
                // [THE FIX] - Multiply the wall normal by 1.5 to guarantee a strong push AWAY from the wall
                Vector3 jumpDir = (currentWallHit.normal * 1.5f + transform.forward).normalized; 
                moveDirection = jumpDir; 
                currentSpeed = wallJumpPushForce;
                
                wallJumpTimer = wallJumpCooldown; 
                jumpsRemaining = 1;
                
                isWallSliding = false;
                isWallRunning = false;
                
                currentDipY = -jumpDipAmount; 
                return;
            }

            if (jumpsRemaining > 0)
            {
                if (isSliding) currentSpeed += 3f; 
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpsRemaining--;
                isSliding = false; 
                
                currentDipY = -jumpDipAmount; 
            }
        }
        else if (Input.GetButton("Jump"))
        {
            TryClimbLedge();
        }
    }
    
    bool TryClimbLedge()
    {
        if (isGrounded) return false;

        Vector3 chestPos = transform.position + Vector3.up * 0.5f;

        if (Physics.Raycast(chestPos, transform.forward, out RaycastHit wallHit, forwardRayLength, groundMask))
        {
            Vector3 downStart = wallHit.point + (transform.forward * 0.1f) + (Vector3.up * downwardRayHeight);
            
            if (Physics.Raycast(downStart, Vector3.down, out RaycastHit ledgeHit, downwardRayHeight, groundMask))
            {
                if (ledgeHit.point.y > transform.position.y)
                {
                    StartCoroutine(ClimbLedgeRoutine(ledgeHit.point));
                    return true; 
                }
            }
        }
        return false; 
    }

    IEnumerator ClimbLedgeRoutine(Vector3 targetPosition)
    {
        isClimbing = true;
        velocity = Vector3.zero; 

        Vector3 endPosition = targetPosition + Vector3.up * (controller.height / 2f);
        float climbProgress = 0f;
        Vector3 startPosition = transform.position;

        controller.enabled = false;

        while (climbProgress < 1f)
        {
            climbProgress += Time.deltaTime * ledgeClimbSpeed;
            transform.position = Vector3.Lerp(startPosition, endPosition, climbProgress);
            yield return null;
        }

        controller.enabled = true;
        isClimbing = false;

        velocity.y = Mathf.Sqrt(ledgeVaultBoost * -2f * gravity);
        currentSpeed = runSpeed; 
        moveDirection = transform.forward;
        currentDipY = -jumpDipAmount; 
    }

    void HandleGameFeel()
    {
        if (playerCamera == null) return;

        float targetFOV = normalFOV;
        if (isDashing) targetFOV = dashFOV;
        else if (isSliding) targetFOV = runFOV + 5f;
        else if (currentSpeed > walkSpeed + 1f && Input.GetAxis("Vertical") > 0) targetFOV = runFOV;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, fovTransitionSpeed * Time.deltaTime);

        float targetTilt = 0f;
        if (isWallRunning)
        {
            float wallDot = Vector3.Dot(transform.right, currentWallHit.normal);
            targetTilt = wallDot > 0 ? -wallRunCameraTilt : wallRunCameraTilt;
        }
        else if (isWallSliding)
        {
            float wallDot = Vector3.Dot(transform.right, currentWallHit.normal);
            targetTilt = wallDot * maxTiltAngle * 1.5f; 
        }
        else if (moveDirection.magnitude > 0.1f && !isClimbing)
        {
            float xInput = Input.GetAxisRaw("Horizontal");
            targetTilt = -xInput * maxTiltAngle;
        }
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, tiltTransitionSpeed * Time.deltaTime);
        
        float bobOffset = 0f;
        if (isGrounded && !isSliding && !isDashing)
        {
            if (currentSpeed > 0.5f)
            {
                float speedMult = (Input.GetKey(KeyCode.LeftShift)) ? runBobSpeed : walkBobSpeed;
                float amountMult = (Input.GetKey(KeyCode.LeftShift)) ? runBobAmount : walkBobAmount;
                headBobTimer += Time.deltaTime * speedMult;
                bobOffset = Mathf.Sin(headBobTimer) * amountMult;
            }
            else
            {
                headBobTimer += Time.deltaTime * idleBobSpeed;
                bobOffset = Mathf.Sin(headBobTimer) * idleBobAmount;
            }
        }
        else 
        { 
            headBobTimer = 0f; 
        }

        currentDipY = Mathf.Lerp(currentDipY, 0f, Time.deltaTime * 10f);

        Vector3 smoothTargetPos = cameraBaseLocalPos + new Vector3(0, bobOffset + currentDipY, 0);
        playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, smoothTargetPos, Time.deltaTime * 15f);
        playerCamera.transform.localPosition += currentShakeOffset;
    }

    IEnumerator CameraShakeRoutine(float duration, float magnitude)
    {
        float elapsed = 0.0f;
        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            currentShakeOffset = new Vector3(x, y, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        currentShakeOffset = Vector3.zero;
    }

    void ApplyGravity()
    {
        if (controller == null || !controller.enabled) return; 

        if (!isDashing && !isClimbing) 
        {
            if (isWallRunning)
            {
                velocity.y = wallRunGravity; 
            }
            else
            {
                velocity.y += gravity * Time.deltaTime;
                
                if (isWallSliding && velocity.y < wallSlideSpeed)
                {
                    velocity.y = wallSlideSpeed;
                }
            }
        }
        controller.Move(velocity * Time.deltaTime);
    }
    
    void HandleSpeedLines()
    {
        if (speedLinesParticle == null) return;
        
        if (!speedLinesParticle.isPlaying)
        {
            speedLinesParticle.Play();
        }

        float targetEmission = 0f;
        
        if (currentSpeed >= minSpeedToShowLines)
        {
            float speedRange = Mathf.Max(0.1f, runSpeed - minSpeedToShowLines);
            float speedFactor = (currentSpeed - minSpeedToShowLines) / speedRange;
            
            targetEmission = Mathf.Lerp(0f, maxParticleEmission, speedFactor);
        }
        else if (isDashing) 
        {
            targetEmission = maxParticleEmission * 1.5f; 
        }
        
        currentParticleEmission = Mathf.Lerp(currentParticleEmission, targetEmission, fadeSpeed * Time.deltaTime);
        
        var emission = speedLinesParticle.emission;
        emission.rateOverTime = currentParticleEmission;
    }
    
    public float GetCurrentSpeed() 
    {
        return currentSpeed;
    }

    public string GetMovementState()
    {
        if (isDashing) return "DASHING";
        if (isClimbing) return "VAULTING";
        if (isWallRunning) return "WALL RUNNING"; 
        if (isWallSliding) return "WALL SLIDING";
        if (isSliding) return "SLIDING";
        if (!isGrounded) return "IN AIR";
        if (Input.GetKey(KeyCode.LeftControl)) return "CROUCHING";
        if (currentSpeed > walkSpeed + 0.5f) return "RUNNING";
        if (currentSpeed > 0.5f) return "WALKING";
        return "IDLE";
    }
}
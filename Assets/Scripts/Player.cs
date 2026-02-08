using System.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using UnityEngine.EventSystems;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks.CompilerServices;
using Unity.Cinemachine;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine.Rendering;
using System;
using UnityEditor.VersionControl;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Unity.VisualScripting.FullSerializer;

public class Player : MonoBehaviour
{
    public Rigidbody2D rb;
    public Collider2D col;
    public LayerMask playerLayer;

    [Header("Walking")]
    public PlayerInput playerInput;
    public float walkSpeed;
    public int facingDirection = 1;
    public float smoothTime;
    private Vector2 currentVelocity;
    public float coyoteTime;
    private float coyoteCounter;

    //inputs
    private Vector2 moveInput;
    private bool jumpPressed;
    private bool jumpReleased;
    public int jumpCounter;

    [Header("Jumping")]
    public int extraJumps = 2;
    public float jumpForce;
    public float normalGravity;
    public float fallGravity;
    public float jumpGravity;
    public float jumpCooldown;
    private float timeSinceJump;
    public float maxFallSpeed;

    [Header("Dash")]
    public float dashSpeed;
    public bool dashPressed;
    public float dashCooldown;
    private float timeSinceDash;
    private int dashDirection;
    public float dashDuration;
    public float dashGravity;
    private bool isDashing;
    public float dashProportions;
    public float dashFallMultiplier;
    public float dashAcceleration;
    public float dashDirYMod;

    [Header("Dodge")]
    public float dodgeSpeed;
    public bool dodgePressed;
    public float dodgeCooldown;
    private float timeSinceDodge;
    public float dodgeDuration;
    private bool isDodging;
    public float dodgeAcceleration;

    [Header("Smash")]
    public float smashSpeed;
    public float smashGravity;
    public float smashFallMultiplier;
    private bool isSmashing;
    private bool smashPressed;
    public float smashCooldown;
    private float timeSinceSmash;
    private float airTimer;
    public float smashAcceleration;
    private bool smashCancelled;
    public float smashMaxFallSpeed;

    [Header("Crouch")]
    public bool isCrouching;

    [Header("Ground Check")]
    public Transform groundCheck;
    public Vector2 groundCheckSize;
    public LayerMask groundLayer;
    private bool isGrounded;
    public float groundCheckDistance;

    [Header("Wall Check")]
    public Transform wallCheck;
    public Vector2 wallCheckSize;
    private bool touchingWall;
    public float wallCheckDistance;

    [Header("Mantle")]
    public Transform ledgeCheck;
    public Vector2 ledgeCheckSize;
    public float ledgeForwardCheck;
    public Vector2 airCheckSize;
    public Vector2 airCheckOffset;
    public Vector2 backCheckSize;
    public Vector2 backCheckOffset;
    public float ledgeUpCheck;
    public float ledgeDownCheck;
    private bool isMantling;
    public bool ledgeDetected;
    private bool canGrabLedge;
    public Vector2 offset1;
    public Vector2 offset2;
    private Vector2 climbStartPosition;
    private Vector2 climbEndPosition;
    public float mantleAnimTime;
    public float mantleCooldown;
    private float timeSinceMantle;
    private Vector2 detectedLedgePosition;
    private Vector2 ledgePoint;

    [Header("One Way Platforms")]
    public LayerMask oneWayLayer;
    public float dropThroughTime;
    private bool isDropping;
    public int playerLayerInt;
    public int platformLayerInt;
    public float fallThroughDistance;
    public bool platformBelow;
    public bool dropPressed;

    [Header("Camera Shakes")]
    public CinemachineCamera cineCamera;
    private CinemachineBasicMultiChannelPerlin cameraNoise;



    private void Start()
    {
        rb.gravityScale = normalGravity;
        currentVelocity = Vector2.zero;

        playerLayerInt = LayerMask.NameToLayer("Player");
        platformLayerInt = LayerMask.NameToLayer("OneWayPlatform");

        if(cineCamera != null)
        {
            cameraNoise = cineCamera.GetCinemachineComponent(CinemachineCore.Stage.Noise) as CinemachineBasicMultiChannelPerlin;
        }
    }


    void Update()
    {
        if(isDashing || isDodging || isMantling) return;

        Flip();
    }


    void FixedUpdate()
    {
        if(isDashing || isDodging || isMantling) return;

        ApplyVariableGravity();
        CheckGrounded();
        CheckWalls();
        CheckLedge();
        PlatformBelow();
        AirTimer();
        HandleMovement();
        HandleDodge();
        HandleDash();
        HandleSmash();
        HandleJump();
        HandleMantle();
        HandleCrouch();
        HandleCoyote();
        ClampFallSpeed();
    }


    private void HandleMovement()
    {
        if(isDashing || isSmashing || isDodging || isMantling) return;
        
        float targetSpeed = moveInput.x * walkSpeed;
        float newX = Mathf.SmoothDamp(rb.linearVelocity.x, targetSpeed, ref currentVelocity.x, smoothTime); 
        rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
        
    }


    private void HandleCoyote()
    {
        if(isGrounded)
        {
            coyoteCounter = coyoteTime;
        }
        else
        {
            coyoteCounter -= Time.deltaTime;
        }
    }


    private void AirTimer()
    {
        if(isGrounded)
        {
           airTimer = 0f; 
        }
        else
        {
            airTimer += Time.deltaTime;
        }
    }


    private void HandleJump()
    {
        if(isDashing || isSmashing || isDodging || isMantling) return;

        timeSinceJump += Time.deltaTime;

        if(jumpPressed && (isGrounded || jumpCounter > 0 || coyoteCounter > 0f) && timeSinceJump >= jumpCooldown)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpPressed = false;
            jumpReleased = false;
            timeSinceJump = 0f;
            
            if(!isGrounded && coyoteCounter <= 0f)
            {
                jumpCounter--;
            }

            coyoteCounter = 0f;
        }
        if(jumpReleased)
        {
            //nothing for now
            jumpReleased = false;
        }

        if(isGrounded && timeSinceJump >= jumpCooldown) //when ground is touched after cooldown
        {
            jumpCounter = extraJumps;
        }
    }


    private void HandleDash()
    {
        if(jumpCounter <= 0)
        { 
            dashPressed = false; 
            return;
        }

        timeSinceDash += Time.deltaTime;

        if(dashPressed && timeSinceDash >= dashCooldown && jumpCounter > 0)
        {
            _ = Dash();
            dashPressed = false;
            timeSinceDash = 0f;
        }
    }


    private async UniTaskVoid Dash()
    {
        CancelSmash();

        isDashing = true;
        var originalGravity = rb.gravityScale;

        float elapsed = 0f;

        if(jumpCounter > 0 && !isGrounded) jumpCounter -= 1;

        rb.linearVelocity = Vector2.zero;

        Vector2 inputDir = moveInput;
        if(inputDir == Vector2.zero || isGrounded) inputDir = new Vector2(facingDirection, 0);
        if(Mathf.Abs(inputDir.x) < 0.1f) inputDir.x = facingDirection;
        inputDir.Normalize();
        Vector2 dashDir = inputDir;
        dashDir.y *= dashDirYMod;
        dashDir.Normalize();

        while(elapsed < dashDuration)
        {
            //float targetSpeed = dashSpeed * dashDir;
            Vector2 targetVel = dashDir * dashSpeed;

            float maxDelta = dashSpeed / dashDuration * Time.fixedDeltaTime * dashAcceleration;

            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, targetVel, maxDelta);

            float segment1Duration = dashDuration * dashProportions;
            float segment2Duration = dashDuration - segment1Duration;

            if(elapsed < dashDuration * dashProportions)
            {
                //rb.gravityScale = dashGravity;

                float t = elapsed / segment1Duration;
                t = t * t;
                rb.gravityScale = Mathf.Lerp(dashGravity, fallGravity * dashFallMultiplier / 2, t);
            }
            else
            {
                //rb.gravityScale = fallGravity * 2;

                float t = (elapsed - segment1Duration) / segment2Duration;
                t = t * t;
                rb.gravityScale = Mathf.Lerp(fallGravity * dashFallMultiplier / 2, fallGravity * dashFallMultiplier, t);
            }

            bool hitwall = Physics2D.OverlapBox(wallCheck.position, wallCheckSize, 0, groundLayer);

            if(hitwall)
            {
                break;
            }

            elapsed += Time.deltaTime;
            await UniTask.Yield();
        }

        isDashing = false;
        rb.gravityScale = originalGravity;
    }


    private void HandleSmash()
    {
        if(isDashing || isGrounded || isSmashing || isGrounded || isDodging || isMantling) return;

        timeSinceSmash += Time.deltaTime;

        if(smashPressed && timeSinceSmash >= smashCooldown && airTimer >= jumpCooldown && !isGrounded)
        {
            isSmashing = true;
            smashPressed = false;
            jumpPressed = false;
            _ = Smash();
            timeSinceSmash = 0f;
        }
    }


    private async UniTaskVoid Smash()
    {
        smashCancelled = false;
        jumpPressed = false;
        rb.gravityScale = smashGravity;
        var originalVelocityX = rb.linearVelocityX;
        rb.linearVelocityX = 0f;
        float originalMaxFallSpeed = maxFallSpeed;
        maxFallSpeed = smashMaxFallSpeed;

        if(rb.linearVelocity.y > smashSpeed * 0.5f) 
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, smashSpeed * 0.5f);
        }

        while(!isGrounded && isSmashing)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.MoveTowards(rb.linearVelocity.y, smashSpeed, smashAcceleration * Time.deltaTime));

            if((jumpPressed && jumpCounter > 0 && rb.linearVelocity.y <= -1f) || dashPressed || isDashing)
            {
                smashCancelled = true;
                isSmashing = false;
                break;
            }

            await UniTask.WaitForFixedUpdate();
        }

        rb.gravityScale = fallGravity * smashFallMultiplier;
        isSmashing = false;
        rb.linearVelocityX = originalVelocityX;
        maxFallSpeed = originalMaxFallSpeed;

        if(isGrounded && !smashCancelled)
        {
            OnSmashImpact();
        }
        smashCancelled = false;
    }

    
    void CancelSmash()
    {
        if (!isSmashing) return;

        smashCancelled = true;
        isSmashing = false;
        smashPressed = false;

        rb.gravityScale = fallGravity;
    }

    private void OnSmashImpact()
    {
        _ = CameraShake(2f, 2f, 0.25f);
    }


    
    private void HandleDodge()
    {
        timeSinceDodge += Time.deltaTime;

        if(dodgePressed && timeSinceDodge >= dodgeCooldown)
        {
            _ = Dodge();
            dodgePressed = false;
            timeSinceDodge = 0f;
        }
    }


    private async UniTaskVoid Dodge()
    {
        CancelSmash();

        isDodging = true;

        if(jumpCounter > 0 && !isGrounded) jumpCounter -= 1;

        float elapsed = 0f;

        rb.linearVelocity = Vector2.zero;

        while(elapsed < dodgeDuration)
        {
            float targetSpeed = dodgeSpeed * facingDirection;

            float maxDelta = dodgeSpeed / dodgeDuration * Time.deltaTime * dodgeAcceleration;

            rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, maxDelta), rb.linearVelocity.y);

            bool hitwall = Physics2D.OverlapBox(wallCheck.position, wallCheckSize, 0, groundLayer);
            if(hitwall) break;

            elapsed += Time.deltaTime;
            await UniTask.Yield();
        }

        isDodging = false;
    }


    private void HandleCrouch()
    { 
        bool crouchInput = moveInput.y <= -0.2f;

        if(!isGrounded || isSmashing || isDashing || isDodging || isMantling)
        {
            isCrouching = false;
            return;
        }

        if (moveInput.x != 0f)
        {
            isCrouching = false;
            return;
        }

        if(crouchInput)
        {
            isCrouching = true;
        }
        else
        {
            isCrouching = false;
        }
           
    }


    private void HandleMantle()
    {
        timeSinceMantle += Time.deltaTime;
        
        canGrabLedge = false;

        if(isDashing || isSmashing || isDodging || isGrounded || col.IsTouchingLayers(oneWayLayer)) return;

        if(ledgeDetected && rb.linearVelocity.y <= 12 && moveInput.x * facingDirection >= 0.15f && timeSinceMantle >= mantleCooldown) 
            canGrabLedge = true;

        if(ledgeDetected && canGrabLedge)
        {
            timeSinceMantle = 0f;
            canGrabLedge = false; 
            _ = Mantle();
        }
    }

    private async UniTaskVoid Mantle()
    {
        isMantling = true;

        Collider2D ledgeCol =  Physics2D.OverlapBox(ledgeCheck.position, ledgeCheckSize, 0, groundLayer | oneWayLayer);
        if(col != null)
        {
            Bounds b = ledgeCol.bounds;
            ledgePoint = new Vector2(facingDirection > 0 ? b.min.x : b.max.x, b.max.y);
        }

        Vector2 ledgePosition = detectedLedgePosition;

        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;

        climbStartPosition = ledgePosition + new Vector2(offset1.x * facingDirection, offset1.y);
        climbEndPosition = ledgePosition + new Vector2(offset2.x * facingDirection, offset2.y);

        transform.position = climbStartPosition;

        await UniTask.Delay((int)(mantleAnimTime * 1000));

        transform.position = climbEndPosition;
        rb.simulated = true;

        isMantling = false;
    }


    private bool PlatformBelow()
    {
        RaycastHit2D hit = Physics2D.Raycast(
            col.bounds.center,
            Vector2.down,
            fallThroughDistance,
            oneWayLayer
        );

        platformBelow = hit.collider != null;
        return hit.collider != null;
    }


    void ApplyVariableGravity()
    {
        if(rb.linearVelocity.y < -0.1f) // falling
        {
            rb.gravityScale = fallGravity;
        }
        else if (rb.linearVelocity.y > 0.1f) // rising
        {
            rb.gravityScale = jumpGravity;
        }
        else
        {
            rb.gravityScale = normalGravity;
        }
    }


    void ClampFallSpeed()
    {
        if(rb.linearVelocity.y < maxFallSpeed)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, maxFallSpeed);
        }
    }


    public async UniTaskVoid CameraShake(float amplitude, float frequency, float duration)
    {
        if(cameraNoise == null) return;

        cameraNoise.AmplitudeGain = 0f;
        cameraNoise.FrequencyGain = 0f;

        cameraNoise.AmplitudeGain = amplitude;
        cameraNoise.FrequencyGain = frequency;

        await UniTask.Delay((int)(duration * 1000));

        cameraNoise.AmplitudeGain = 0f;
        cameraNoise.FrequencyGain = 0f;
    }


    void CheckGrounded()
    {
        if(rb.linearVelocity.y > 0.2f || isDropping)
        {
            isGrounded = false;
            return;
        }

        RaycastHit2D hit = Physics2D.BoxCast(groundCheck.position, groundCheckSize, 0f, Vector2.down, groundCheckDistance, groundLayer | oneWayLayer);

        isGrounded = hit.collider != null;
    }

    void CheckWalls()
    {
        RaycastHit2D hit = Physics2D.BoxCast(wallCheck.position, wallCheckSize, 0f, Vector2.right * facingDirection, wallCheckDistance, groundLayer | oneWayLayer);

        touchingWall = hit.collider != null;
    }

    void CheckLedge()
    {
        if(col.IsTouchingLayers(oneWayLayer)) return;

        ledgeDetected = false;

        Vector2 dir = Vector2.right * facingDirection;

        Vector2 backCheckPos = new Vector2(ledgeCheck.position.x + backCheckOffset.x * facingDirection, ledgeCheck.position.y + backCheckOffset.y);

        bool backCheck = Physics2D.OverlapBox(
            backCheckPos,
            backCheckSize,
            0f,
            oneWayLayer
        );
        if(backCheck) return;

        RaycastHit2D wallHit = Physics2D.BoxCast(
            ledgeCheck.position,
            ledgeCheckSize,
            0f, dir,
            ledgeForwardCheck,
            groundLayer | oneWayLayer
        );
        if(!wallHit) return;

        Vector2 airCheckPos = wallHit.point + Vector2.up * ledgeUpCheck;

        bool airAbove = Physics2D.OverlapBox(
            airCheckPos,
            airCheckSize,
            0f,
            groundLayer | oneWayLayer
        );
        if(airAbove) return; //airAbove is inverse

        Vector2 topCastOrigin = wallHit.point+Vector2.up * ledgeUpCheck + dir * 0.1f;

        RaycastHit2D topHit = Physics2D.Raycast(
            topCastOrigin,
            Vector2.down,
            ledgeDownCheck,
            groundLayer | oneWayLayer
        );
        if(!topHit) return;

        Bounds b = topHit.collider.bounds;

        float x = facingDirection > 0 ? b.min.x : b.max.x;
        float y = b.max.y;

        detectedLedgePosition = new Vector2(x, y);
        ledgeDetected = true;
    }


    void Flip()
    {
        if (moveInput.x > 0.1f)
        {
            facingDirection = 1;
        }
        else if (moveInput.x < -0.1f)
        {
            facingDirection = -1;
        }

        transform.localScale = new Vector3(facingDirection, 2, 1);
    }


    public void OnMove (InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }


    public void OnJump (InputValue value)
    {
        if(value.isPressed)
        {
            if(!isGrounded && jumpCounter <= 0 && coyoteCounter <= 0f) return;
            jumpPressed = true;
            jumpReleased = false;
        }
        else //jump is released
        {
            jumpReleased = true;
        }
    }


    public void OnDash (InputValue value)
    {
        if(value.isPressed)
        {
            if(timeSinceDash <= dashCooldown) return;
            dashPressed = true;
        }
    }


    public void OnSmash (InputValue value)
    {
        if(value.isPressed && !isGrounded)
        {
            smashPressed = true;
            jumpPressed = false;
            if(platformBelow)
            {
                smashPressed = true;
                dropPressed = true;
            }
        }
        else if(value.isPressed && platformBelow)
        {
            jumpPressed = false;
            smashPressed = false;
            dropPressed = true;
        }
    }


    public void OnDodge (InputValue value)
    {
        if(value.isPressed)
        {
            dodgePressed = true;
        }
        else
        {
            dodgePressed = false;
        }
    }



    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(groundCheck.position + Vector3.down * groundCheckDistance, groundCheckSize);
        Gizmos.DrawWireCube(wallCheck.position + Vector3.right * wallCheckDistance, wallCheckSize);

        Gizmos.color = Color.purple;
        Gizmos.DrawWireCube(ledgeCheck.position + Vector3.right * facingDirection * ledgeForwardCheck, ledgeCheckSize);
        Gizmos.DrawWireCube(ledgeCheck.position + Vector3.up * ledgeUpCheck, airCheckSize);

        Vector2 dir = Vector2.right * facingDirection;

        Vector2 topOrigin =
            ledgeCheck.position + Vector3.up * ledgeUpCheck + (Vector3)(dir * ledgeForwardCheck);

        Gizmos.DrawWireCube(topOrigin, airCheckSize);
        Gizmos.DrawLine(topOrigin, topOrigin + Vector2.down * ledgeDownCheck);
        Gizmos.DrawWireCube(new Vector2(ledgeCheck.position.x + backCheckOffset.x * facingDirection, ledgeCheck.position.y + backCheckOffset.y), backCheckSize);

    }
}

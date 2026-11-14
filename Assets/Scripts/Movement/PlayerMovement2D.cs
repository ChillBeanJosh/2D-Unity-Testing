using System.Collections.Generic;
using UnityEngine;

//Struct to hold player input data for movement and impulse actions:
public struct CharacterInput2D
{
    public Vector2 Move;
    public bool ImpulsePressed;
    public bool SplineRidePressed;
}

public class PlayerMovement2D : MonoBehaviour
{
    [Header("Player Setup:")]
    [SerializeField] private bool isSideScroller;
    [Space]
    [SerializeField] private Transform root;
    [Space]
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Transform wideZoomCameraTarget;
    [Space]
    [SerializeField] private Collider2D playerCollider;

    [Header("-------------------------------------------------------")]
    [Header("Global Physics State:")]
    private Vector2 previousPosition;
    private Vector2 currentPosition;
    private Vector2 currentVelocity;
    private Vector2 totalAcceleration;
    private Vector2 pendingExternalAcceleration;

    [Header("-------------------------------------------------------")]
    [Header("Physics Settings:")]
    [SerializeField] private float mass = 80f;
    [Space]
    [SerializeField] private float gravity = 9.8f;
    [Space]
    [SerializeField] private float dragCoefficient = 8f;
    private float currentDragCoefficient;
    [Space]
    [SerializeField] private float meshRotationSpeed = 10f;

    [Header("-------------------------------------------------------")]
    [Header("Impulse Settings:")]

    [SerializeField] private float jumpStrength = 12f;
    [SerializeField] private float wallJumpStrength = 12f;
    [SerializeField] private float jumpBufferDuration = 0.15f;
    [SerializeField] private float coyoteTimeDuration = 0.12f;

    private Vector2 jumpSurfaceNormal = Vector2.up;
    private bool hasJumpSurface;
    private float jumpBufferTimer;
    private float coyoteTimeTimer;
    [Space]
    [SerializeField] private float dashStrength = 15f;

    [Header("-------------------------------------------------------")]
    [Header("Spline Riding Settings:")]
    [SerializeField] private float minSplineSpeed = 4f;
    [SerializeField] private float splineDriveAcceleration = 8f;
    [Space]
    private ParametricSpline2D _nearbySpline;
    private ParametricSpline2D _activeSpline;
    private bool _isSplineRiding;
    private bool _requestedSplineRide;
    private float _splineDistance;
    private float _splineSpeed;

    [Header("-------------------------------------------------------")]
    [Header("Collision Parameters:")]
    [SerializeField] private LayerMask collisionLayers = Physics2D.DefaultRaycastLayers;
    [SerializeField] private int maxBounces = 5;
    [SerializeField] float skinWidth = 0.015f;
    [SerializeField] float maxSlopeAngle = 45f;
    [SerializeField] private float collisionRadius = 0.3f;
    [SerializeField] private float groundCheckDistance = 0.05f;
    private bool isGrounded;
    private Vector2 groundNormal = Vector2.up;

    [Header("-------------------------------------------------------")]
    [Header("Stuck Prevention Settings: ")]
    [SerializeField] private float maxStuckDuration = 0.2f;
    private float stuckTimer = 0f;

    [Header("-------------------------------------------------------")]
    [Header("Arena Bounding Box (2D)")]
    [SerializeField] private bool useBoundingBox = true;
    [Space]
    [SerializeField] private Vector2 boxMin = new Vector2(-15f, -15f);
    [SerializeField] private Vector2 boxMax = new Vector2(15f, 15f);
    [Space]
    [SerializeField] private float boxSpringConstant = 300f;

    [Header("-------------------------------------------------------")]
    [Header("Vector Visualization: ")]
    [SerializeField] private bool showDebugGizmos = true;
    [Space]
    [SerializeField] private Color individualAccelerationColor = Color.blue;
    [SerializeField] private Color totalAccelerationColor = Color.red;
    [Space]
    [SerializeField] private Color velocityColor = Color.green;
    [Space]
    [SerializeField] private Color trajectoryColor = Color.yellow;
    [Space]
    [SerializeField] private int trajectorySteps = 30;
    [Space]

 
    private Vector2 _requestedMovement;
    private bool _requestedImpulse;

    private struct CollisionDebugHit2D
    {
        public Vector2 point;
        public Vector2 normal;
        public CollisionDebugHit2D(Vector2 point, Vector2 normal)
        {
            this.point = point;
            this.normal = normal;
        }
    }

    private readonly List<CollisionDebugHit2D> _collisionDebugHits = new List<CollisionDebugHit2D>();
    private List<Vector2> _activeAccelerations = new List<Vector2>();
    private const float Epsilon = 0.0001f;

    public float SplineDriveAcceleration { get => splineDriveAcceleration; set => splineDriveAcceleration = value; }

    void Awake()
    {
        if (playerCollider == null)
        {
            Debug.LogError("NEEDS COLLIDER 2D COMPONENT~!!!!");
            enabled = false;
            return;
        }

        //Initialize Position Data:
        currentPosition = transform.position;
        previousPosition = currentPosition;

        //Initialize Default Drag Coefficient:
        currentDragCoefficient = dragCoefficient;
    }

    #region Update Read
    public void UpdateInput(CharacterInput2D input)
    {
        Vector2 moveInput = input.Move;

        // If side-scroller mode is active, strip out vertical input
        if (isSideScroller)
        {
            moveInput.y = 0f;
        }

        _requestedMovement = Vector2.ClampMagnitude(moveInput, 1f);
        _requestedImpulse |= input.ImpulsePressed;
        _requestedSplineRide |= input.SplineRidePressed;
    }

    public void UpdateBody(float deltaTime)
    {
        //Player Position Update Interpolation:
        float interpolationFactor = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
        root.position = Vector2.Lerp(previousPosition, currentPosition, interpolationFactor);

        if (isSideScroller)
        {
            if (Mathf.Abs(currentVelocity.x) > Epsilon)
            {
                //Compute Target Rotation Direction, based off the players direction:
                float targetAngle = currentVelocity.x > 0f ? 0f : 180f;

                //Apply Target Rotation to the Player:
                Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
                root.rotation = Quaternion.Slerp(root.rotation, targetRotation, meshRotationSpeed * deltaTime);
            }
        }
        else
        {
            //Player Rotation Update based on Velocity Vector:
            Vector2 facingDirection = currentVelocity;

            if (facingDirection.sqrMagnitude > Epsilon * Epsilon)
            {
                //Compute The Z-Axis, Using the XY Vector:
                float targetAngle = Mathf.Atan2(facingDirection.y, facingDirection.x) * Mathf.Rad2Deg;

                //Apply the Z Rotation to the Player:
                Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
                root.rotation = Quaternion.Slerp(root.rotation, targetRotation, meshRotationSpeed * deltaTime);
            }
        }
    }
    #endregion

    #region Physics Functions
    #region Force Control Functions
    //1.) Add Constant Acceleration to Accumulator:
    public void AddAcceleration(Vector2 acceleration)
    {
        totalAcceleration += acceleration;
        _activeAccelerations.Add(acceleration);
    }

    //2.) Add Mass Based Acceleration to Accumulator:
    public void AddForce(Vector2 force)
    {
        pendingExternalAcceleration += force / mass;
    }

    //3.) Add Stronger (Velocity) Force after Accumulator:
    public void AddImpulse(Vector2 impulse)
    {
        currentVelocity += impulse / mass;
    }

    public void AddExternalAcceleration(Vector2 acceleration)
    {
        pendingExternalAcceleration += acceleration;
    }
    #endregion

    #region Constant Physics
    public void GatherIntrinsicAccelerations(float moveAcceleration)
    {
        totalAcceleration = Vector2.zero;
        _activeAccelerations.Clear();

        // Apply Pending External Accelerations:
        AddAcceleration(pendingExternalAcceleration);
        pendingExternalAcceleration = Vector2.zero;


        //SPLINE RIDING PHYSICS:
        if (_isSplineRiding && _activeSpline != null)
        {
            Vector2 tangent = _activeSpline.GetTangentAtDistance(_splineDistance);

            //1.) Directional Input Acceleration:
            //Input in the direction of travel increases speed.
            //Input against the direction of travel reduces speed.

            float driveInput = Mathf.Clamp(Vector2.Dot(_requestedMovement, tangent), -1f, 1f);
            AddAcceleration(tangent * driveInput * SplineDriveAcceleration);


            //2.) Curvature Acceleration:
            //Calculate the normal acceleration required to follow the curve.

            Vector2 curvature = _activeSpline.GetCurvatureVectorAtDistance(_splineDistance);
            Vector2 splineAcceleration = curvature * (_splineSpeed * _splineSpeed);
            AddAcceleration(splineAcceleration);

            return;
        }


        //EXISTING PLAYER PHYSICS:

        //1.) Player Acceleration:
        if (_requestedMovement.sqrMagnitude > Epsilon)
        {
            Vector2 appliedAcceleration = _requestedMovement * moveAcceleration;

            if (isSideScroller)
            {
                appliedAcceleration.y = 0f;
            }

            AddAcceleration(appliedAcceleration);
        }

        //2.) Apply Drag/Friction:
        Vector2 dragAcceleration = -currentVelocity * currentDragCoefficient;

        AddAcceleration(dragAcceleration);

        //3.) Apply Gravity:
        if (isSideScroller)
        {
            if (!isGrounded)
            {
                Vector2 gravityAcceleration = Vector2.down * gravity;

                AddAcceleration(gravityAcceleration);
            }
            else
            {
                if (currentVelocity.y < 0f)
                {
                    currentVelocity.y = 0f;
                }
            }
        }
    }

    public void GatherIntrinsicPhysics()
    {
        bool bufferRefreshedThisFrame = false;

        //1.) Process Spline Input:
        if (_requestedSplineRide)
        {
            if (_isSplineRiding)
            {
                StopSplineRide();
            }
            else
            {
                TryStartSplineRide();
            }

            _requestedSplineRide = false;
        }


        //2.) Spline Riding Physics:
        if (_isSplineRiding)
        {
            //Spacebar to Exit Spline Early:
            if (_requestedImpulse)
            {
                StopSplineRide();

                _requestedImpulse = false;
            }

            return;
        }


        //3.) Existing Physics:
        ApplyBoundingBoxForces();


        //4.) Update Coyote Timer:
        //Refresh while grounded, otherwise count down.

        if (isGrounded)
        {
            coyoteTimeTimer = coyoteTimeDuration;
        }
        else
        {
            coyoteTimeTimer = Mathf.Max(
                0f,
                coyoteTimeTimer - Time.fixedDeltaTime
            );
        }


        //5.) Process Jump / Dash Input:
        if (_requestedImpulse)
        {
            if (isSideScroller)
            {
                //Refresh Jump Buffer:
                jumpBufferTimer = jumpBufferDuration;

                bufferRefreshedThisFrame = true;
            }
            else
            {
                //Existing Top-Down Dash:
                Vector2 dashDirection =
                    _requestedMovement.sqrMagnitude > Epsilon
                    ? _requestedMovement.normalized
                    : currentVelocity.normalized;

                AddImpulse(dashDirection * dashStrength);
            }

            _requestedImpulse = false;
        }


        //6.) Process Buffered Jump:
        if (isSideScroller && jumpBufferTimer > 0f)
        {
            bool jumped = false;


            //GROUND JUMP / COYOTE TIME HAS PRIORITY:
            if (isGrounded || coyoteTimeTimer > 0f)
            {
                ApplySurfaceJump(groundNormal, true);

                jumped = true;
            }


            //WALL JUMP:
            else if (hasJumpSurface)
            {
                ApplySurfaceJump(jumpSurfaceNormal, false);

                jumped = true;
            }


            //7.) Consume Jump Buffer:
            if (jumped)
            {
                jumpBufferTimer = 0f;
            }
            else if (!bufferRefreshedThisFrame)
            {
                jumpBufferTimer = Mathf.Max(
                    0f,
                    jumpBufferTimer - Time.fixedDeltaTime
                );
            }
        }
    }

    public void ApplyMovement()
    {
        float dt = Time.fixedDeltaTime;

        if (_isSplineRiding)
        {
            ApplySplineMovement(dt);
            return;
        }


        _collisionDebugHits.Clear();

        previousPosition = currentPosition;

        //Semi Implicit Euler, Computes Velocity + Position Displacement, using TotalAcceleration:
        currentVelocity += totalAcceleration * dt;
        Vector2 desiredDisplacement = currentVelocity * dt;

        //Initial Ground Check:
        if (isSideScroller) UpdateGroundedState();

        //Check Collision Update, Then Apply to Current Position using Position Displacement:
        Vector2 resolvedDisplacement = CollideAndSlide(desiredDisplacement, currentPosition, 0, false, currentVelocity);

        bool isTryingToMove = desiredDisplacement.sqrMagnitude > (Epsilon * Epsilon);
        bool movementBlocked = resolvedDisplacement.sqrMagnitude <= (Epsilon * Epsilon);

        if (isTryingToMove && movementBlocked)
        {
            stuckTimer += dt;
            if (stuckTimer >= maxStuckDuration)
            {
                Debug.LogWarning($"[PlayerMovement2D] Player stuck detected! Executing unstick routine at position: {currentPosition}");

                // Unstick: Push player outward along ground normal or straight up
                Vector2 unstickDirection = isGrounded && groundNormal != Vector2.zero ? groundNormal : Vector2.up;
                currentPosition += unstickDirection * (collisionRadius * 0.75f);

                currentVelocity = Vector2.zero;
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }


        currentPosition += resolvedDisplacement;

        if (dt > Epsilon) currentVelocity = resolvedDisplacement / dt;
        transform.position = currentPosition;

        //Ground Check After Position Update:
        if (isSideScroller) UpdateGroundedState();
    }

    #endregion

    #region Collision Info
    private Vector2 CollideAndSlide(Vector2 velocity, Vector2 position, int depth, bool gravityPass, Vector2 initialVelocity)
    {
        if (depth > maxBounces) return Vector2.zero;
        if (velocity.sqrMagnitude <= Epsilon * Epsilon) return Vector2.zero;

        Vector2 direction = velocity.normalized;
        float distance = velocity.magnitude + skinWidth;

        //Player Collider:
        RaycastHit2D hit = Physics2D.CircleCast(position, collisionRadius, direction, distance, collisionLayers);

        //Push away from normal if inside:
        if (hit.collider != null)
        {
            //Register Surface For Jump Detection:
            RegisterJumpSurface(hit);
            _collisionDebugHits.Add(new CollisionDebugHit2D(hit.point, hit.normal));

            if (hit.distance <= 0f)
            {
                // Embedded overlap escape
                Vector2 pushOut = hit.normal == Vector2.zero ? Vector2.up : hit.normal;
                return pushOut * 0.05f;
            }
        }

        if (hit.collider == null) return velocity;

        float travelDistance = Mathf.Max(0f, hit.distance - skinWidth);
        Vector2 snapToSurface = direction * travelDistance;
        Vector2 leftOver = velocity - snapToSurface;

        float angle = Vector2.Angle(Vector2.up, hit.normal);
        if (angle <= maxSlopeAngle)
        {
            //GROUNDED SURFACE / FLOOR COLLISION:

            isGrounded = true;
            groundNormal = hit.normal;

            if (gravityPass) return snapToSurface;
            leftOver = ProjectAndScale(leftOver, hit.normal);
        }
        else
        {
            //STEPPED SURFACE / WALL COLLISION:

            if (isSideScroller)
            {
                //1.) Project Remaining Movement Along Surface:
                leftOver = ProjectAndScale(leftOver, hit.normal);

                //2.) Prevent Upward Wall Climbing:
                if (leftOver.y > 0f)
                {
                    leftOver.y = 0f;
                }
            }
            else
            {
                //Existing Top-Down Steep Surface Sliding:
                Vector2 tangent = new Vector2(-hit.normal.y, hit.normal.x);

                float dotTangent = Vector2.Dot(leftOver.normalized, tangent);

                if (Mathf.Abs(dotTangent) > 0.001f)
                {
                    leftOver = tangent * (leftOver.magnitude * Mathf.Sign(dotTangent));
                }
                else
                {
                    leftOver = ProjectAndScale(leftOver, hit.normal);
                }
            }
        }

        if (snapToSurface.sqrMagnitude <= Epsilon * Epsilon) snapToSurface = Vector2.zero;
        if (leftOver.sqrMagnitude <= Epsilon * Epsilon) leftOver = Vector2.zero;

        Vector2 recursiveResult = CollideAndSlide(leftOver, position + snapToSurface, depth + 1, gravityPass, initialVelocity);
        return snapToSurface + recursiveResult;
    }

    private Vector2 ProjectAndScale(Vector2 vector, Vector2 normal)
    {
        float magnitude = vector.magnitude;
        if (magnitude <= Epsilon) return Vector2.zero;

        Vector2 projected = vector - normal * Vector2.Dot(vector, normal);

        if (projected.sqrMagnitude <= Epsilon * Epsilon) return Vector2.zero;
        return projected.normalized * magnitude;
    }

    private void UpdateGroundedState()
    {
        isGrounded = false;

        //Default to normal drag:
        currentDragCoefficient = dragCoefficient;

        float castRadius = Mathf.Max(
            0.001f,
            collisionRadius - skinWidth
        );

        float castDistance =
            groundCheckDistance + skinWidth + 0.05f;

        RaycastHit2D hit = Physics2D.CircleCast(
            currentPosition,
            castRadius,
            Vector2.down,
            castDistance,
            collisionLayers
        );

        if (hit.collider == null)
            return;

        float angle = Vector2.Angle(Vector2.up, hit.normal);

        if (angle <= maxSlopeAngle)
        {
            isGrounded = true;
            groundNormal = hit.normal;

            //Check for Custom Surface Properties:
            FrictionSurface2D surface = hit.collider.GetComponentInParent<FrictionSurface2D>();

            if (surface != null)
            {
                currentDragCoefficient = surface.DragCoefficient;
            }
        }
    }
    #endregion
    #endregion


    #region Spline Functions
    #region Spline Ride Trigger Assignment
    //Assign Spline To Player When In Trigger:
    public void RegisterNearbySpline(ParametricSpline2D spline)
    {
        _nearbySpline = spline;
    }

    //Unassign Spline From Player When Out Trigger:
    public void UnregisterNearbySpline(ParametricSpline2D spline)
    {
        if (_nearbySpline == spline)
        {
            _nearbySpline = null;
        }
    }
    #endregion

    #region Spline Ride Entry/Exit
    private void TryStartSplineRide()
    {
        //Ensure a Spline is Assigned:
        if (_nearbySpline == null) return;

        //Ensure spline has been calculated:
        if (_nearbySpline.functions.Count == 0)
        {
            _nearbySpline.CalculateSpline();
        }
        //If already calculated, begin table of time[1] to distance[2]:
        else
        {
            _nearbySpline.RebuildArcLengthTable();
        }

        //After calculating, if distance is short, exit:
        if (_nearbySpline.TotalLength <= Epsilon) return;



        //If not too short, assign as [ACTIVE]:
        _activeSpline = _nearbySpline;

        //Find the closest point to the player:
        _splineDistance = _activeSpline.FindClosestDistance(currentPosition);

        //Get the direction of travel:
        Vector2 tangent = _activeSpline.GetTangentAtDistance(_splineDistance);

        //Preserve Existing Forward Velocity, Enforce Minimum Speed:
        _splineSpeed = Mathf.Max
        (
            minSplineSpeed,
            Vector2.Dot(currentVelocity, tangent)
        );

        //Compute Position on Spline, given current distance:
        currentPosition = _activeSpline.EvaluateAtDistance(_splineDistance);

        //Store and Apply the Computed Position:
        previousPosition = currentPosition;
        transform.position = currentPosition;

        //[MANUALLY] assign velocity/speed while on the spline:
        currentVelocity = tangent * _splineSpeed;
    
        _isSplineRiding = true;
        Debug.Log("[PlayerMovement2D] Started spline riding!");
    }


    private void StopSplineRide()
    {
        if (_isSplineRiding && _activeSpline != null)
        {
            Vector2 tangent = _activeSpline.GetTangentAtDistance(_splineDistance);

            //Preserve the current direction and speed:
            currentVelocity = tangent * _splineSpeed;
        }

        //Disable Checks:
        _isSplineRiding = false;
        _activeSpline = null;

        Debug.Log("[PlayerMovement2D] Stopped spline riding!");
    }
    #endregion

    #region Spline Physics
    private void ApplySplineMovement(float dt)
    {
        _collisionDebugHits.Clear();
        previousPosition = currentPosition;

        //End When Spline Ride Exits:
        if (_activeSpline == null)
        {
            StopSplineRide();
            return;
        }


        //1.) Get Current Spline Direction:
        Vector2 tangent = _activeSpline.GetTangentAtDistance(_splineDistance);


        //2.) Update Spline Speed:
        //Only acceleration in the tangent direction changes travel speed.

        float tangentialAcceleration = Vector2.Dot(totalAcceleration, tangent);
        _splineSpeed = Mathf.Max
        (
            minSplineSpeed,
            _splineSpeed + tangentialAcceleration * dt
        );


        //3.) Calculate Next Spline Distance:
        float oldDistance = _splineDistance;

        float nextDistance = Mathf.Min
        (
            oldDistance + _splineSpeed * dt,
            _activeSpline.TotalLength
        );


        //4.) Evaluate Next Position:
        Vector2 targetPosition = _activeSpline.EvaluateAtDistance(nextDistance);
        Vector2 desiredDisplacement = targetPosition - currentPosition;

     
        //5.) Resolve Movement Using Existing Collision System:
        Vector2 resolvedDisplacement = Vector2.zero;
        if (desiredDisplacement.sqrMagnitude > Epsilon * Epsilon)
        {
            resolvedDisplacement = CollideAndSlide
            (
                desiredDisplacement,
                currentPosition,
                0,
                false,
                currentVelocity
            );
        }


        //6.) Determine How Much of the Path Was Traversed:
        float distanceSquared = desiredDisplacement.sqrMagnitude;

        float progressFraction = distanceSquared > Epsilon * Epsilon
            ? Mathf.Clamp01(Vector2.Dot(resolvedDisplacement, desiredDisplacement) / distanceSquared)
            : 1f;


        //7.) Update Position and Spline Progress:
        currentPosition += resolvedDisplacement;
        transform.position = currentPosition;

        _splineDistance = Mathf.Lerp
        (
            oldDistance,
            nextDistance,
            progressFraction
        );


        //8.) Update Velocity Direction:
        Vector2 newTangent =_activeSpline.GetTangentAtDistance(_splineDistance);
        currentVelocity = newTangent * _splineSpeed;


        //9.) Handle Blocked Movement:
        if (desiredDisplacement.sqrMagnitude > Epsilon * Epsilon && progressFraction <= 0.001f)
        {
            stuckTimer += dt;

            if (stuckTimer >= maxStuckDuration)
            {
                Debug.LogWarning("[PlayerMovement2D] Spline movement blocked!");

                StopSplineRide();
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }


        //10.) Stop When the End of the Spline Is Reached:
        if (_isSplineRiding && _splineDistance >= _activeSpline.TotalLength - Epsilon)
        {
            StopSplineRide();
        }


        if (isSideScroller)
        {
            UpdateGroundedState();
        }
    }
    #endregion
    #endregion

    #region External Forces
    private void ApplyBoundingBoxForces()
    {
        if (!useBoundingBox) return;

        //Player Radius + Initialization of Box Force Vector:
        float radius = collisionRadius;
        Vector2 boxForce = Vector2.zero;

        //[Left] Boundary Distance:
        if (currentPosition.x < boxMin.x + radius)
        {
            boxForce.x += boxSpringConstant * ((boxMin.x + radius) - currentPosition.x);
        }
        //[Right] Boundary Distance:
        else if (currentPosition.x > boxMax.x - radius)
        {
            boxForce.x -= boxSpringConstant * (currentPosition.x - (boxMax.x - radius));
        }

        //[Bottom] Boundary Distance:
        if (currentPosition.y < boxMin.y + radius)
        {
            boxForce.y += boxSpringConstant * ((boxMin.y + radius) - currentPosition.y);
        }
        //[Top] Boundary Distance:
        else if (currentPosition.y > boxMax.y - radius)
        {
            boxForce.y -= boxSpringConstant * (currentPosition.y - (boxMax.y - radius));
        }

        //Apply Force to Player, using distance from bounding box edges to calculate spring force:
        if (boxForce.sqrMagnitude > Epsilon) AddForce(boxForce);
    }

    private void RegisterJumpSurface(RaycastHit2D hit)
    {
        if (hit.collider == null) return;

        //Calculate Surface Angle:
        float surfaceAngle = Vector2.Angle(Vector2.up, hit.normal);

        //Only Register Non-Walkable Surfaces:
        if (surfaceAngle <= maxSlopeAngle) return;

        jumpSurfaceNormal = hit.normal.normalized;
        hasJumpSurface = true;
    }

    private void ApplySurfaceJump(Vector2 surfaceNormal, bool isGroundJump)
    {
        //1.) Get Direction Away From Surface:
        Vector2 jumpDirection = surfaceNormal.normalized;


        //2.) Remove Velocity Into Surface:
        float velocityIntoSurface = Vector2.Dot(currentVelocity, jumpDirection);

        if (velocityIntoSurface < 0f)
        {
            currentVelocity -= jumpDirection * velocityIntoSurface;
        }


        //3.) Apply Jump Velocity:
        if (isGroundJump)
        {
            //GROUND JUMP:
            currentVelocity += jumpDirection * jumpStrength;

            //Consume Coyote Time:
            coyoteTimeTimer = 0f;
        }
        else
        {
            //WALL JUMP:
            Vector2 wallJumpVelocity = Vector2.up * jumpStrength + jumpDirection * wallJumpStrength;
            currentVelocity += wallJumpVelocity;
        }


        //4.) Reset Surface States:
        isGrounded = false;
        hasJumpSurface = false;
    }

    #endregion

    #region Getter Functions
    public Transform GetCameraTarget() => cameraTarget;
    public Transform GetWideZoomCameraTarget() => wideZoomCameraTarget;

    public Vector2 GetTotalAcceleration() => totalAcceleration;
    public Vector2 GetCurrentVelocity() => currentVelocity;
    public Vector2 GetCurrentPosition() => currentPosition;

    public float GetDragCoefficient() => dragCoefficient;
    public void SetDragCoefficient(float newDrag)
    {
        dragCoefficient = Mathf.Max(0f, newDrag);
    }


    public bool GetGroundedStatus() => isGrounded;
    public void SetSideScroller(bool toggled)
    {
        isSideScroller = toggled;
    }
    #endregion

    #region Gizmos Visualization
    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        

        //INDEPENDENT ACCELERATION VECTORS:
        Gizmos.color = individualAccelerationColor;
        foreach (var acc in _activeAccelerations)
        {
            Gizmos.DrawRay(currentPosition, acc);
        }

        //TOTAL ACCELERATION VECTOR:
        Gizmos.color = totalAccelerationColor;
        Gizmos.DrawRay(currentPosition, totalAcceleration);

        //CURRENT VELOCITY VECTOR:
        Gizmos.color = velocityColor;
        Gizmos.DrawRay(currentPosition, currentVelocity);

        //POSITION TRAJECTORY PATH:
        Gizmos.color = trajectoryColor;
        Vector2 simPosition = currentPosition;
        Vector2 simVelocity = currentVelocity;
        float dt = Time.fixedDeltaTime;

        for (int i = 0; i < trajectorySteps; i++)
        {
            simVelocity += totalAcceleration * dt;
            Vector2 nextSimPosition = simPosition + simVelocity * dt;

            Gizmos.DrawLine(simPosition, nextSimPosition);
            simPosition = nextSimPosition;
        }



        //PLAYER COLLISION CIRCLE:
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(currentPosition, collisionRadius);

        //COLLISION DEBUG HITS:
        Gizmos.color = Color.cyan;
        foreach (CollisionDebugHit2D collision in _collisionDebugHits)
        {
            Gizmos.DrawSphere(collision.point, 0.15f);
            Gizmos.DrawRay(collision.point, collision.normal);
        }



        //2D BOUNDING BOX:
        Gizmos.color = Color.magenta;
        Vector2 boxCenter = (boxMin + boxMax) * 0.5f;
        Vector2 boxSize = boxMax - boxMin;
        Gizmos.DrawWireCube(boxCenter, boxSize);
    }

    #endregion
}

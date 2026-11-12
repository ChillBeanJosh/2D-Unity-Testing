using System.Collections.Generic;
using UnityEngine;

//Struct to hold player input data for movement and impulse actions:
public struct CharacterInput2D
{
    public Vector2 Move;
    public bool ImpulsePressed;
}

public class PlayerMovement2D : MonoBehaviour
{
    [Header("Player Setup: ")]
    [SerializeField] private bool isSideScroller;
    [Space]
    [SerializeField] private Transform root;
    [Space]
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Transform wideZoomCameraTarget;
    [Space]
    [SerializeField] private Collider2D playerCollider;
    [Space]


    [Header("Global Physics State")]
    private Vector2 previousPosition;
    private Vector2 currentPosition;
    private Vector2 currentVelocity;
    private Vector2 totalAcceleration;


    [Header("Physics Settings")]
    [SerializeField] private float mass = 80f;
    [Space]
    [SerializeField] private float gravity = 9.8f;
    [Space]
    [SerializeField] private float dragCoefficient = 8f;
    [Space]
    [SerializeField] private float meshRotationSpeed = 10f;


    [Header("Impulse Settings (Jump / Dash): ")]
    [SerializeField] private float jumpStrength = 12f;
    [SerializeField] private float dashStrength = 15f;
    [Space]


    [Header("Collision Parameters: ")]
    [SerializeField] private LayerMask collisionLayers = Physics2D.DefaultRaycastLayers;
    [SerializeField] private int maxBounces = 5;
    [SerializeField] float skinWidth = 0.015f;
    [SerializeField] float maxSlopeAngle = 45f;
    [SerializeField] private float collisionRadius = 0.3f;
    [SerializeField] private float groundCheckDistance = 0.05f;
    private bool isGrounded;
    private Vector2 groundNormal = Vector2.up;


    [Header("Stuck Prevention Settings: ")]
    [SerializeField] private float maxStuckDuration = 0.2f;
    private float stuckTimer = 0f;


    [Header("Arena Bounding Box (2D)")]
    [SerializeField] private bool useBoundingBox = true;
    [Space]
    [SerializeField] private Vector2 boxMin = new Vector2(-15f, -15f);
    [SerializeField] private Vector2 boxMax = new Vector2(15f, 15f);
    [Space]
    [SerializeField] private float boxSpringConstant = 300f;
    [Space]


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
    }
 
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
        AddAcceleration(force / mass);
    }

    //3.) Add Stronger (Velocity) Force after Accumulator:
    public void AddImpulse(Vector2 impulse)
    {
        currentVelocity += impulse / mass;
    }
    #endregion

    #region Constant Physics
    public void GatherIntrinsicAccelerations(float moveAcceleration)
    {
        _activeAccelerations.Clear();
        totalAcceleration = Vector2.zero;

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

        //2.) Apply Drag/Friction Force: 
        Vector2 dragAcceleration = -currentVelocity * dragCoefficient;
        AddAcceleration(dragAcceleration);

        //3.) Apply Gravity:
        if (isSideScroller)
        {
            if (!isGrounded)
            {
                //Apply Falling Force:
                Vector2 gravityAcceleration = Vector2.down * gravity;
                AddAcceleration(gravityAcceleration);
            }
            else
            {
                //Remove downwards Force, to avoid Ground Clipping:
                if (currentVelocity.y < 0f)
                {
                    currentVelocity.y = 0f;
                }
            }
        }       
    }

    public void GatherIntrinsicPhysics()
    {
        //Bounding Box Spring Force:
        ApplyBoundingBoxForces();


        //On Input [Spacebar]:
        if (_requestedImpulse)
        {
            if(isSideScroller)
            {
                //Jump Logic:
                if (isGrounded)
                {
                    currentVelocity.y = 0f;
                    AddImpulse(Vector2.up * jumpStrength);

                    isGrounded = false;
                }
            } 
            else
            {
                //Dash Logic:

                //If moving use input logic, else use players current direction:
                Vector2 dashDirection = _requestedMovement.sqrMagnitude > Epsilon ? _requestedMovement.normalized : currentVelocity.normalized;
                AddImpulse(dashDirection * dashStrength);
            }
            _requestedImpulse = false;
        }
    }

    public void ApplyMovement()
    {
        float dt = Time.fixedDeltaTime;
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

    private Vector2 CollideAndSlide(Vector2 velocity, Vector2 position, int depth, bool gravityPass, Vector2 initialVelocity)
    {
        if (depth > maxBounces) return Vector2.zero;
        if (velocity.sqrMagnitude <= Epsilon * Epsilon) return Vector2.zero;

        Vector2 direction = velocity.normalized;
        float distance = velocity.magnitude + skinWidth;

        RaycastHit2D hit = Physics2D.CircleCast(position, collisionRadius, direction, distance, collisionLayers);

        // If stuck directly inside a collider, push out immediately along inverse normal or Up
        if (hit.collider != null)
        {
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
            isGrounded = true;
            groundNormal = hit.normal;

            if (gravityPass) return snapToSurface;
            leftOver = ProjectAndScale(leftOver, hit.normal);
        }
        else
        {
            // Cubic Spline Corner/Steep Slope Safe Slide:
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
        groundNormal = Vector2.up;

        float castRadius = Mathf.Max(0.001f, collisionRadius - skinWidth);
        float castDistance = groundCheckDistance + skinWidth + 0.05f;

        RaycastHit2D hit = Physics2D.CircleCast(currentPosition, castRadius, Vector2.down, castDistance, collisionLayers);
        if (hit.collider == null) return;

        float angle = Vector2.Angle(Vector2.up, hit.normal);
        if (angle <= maxSlopeAngle)
        {
            isGrounded = true;
            groundNormal = hit.normal;
        }
    }

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

    #region Getter Functions
    public Transform GetCameraTarget() => cameraTarget;
    public Transform GetWideZoomCameraTarget() => wideZoomCameraTarget;

    public Vector2 GetTotalAcceleration() => totalAcceleration;
    public Vector2 GetCurrentVelocity() => currentVelocity;
    public Vector2 GetCurrentPosition() => currentPosition;

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

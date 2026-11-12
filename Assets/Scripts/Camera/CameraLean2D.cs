using UnityEngine;

public class CameraLean2D : MonoBehaviour
{
    [SerializeField] private float attackDamping = 0.5f;
    [SerializeField] private float decayDamping = 0.3f;
    [Space]
    [Range(0f, 1f)]
    [SerializeField] private float walkLeanStrength = 0.075f;
    [Range(0f, 1f)]
    [SerializeField] private float slideLeanStrength = 0.2f;
    [Space]
    [SerializeField] private float leanStrengthResponse = 5f;

    private Vector2 _dampedAcceleration;
    private Vector2 _dampedAccelerationVelocity;
    private float _smoothLeanStrength;

    public void Initialize()
    {
        _smoothLeanStrength = walkLeanStrength;
    }

    public void UpdateLean(float deltaTime, bool sliding, Vector2 acceleration)
    {
        var damping = acceleration.magnitude > _dampedAcceleration.magnitude ? attackDamping : decayDamping;

        _dampedAcceleration = Vector2.SmoothDamp(
            current: _dampedAcceleration,
            target: acceleration,
            currentVelocity: ref _dampedAccelerationVelocity,
            smoothTime: damping,
            maxSpeed: float.PositiveInfinity,
            deltaTime: deltaTime
        );

        // Reset local rotation
        transform.localRotation = Quaternion.identity;

        var targetLeanStrength = sliding ? slideLeanStrength : walkLeanStrength;
        _smoothLeanStrength = Mathf.Lerp(_smoothLeanStrength, targetLeanStrength, 1f - Mathf.Exp(-leanStrengthResponse * deltaTime));

        // In 2D, horizontal acceleration drives Z-axis rotation (banking/leaning)
        float leanAngle = -_dampedAcceleration.x * _smoothLeanStrength;
        transform.localRotation = Quaternion.Euler(0f, 0f, leanAngle);
    }

    public void UpdateLeanStrengthValue(float newValue)
    {
        slideLeanStrength = newValue;
    }
}

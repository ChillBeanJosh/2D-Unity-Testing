using UnityEngine;

public class CameraSpring2D : MonoBehaviour
{
    private Vector2 _springPosition;
    private Vector2 _springVelocity;

    [Header("Damping Ratio Settings:")]
    [Range(0f, 2f)]
    [SerializeField] private float dampingRatio = 0.5f;

    [Header("Oscillation Speed:")]
    [SerializeField] private float frequency = 18f;

    [Header("Spring Offset Settings:")]
    [SerializeField] private float angularDisplacement = 2f;
    [SerializeField] private float linearDisplacement = 0.05f;

    public void Initialize()
    {
        _springPosition = transform.localPosition;
        _springVelocity = Vector2.zero;
    }

    public void UpdateSpring(float deltaTime)
    {
        transform.localPosition = Vector3.zero;

        // Solve 2D spring mechanics
        Spring(ref _springPosition, ref _springVelocity, Vector2.zero, dampingRatio, frequency, deltaTime);

        // Apply Z-axis tilt rotation and 2D local position offset
        float tiltAngle = _springPosition.x * angularDisplacement;
        transform.localRotation = Quaternion.Euler(0f, 0f, -tiltAngle);
        transform.localPosition = new Vector3(_springPosition.x * linearDisplacement, _springPosition.y * linearDisplacement, 0f);
    }

    private void Spring(ref Vector2 currentPosition, ref Vector2 currentVelocity, Vector2 targetPosition, float dampingRatio, float angularFrequency, float timeStep)
    {
        var f = 1.0f + 2.0f * timeStep * dampingRatio * angularFrequency;
        var oo = angularFrequency * angularFrequency;
        var hoo = timeStep * oo;
        var hhoo = timeStep * hoo;
        var detInv = 1.0f / (f + hhoo);
        var detX = f * currentPosition + timeStep * currentVelocity + hhoo * targetPosition;
        var detV = currentVelocity + hoo * (targetPosition - currentPosition);
        currentPosition = detX * detInv;
        currentVelocity = detV * detInv;
    }
}

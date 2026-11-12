using UnityEngine;

public struct CameraInput2D
{
    public Vector2 Look;
}

public class PlayerCamera2D : MonoBehaviour
{
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);
    private Transform _target;

    public void Initialize(Transform target)
    {
        _target = target;
        if (_target != null)
        {
            transform.position = _target.position + offset;
        }
    }

    public void UpdatePosition(Transform target)
    {
        _target = target;
    }

    public void LateUpdate()
    {
        if (_target == null) return;

        //Lerp Camera To Remove Jitter:
        Vector3 targetPosition = _target.position + offset;
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
    }
}

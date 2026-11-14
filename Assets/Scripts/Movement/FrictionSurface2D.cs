using UnityEngine;

public class FrictionSurface2D : MonoBehaviour
{
    [Header("Surface Friction Settings:")]
    [SerializeField] private float dragCoefficient = 8f;

    public float DragCoefficient => dragCoefficient;
}

using UnityEngine;

public class ForceArea : MonoBehaviour
{
    [Header("References: ")]
    [SerializeField] private PlayerMovement2D playerMovement;
    [SerializeField] private ParticleSystem forceEffect;
    [Space]

    [Header("Boost Panel Settings:")]
    [SerializeField] private float forceAcceleration = 1000f;
    [SerializeField] private Vector2 forceDirection;


    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.transform == playerMovement.transform)
        {
            Debug.Log($"[Force Area Activated!] Forcing player: {other.gameObject.name}");
            ApplyForce();
            PlayForceEffect();
        }
    }

    private void ApplyForce()
    {
        if (playerMovement != null)
        {
            Vector2 boostVector = forceDirection.normalized * forceAcceleration;
            //playerMovement.AddForce(boostVector);
            playerMovement.AddExternalAcceleration(boostVector);
        }
    }

    private void PlayForceEffect()
    {
        if (forceEffect != null)
        {
            forceEffect.Play();
        }
    }
}


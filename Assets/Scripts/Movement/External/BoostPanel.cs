using UnityEngine;

public class BoostPanel : MonoBehaviour
{
    [Header("References: ")]
    [SerializeField] private PlayerMovement2D playerMovement;
    [SerializeField] private ParticleSystem boostEffect;
    [Space]

    [Header("Boost Panel Settings:")]
    [SerializeField] private float boostAcceleration = 1000f;
    [SerializeField] private Vector2 boostDirection;

    private void OnValidate()
    {
        UpdateEffectRotation();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.transform == playerMovement.transform)
        {
            Debug.Log($"[Boost Panel Activated!] Boosting player: {other.gameObject.name}");
            ApplyBoost();
            PlayBoostEffect();
        }
    }

    private void ApplyBoost()
    {
        if (playerMovement != null)
        {
            Vector2 boostVector = boostDirection.normalized * boostAcceleration;
            playerMovement.AddImpulse(boostVector);
        }
    }

    private void PlayBoostEffect()
    {
        if (boostEffect != null)
        {
            boostEffect.Play();
        }
    }

    private void UpdateEffectRotation()
    {
        if (boostEffect != null && boostDirection != Vector2.zero)
        {
            //Compute The Z-Axis, Using the XY Vector:
            float angle = Mathf.Atan2(boostDirection.y, boostDirection.x) * Mathf.Rad2Deg;

            //Subtract by 90, to ensure right [x] is proper initial at 0/2PI, since default is at up [y] / PI/2:
            angle -= 90f;

            //Apply the Z Rotation to the Player:
            boostEffect.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}

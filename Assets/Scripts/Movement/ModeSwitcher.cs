using UnityEngine;

public class ModeSwitcher : MonoBehaviour
{
    [Header("References: ")]
    [SerializeField] private PlayerMovement2D playerMovement;
    [SerializeField] private ParticleSystem splashEffect;


    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") || (playerMovement != null && other.transform == playerMovement.transform))
        {
            Debug.Log("[Mode Switcher] Entering Top-Down / Arena Zone.");
            PlaySplashEffect();
            playerMovement.SetSideScroller(false);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") || (playerMovement != null && other.transform == playerMovement.transform))
        {
            Debug.Log("[Mode Switcher] Exiting Zone. Returning to Side-Scroller.");
            playerMovement.SetSideScroller(true);
        }
    }

    private void PlaySplashEffect()
    {
        if (splashEffect != null)
        {
            splashEffect.Play();
        }
    }
}

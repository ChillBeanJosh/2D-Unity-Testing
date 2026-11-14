using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SplineRideTrigger2D : MonoBehaviour
{
    [Header("Refrences: ")]
    [SerializeField] private ParametricSpline2D spline;
    [SerializeField] private PlayerMovement2D playerMovement;
    [SerializeField] private ParticleSystem initalRailEffect;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (playerMovement != null && spline != null)
        {
            playerMovement.RegisterNearbySpline(spline);
            PlayInitialRailEffect();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (playerMovement != null && spline != null)
        {
            playerMovement.UnregisterNearbySpline(spline);
            StopInitialRailEffect();
        }
    }

    private void PlayInitialRailEffect()
    {
        if (initalRailEffect != null && !initalRailEffect.isPlaying)
        {
            initalRailEffect.Play();
        }
    }

    private void StopInitialRailEffect()
    {
        if (initalRailEffect != null && initalRailEffect.isPlaying)
        {
            initalRailEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}

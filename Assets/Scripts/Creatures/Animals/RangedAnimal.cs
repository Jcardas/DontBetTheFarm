using UnityEngine;

// A farm animal that attacks from range by launching a projectile (the llama's spit), and backs away from enemies
// that get too close so it keeps its spacing instead of running into melee.
public class RangedAnimal : FarmAnimal
{
    [Header("Ranged Attack")]
    [Tooltip("Launched at the target on every attack. It deals this animal's attack damage. Melee if empty.")]
    [SerializeField] protected Projectile projectile;
    [Tooltip("Height the projectile leaves from.")]
    [SerializeField] protected float launchHeight = 1.4f;
    [Tooltip("Backs away when an enemy gets closer than this.")]
    [SerializeField] protected float keepAwayRange = 3f;

    public override float KeepAwayRange => keepAwayRange;

    public override void PerformAttack(Creature target)
    {
        if (projectile == null)
        {
            base.PerformAttack(target);
            return;
        }

        Vector3 from = transform.position + Vector3.up * launchHeight + transform.forward * 0.5f;
        Instantiate(projectile, from, transform.rotation).Launch(target, attackDamage);
        TriggerAttackVisual();
    }
}

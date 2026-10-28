using UnityEngine;

[RequireComponent(typeof(Selectable), typeof(Gatherer), typeof(AudioSource))]
public class Farmer : Creature
{
    [Header("Combat")]
    [Tooltip("Backs away when an alien gets closer than this, so he keeps shooting instead of getting swarmed.")]
    [SerializeField] protected float keepAwayRange = 3f;

    private Shotgun shotgun;

    public override float KeepAwayRange => keepAwayRange;

    protected override void Reset()
    {
        // Use global constants for farm animal stats
        maxHealth = GameConstants.Instance.BaseFarmer.MaxHealth;
        moveSpeed = GameConstants.Instance.BaseFarmer.Speed;
        attackSpeed = GameConstants.Instance.BaseFarmer.AttackSpeed;
        team = GameConstants.Instance.BaseFarmer.Team;
    }

    protected override void Awake()
    {
        base.Awake();
        shotgun = GetComponent<Shotgun>();
    }

    // The farmer never melees: his automatic attack is a shotgun blast at any alien inside his attack range.
    // Space (PlayerControls) fires the same gun by hand, out to the gun's full range.
    public override void PerformAttack(IDamageable target)
    {
        if (shotgun != null) shotgun.Fire(target.Position);
    }

    protected override void HandleZeroHealth()
    {
        // When the farmer's health reaches zero, trigger the game over sequence.
        GameOver();
    }

    protected virtual void GameOver()
    {
        // Implement game over logic here
        Debug.Log("Game Over! The farmer has been defeated.");
    }
}

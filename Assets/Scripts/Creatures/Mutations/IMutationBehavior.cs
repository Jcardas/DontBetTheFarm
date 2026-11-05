public interface IMutationBehavior
{
    /// <summary>
    /// Called when the mutation is instantiated onto the creature.
    /// Provides a reference to the host creature.
    /// </summary>
    void Initialize(Creature owner);

    /// <summary>
    /// Called immediately after the mutation is fully mounted on the socket.
    /// Use to start timers, enable particle effects, or hook up events.
    /// </summary>
    void OnEquipped();

    /// <summary>
    /// Called right before the mutation GameObject is destroyed.
    /// Use to clean up active particle effects, coroutines, or debuffs.
    /// </summary>
    void OnUnequipped();

    /// <summary>
    /// Called by Creature.PerformAttack() whenever the host creature attacks a target.
    /// Useful for on-hit debuffs like Venom Stinger's slow.
    /// </summary>
    void OnCreatureAttack(Creature target);
}
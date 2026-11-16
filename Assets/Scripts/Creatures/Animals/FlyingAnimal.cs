using UnityEngine;

// Bridges legacy FlyingAnimal to the modular CreatureFlight system.
[RequireComponent(typeof(CreatureFlight))]
public class FlyingAnimal : Animal
{
    [Header("Flight")]
    [Tooltip("Target cruise altitude when flying, in metres.")]
    [SerializeField] protected float flightHeight = 1.5f;

    protected override void Awake()
    {
        base.Awake();
        // Ensure NavMeshAgent baseOffset stays 0 so grounded movement is flush with the ground
        if (NavMeshAgent != null)
            NavMeshAgent.baseOffset = 0f;
    }

    protected override void AnimalKnockedOut()
    {
        base.AnimalKnockedOut();
        if (Flight != null)
            Flight.ForceGroundImmediate();
    }

    protected override void HandleRevive()
    {
        base.HandleRevive();
        // Goose revives on the ground; CreatureFlight will take off automatically when it moves or engages targets.
    }
}
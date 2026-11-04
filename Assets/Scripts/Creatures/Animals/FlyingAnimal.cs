using UnityEngine;

// A farm animal that flies (the goose): it follows the NavMesh like everyone else, but its body is lifted into the air.
// It drops to the ground when knocked out and takes off again when revived.
public class FlyingAnimal : FarmAnimal
{
    [Header("Flight")]
    [Tooltip("How high it flies above the ground, in metres.")]
    [SerializeField] protected float flightHeight = 1.5f;

    public float FlightHeight => flightHeight;

    protected override void Awake()
    {
        base.Awake();
        SetHeight(flightHeight);
    }

    protected override void AnimalKnockedOut()
    {
        base.AnimalKnockedOut();
        SetHeight(0f);
    }

    protected override void HandleRevive()
    {
        base.HandleRevive();
        SetHeight(flightHeight);
    }

    private void SetHeight(float height)
    {
        if (NavMeshAgent != null) NavMeshAgent.baseOffset = height;
    }
}

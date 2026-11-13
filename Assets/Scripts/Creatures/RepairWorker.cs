
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Creature))]
public class RepairWorker : MonoBehaviour
{
    [SerializeField] private bool isFarmer;
    [SerializeField] private bool hasHammerLimbs;

    private Creature creature;

    public bool CanRepair =>
        creature != null &&
        !creature.Down &&
        (isFarmer || hasHammerLimbs);

    private void Awake()
    {
        creature = GetComponent<Creature>();
    }

    public void SetHammerLimbs(bool value)
    {
        hasHammerLimbs = value;
    }

    public bool WalkTo(Collider target)
    {
        if (!CanRepair || !target)
            return false;

        NavMeshAgent agent = creature.NavMeshAgent;

        if (!agent || !agent.enabled || !agent.isOnNavMesh)
            return false;

        creature.CreatureBrain.Obey();
        Reach.WalkTo(agent, target);
        creature.CreatureBrain.SetGuardPost(agent.destination);

        return true;
    }

    public void Stop()
    {
        if (creature != null &&
            creature.NavMeshAgent != null &&
            creature.NavMeshAgent.isOnNavMesh)
        {
            creature.NavMeshAgent.ResetPath();
        }
    }
}


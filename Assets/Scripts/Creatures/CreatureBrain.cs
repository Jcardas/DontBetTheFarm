using UnityEngine;

/// <summary>
/// Handles pathfinding, decision-making, and other AI behaviors for creatures.
/// </summary>
public class CreatureBrain : MonoBehaviour
{
    ///
    /// The creature that this brain controls.
    /// Can access the creature's stats, position, and other properties to make decisions.
    /// Also, can reference the navMeshAgent for movement and pathfinding.
    private Creature creature;

    private Vector3 guardPost;

    /// <summary>
    /// A flag indicating whether the creature is passive.
    /// </summary>
    private bool isPassive;
    
    

    private void Awake()
    {
        // Get the Creature component from the GameObject this script is attached to.
        creature = GetComponent<Creature>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        guardPost = transform.position;
    }



    /// <summary>
    /// Initializes the CreatureBrain with the specified creature.
    /// </summary>
    /// <param name="creature">The creature to initialize the brain with.</param>
    public void Initialize(Creature creature)
    {
        this.creature = creature;
    }


    public void Guard(Vector3 point)
    {
        
        // Snap the destination to the closest valid point on the NavMesh
        guardPost = UnityEngine.AI.NavMesh.SamplePosition(point, out UnityEngine.AI.NavMeshHit hit, 3f, UnityEngine.AI.NavMesh.AllAreas)
            ? hit.position
            : point;

        // Ensure the agent is active and valid before issuing movement
        if (creature != null && creature.NavMeshAgent != null && creature.NavMeshAgent.isOnNavMesh)
        {
            creature.NavMeshAgent.SetDestination(guardPost);
        }
    }
}
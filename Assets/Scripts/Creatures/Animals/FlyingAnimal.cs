using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// A farm animal that flies (the goose). Its NavMeshAgent runs on its own flat "air" NavMesh that covers the whole map
// with nothing cut out of it, so it flies straight over pen fences, the barn and trees, and its body is lifted into the
// air. Ground units and fences never block it. It drops to the ground when knocked out and takes off again when revived.
public class FlyingAnimal : FarmAnimal
{
    [Header("Flight")]
    [Tooltip("How high it flies above the ground, in metres.")]
    [SerializeField] protected float flightHeight = 2.25f;

    public float FlightHeight => flightHeight;

    // One air NavMesh for every flyer, built the first time one starts, over the bounds of the ground NavMesh.
    static NavMeshDataInstance air;
    static int airAgentType;

    protected override void Awake()
    {
        base.Awake();
        SetHeight(flightHeight);
    }

    protected override void Start()
    {
        base.Start();
        if (!air.valid) BuildAir();
        if (!air.valid || NavMeshAgent == null) return;
        bool wasEnabled = NavMeshAgent.enabled;
        NavMeshAgent.enabled = false;
        NavMeshAgent.agentTypeID = airAgentType;
        NavMeshAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        NavMeshAgent.enabled = wasEnabled;
        if (wasEnabled) NavMeshAgent.Warp(transform.position);
    }

    static void BuildAir()
    {
        var tri = NavMesh.CalculateTriangulation();
        if (tri.vertices.Length == 0) return;
        var ground = new Bounds(tri.vertices[0], Vector3.zero);
        foreach (var v in tri.vertices) ground.Encapsulate(v);

        NavMeshBuildSettings settings = NavMesh.CreateSettings();
        settings.agentRadius = 0.3f;
        settings.agentHeight = 1f;
        airAgentType = settings.agentTypeID;
        var floor = new NavMeshBuildSource
        {
            shape = NavMeshBuildSourceShape.Box,
            size = new Vector3(ground.size.x, 0.1f, ground.size.z),
            transform = Matrix4x4.Translate(new Vector3(ground.center.x, ground.min.y - 0.05f, ground.center.z)),
        };
        var data = NavMeshBuilder.BuildNavMeshData(settings, new List<NavMeshBuildSource> { floor },
            new Bounds(ground.center, ground.size + new Vector3(2f, 4f, 2f)), Vector3.zero, Quaternion.identity);
        air = NavMesh.AddNavMeshData(data);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetAir() => air = default;

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

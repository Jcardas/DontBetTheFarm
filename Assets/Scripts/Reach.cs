using UnityEngine;
using UnityEngine.AI;

// Shared "walk up to that thing" helpers. Targets are measured to the nearest face of their collider,
// so a big barn and a small alien both count as reached when you are standing next to them.
public static class Reach
{
    // Flat distance from a point to the nearest face of `target`. 0 when the point is inside it.
    public static float Gap(Vector3 from, Collider target)
    {
        Vector3 nearest = NearestPoint(target, from);
        nearest.y = from.y;
        return Vector3.Distance(from, nearest);
    }

    // ClosestPoint only supports box, sphere, capsule and convex mesh colliders. On a concave MeshCollider
    // it returns the query point itself, which would make every target count as reached, so use its bounds.
    static Vector3 NearestPoint(Collider c, Vector3 p) =>
        c is MeshCollider { convex: false } ? c.ClosestPointOnBounds(p) : c.ClosestPoint(p);

    // Path to the side of `target` facing the agent. Aiming at the centre fails for barns and trees,
    // because their centre is cut out of the NavMesh.
    public static void WalkTo(NavMeshAgent agent, Collider target)
    {
        Vector3 side = NearestPoint(target, agent.transform.position);
        if (NavMesh.SamplePosition(side, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }
}

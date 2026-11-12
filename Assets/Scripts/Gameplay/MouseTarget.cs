using UnityEngine;

// What the mouse is over, the same answer for every script that asks. A creature wins, even one behind a building
// or a fence, so a unit is never hidden from the mouse; otherwise it is whatever the ray hits first.
public static class MouseTarget
{
    static readonly int Creatures = LayerMask.GetMask("Creature");

    public static bool Find(Ray ray, out RaycastHit hit, float maxDistance = Mathf.Infinity, int layers = Physics.DefaultRaycastLayers) =>
        Physics.Raycast(ray, out hit, maxDistance, Creatures & layers, QueryTriggerInteraction.Ignore)
        || Physics.Raycast(ray, out hit, maxDistance, layers, QueryTriggerInteraction.Ignore);
}

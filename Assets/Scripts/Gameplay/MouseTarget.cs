using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// What the mouse is over, the same answer for every script that asks. A creature wins, even one behind a building
// or a fence, so a unit is never hidden from the mouse; then a building by its whole outline; otherwise it is
// whatever the ray hits first.
public static class MouseTarget
{
    static readonly int Creatures = LayerMask.GetMask("Creature");
    static readonly List<MeshRenderer> parts = new();

    public static bool Find(Ray ray, out RaycastHit hit, float maxDistance = Mathf.Infinity, int layers = Physics.DefaultRaycastLayers) =>
        Physics.Raycast(ray, out hit, maxDistance, Creatures & layers, QueryTriggerInteraction.Ignore)
        || Physics.Raycast(ray, out hit, maxDistance, layers, QueryTriggerInteraction.Ignore);

    // The building the ray reaches before `hit`, judged by one box around all its parts, so the gaps in a pen's
    // fence still count as the pen. No collider is added, so nothing walks into or bumps that box.
    public static Buildable Building(Ray ray, RaycastHit hit)
    {
        if (BuildingManager.Instance == null || (Creatures & 1 << hit.collider.gameObject.layer) != 0) return null;
        Buildable nearest = null;
        float within = hit.distance + 0.01f; // the hit may be the building's own outer face
        foreach (var building in BuildingManager.Instance.Buildings)
        {
            if (building == null) continue;
            building.GetComponentsInChildren(parts);
            if (parts.Count == 0) continue;
            var box = parts[0].bounds;
            foreach (var part in parts) box.Encapsulate(part.bounds);
            if (!box.IntersectRay(ray, out float at) || at > within) continue;
            within = at;
            nearest = building;
        }
        return nearest;
    }

    // The building whose menu a right-click opens: one that has a menu, while no unit is picked
    // (picked units take a right-click as an order).
    public static Buildable Menu(Ray ray, RaycastHit hit) =>
        Building(ray, hit) is IContextInteractable building && !Selectable.All.Any(s => s.Selected) ? (Buildable)building : null;
}

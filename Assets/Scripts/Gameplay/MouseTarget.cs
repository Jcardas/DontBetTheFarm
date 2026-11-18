using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// What the mouse is over, the same answer for every script that asks. A creature wins, even one behind a building
// or a fence (including knocked-out animals inside a pen), so a unit is never hidden from the mouse;
// then a building by its whole outline; otherwise it is whatever the ray hits first.
public static class MouseTarget
{
    static readonly int Creatures = LayerMask.GetMask("Creature");
    static readonly List<Collider> colliders = new();
    static readonly List<MeshRenderer> parts = new();

    // Knocked-out animals inside a pen or standing creatures win over buildings; otherwise the Barn keeps priority
    // over creatures behind it so it can still be hovered and right-clicked.
    public static bool Find(Ray ray, out RaycastHit hit, float maxDistance = Mathf.Infinity, int layers = Physics.DefaultRaycastLayers)
    {
        bool any = Physics.Raycast(ray, out hit, maxDistance, layers, QueryTriggerInteraction.Ignore);

        if (Physics.Raycast(ray, out RaycastHit creature, maxDistance, Creatures & layers, QueryTriggerInteraction.Ignore))
        {
            var downedAnimal = creature.collider.GetComponentInParent<Animal>();
            if (downedAnimal != null && downedAnimal.Down)
            {
                hit = creature;
                return true;
            }

            if (any && hit.collider.GetComponentInParent<Barn>() != null)
                return true;

            hit = creature;
            return true;
        }

        // Fallback bounds check for knocked-out animals whose colliders may be occluded by a pen's BoxCollider
        Animal downedByBounds = FindDownedAnimalByBounds(ray, maxDistance);
        if (downedByBounds != null && downedByBounds.Body != null)
        {
            if (Physics.Raycast(ray, out RaycastHit directDowned, maxDistance, 1 << downedByBounds.Body.gameObject.layer, QueryTriggerInteraction.Collide))
            {
                hit = directDowned;
                return true;
            }
        }

        return any;
    }

    static Animal FindDownedAnimalByBounds(Ray ray, float maxDistance)
    {
        Animal closest = null;
        float bestDist = maxDistance;
        foreach (Animal a in Object.FindObjectsByType<Animal>(FindObjectsSortMode.None))
        {
            if (a == null || !a.Down || !a.isActiveAndEnabled) continue;
            if (a.Body != null && a.Body.bounds.IntersectRay(ray, out float dist) && dist < bestDist)
            {
                bestDist = dist;
                closest = a;
            }
        }
        return closest;
    }

    // The building the ray reaches before `hit`, judged by its active colliders so fence gaps still count as the pen
    // without rooftop props (radar, telescope, saucer) inflating the click box into empty air.
    public static Buildable Building(Ray ray, RaycastHit hit)
    {
        if (BuildingManager.Instance == null || (Creatures & 1 << hit.collider.gameObject.layer) != 0) return null;
        Buildable nearest = null;
        float within = hit.distance + 0.01f; // the hit may be the building's own outer face
        foreach (var building in BuildingManager.Instance.Buildings)
        {
            if (building == null || !TryGetBounds(building, out Bounds box)) continue;
            if (!box.IntersectRay(ray, out float at) || at > within) continue;
            within = at;
            nearest = building;
        }
        return nearest;
    }

    static bool TryGetBounds(Buildable building, out Bounds box)
    {
        building.GetComponentsInChildren(colliders);
        box = default;
        bool hasBounds = false;
        foreach (var col in colliders)
        {
            if (!col.enabled || !col.gameObject.activeInHierarchy) continue;
            if (!hasBounds) { box = col.bounds; hasBounds = true; }
            else box.Encapsulate(col.bounds);
        }
        if (hasBounds) return true;

        // Fallback to renderer bounds if a broken building has no active colliders.
        building.GetComponentsInChildren(parts);
        if (parts.Count == 0) return false;
        box = parts[0].bounds;
        foreach (var part in parts) box.Encapsulate(part.bounds);
        return true;
    }

    // The building whose menu a right-click opens: an intact building that has a menu, while no unit is picked
    // (picked units take a right-click as an order), or the barn while the farmer is in it.
    public static Buildable Menu(Ray ray, RaycastHit hit) =>
        Building(ray, hit) is IContextInteractable interactable &&
        interactable is Buildable { IsBroken: false } building &&
        (building is Barn { HasFarmer: true } || !Selectable.All.Any(s => s.Selected))
            ? building
            : null;
}
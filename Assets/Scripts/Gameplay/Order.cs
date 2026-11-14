using UnityEngine;

public enum OrderKind { Move, Work, Harvest, Attack, Repair }

// One player order, read once from what was clicked so every picked unit gets the same order.
// Each unit's Selectable decides what it means for that unit: a cow can't attack, so it walks there instead.
public readonly struct Order
{
    public readonly OrderKind kind;
    public readonly Vector3 point;
    public readonly ResourceNode node;
    public readonly WheatField field;
    public readonly Buildable building;
    public readonly Creature enemy;

    Order(
        OrderKind kind, 
        Vector3 point,
        ResourceNode node = null,
        WheatField field = null, 
        Buildable building = null,
        Creature enemy = null)
    {
        this.kind = kind;
        this.point = point;
        this.node = node;
        this.field = field;
        this.building = building;
        this.enemy = enemy;
    }

    public static Order ForRepair(Buildable target) =>
        new(OrderKind.Repair, target.transform.position, building: target);

    public static Order From(RaycastHit hit)
    {
        var enemy = hit.collider.GetComponentInParent<Creature>();
        if (enemy != null &&
            enemy.Team == GameConstants.Team.Alien &&
            !enemy.Down)
        {
            return new Order(
                OrderKind.Attack,
                enemy.transform.position,
                enemy: enemy);
        }
        
        Buildable broken = hit.collider.GetComponentInParent<Buildable>();
        if (broken && broken.IsBroken)
            return ForRepair(broken);

        var field = hit.collider.GetComponentInParent<WheatField>();
        if (field && !field.IsBroken)
            return new Order(OrderKind.Harvest, hit.point, field: field);

        var node = hit.collider.GetComponentInParent<ResourceNode>();
        if (node != null)
            return new Order(OrderKind.Work, hit.point, node: node);

        return new Order(OrderKind.Move, hit.point);
    }

    // Renderer bounds make broken pens/barns clickable even without colliders.
    public static Buildable BrokenAt(Ray ray)
    {
        Buildable closest = null;
        float closestDistance = float.MaxValue;

        foreach (Buildable candidate in
            Object.FindObjectsByType<Buildable>(FindObjectsSortMode.None))
        {
            if (!candidate || !candidate.IsBroken) continue;

            foreach (Renderer renderer in candidate.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled ||
                    !renderer.bounds.IntersectRay(ray, out float distance) ||
                    distance >= closestDistance) continue;

                closestDistance = distance;
                closest = candidate;
            }
        }

        return closest;
    }

    public Order Offset(Vector3 by) =>
        new(kind, point + by, node, field, building, enemy);
}
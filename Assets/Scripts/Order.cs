using UnityEngine;

public enum OrderKind { Move, Work, Harvest, Attack }

// One player order, read once from what was clicked so every picked unit gets the same order.
// Each unit's Selectable decides what it means for that unit: a cow can't attack, so it walks there instead.
public readonly struct Order
{
    public readonly OrderKind kind;
    public readonly Vector3 point;
    public readonly ResourceNode node;
    public readonly WheatField field;
    public readonly Health enemy;

    Order(OrderKind kind, Vector3 point, ResourceNode node = null, WheatField field = null, Health enemy = null)
    {
        this.kind = kind;
        this.point = point;
        this.node = node;
        this.field = field;
        this.enemy = enemy;
    }

    // First rule that fits wins: a standing alien, then a crop field, then a resource, then the ground.
    public static Order From(RaycastHit hit)
    {
        var enemy = hit.collider.GetComponentInParent<Health>();
        if (enemy != null && enemy.team == Team.Alien && !enemy.Down) return new Order(OrderKind.Attack, enemy.transform.position, enemy: enemy);
        var field = hit.collider.GetComponentInParent<WheatField>();
        if (field != null) return new Order(OrderKind.Harvest, hit.point, field: field);
        var node = hit.collider.GetComponentInParent<ResourceNode>();
        if (node != null) return new Order(OrderKind.Work, hit.point, node);
        return new Order(OrderKind.Move, hit.point);
    }

    public Order Offset(Vector3 by) => new(kind, point + by, node, field, enemy);
}

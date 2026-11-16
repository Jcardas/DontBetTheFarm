using UnityEngine;

public enum OrderKind { Move, Work, Harvest, Attack, Repair, Pen, Barn, Revive }

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
    public readonly Animal animalToRevive;

    private Order(
        OrderKind kind,
        Vector3 point,
        ResourceNode node = null,
        WheatField field = null,
        Buildable building = null,
        Creature enemy = null,
        Animal animalToRevive = null)
    {
        this.kind = kind;
        this.point = point;
        this.node = node;
        this.field = field;
        this.building = building;
        this.enemy = enemy;
        this.animalToRevive = animalToRevive;
    }

    public static Order ForRepair(Buildable target) =>
        new(OrderKind.Repair, target.transform.position, building: target);

    public static Order ForPen(AnimalPen target) =>
        new(OrderKind.Pen, target.transform.position, building: target);

    public static Order ForRevive(Animal target) =>
        new(OrderKind.Revive, target.transform.position, animalToRevive: target);

    // `ray` finds a pen or the barn by its whole outline, so a gap in a pen's fence still counts as the pen.
    public static Order From(Ray ray, RaycastHit hit)
    {
        var creature = hit.collider.GetComponentInParent<Creature>();
        if (creature != null)
        {
            if (creature.Team == GameConstants.Team.Alien && !creature.Down)
            {
                return new Order(
                    OrderKind.Attack,
                    creature.transform.position,
                    enemy: creature);
            }

            if (creature is Animal knockedOutAnimal && knockedOutAnimal.Down)
            {
                return ForRevive(knockedOutAnimal);
            }
        }

        Buildable broken = hit.collider.GetComponentInParent<Buildable>();
        if (broken && broken.IsBroken)
            return ForRepair(broken);

        var field = hit.collider.GetComponentInParent<WheatField>();
        if (field && !field.IsBroken && field.mature)
            return new Order(OrderKind.Harvest, hit.point, field: field);

        var node = hit.collider.GetComponentInParent<ResourceNode>();
        if (node != null)
            return new Order(OrderKind.Work, hit.point, node: node);

        var building = MouseTarget.Building(ray, hit);
        if (building is AnimalPen pen && !pen.IsBroken)
            return new Order(OrderKind.Pen, hit.point, building: pen);
        if (building is Barn barn && !barn.Down && !barn.IsBroken)
            return new Order(OrderKind.Barn, barn.Door, building: barn);

        return new Order(OrderKind.Move, hit.point);
    }

    // Solid mesh bounds on the broken visual make broken pens/barns/fields clickable without oversized VFX/plane hitboxes.
    public static Buildable BrokenAt(Ray ray)
    {
        Buildable closest = null;
        float closestDistance = float.MaxValue;

        foreach (Buildable candidate in
            Object.FindObjectsByType<Buildable>(FindObjectsSortMode.None))
        {
            if (!candidate || !candidate.IsBroken) continue;

            Transform searchRoot = candidate.BrokenVisual != null
                ? candidate.BrokenVisual.transform
                : candidate.transform;

            foreach (Renderer renderer in searchRoot.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer) continue;

                Bounds b = renderer.bounds;
                b.Expand(new Vector3(-Mathf.Min(0.8f, b.size.x * 0.15f), 0f, -Mathf.Min(0.8f, b.size.z * 0.15f)));

                if (!b.IntersectRay(ray, out float distance) ||
                    distance >= closestDistance) continue;

                closestDistance = distance;
                closest = candidate;
            }
        }

        return closest;
    }

    public Order Offset(Vector3 by) =>
        new(kind, point + by, node, field, building, enemy, animalToRevive);
}
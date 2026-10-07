using System.Collections.Generic;
using UnityEngine;

public enum UnitType { Farmer, Cow, Horse }

// A farm unit the player can pick and order (farmer, cows, horses). Units with no orders keep running their own AI;
// orders only override where they stand, what they work on or whom they fight. A knocked-out unit cannot stay selected.
[RequireComponent(typeof(Health))]
public class Selectable : MonoBehaviour
{
    static readonly List<Selectable> all = new();
    public static IReadOnlyList<Selectable> All => all;

    [Tooltip("Selected when the game starts and when the player presses Escape. On for the farmer.")]
    public bool selectedAtStart;
    [Tooltip("Double-clicking a unit picks every standing unit of the same type on screen.")]
    public UnitType unitType;
    [Tooltip("Flat ring under the unit, shown only while it is selected.")]
    public GameObject ring;
    [Tooltip("Fainter ring shown while the mouse is over the unit, unless it is selected.")]
    public GameObject hoverRing;

    Health health;
    Gatherer gatherer;
    Fighter fighter;
    bool selected, hovered;

    public bool Down => health.Down;

    public bool Selected
    {
        get => selected;
        set
        {
            selected = value && !Down;
            ShowRings();
        }
    }

    public bool Hovered
    {
        get => hovered;
        set
        {
            hovered = value && !Down;
            ShowRings();
        }
    }

    void ShowRings()
    {
        if (ring) ring.SetActive(selected);
        if (hoverRing) hoverRing.SetActive(hovered && !selected);
    }

    void Awake()
    {
        health = GetComponent<Health>();
        gatherer = GetComponent<Gatherer>();
        fighter = GetComponent<Fighter>();
    }

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    void Update()
    {
        if (!Down) return;
        if (selected) Selected = false;
        if (hovered) Hovered = false;
    }

    // One order, routed by what this unit can do. First rule that fits wins.
    public void Command(Order order)
    {
        if (order.kind == OrderKind.Attack && fighter)
        {
            if (gatherer) gatherer.working = false;
            fighter.Attack(order.enemy);
            return;
        }
        if (fighter && !fighter.anchored) fighter.Obey();
        if (order.kind == OrderKind.Work && gatherer) gatherer.Work(order.node);
        else if (fighter && fighter.anchored) fighter.Guard(order.point);
        else if (gatherer) gatherer.MoveTo(order.point);
    }
}

using System.Collections.Generic;
using UnityEngine;

// A farm unit the player can pick and order (farmer, cows, horses). Units with no orders keep running their own AI;
// orders only override where they stand or what they work on. A knocked-out unit cannot stay selected.
[RequireComponent(typeof(Health))]
public class Selectable : MonoBehaviour
{
    static readonly List<Selectable> all = new();
    public static IReadOnlyList<Selectable> All => all;

    [Tooltip("Selected when the night starts and when the player presses Escape. On for the farmer.")]
    public bool selectedAtStart;
    [Tooltip("Flat ring under the unit, shown only while it is selected.")]
    public GameObject ring;

    Health health;
    Gatherer gatherer;
    Fighter fighter;
    bool selected;

    public bool Down => health.Down;

    public bool Selected
    {
        get => selected;
        set
        {
            selected = value && !Down;
            if (ring) ring.SetActive(selected);
        }
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
        if (selected && Down) Selected = false;
    }

    // One order, routed by what this unit can do. First rule that fits wins.
    public void Command(Vector3 point, ResourceNode node)
    {
        if (node && gatherer) gatherer.Work(node);
        else if (fighter && fighter.anchored) fighter.Guard(point);
        else if (gatherer) gatherer.MoveTo(point);
    }
}

using System.Collections.Generic;
using UnityEngine;


// A farm unit the player can pick and order (farmer, cows, horses). Units with no orders keep running their own AI;
// orders only override where they stand, what they work on or whom they fight. A knocked-out unit cannot stay selected.
[RequireComponent(typeof(Creature))]
public class Selectable : MonoBehaviour
{
    static readonly List<Selectable> all = new();
    public static IReadOnlyList<Selectable> All => all;

    // [Tooltip("Double-clicking a unit picks every standing unit of the same type on screen.")]
    // public UnitType unitType;
    [Tooltip("Flat ring under the unit, shown only while it is selected.")]
    public GameObject ring;

    [Tooltip("Fainter ring shown while the mouse is over the unit, unless it is selected.")]
    public GameObject hoverRing;

    private Creature creature;
    private CreatureBrain brain;
    private bool selected;
    private bool hovered;

    public Creature Creature => creature;
    public bool Down => creature == null || creature.Down;

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

    private void Awake()
    {
        creature = GetComponent<Creature>();
        brain = GetComponent<CreatureBrain>();
    }

    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);

    private void Update()
    {
        if (!Down) return;
        if (selected) Selected = false;
        if (hovered) Hovered = false;
    }

    private void ShowRings()
    {
        if (ring) ring.SetActive(selected);
        if (hoverRing) hoverRing.SetActive(hovered && !selected);
    }

    public void Command(Order order)
    {
        if (Down || brain == null) return;
        brain.ExecuteCommand(order);
    }

    public string GetCreatureName()
    {
        return creature != null ? creature.CreatureName : gameObject.name;
    }
}
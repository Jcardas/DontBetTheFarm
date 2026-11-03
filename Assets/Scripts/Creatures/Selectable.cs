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
    private Creature creature;
    private CreatureBrain brain;
    private SelectionHighlight highlight;
    private bool selected;
    private bool hovered;

    public Creature Creature => creature;
    // Knocked out, or switched off because the unit isn't on the player's side right now: either way it can't be
    // picked or ordered.
    public bool Down => creature == null || creature.Down || !enabled;

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
        if (!TryGetComponent(out highlight)) highlight = gameObject.AddComponent<SelectionHighlight>();
    }

    public SelectionHighlight Highlight => highlight;

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
        highlight.Selected = selected;
        highlight.Hovered = hovered;
    }

    public void Command(Order order)
    {
        if (Down || brain == null) return;
        // Turn to the clicked spot at once; the NavMeshAgent alone eases round over half a second as it sets off.
        Vector3 toward = order.point - transform.position;
        toward.y = 0f;
        if (toward.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toward);
        brain.ExecuteCommand(order);
    }

    public string GetCreatureName()
    {
        return creature != null ? creature.CreatureName : gameObject.name;
    }
}
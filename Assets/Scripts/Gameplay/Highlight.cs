using UnityEngine;

// Outlines something the player is pointing at or has picked (Quick Outline, Assets/ThirdParty/QuickOutline).
// A creature also always shows a faint outline wherever something hides it from the camera,
// so units behind the barn are never lost. Creature adds one to itself, and parts CreatureMutator adds later are
// picked up the same frame; PlayerControls adds one to a building the first time it is hovered.
[DisallowMultipleComponent]
public class Highlight : MonoBehaviour
{
    Outline outline;
    Creature creature;
    CreatureMutator mutator;
    bool hovered, selected, partsChanged;

    public bool Hovered
    {
        get => hovered;
        set { hovered = value; Show(); }
    }

    public bool Selected
    {
        get => selected;
        set { selected = value; Show(); }
    }

    void Awake()
    {
        creature = GetComponent<Creature>();
        mutator = GetComponent<CreatureMutator>();
        // A creature's outline goes on its model (ToyBob's object), so the rings under it aren't outlined too.
        // A building's goes on its ground plate when it has an active renderer, or on the root so broken visuals outline cleanly.
        Component model = GetComponentInChildren<ToyBob>();
        if (model == null && TryGetComponent(out Buildable building) &&
            building.GroundElement != null && building.GroundElement.GetComponentInChildren<Renderer>() != null)
        {
            model = building.GroundElement;
        }
        outline = (model != null ? model.gameObject : gameObject).AddComponent<Outline>();
        Show();
    }

    void OnEnable()
    {
        if (mutator != null) mutator.OnMutationsUpdated += PartsChanged;
    }

    void OnDisable()
    {
        if (mutator != null) mutator.OnMutationsUpdated -= PartsChanged;
    }

    void PartsChanged() => partsChanged = true;

    // Re-indexes child renderers whenever a building swaps between its repaired and broken models.
    public void Refresh()
    {
        if (outline != null) outline.Refresh();
    }

    // Once per frame however many parts were swapped.
    void LateUpdate()
    {
        if (!partsChanged) return;
        partsChanged = false;
        outline.Refresh();
    }

    // Lit (hovered or selected) shows through everything; otherwise a creature shows only where it is hidden.
    void Show()
    {
        bool lit = hovered || selected;
        bool on = lit || creature != null;
        if (on) outline.Refresh(); // refresh cached renderers in case the building broke or was repaired since last lit
        outline.enabled = on;
        outline.OutlineMode = lit ? Outline.Mode.OutlineAll : Outline.Mode.OutlineHidden;
        outline.OutlineWidth = lit ? GameConstants.Instance.LitOutlineWidth : GameConstants.Instance.HiddenOutlineWidth;

        // Default to Farm colors for non-creature highlightables (e.g., buildings)
        var team = creature != null ? creature.Team : GameConstants.Team.Farm;
        var colors = GameConstants.Instance.GetTeamColors(team);

        outline.OutlineColor = selected ? colors.selectedColor
            : hovered ? colors.hoverColor
            : colors.hiddenColor;
    }
}
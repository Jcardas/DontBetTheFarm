using UnityEngine;

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
        var model = GetComponentInChildren<ToyBob>();
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
        outline.enabled = lit || creature != null;
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

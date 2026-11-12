using UnityEngine;

// Outlines something the player is pointing at or has picked (Quick Outline, Assets/ThirdParty/QuickOutline).
// A creature also always shows a faint outline wherever something hides it from the camera,
// so units behind the barn are never lost. Creature adds one to itself; parts CreatureMutator adds later are
// picked up the same frame.
[DisallowMultipleComponent]
public class Highlight : MonoBehaviour
{
    // Blue for the player's side, red for aliens; the shade tells the states apart.
    static readonly Color HiddenColor = new(0.4f, 0.65f, 1f, 0.35f);
    static readonly Color HoverColor = new(0.6f, 0.85f, 1f, 0.7f);
    static readonly Color SelectedColor = new(0.2f, 0.55f, 1f, 0.85f);
    static readonly Color EnemyHidden = new(1f, 0.35f, 0.3f, 0.35f);
    static readonly Color EnemyHover = new(1f, 0.5f, 0.45f, 0.7f);
    const float HiddenWidth = 1.5f, LitWidth = 2f;

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
        outline.OutlineWidth = lit ? LitWidth : HiddenWidth;
        bool enemy = creature != null && creature.Team == GameConstants.Team.Alien;
        outline.OutlineColor = selected ? SelectedColor
            : hovered ? (enemy ? EnemyHover : HoverColor)
            : enemy ? EnemyHidden : HiddenColor;
    }
}

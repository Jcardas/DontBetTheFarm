using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Shows a creature's fights: its model flashes when it takes a hit, and a health bar over its head shows while it is
// hurt (hidden at full health and once it is down). Farm units get a green bar, aliens a red one.
// It only reads Creature's health, so any damage source (melee, the shotgun, a boss) shows up without calling it.
[RequireComponent(typeof(Creature))]
public class CombatFeedback : MonoBehaviour
{
    [SerializeField] private CreatureMutator mutator; // Subscribe to the mutator so we see all mutation parts too
    
    static readonly Color FarmBar = new(0.35f, 0.85f, 0.3f), AlienBar = new(0.9f, 0.2f, 0.15f);
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static Material onTop;

    [Tooltip("Colour the model turns for an instant when it takes a hit.")]
    public Color flashColor = new(1f, 0.25f, 0.2f);
    public float flashSeconds = 0.12f;
    [Tooltip("Metres above the top of the model.")]
    public float barGap = 0.25f;
    public Vector2 barSize = new(0.9f, 0.1f);

    Creature creature;
    GameObject toyObject; // Holds the main model root (e.g., Cow)
    Renderer[] model;
    MaterialPropertyBlock flashBlock;
    RectTransform bar, fill;
    int lastHealth;
    float flashEnds;
    bool tinted;

    public bool Flashing => Time.time < flashEnds;
    public bool BarShowing => bar != null && bar.gameObject.activeSelf;
    public float BarFill => fill != null ? fill.anchorMax.x : 0f;
    public Color BarColor => fill != null ? fill.GetComponent<Image>().color : Color.white;

    void Awake()
    {
        if (mutator == null)
        {
            mutator = GetComponentInParent<CreatureMutator>() ?? GetComponent<CreatureMutator>();
        }
        
        creature = GetComponent<Creature>();

        // Find the GameObject that holds ToyBob, fallback to this GameObject
        var toy = GetComponentInChildren<ToyBob>(true);
        toyObject = toy ? toy.gameObject : gameObject;

        // Fetch initial renderers under the toy root
        UpdateRenderers();

        flashBlock = new MaterialPropertyBlock();
        flashBlock.SetColor(BaseColor, flashColor);
        if (!onTop)
        {
            onTop = new Material(Canvas.GetDefaultCanvasMaterial());
            onTop.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
        }
        BuildBar();
    }

    private void OnEnable()
    {
        if (mutator != null)
        {
            mutator.OnMutationsUpdated += HandleMutationsChangedUpdated;
        }
    }
    
    private void OnDisable()
    {
        if (mutator != null)
        {
            mutator.OnMutationsUpdated -= HandleMutationsChangedUpdated;
        }
    }

    private void HandleMutationsChangedUpdated()
    {
        UpdateRenderers();
    }

    private void UpdateRenderers()
    {
        model = GetComponentsInChildren<Renderer>(true);

        foreach (var r in model)
        {
            if (r != null)
            {
                r.SetPropertyBlock(tinted ? flashBlock : null);
            }
        }
    }

    void Start() => lastHealth = creature.CurrentHealth;

    void BuildBar()
    {
        float top = 1f;
        foreach (var r in model)
        {
            if (r != null)
            {
                top = Mathf.Max(top, r.bounds.max.y - transform.position.y);
            }
        }

        var canvas = new GameObject("HealthBar", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        bar = (RectTransform)canvas.transform;
        bar.SetParent(transform, false);
        bar.localPosition = Vector3.up * (top + barGap);
        bar.sizeDelta = barSize * 100f;
        bar.localScale = Vector3.one * 0.01f;
        Layer("Back", new Color(0f, 0f, 0f, 0.6f));
        fill = Layer("Fill", creature.Team == GameConstants.Team.Alien ? AlienBar : FarmBar);
        bar.gameObject.SetActive(false);
    }

    RectTransform Layer(string name, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.color = color;
        image.material = onTop;
        image.raycastTarget = false;
        var rect = image.rectTransform;
        rect.SetParent(bar, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    void LateUpdate()
    {
        int health = creature.CurrentHealth;
        if (health < lastHealth) flashEnds = Time.time + flashSeconds;
        lastHealth = health;

        if (Flashing != tinted)
        {
            tinted = Flashing;
            foreach (var r in model)
            {
                if (r == null) continue;
                r.SetPropertyBlock(tinted ? flashBlock : null);
            }
        }

        bool hurt = !creature.Down && health < creature.MaxHealth;
        bar.gameObject.SetActive(hurt);
        if (!hurt) return;
        fill.anchorMax = new Vector2((float)health / creature.MaxHealth, 1f);
        if (Camera.main) bar.rotation = Camera.main.transform.rotation;
    }
}
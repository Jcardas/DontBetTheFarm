using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Health bar over a building, shown while it is damaged but not yet broken (playtest: players couldn't tell a
// pen or field was under attack). Buildable adds it in Awake, so no prefab edits. Same look as CombatFeedback's bars.
[RequireComponent(typeof(Buildable))]
public class BuildingHealthBar : MonoBehaviour
{
    static readonly Color Fill = new(0.35f, 0.85f, 0.3f);
    static Material onTop;

    public float barGap = 0.5f;
    public Vector2 barSize = new(2.4f, 0.22f);

    Buildable building;
    RectTransform bar, fill;

    void Awake() => building = GetComponent<Buildable>();

    void Start()
    {
        if (!onTop)
        {
            onTop = new Material(Canvas.GetDefaultCanvasMaterial());
            onTop.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
        }

        float top = 1f;
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            if (r != null && r is not ParticleSystemRenderer) top = Mathf.Max(top, r.bounds.max.y - transform.position.y);

        var canvas = new GameObject("BuildingHealthBar", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        bar = (RectTransform)canvas.transform;
        bar.SetParent(transform, false);
        bar.position = transform.position + Vector3.up * (top + barGap);
        bar.sizeDelta = barSize * 100f;

        bar.localScale = new Vector3(0.01f / transform.lossyScale.x, 0.01f / transform.lossyScale.y, 0.01f / transform.lossyScale.z);
        Layer("Back", new Color(0f, 0f, 0f, 0.6f));
        fill = Layer("Fill", Fill);
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
        if (bar == null || building.MaxHealth <= 0) return;
        bool hurt = !building.IsBroken && building.CurrentHealth < building.MaxHealth;
        if (bar.gameObject.activeSelf != hurt) bar.gameObject.SetActive(hurt);
        if (!hurt) return;
        fill.anchorMax = new Vector2((float)building.CurrentHealth / building.MaxHealth, 1f);
        if (Camera.main) bar.rotation = Camera.main.transform.rotation;
    }
}

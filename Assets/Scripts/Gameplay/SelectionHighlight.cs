using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

// Shows that something is hovered or selected. Creatures get a soft glowing ring on the ground under them (flying ones
// too); anything else, like an animal pen, gets an outline. Selectable adds it to every unit by itself; a building gets
// it by having the component on its prefab. Materials live in Resources/Selection so no setup is needed.
public class SelectionHighlight : MonoBehaviour
{
    static readonly Color SelectedColor = new(0.55f, 1f, 0.6f, 0.85f);
    static readonly Color HoverColor = new(0.55f, 1f, 0.6f, 0.35f);
    static readonly Color TurnedColor = new(1f, 0.3f, 0.25f, 0.7f);
    // The outline is opaque, so hover vs selected is brightness, not alpha.
    static readonly Color OutlineSelected = new(0.6f, 1f, 0.65f, 1f);
    static readonly Color OutlineHover = new(0.35f, 0.65f, 0.4f, 1f);
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static Material ringMaterial, outlineMaterial;

    bool hovered, selected, turned;
    Creature creature;
    NavMeshAgent agent;
    Transform ring;
    readonly List<Renderer> outlines = new();
    MaterialPropertyBlock block;

    public bool Hovered
    {
        get => hovered;
        set { hovered = value; Refresh(); }
    }

    public bool Selected
    {
        get => selected;
        set { selected = value; Refresh(); }
    }

    // A stolen animal fighting for the aliens (SideMark). Its red ring stays on, under any hover or select glow.
    public bool Turned
    {
        get => turned;
        set { turned = value; Refresh(); }
    }

    public bool Showing => hovered || selected || turned;

    void Awake()
    {
        creature = GetComponent<Creature>();
        agent = GetComponent<NavMeshAgent>();
        block = new MaterialPropertyBlock();
    }

    void OnDisable()
    {
        hovered = selected = false;
        Refresh();
    }

    void Refresh()
    {
        bool show = Showing;
        if (show && ring == null && outlines.Count == 0) Build();
        if (ring) ring.gameObject.SetActive(show);
        foreach (var r in outlines) if (r) r.gameObject.SetActive(show);
        if (show) Tint();
    }

    void LateUpdate()
    {
        if (!Showing) return;
        if (ring) ring.position = new Vector3(transform.position.x, GroundY() + 0.03f, transform.position.z);
        if (selected) Tint();
    }

    // Selected pulses gently; hovered is a faint steady glow.
    void Tint()
    {
        float pulse = selected ? 0.8f + 0.2f * Mathf.Sin(Time.time * 3f) : 1f;
        if (ring)
        {
            Color c = selected ? SelectedColor : hovered ? HoverColor : TurnedColor;
            c.a *= pulse;
            block.SetColor(BaseColor, c);
            ring.GetComponent<Renderer>().SetPropertyBlock(block);
        }
        if (outlines.Count == 0) return;
        block.SetColor(BaseColor, (selected ? OutlineSelected : OutlineHover) * new Color(pulse, pulse, pulse, 1f));
        foreach (var r in outlines) if (r) r.SetPropertyBlock(block);
    }

    // The agent's base offset lifts flyers off the NavMesh, so the ground is that far below them.
    float GroundY() => transform.position.y - (agent != null ? agent.baseOffset : 0f);

    void Build()
    {
        if (creature != null) BuildRing();
        else BuildOutline();
    }

    void BuildRing()
    {
        ringMaterial ??= Resources.Load<Material>("Selection/GlowRing");
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.name = "Selection Ring";
        ring = quad.transform;
        ring.SetParent(transform, false);
        ring.rotation = Quaternion.Euler(90f, 0f, 0f);

        Bounds body = creature.Body != null ? creature.Body.bounds : new Bounds(transform.position, Vector3.one);
        float size = 2f * Mathf.Max(body.extents.x, body.extents.z) * 1.35f + 0.3f;
        Vector3 parentScale = transform.lossyScale;
        ring.localScale = new Vector3(size / parentScale.x, size / parentScale.z, 1f);

        var r = quad.GetComponent<Renderer>();
        r.sharedMaterial = ringMaterial;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    // A copy of each mesh drawn slightly fatter with its front faces culled, which leaves a rim around the real one.
    void BuildOutline()
    {
        outlineMaterial ??= Resources.Load<Material>("Selection/Outline");
        foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || !filter.TryGetComponent<MeshRenderer>(out var source) || !source.enabled) continue;
            var copy = new GameObject("Outline");
            copy.transform.SetParent(filter.transform, false);
            copy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var r = copy.AddComponent<MeshRenderer>();
            var mats = new Material[filter.sharedMesh.subMeshCount];
            for (int i = 0; i < mats.Length; i++) mats[i] = outlineMaterial;
            r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            outlines.Add(r);
        }
    }
}

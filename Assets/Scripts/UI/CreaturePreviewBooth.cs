using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

public class CreaturePreviewBooth : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Camera previewCamera;
    [SerializeField] private RenderTexture liveRenderTexture;

    private readonly SlotType[] _slots = { SlotType.Head, SlotType.Back, SlotType.Limbs, SlotType.Tail };
    private readonly Dictionary<Creature, Texture2D> _portraitCache = new();
    private GameObject _liveInstance;
    private int _previewLayer;

    private void Awake() => _previewLayer = LayerMask.NameToLayer("UI_Preview");

    private void Update()
    {
        if (_liveInstance != null && _liveInstance.activeInHierarchy)
            _liveInstance.transform.Rotate(Vector3.up, 15f * Time.deltaTime, Space.World);
    }

    public void BindViewport(VisualElement viewport)
    {
        if (viewport == null || liveRenderTexture == null) return;
        var bg = new StyleBackground(Background.FromRenderTexture(liveRenderTexture));
        viewport.style.backgroundImage = bg;
        var inner = viewport.Q("Creature3DRender");
        if (inner != null && inner != viewport)
            inner.style.backgroundImage = bg;
    }

    public void ShowLive(Creature target, System.Func<SlotType, MutationData> mutationSelector = null)
    {
        ClearLive();
        if (spawnPoint == null || target == null) return;

        _liveInstance = SpawnSanitizedClone(target, spawnPoint.rotation, mutationSelector);
        if (previewCamera != null) previewCamera.enabled = _liveInstance != null;
    }

    public void ClearLive()
    {
        if (_liveInstance != null) Destroy(_liveInstance);
        if (previewCamera != null) previewCamera.enabled = false;
    }

    public Texture2D GetPortrait(Creature target)
    {
        if (target == null || previewCamera == null || spawnPoint == null) return null;
        if (_portraitCache.TryGetValue(target, out var cached) && cached != null) return cached;

        bool hadLive = _liveInstance != null && _liveInstance.activeSelf;
        if (hadLive) _liveInstance.SetActive(false);

        GameObject clone = SpawnSanitizedClone(target, spawnPoint.rotation * Quaternion.Euler(0f, 35f, 0f));

        var tempRT = RenderTexture.GetTemporary(256, 256, 16, RenderTextureFormat.ARGB32);
        var prevTarget = previewCamera.targetTexture;
        var prevActive = RenderTexture.active;
        bool prevEnabled = previewCamera.enabled;

        previewCamera.targetTexture = tempRT;
        previewCamera.enabled = true;
        previewCamera.Render();

        RenderTexture.active = tempRT;
        var tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
        tex.Apply();

        previewCamera.targetTexture = prevTarget;
        previewCamera.enabled = prevEnabled;
        RenderTexture.active = prevActive;
        RenderTexture.ReleaseTemporary(tempRT);
        DestroyImmediate(clone);

        if (hadLive) _liveInstance.SetActive(true);
        return _portraitCache[target] = tex;
    }

    public void InvalidatePortrait(Creature target)
    {
        if (target != null && _portraitCache.Remove(target, out var tex) && tex != null)
            Destroy(tex);
    }

    public void ClearCache()
    {
        foreach (var tex in _portraitCache.Values)
            if (tex != null) Destroy(tex);
        _portraitCache.Clear();
    }

    private GameObject SpawnSanitizedClone(Creature source, Quaternion rot, System.Func<SlotType, MutationData> selector = null)
    {
        bool wasActive = spawnPoint.gameObject.activeSelf;
        spawnPoint.gameObject.SetActive(false);

        // Prefer the clean SourcePrefab when available so runtime components/colliders aren't cloned
        GameObject template = (source is Animal animal && animal.SourcePrefab != null)
            ? animal.SourcePrefab.gameObject
            : source.gameObject;

        GameObject clone = Instantiate(template, spawnPoint.position, rot, spawnPoint);

        // Strip gameplay scripts immediately while inactive so their Awake()/OnEnable() never fire on SetActive(true)
        foreach (var h in clone.GetComponentsInChildren<Highlight>(true)) DestroyImmediate(h);
        foreach (var b in clone.GetComponentsInChildren<CreatureBrain>(true)) DestroyImmediate(b);
        foreach (var a in clone.GetComponentsInChildren<NavMeshAgent>(true)) DestroyImmediate(a);
        foreach (var s in clone.GetComponentsInChildren<Selectable>(true)) DestroyImmediate(s);
        foreach (var g in clone.GetComponentsInChildren<Gatherer>(true)) DestroyImmediate(g);
        foreach (var t in clone.GetComponentsInChildren<ToyBob>(true)) DestroyImmediate(t);
        foreach (var o in clone.GetComponentsInChildren<Outline>(true)) DestroyImmediate(o);
        foreach (var c in clone.GetComponentsInChildren<Creature>(true)) DestroyImmediate(c);
        foreach (var col in clone.GetComponentsInChildren<Collider>(true)) DestroyImmediate(col);
        foreach (var rb in clone.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);

        foreach (var r in clone.GetComponentsInChildren<Renderer>(true))
            r.enabled = true;

        spawnPoint.gameObject.SetActive(wasActive);

        if (clone.TryGetComponent<CreatureMutator>(out var cloneMut))
        {
            source.TryGetComponent<CreatureMutator>(out var liveMut);
            foreach (SlotType slot in _slots)
            {
                MutationData m = selector != null ? selector(slot) : liveMut?.GetEquippedMutation(slot);
                if (m != null) cloneMut.EquipMutation(m, slot);
                else cloneMut.UnequipMutation(slot);
            }
        }

        SetLayer(clone, _previewLayer);
        return clone;
    }

    private static void SetLayer(GameObject obj, int layer)
    {
        if (layer < 0) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform) SetLayer(child.gameObject, layer);
    }
}
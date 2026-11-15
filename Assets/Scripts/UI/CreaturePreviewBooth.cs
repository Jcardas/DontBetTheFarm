using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

public class CreaturePreviewBooth : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Camera previewCamera;
    [SerializeField] private RenderTexture liveRenderTexture;
    [SerializeField] private float dragSpinSpeed = 0.5f;

    private readonly SlotType[] _slots = { SlotType.Head, SlotType.Back, SlotType.Limbs, SlotType.Tail };
    private readonly Dictionary<Creature, Texture2D> _portraitCache = new();
    private GameObject _liveInstance;
    private int _previewLayer;

    private bool _isSpinning;
    private float _lastPointerX;

    private void Awake() => _previewLayer = LayerMask.NameToLayer("UI_Preview");

    private void Update()
    {
        if (_liveInstance != null && _liveInstance.activeInHierarchy && !_isSpinning)
            _liveInstance.transform.Rotate(Vector3.up, 15f * Time.deltaTime, Space.World);
    }

    public void BindViewport(VisualElement viewport)
    {
        if (viewport == null) return;

        if (liveRenderTexture != null)
        {
            var bg = new StyleBackground(Background.FromRenderTexture(liveRenderTexture));
            viewport.style.backgroundImage = bg;
            var inner = viewport.Q("Creature3DRender");
            if (inner != null && inner != viewport)
                inner.style.backgroundImage = bg;
        }

        var target = viewport.Q("Creature3DRender") ?? viewport;
        target.pickingMode = PickingMode.Position;

        target.UnregisterCallback<PointerDownEvent>(OnViewportPointerDown);
        target.UnregisterCallback<PointerMoveEvent>(OnViewportPointerMove);
        target.UnregisterCallback<PointerUpEvent>(OnViewportPointerUp);
        target.UnregisterCallback<PointerCaptureOutEvent>(OnViewportCaptureOut);

        target.RegisterCallback<PointerDownEvent>(OnViewportPointerDown);
        target.RegisterCallback<PointerMoveEvent>(OnViewportPointerMove);
        target.RegisterCallback<PointerUpEvent>(OnViewportPointerUp);
        target.RegisterCallback<PointerCaptureOutEvent>(OnViewportCaptureOut);
    }

    private void OnViewportPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0 || _liveInstance == null) return;
        _isSpinning = true;
        _lastPointerX = evt.position.x;
        if (evt.currentTarget is VisualElement ve)
            ve.CapturePointer(evt.pointerId);
    }

    private void OnViewportPointerMove(PointerMoveEvent evt)
    {
        if (!_isSpinning || _liveInstance == null) return;
        float deltaX = evt.position.x - _lastPointerX;
        _lastPointerX = evt.position.x;
        _liveInstance.transform.Rotate(Vector3.up, -deltaX * dragSpinSpeed, Space.World);
    }

    private void OnViewportPointerUp(PointerUpEvent evt)
    {
        if (!_isSpinning) return;
        _isSpinning = false;
        if (evt.currentTarget is VisualElement ve && ve.HasPointerCapture(evt.pointerId))
            ve.ReleasePointer(evt.pointerId);
    }

    private void OnViewportCaptureOut(PointerCaptureOutEvent evt) => _isSpinning = false;

    public void ShowLive(Creature target, System.Func<SlotType, MutationData> mutationSelector = null)
    {
        ClearLive();
        if (spawnPoint == null || target == null) return;

        _liveInstance = SpawnSanitizedClone(target, spawnPoint.rotation, mutationSelector);
        if (previewCamera != null) previewCamera.enabled = _liveInstance != null;
    }

    public void ClearLive()
    {
        _isSpinning = false;
        if (_liveInstance != null) Destroy(_liveInstance);
        _liveInstance = null;
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

        GameObject template = (source is Animal animal && animal.SourcePrefab != null)
            ? animal.SourcePrefab.gameObject
            : source.gameObject;

        GameObject clone = Instantiate(template, spawnPoint.position, rot, spawnPoint);

        // Strip dependent gameplay scripts FIRST, then base components (Creature, NavMeshAgent), then physics
        StripComponents<Highlight>(clone);
        StripComponents<Outline>(clone);
        StripComponents<Selectable>(clone);
        StripComponents<Gatherer>(clone);
        StripComponents<ToyBob>(clone);
        StripComponents<CreatureBrain>(clone);
        StripComponents<NavMeshAgent>(clone);
        StripComponents<Creature>(clone);
        StripComponents<Rigidbody>(clone);
        StripComponents<Collider>(clone);

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

    private static void StripComponents<T>(GameObject root) where T : Component
    {
        var comps = root.GetComponentsInChildren<T>(true);
        for (int i = comps.Length - 1; i >= 0; i--)
        {
            if (comps[i] != null)
                DestroyImmediate(comps[i]);
        }
    }

    private static void SetLayer(GameObject obj, int layer)
    {
        if (layer < 0) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform) SetLayer(child.gameObject, layer);
    }
}
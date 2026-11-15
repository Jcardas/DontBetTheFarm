using System.Collections.Generic;
using UnityEngine;
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

    private Creature _currentTarget;
    private bool _wasTargetDown;
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
        if (spawnPoint == null || target == null)
        {
            ClearLive();
            return;
        }

        bool stateChanged = _currentTarget != target || _wasTargetDown != target.Down;
        Quaternion rot = (!stateChanged && _liveInstance != null)
            ? _liveInstance.transform.rotation
            : GetTargetRotation(target);

        ClearLive();
        _currentTarget = target;
        _wasTargetDown = target.Down;

        _liveInstance = BuildCleanPreview(target, rot, mutationSelector);
        if (previewCamera != null) previewCamera.enabled = _liveInstance != null;
    }

    private Quaternion GetTargetRotation(Creature target)
    {
        Quaternion yawRot = spawnPoint.rotation;
        if (previewCamera != null)
        {
            Vector3 toCamera = previewCamera.transform.position - spawnPoint.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.001f)
                yawRot = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }

        // Upright when alive; rolled 90 degrees onto its side when knocked out / dead
        return target.Down ? yawRot * Quaternion.Euler(0f, 0f, 90f) : yawRot;
    }

    public void ClearLive()
    {
        _isSpinning = false;
        _currentTarget = null;
        if (_liveInstance != null)
        {
            DestroyImmediate(_liveInstance);
            _liveInstance = null;
        }
        if (previewCamera != null) previewCamera.enabled = false;

        if (liveRenderTexture != null)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = liveRenderTexture;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = prev;
        }
    }

    public Texture2D GetPortrait(Creature target)
    {
        if (target == null || previewCamera == null || spawnPoint == null) return null;
        if (_portraitCache.TryGetValue(target, out var cached) && cached != null) return cached;

        bool hadLive = _liveInstance != null && _liveInstance.activeSelf;
        if (hadLive) _liveInstance.SetActive(false);

        GameObject clone = BuildCleanPreview(target, spawnPoint.rotation * Quaternion.Euler(0f, 35f, 0f));

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

    private GameObject BuildCleanPreview(Creature source, Quaternion rot, System.Func<SlotType, MutationData> selector = null)
    {
        bool wasActive = spawnPoint.gameObject.activeSelf;
        spawnPoint.gameObject.SetActive(false);

        GameObject template = (source is Animal animal && animal.SourcePrefab != null && animal.SourcePrefab != source)
            ? animal.SourcePrefab.gameObject
            : source.gameObject;

        // 1. Create a completely clean root GameObject with NO gameplay/AI/physics components
        var previewRoot = new GameObject($"{source.CreatureName}_Preview");
        previewRoot.transform.SetParent(spawnPoint, false);
        previewRoot.transform.SetPositionAndRotation(spawnPoint.position, rot);
        previewRoot.transform.localScale = template.transform.localScale;

        // 2. Copy only the visual child hierarchy (meshes, armature, socket transforms)
        foreach (Transform child in template.transform)
        {
            if (child.name.IndexOf("Selection", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name.IndexOf("Ring", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name.IndexOf("Outline", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.GetComponentInChildren<Canvas>(true) != null)
            {
                continue;
            }

            var childCopy = Instantiate(child.gameObject, previewRoot.transform, false);
            childCopy.name = child.name;

            // Restore exact prefab rest transform from the live ToyBob without any Euler angle math
            if (child.TryGetComponent<ToyBob>(out var sourceBob))
            {
                childCopy.transform.localPosition = sourceBob.RestPosition;
                childCopy.transform.localRotation = sourceBob.RestRotation;
            }

            foreach (var mb in childCopy.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null) DestroyImmediate(mb);
            foreach (var col in childCopy.GetComponentsInChildren<Collider>(true))
                if (col != null) DestroyImmediate(col);
            foreach (var rb in childCopy.GetComponentsInChildren<Rigidbody>(true))
                if (rb != null) DestroyImmediate(rb);
        }

        // 3. Reset Animator to neutral bind pose
        foreach (var anim in previewRoot.GetComponentsInChildren<Animator>(true))
        {
            if (anim == null) continue;
            anim.Rebind();
            anim.Update(0f);
            anim.enabled = false;
        }

        // 4. Strip QuickOutline / Highlight injected materials & rendering layer masks
        foreach (var r in previewRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            if (r.gameObject.activeInHierarchy) r.enabled = true;
            r.renderingLayerMask = 1;

            if (r.sharedMaterials != null && r.sharedMaterials.Length > 1)
            {
                var mats = new List<Material>(r.sharedMaterials);
                for (int i = mats.Count - 1; i >= 0; i--)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    string matName = m.name.ToLowerInvariant();
                    if (matName.Contains("outline") || matName.Contains("highlight"))
                        mats.RemoveAt(i);
                }
                r.sharedMaterials = mats.Count > 0 ? mats.ToArray() : new[] { r.sharedMaterials[0] };
            }
        }

        // 5. Attach CreatureMutator, purge any live-cloned socket attachments, and apply current/staged mutations
        if (template.TryGetComponent<CreatureMutator>(out var templateMut))
        {
            var previewMut = previewRoot.AddComponent<CreatureMutator>();
            previewMut.CopySocketsFrom(templateMut, template.transform, previewRoot.transform);
            previewMut.ClearAllSocketChildrenImmediate();
            previewMut.InitializeForPreview();

            source.TryGetComponent<CreatureMutator>(out var liveMut);
            foreach (SlotType slot in _slots)
            {
                MutationData m = selector != null ? selector(slot) : liveMut?.GetEquippedMutation(slot);
                if (m != null) previewMut.EquipMutation(m, slot);
                else previewMut.UnequipMutation(slot);
            }
        }

        spawnPoint.gameObject.SetActive(wasActive);
        SetLayer(previewRoot, _previewLayer);
        return previewRoot;
    }

    private static void SetLayer(GameObject obj, int layer)
    {
        if (layer < 0) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform) SetLayer(child.gameObject, layer);
    }
}
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerControls : MonoBehaviour
{
    const float DragPixels = 8f;
    const float Spacing = 1.2f;
    const float DoubleClickSeconds = 0.35f;

    enum LeftButton { Idle, Pressed, Dragging }

    [Tooltip("Spawned where each order lands, so the player sees the click register.")]
    public OrderMarker marker;

    InputActions input;
    InputAction[] groupKeys;
    LeftButton left;
    Vector2 pressedAt;
    Shotgun shotgun;
    CameraController cameraController;
    PenUIController penUI;
    Selectable lastClicked;
    HashSet<Selectable> lit = new();
    Highlight litTarget;
    CursorType cursor;
    float lastClickAt;
    readonly List<Selectable>[] groups = { new(), new(), new() };
    readonly Plane ground = new(Vector3.up, Vector3.zero);

    void Awake()
    {
        input = new InputActions();
        groupKeys = new[] { input.Player.Group1, input.Player.Group2, input.Player.Group3 };
    }

    void OnEnable()
    {
        input.Player.Enable();
        input.Camera.MiddleClick.Enable();
    }

    void OnDisable()
    {
        input.Player.Disable();
        input.Camera.MiddleClick.Disable();
    }

    void OnDestroy() => input?.Dispose();

    void Start()
    {
        shotgun = FindAnyObjectByType<Shotgun>();
        cameraController = FindAnyObjectByType<CameraController>();
        penUI = FindAnyObjectByType<PenUIController>(FindObjectsInactive.Include);
    }

    void Update()
    {
        if (penUI != null && penUI.IsOpen) return;

        var player = input.Player;
        bool shift = player.Add.IsPressed(), ctrl = player.SaveGroup.IsPressed();
        ReadKeys(shift);

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector2 at = player.Point.ReadValue<Vector2>();
        Ray ray = cam.ScreenPointToRay(at);
        if (player.Fire.WasPressedThisFrame() && shotgun && shotgun.isActiveAndEnabled && ground.Raycast(ray, out float along))
            shotgun.Fire(ray.GetPoint(along));

        bool overSomething = MouseTarget.Find(ray, out RaycastHit hit);
        var unit = overSomething ? hit.collider.GetComponentInParent<Selectable>() : null;

        if (player.Command.WasPressedThisFrame())
        {
            if (Order.BrokenAt(ray) == null && overSomething)
                Command(ray, hit);
        }

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (left == LeftButton.Idle && player.Select.WasPressedThisFrame() && !overUI)
        {
            left = LeftButton.Pressed;
            pressedAt = at;
        }
        if (left == LeftButton.Pressed && (at - pressedAt).magnitude > DragPixels) left = LeftButton.Dragging;
        Hover(left == LeftButton.Dragging ? Selectable.All.Where(s => OnScreen(cam, s, Box(pressedAt, at))) : new[] { unit });
        HoverTarget(left != LeftButton.Dragging ? Target(ray, overSomething, hit) : null);
        if (left == LeftButton.Idle || !player.Select.WasReleasedThisFrame()) return;

        if (left == LeftButton.Dragging) Pick(Selectable.All.Where(s => OnScreen(cam, s, Box(pressedAt, at))), shift, ctrl);
        else Click(cam, unit, shift, ctrl);
        left = LeftButton.Idle;
    }

    void LateUpdate()
    {
        if (penUI != null && penUI.IsOpen) return;

        if (cameraController != null &&
            (cameraController.IsDragging || cameraController.IsEdgeScrolling))
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector2 at = input.Player.Point.ReadValue<Vector2>();
        Ray ray = cam.ScreenPointToRay(at);
        bool overSomething = MouseTarget.Find(ray, out RaycastHit hit);
        var unit = overSomething
            ? hit.collider.GetComponentInParent<Selectable>()
            : null;

        ShowCursor(shotgun && shotgun.Reloading
            ? ReloadCursor(shotgun.ReloadProgress)
            : BrokenTargetAt(ray) != null
                ? CursorType.Repair
                : overSomething
                    ? CursorFor(ray, hit, unit)
                    : CursorType.Default);
    }

    void ReadKeys(bool shift)
    {
        var player = input.Player;

        if (player.Cancel.WasPressedThisFrame()) Clear();
        for (int g = 0; g < groups.Length; g++)
        {
            if (!groupKeys[g].WasPressedThisFrame()) continue;
            if (player.SaveGroup.IsPressed()) groups[g] = Selectable.All.Where(s => s.Selected).ToList();
            else Pick(groups[g], shift);
        }
    }

    static void Clear()
    {
        foreach (var s in Selectable.All) s.Selected = false;
        Gatherer.DismissRepairUI();
    }

    void Hover(IEnumerable<Selectable> units)
    {
        var now = units.Where(s => s != null).ToHashSet();
        foreach (var s in lit) if (s && !now.Contains(s)) s.Hovered = false;
        foreach (var s in now) if (!lit.Contains(s)) s.Hovered = true;
        lit = now;
    }

    static Buildable BrokenTargetAt(Ray ray)
    {
        Buildable broken = Order.BrokenAt(ray);
        return broken && AnyPickedCan(Order.ForRepair(broken)) ? broken : null;
    }

    static Component Target(Ray ray, bool overSomething, RaycastHit hit)
    {
        var broken = BrokenTargetAt(ray);
        if (broken != null) return broken;
        if (!overSomething) return null;

        var order = Order.From(ray, hit);
        if (AnyPickedCan(order))
        {
            if (order.enemy) return order.enemy;
            if (order.node) return order.node;
            if (order.field) return order.field;
            if (order.building) return order.building;
        }

        // Only open/highlight context menus on non-broken IContextInteractable buildings or AnimalPen interior.
        var menuTarget = MouseTarget.Menu(ray, hit) ?? (MouseTarget.Building(ray, hit) as AnimalPen);
        return menuTarget is Buildable b && b.IsBroken ? null : menuTarget;
    }

    static bool AnyPickedCan(Order order) => Selectable.All.Any(s => s.Selected && s.CanObey(order));

    static bool AnyPicked => Selectable.All.Any(s => s.Selected);

    void HoverTarget(Component target)
    {
        Highlight next = null;
        if (target && !target.TryGetComponent(out next)) next = target.gameObject.AddComponent<Highlight>();
        if (next == litTarget) return;
        if (litTarget) litTarget.Hovered = false;
        litTarget = next;
        if (next) next.Hovered = true;
    }

    static CursorType CursorFor(Ray ray, RaycastHit hit, Selectable unit)
    {
        var order = Order.From(ray, hit);
        if (AnyPickedCan(order))
        {
            return order.kind switch
            {
                OrderKind.Attack => CursorType.Attack,
                OrderKind.Harvest => CursorType.Harvest,
                OrderKind.Work => CursorType.Chop,
                OrderKind.Repair => CursorType.Repair,
                OrderKind.Pen => CursorType.AssignToPen,
                OrderKind.Barn => CursorType.EnterBarn,
                _ when unit && !unit.Down => CursorType.Interact,
                _ => CursorType.MoveTo,
            };
        }

        // Only buildings implementing IContextInteractable (via MouseTarget.Menu) or AnimalPen interior get OpenMenu
        var menuTarget = MouseTarget.Menu(ray, hit) ?? (MouseTarget.Building(ray, hit) as AnimalPen);
        if (menuTarget && !(menuTarget is Buildable b && b.IsBroken))
            return CursorType.OpenMenu;

        if (unit && !unit.Down)
            return CursorType.Interact;

        return AnyPicked ? CursorType.MoveTo : CursorType.Default;
    }

    static CursorType ReloadCursor(float progress) => CursorType.Reload0 + Mathf.Min(3, (int)(progress * 4f));

    void ShowCursor(CursorType kind)
    {
        bool changed = kind != cursor;
        cursor = kind;
        if ((changed || kind != CursorType.Default) && !input.Camera.MiddleClick.IsPressed())
        {
            CursorManager.Instance?.SetCursor(kind);
        }
    }

    void Click(Camera cam, Selectable unit, bool shift, bool ctrl)
    {
        if (unit != null && unit.Down) unit = null;
        bool twice = unit != null && unit == lastClicked && Time.unscaledTime - lastClickAt < DoubleClickSeconds;
        lastClicked = unit;
        lastClickAt = Time.unscaledTime;

        if (unit == null || !Gatherer.IsActiveRepairer(unit))
            Gatherer.DismissRepairUI();

        if (unit == null)
        {
            if (!shift && !ctrl) Clear();
        }
        else if (twice) Pick(Selectable.All.Where(s => s.GetCreatureName() == unit.GetCreatureName() && OnScreen(cam, s, cam.pixelRect)), shift, ctrl);
        else if (ctrl) unit.Selected = false;
        else if (shift || unit.Selected) unit.Selected = !unit.Selected;
        else foreach (var s in Selectable.All) s.Selected = s == unit;
    }

    static void Pick(IEnumerable<Selectable> units, bool shift, bool ctrl = false)
    {
        var standing = units.Where(s => !s.Down).ToList();
        if (ctrl)
        {
            foreach (var s in standing) s.Selected = false;
            return;
        }
        if (standing.Count == 0) return;
        foreach (var s in Selectable.All) s.Selected = standing.Contains(s) || (shift && s.Selected);
    }

    static bool OnScreen(Camera cam, Selectable s, Rect area)
    {
        Vector3 p = cam.WorldToScreenPoint(s.transform.position);
        return p.z > 0f && area.Contains(p);
    }

    // Yields to ContextInteractionManager whenever clicking on or inside an intact building (including AnimalPen interior).
    void Command(Ray ray, RaycastHit hit)
    {
        var order = Order.From(ray, hit);
        if (order.kind != OrderKind.Move && AnyPickedCan(order))
        {
            Command(order);
            return;
        }

        var menuTarget = MouseTarget.Menu(ray, hit) ?? (MouseTarget.Building(ray, hit) as AnimalPen);
        if (menuTarget && !(menuTarget is Buildable b && b.IsBroken))
        {
            Gatherer.DismissRepairUI();
            return;
        }

        Command(order);
    }

    void Command(Order order)
    {
        var group = Selectable.All.Where(s => s.Selected).ToList();

        bool keepingSameRepair = order.kind == OrderKind.Repair &&
                                 group.Any(s => Gatherer.IsActiveRepairerOn(s, order.building));
        if (!keepingSameRepair)
            Gatherer.DismissRepairUI();

        if (order.kind == OrderKind.Repair)
        {
            foreach (Selectable unit in group)
            {
                Gatherer gatherer = unit.GetComponent<Gatherer>();
                if (!gatherer || !gatherer.CanRepair) continue;
                if (marker) Instantiate(marker, order.point, Quaternion.identity).Show(order.kind);
                unit.Command(order);
                return;
            }
            return;
        }

        if (marker) Instantiate(marker, order.point, Quaternion.identity).Show(order.kind);
        for (int i = 0; i < group.Count; i++) group[i].Command(order.Offset(Spread(i)));
    }

    static Vector3 Spread(int i)
    {
        if (i == 0) return Vector3.zero;
        int ring = 1;
        while (i > 3 * ring * (ring + 1)) ring++;
        int firstOnRing = 3 * (ring - 1) * ring + 1;
        float angle = (i - firstOnRing) * 2f * Mathf.PI / (6 * ring);

        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (ring * Spacing);
    }

    static Rect Box(Vector2 a, Vector2 b) =>
        Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

    void OnGUI()
    {
        if (left != LeftButton.Dragging) return;
        Rect r = Box(pressedAt, input.Player.Point.ReadValue<Vector2>());
        r.y = Screen.height - r.yMax;
        var tint = new Color(0.4f, 0.7f, 1f);
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(tint.r, tint.g, tint.b, 0.12f), 0f, 0f);
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(tint.r, tint.g, tint.b, 0.8f), 1f, 0f);
    }
}
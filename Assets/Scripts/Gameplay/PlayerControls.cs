using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// The player's mouse and keyboard. Nothing is picked at the start. Left-click only selects: click a unit to pick just it,
// click a picked unit to drop it from the selection, double-click to pick every unit of its type on screen, or drag a box
// around several (Shift-click toggles one unit, Shift-drag adds, Ctrl-click and Ctrl-drag drop). Left-clicking anything
// that isn't a unit, or pressing Escape, drops everyone. Right-click the ground, a resource, an alien,
// (for animals) a pen or (for the farmer) the barn to order the picked units there; with nobody picked (or on a broken
// building), right-click opens its context menu (ContextInteractionManager). Ctrl+1..3 saves the picked units as a group,
// 1..3 picks the group again (Shift+1..3 adds it). J toggles the Barn Research menu.
// Space fires the farmer's shotgun toward the cursor.
// The cursor shows what a right-click would do on whatever the mouse is over, or how far along a shotgun reload is.
public class PlayerControls : MonoBehaviour
{
    const float DragPixels = 8f;
    const float Gap = 0.4f; // between the edges of units standing side by side after a group order
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
    BarnResearchUIController barnUI;
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

    // Middle-drag belongs to CameraController; its MiddleClick is enabled here only so hover cursors can yield to Grab.
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
        barnUI = FindAnyObjectByType<BarnResearchUIController>();
    }

    void Update()
    {
        var player = input.Player;
        bool shift = player.Add.IsPressed(), ctrl = player.SaveGroup.IsPressed();
        ReadKeys(shift);

        // Stop world selection, shotgun firing, and building highlights while modal UI or building placement/demolition is active
        if (UIBlocker.IsModalUIOpen || (BuildingManager.Instance != null && BuildingManager.Instance.IsBusy))
        {
            Hover(System.Linq.Enumerable.Empty<Selectable>());
            HoverTarget(null);
            left = LeftButton.Idle;
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector2 at = player.Point.ReadValue<Vector2>();
        bool overUI = UIBlocker.IsPointerOverUI(at);

        Ray ray = cam.ScreenPointToRay(at);
        if (!overUI && player.Fire.WasPressedThisFrame() && shotgun && shotgun.isActiveAndEnabled && ground.Raycast(ray, out float along))
            shotgun.Fire(ray.GetPoint(along));

        RaycastHit hit = default;
        bool overSomething = !overUI && MouseTarget.Find(ray, out hit);
        var unit = overSomething ? hit.collider.GetComponentInParent<Selectable>() : null;

        if (!overUI && player.Command.WasPressedThisFrame())
        {
            if (Order.BrokenAt(ray) == null && overSomething)
                Command(ray, hit);
        }

        // Ignore left-clicks over UI so clicking UI panels/buttons doesn't clear selection or dismiss panels early.
        if (left == LeftButton.Idle && player.Select.WasPressedThisFrame() && !overUI)
        {
            left = LeftButton.Pressed;
            pressedAt = at;
        }
        if (left == LeftButton.Pressed && (at - pressedAt).magnitude > DragPixels) left = LeftButton.Dragging;

        Hover(left == LeftButton.Dragging
            ? Selectable.All.Where(s => OnScreen(cam, s, Box(pressedAt, at)))
            : (overUI ? System.Linq.Enumerable.Empty<Selectable>() : new[] { unit }));

        // Do not gate on overSomething: broken buildings without colliders are still found via renderer bounds.
        HoverTarget(!overUI && left != LeftButton.Dragging ? Target(ray, overSomething, hit) : null);

        if (left == LeftButton.Idle || !player.Select.WasReleasedThisFrame()) return;

        if (left == LeftButton.Dragging) Pick(Selectable.All.Where(s => OnScreen(cam, s, Box(pressedAt, at))), shift, ctrl);
        else if (!overUI) Click(cam, unit, shift, ctrl);
        left = LeftButton.Idle;
    }

    void LateUpdate()
    {
        if (UIBlocker.IsModalUIOpen)
        {
            ShowCursor(CursorType.Default);
            return;
        }

        if (cameraController != null &&
            (cameraController.IsDragging || cameraController.IsEdgeScrolling))
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector2 at = input.Player.Point.ReadValue<Vector2>();
        if (UIBlocker.IsPointerOverUI(at))
        {
            ShowCursor(CursorType.Default);
            return;
        }

        if (BuildingManager.Instance != null && BuildingManager.Instance.IsDemolishing)
        {
            ShowCursor(CursorType.Repair);
            return;
        }

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

        // Playtest shortcut: press R to restart the current scene
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        // Toggle Barn Research menu with J
        if (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame && barnUI != null)
        {
            if (barnUI.IsMenuOpen) barnUI.CloseMenu();
            else barnUI.OpenMenu();
        }

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

    // Lights the unit under the mouse or, while dragging, every unit the box would pick.
    void Hover(IEnumerable<Selectable> units)
    {
        var now = units.Where(s => s != null).ToHashSet();
        foreach (var s in lit) if (s && !now.Contains(s)) s.Hovered = false;
        foreach (var s in now) if (!lit.Contains(s)) s.Hovered = true;
        lit = now;
    }

    // Returns the broken building under the ray only when a picked unit can repair it.
    static Buildable BrokenTargetAt(Ray ray)
    {
        Buildable broken = Order.BrokenAt(ray);
        return broken && AnyPickedCan(Order.ForRepair(broken)) ? broken : null;
    }

    // What lights up under the mouse besides units: what a right-click would act on, when a picked unit can do it
    // (an alien to attack, a tree to chop, a field to harvest, a pen for animals, or a broken building to repair),
    // or an intact building whose menu it would open.
    static Component Target(Ray ray, bool overSomething, RaycastHit hit)
    {
        var broken = BrokenTargetAt(ray);
        if (broken != null) return broken;
        if (!overSomething) return null;

        var knockedOut = hit.collider.GetComponentInParent<Animal>();
        if (knockedOut != null && knockedOut.Down) return knockedOut;

        var order = Order.From(ray, hit);
        if (AnyPickedCan(order))
        {
            if (order.enemy is Component enemyComp && enemyComp) return enemyComp;
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

    // Lights the target the way units light up; it gets its Highlight the first time.
    void HoverTarget(Component target)
    {
        Highlight next = null;
        if (target && !target.TryGetComponent(out next)) next = target.gameObject.AddComponent<Highlight>();
        if (next == litTarget) return;
        if (litTarget) litTarget.Hovered = false;
        litTarget = next;
        if (next) next.Hovered = true;
    }

    // Same precedence as Order.From, so the cursor always previews the order a right-click would give, and only
    // when a picked unit can carry it out: with just cows picked, a tree shows the walk-here cursor, since they walk there.
    static CursorType CursorFor(Ray ray, RaycastHit hit, Selectable unit)
    {
        var knockedOut = hit.collider.GetComponentInParent<Animal>();
        if (knockedOut != null && knockedOut.Down)
            return CursorType.Interact;

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

    // Quarter steps. Reload100 is never shown: the frame the reload completes, the hover cursor comes back.
    static CursorType ReloadCursor(float progress) => CursorType.Reload0 + Mathf.Min(3, (int)(progress * 4f));

    // CameraController also sets cursors (Grab, edge arrows) and resets to Default when its own state ends,
    // so a non-Default hover cursor is re-asserted every frame; SetCursor ignores repeats. Grab wins while held.
    void ShowCursor(CursorType kind)
    {
        bool changed = kind != cursor;
        cursor = kind;
        if ((changed || kind != CursorType.Default) && !input.Camera.MiddleClick.IsPressed())
        {
            CursorManager.Instance?.SetCursor(kind);
        }
    }

    // `unit` is whatever unit is under the mouse, or null.
    void Click(Camera cam, Selectable unit, bool shift, bool ctrl)
    {
        if (unit != null && unit.Down) unit = null;
        bool twice = unit != null && unit == lastClicked && Time.unscaledTime - lastClickAt < DoubleClickSeconds;
        lastClicked = unit;
        lastClickAt = Time.unscaledTime;

        // Left-clicking the ground or another unit dismisses any open repair panel unless the active repairer was clicked.
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

    // Picks the standing `units` instead of the current selection (Shift adds them to it, Ctrl drops them from it).
    // Picking nobody leaves the selection as it was.
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

    // Yields to ContextInteractionManager whenever clicking on or inside an intact building (including AnimalPen interior)
    // or on a knocked-out animal so its context menu opens instead of auto-executing.
    void Command(Ray ray, RaycastHit hit)
    {
        var clickedAnimal = hit.collider.GetComponentInParent<Animal>();
        if (clickedAnimal != null && clickedAnimal.Down)
        {
            Gatherer.DismissRepairUI();
            return;
        }

        // Always let AnimalPen open its context menu ("View Pen" / "Assign to pen") instead of auto-assigning on right-click.
        if (MouseTarget.Building(ray, hit) is AnimalPen { IsBroken: false })
        {
            Gatherer.DismissRepairUI();
            return;
        }

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

    // Everyone gets the same order, but each unit walks to its own spot around the point, so a group stands
    // side by side there instead of shoving for one spot.
    void Command(Order order)
    {
        ContextMenuEventBus.RequestClose();
        var group = Selectable.All.Where(s => s.Selected).ToList();

        // Any right-click dismisses an open repair panel unless re-issuing repair on the same building to the active repairer.
        bool keepingSameRepair = order.kind == OrderKind.Repair &&
                                 group.Any(s => Gatherer.IsActiveRepairerOn(s, order.building));
        if (!keepingSameRepair)
            Gatherer.DismissRepairUI();

        // Only one worker should operate the shared repair panel.
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
        var spots = Spots(order.point, group.Count, SpacingFor(group));
        foreach (var (unit, spot) in Pair(group, spots)) unit.Command(order.Offset(spot - order.point));
    }

    // Centre to centre, room for the widest picked unit plus a little gap.
    static float SpacingFor(List<Selectable> group) =>
        Gap + 2f * group.Select(s => s.TryGetComponent(out NavMeshAgent a) ? a.radius * s.transform.lossyScale.x : 0f).DefaultIfEmpty().Max();

    // `count` spots on the NavMesh around `centre`, at least `spacing` apart: the centre first, then rings outwards.
    // Spots inside a wall or a fence are skipped, so the group packs round obstacles.
    static List<Vector3> Spots(Vector3 centre, int count, float spacing)
    {
        var spots = new List<Vector3>();
        for (int i = 0; spots.Count < count && i < count * 4 + 30; i++)
        {
            if (!NavMesh.SamplePosition(centre + Spread(i) * spacing, out NavMeshHit hit, spacing / 2f, NavMesh.AllAreas)) continue;
            if (spots.Any(s => Vector3.Distance(s, hit.position) < spacing * 0.9f)) continue;
            spots.Add(hit.position);
        }
        while (spots.Count < count) spots.Add(centre); // nowhere left to stand nearby
        return spots;
    }

    // Closest unit-and-spot pairs first, so units don't cross each other's paths to get to their spots.
    static IEnumerable<(Selectable, Vector3)> Pair(List<Selectable> units, List<Vector3> spots)
    {
        var free = units.ToList();
        var open = spots.ToList();
        while (free.Count > 0)
        {
            var (unit, spot) = free.SelectMany(u => open.Select(s => (u, s)))
                .OrderBy(p => (p.u.transform.position - p.s).sqrMagnitude).First();
            free.Remove(unit);
            open.Remove(spot);
            yield return (unit, spot);
        }
    }

    // Spot 0 is the point; the rest fill rings one step apart. Ring k holds 6k spots,
    // which keeps neighbours on a ring about one step apart too.
    static Vector3 Spread(int i)
    {
        if (i == 0) return Vector3.zero;
        int ring = 1;
        while (i > 3 * ring * (ring + 1)) ring++; // rings 1..k hold 6 + 12 + ... + 6k = 3k(k+1) spots
        int firstOnRing = 3 * (ring - 1) * ring + 1;
        float angle = (i - firstOnRing) * 2f * Mathf.PI / (6 * ring);

        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring;
    }

    static Rect Box(Vector2 a, Vector2 b) =>
        Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

    void OnGUI()
    {
        if (left != LeftButton.Dragging) return;
        Rect r = Box(pressedAt, input.Player.Point.ReadValue<Vector2>());
        r.y = Screen.height - r.yMax; // input is bottom-left origin, IMGUI is top-left
        var tint = new Color(0.4f, 0.7f, 1f); // blue, like the outlines
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(tint.r, tint.g, tint.b, 0.12f), 0f, 0f);
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(tint.r, tint.g, tint.b, 0.8f), 1f, 0f);
    }
}
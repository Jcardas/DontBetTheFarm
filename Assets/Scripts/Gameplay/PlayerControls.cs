using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// The player's mouse and keyboard. Left-click only selects: click a unit to pick just it, click a picked unit to drop it
// from the selection, double-click to pick every unit of its type on screen, or drag a box around several
// (Shift-click toggles one unit, Shift-drag adds). Right-click the ground, a resource or an alien to order the picked
// units there. Ctrl+1..3 saves the picked units as a group, 1..3 picks the group again (Shift+1..3 adds it).
// Escape goes back to just the farmer. Space fires the farmer's shotgun toward the cursor.
// The cursor shows what a right-click would do on whatever the mouse is over, or how far along a shotgun reload is.
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
    Selectable hovered, lastClicked;
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
        SelectDefault();
        shotgun = FindAnyObjectByType<Shotgun>();
    }

    void Update()
    {
        var player = input.Player;
        bool shift = player.Add.IsPressed();
        ReadKeys(shift);

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector2 at = player.Point.ReadValue<Vector2>();
        Ray ray = cam.ScreenPointToRay(at);
        if (player.Fire.WasPressedThisFrame() && shotgun && ground.Raycast(ray, out float along)) shotgun.Fire(ray.GetPoint(along));
        bool overSomething = Physics.Raycast(ray, out RaycastHit hit);
        var unit = overSomething ? hit.collider.GetComponentInParent<Selectable>() : null;
        Hover(unit);
        ShowCursor(shotgun && shotgun.Reloading ? ReloadCursor(shotgun.ReloadProgress)
            : overSomething ? CursorFor(hit, unit) : CursorType.Default);
        if (player.Command.WasPressedThisFrame() && overSomething) Command(hit);

        if (left == LeftButton.Idle && player.Select.WasPressedThisFrame())
        {
            left = LeftButton.Pressed;
            pressedAt = at;
        }
        if (left == LeftButton.Pressed && (at - pressedAt).magnitude > DragPixels) left = LeftButton.Dragging;
        if (left == LeftButton.Idle || !player.Select.WasReleasedThisFrame()) return;

        if (left == LeftButton.Dragging) Pick(Selectable.All.Where(s => OnScreen(cam, s, Box(pressedAt, at))), shift);
        else if (overSomething) Click(cam, hit, shift);
        left = LeftButton.Idle;
    }

    void ReadKeys(bool shift)
    {
        var player = input.Player;
        if (player.Cancel.WasPressedThisFrame()) SelectDefault();
        for (int g = 0; g < groups.Length; g++)
        {
            if (!groupKeys[g].WasPressedThisFrame()) continue;
            if (player.SaveGroup.IsPressed()) groups[g] = Selectable.All.Where(s => s.Selected).ToList();
            else Pick(groups[g], shift);
        }
    }

    static void SelectDefault()
    {
        foreach (var s in Selectable.All) s.Selected = s.selectedAtStart;
    }

    void Hover(Selectable unit)
    {
        if (unit == hovered) return;
        if (hovered) hovered.Hovered = false;
        hovered = unit;
        if (unit) unit.Hovered = true;
    }

    // Same precedence as Order.From, so the cursor always previews the order a right-click would give.
    static CursorType CursorFor(RaycastHit hit, Selectable unit) => Order.From(hit).kind switch
    {
        OrderKind.Attack => CursorType.Attack,
        OrderKind.Harvest => CursorType.Harvest,
        OrderKind.Work => CursorType.Chop,
        _ => unit && !unit.Down ? CursorType.Interact : CursorType.Default,
    };

    // Quarter steps. Reload100 is never shown: the frame the reload completes, the hover cursor comes back.
    static CursorType ReloadCursor(float progress) => CursorType.Reload0 + Mathf.Min(3, (int)(progress * 4f));

    // CameraController also sets cursors (Grab, edge arrows) and resets to Default when its own state ends,
    // so a non-Default hover cursor is re-asserted every frame; SetCursor ignores repeats. Grab wins while held.
    void ShowCursor(CursorType kind)
    {
        bool changed = kind != cursor;
        cursor = kind;
        if ((changed || kind != CursorType.Default) && !input.Camera.MiddleClick.IsPressed()) CursorManager.Instance?.SetCursor(kind);
    }

    void Click(Camera cam, RaycastHit hit, bool shift)
    {
        var unit = hit.collider.GetComponentInParent<Selectable>();
        if (unit != null && unit.Down) unit = null;
        bool twice = unit != null && unit == lastClicked && Time.unscaledTime - lastClickAt < DoubleClickSeconds;
        lastClicked = unit;
        lastClickAt = Time.unscaledTime;

        if (unit == null) return;
        if (twice) Pick(Selectable.All.Where(s => s.unitType == unit.unitType && OnScreen(cam, s, cam.pixelRect)), shift);
        else if (shift || unit.Selected) unit.Selected = !unit.Selected;
        else foreach (var s in Selectable.All) s.Selected = s == unit;
    }

    // Picks the standing `units` instead of the current selection (Shift adds them to it). Does nothing if none are standing.
    static void Pick(IEnumerable<Selectable> units, bool shift)
    {
        var standing = units.Where(s => !s.Down).ToList();
        if (standing.Count == 0) return;
        foreach (var s in Selectable.All) s.Selected = standing.Contains(s) || (shift && s.Selected);
    }

    static bool OnScreen(Camera cam, Selectable s, Rect area)
    {
        Vector3 p = cam.WorldToScreenPoint(s.transform.position);
        return p.z > 0f && area.Contains(p);
    }

    // Everyone gets the same target; spread points stop a group stacking on one spot when it walks.
    void Command(RaycastHit hit)
    {
        var order = Order.From(hit);
        if (marker) Instantiate(marker, order.point, Quaternion.identity).Show(order.kind);
        var group = Selectable.All.Where(s => s.Selected).ToList();
        for (int i = 0; i < group.Count; i++) group[i].Command(order.Offset(Spread(i)));
    }

    // Unit 0 stands on the point; the rest fill rings Spacing apart. Ring k holds 6k units,
    // which keeps neighbours on a ring about Spacing apart too.
    static Vector3 Spread(int i)
    {
        if (i == 0) return Vector3.zero;
        int ring = 1;
        while (i > 3 * ring * (ring + 1)) ring++; // rings 1..k hold 6 + 12 + ... + 6k = 3k(k+1) units
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
        r.y = Screen.height - r.yMax; // input is bottom-left origin, IMGUI is top-left
        var tint = new Color(1f, 0.85f, 0.1f);
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(tint.r, tint.g, tint.b, 0.15f), 0f, 0f);
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, tint, 1f, 0f);
    }
}

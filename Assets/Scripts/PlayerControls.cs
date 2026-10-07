using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// The player's mouse and keyboard. Left-click a unit to pick it, double-click to pick every unit of its type on screen,
// or drag a box around several (Shift adds). Left-click the ground, a resource or an alien, or right-click anywhere,
// to order the picked units there. Ctrl+1..3 saves the picked units as a group, 1..3 picks the group again
// (Shift+1..3 adds it). Escape goes back to just the farmer. Space swings the farmer's pitchfork.
public class PlayerControls : MonoBehaviour
{
    const float DragPixels = 8f;
    const float Spacing = 1.2f;
    const float DoubleClickSeconds = 0.35f;

    enum LeftButton { Idle, Pressed, Dragging }

    [Tooltip("Spawned where each order lands, so the player sees the click register.")]
    public OrderMarker marker;

    LeftButton left;
    Vector2 pressedAt;
    Pitchfork pitchfork;
    Selectable hovered, lastClicked;
    float lastClickAt;
    readonly List<Selectable>[] groups = { new(), new(), new() };

    void Start()
    {
        SelectDefault();
        pitchfork = FindAnyObjectByType<Pitchfork>();
    }

    void Update()
    {
        Keyboard keys = Keyboard.current;
        bool shift = keys != null && keys.shiftKey.isPressed;
        if (keys != null) ReadKeys(keys, shift);

        Mouse mouse = Mouse.current;
        Camera cam = Camera.main;
        if (mouse == null || cam == null) return;

        Vector2 at = mouse.position.ReadValue();
        bool overSomething = Physics.Raycast(cam.ScreenPointToRay(at), out RaycastHit hit);
        Hover(overSomething ? hit.collider.GetComponentInParent<Selectable>() : null);
        if (mouse.rightButton.wasPressedThisFrame && overSomething) Command(hit);

        if (left == LeftButton.Idle && mouse.leftButton.wasPressedThisFrame)
        {
            left = LeftButton.Pressed;
            pressedAt = at;
        }
        if (left == LeftButton.Pressed && (at - pressedAt).magnitude > DragPixels) left = LeftButton.Dragging;
        if (left == LeftButton.Idle || !mouse.leftButton.wasReleasedThisFrame) return;

        if (left == LeftButton.Dragging) Pick(Selectable.All.Where(s => OnScreen(cam, s, Box(pressedAt, at))), shift);
        else if (overSomething) Click(cam, hit, shift);
        left = LeftButton.Idle;
    }

    void ReadKeys(Keyboard keys, bool shift)
    {
        if (keys.escapeKey.wasPressedThisFrame) SelectDefault();
        if (keys.spaceKey.wasPressedThisFrame && pitchfork) pitchfork.Swing();
        for (int g = 0; g < groups.Length; g++)
        {
            if (!keys[Key.Digit1 + g].wasPressedThisFrame) continue;
            if (keys.ctrlKey.isPressed) groups[g] = Selectable.All.Where(s => s.Selected).ToList();
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

    void Click(Camera cam, RaycastHit hit, bool shift)
    {
        var unit = hit.collider.GetComponentInParent<Selectable>();
        if (unit != null && unit.Down) unit = null;
        bool twice = unit != null && unit == lastClicked && Time.unscaledTime - lastClickAt < DoubleClickSeconds;
        lastClicked = unit;
        lastClickAt = Time.unscaledTime;

        if (unit == null) Command(hit);
        else if (twice) Pick(Selectable.All.Where(s => s.unitType == unit.unitType && OnScreen(cam, s, cam.pixelRect)), shift);
        else if (shift) unit.Selected = !unit.Selected;
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
        if (left != LeftButton.Dragging || Mouse.current == null) return;
        Rect r = Box(pressedAt, Mouse.current.position.ReadValue());
        r.y = Screen.height - r.yMax; // input is bottom-left origin, IMGUI is top-left
        var tint = new Color(1f, 0.85f, 0.1f);
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(tint.r, tint.g, tint.b, 0.15f), 0f, 0f);
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, tint, 1f, 0f);
    }
}

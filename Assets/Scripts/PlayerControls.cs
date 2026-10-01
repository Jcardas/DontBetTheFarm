using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// The player's mouse and keyboard. Left-click a unit to pick it, or drag a box around several (Shift adds).
// Left-click the ground or a resource, or right-click anywhere, to order the picked units there.
// Escape goes back to just the farmer.
public class PlayerControls : MonoBehaviour
{
    const float DragPixels = 8f;
    const float Spacing = 1.2f;

    enum LeftButton { Idle, Pressed, Dragging }

    LeftButton left;
    Vector2 pressedAt;

    void Start() => SelectDefault();

    void Update()
    {
        Mouse mouse = Mouse.current;
        Camera cam = Camera.main;
        if (mouse == null || cam == null) return;
        Keyboard keys = Keyboard.current;
        bool shift = keys != null && keys.shiftKey.isPressed;
        if (keys != null && keys.escapeKey.wasPressedThisFrame) SelectDefault();

        Vector2 at = mouse.position.ReadValue();
        if (mouse.rightButton.wasPressedThisFrame && Physics.Raycast(cam.ScreenPointToRay(at), out RaycastHit rightHit)) Order(rightHit);

        if (left == LeftButton.Idle && mouse.leftButton.wasPressedThisFrame)
        {
            left = LeftButton.Pressed;
            pressedAt = at;
        }
        if (left == LeftButton.Pressed && (at - pressedAt).magnitude > DragPixels) left = LeftButton.Dragging;
        if (left == LeftButton.Idle || !mouse.leftButton.wasReleasedThisFrame) return;

        if (left == LeftButton.Dragging) BoxSelect(cam, Box(pressedAt, at), shift);
        else Click(cam, at, shift);
        left = LeftButton.Idle;
    }

    static void SelectDefault()
    {
        foreach (var s in Selectable.All) s.Selected = s.selectedAtStart;
    }

    static void Click(Camera cam, Vector2 at, bool shift)
    {
        if (!Physics.Raycast(cam.ScreenPointToRay(at), out RaycastHit hit)) return;
        var unit = hit.collider.GetComponentInParent<Selectable>();
        if (unit == null || unit.Down)
        {
            Order(hit);
            return;
        }
        if (shift) unit.Selected = !unit.Selected;
        else foreach (var s in Selectable.All) s.Selected = s == unit;
    }

    static void BoxSelect(Camera cam, Rect box, bool shift)
    {
        var inside = Selectable.All.Where(s =>
        {
            Vector3 p = cam.WorldToScreenPoint(s.transform.position);
            return !s.Down && p.z > 0f && box.Contains(p);
        }).ToList();
        if (inside.Count == 0) return;
        foreach (var s in Selectable.All) s.Selected = inside.Contains(s) || (shift && s.Selected);
    }

    // Everyone heads to the same resource; spread targets stop a group stacking on one spot.
    static void Order(RaycastHit hit)
    {
        var node = hit.collider.GetComponentInParent<ResourceNode>();
        var group = Selectable.All.Where(s => s.Selected).ToList();
        for (int i = 0; i < group.Count; i++) group[i].Command(hit.point + Spread(i), node);
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

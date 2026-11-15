using System.Collections.Generic;
using UnityEngine;

// Draws the building's active physics colliders alongside the renderer bounds used by MouseTarget and Order.BrokenAt,
// so you can see in both Scene and Game view (with Gizmos enabled) which volume the mouse is actually hitting.
public class HitboxDebugVisualizer : MonoBehaviour
{
    [Tooltip("Draws green wireframes for every active Collider on this object and its children.")]
    public bool showColliders = true;

    [Tooltip("Draws a yellow wireframe for the combined MeshRenderer bounds used by MouseTarget.Building.")]
    public bool showCombinedRendererBounds = true;

    [Tooltip("Draws cyan wireframes for each individual active Renderer.bounds checked by Order.BrokenAt.")]
    public bool showIndividualRendererBounds = false;

    [Tooltip("Logs the exact child object name and distance hit by MouseTarget.Find under the cursor when middle-clicking or pressing H.")]
    public bool logHitUnderMouseOnKeyH = true;

    readonly List<MeshRenderer> meshParts = new();

    void Update()
    {
        if (!logHitUnderMouseOnKeyH || Camera.main == null) return;
        if (!UnityEngine.InputSystem.Keyboard.current.hKey.wasPressedThisFrame) return;

        Vector2 at = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(at);

        if (MouseTarget.Find(ray, out RaycastHit hit))
        {
            Debug.Log($"[HitboxDebug] Physics hit '{hit.collider.name}' (root: '{hit.collider.transform.root.name}', " +
                      $"layer: '{LayerMask.LayerToName(hit.collider.gameObject.layer)}', isTrigger: {hit.collider.isTrigger}) at {hit.point}", hit.collider);
        }
        else
        {
            Debug.Log("[HitboxDebug] Physics raycast missed everything.");
        }

        Buildable broken = Order.BrokenAt(ray);
        if (broken != null)
            Debug.Log($"[HitboxDebug] Order.BrokenAt matched '{broken.name}' via renderer bounds.", broken);
    }

    void OnDrawGizmos()
    {
        // 1. Active Physics Colliders (what MouseTarget.Find / Physics.Raycast hits)
        if (showColliders)
        {
            Gizmos.color = Color.green;
            foreach (Collider col in GetComponentsInChildren<Collider>())
            {
                if (!col.enabled || !col.gameObject.activeInHierarchy) continue;

                if (col is BoxCollider box)
                {
                    Matrix4x4 prev = Gizmos.matrix;
                    Gizmos.matrix = box.transform.localToWorldMatrix;
                    Gizmos.DrawWireCube(box.center, box.size);
                    Gizmos.matrix = prev;
                }
                else
                {
                    // Fallback AABB for MeshColliders, SphereColliders, etc.
                    Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
                }
            }
        }

        // 2. Combined MeshRenderer Bounds (what MouseTarget.Building uses to catch fence gaps / buildings)
        if (showCombinedRendererBounds)
        {
            GetComponentsInChildren(meshParts);
            if (meshParts.Count > 0)
            {
                Bounds combined = meshParts[0].bounds;
                for (int i = 1; i < meshParts.Count; i++)
                    combined.Encapsulate(meshParts[i].bounds);

                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(combined.center, combined.size);
            }
        }

        // 3. Individual Renderer Bounds (what Order.BrokenAt checks per renderer)
        if (showIndividualRendererBounds)
        {
            Gizmos.color = Color.cyan;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                Gizmos.DrawWireCube(r.bounds.center, r.bounds.size);
            }
        }
    }
}
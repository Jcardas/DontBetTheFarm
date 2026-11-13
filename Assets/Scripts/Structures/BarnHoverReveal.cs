 using UnityEngine;
using UnityEngine.InputSystem;

public class BarnHoverReveal : MonoBehaviour
{
    [SerializeField] private Transform farmer;
    [SerializeField] private GameObject intactVisual;
    [SerializeField] private Material transparentMaterial;
    [SerializeField] private Camera gameCamera;

    private Renderer[] parts;
    private Material[][] originalMaterials;
    private Collider[] colliders;
    private int[] originalLayers;
    private bool faded;

    private void Awake()
    {
        if (!gameCamera) gameCamera = Camera.main;
        if (!intactVisual || !transparentMaterial) { enabled = false; return; }

        parts = intactVisual.GetComponentsInChildren<Renderer>(true);
        originalMaterials = new Material[parts.Length][];
        for (int i = 0; i < parts.Length; i++)
            originalMaterials[i] = parts[i].sharedMaterials;

        colliders = GetComponentsInChildren<Collider>(true);
        originalLayers = new int[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
            originalLayers[i] = colliders[i].gameObject.layer;
    }

    private void LateUpdate()
    {
        if (!farmer || !gameCamera || Mouse.current == null || !intactVisual.activeInHierarchy)
        {
            Fade(false);
            return;
        }

        Vector3 camPos = gameCamera.transform.position;
        Vector3 toFarmer = farmer.position + Vector3.up - camPos;
        float maxDist = toFarmer.magnitude - 0.2f;
        Ray sight = new Ray(camPos, toFarmer.normalized);
        Ray mouse = gameCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        bool blocking = false, hovered = false;
        foreach (Renderer r in parts)
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;

            blocking |= r.bounds.IntersectRay(sight, out float d) && d > 0 && d < maxDist;
            hovered |= r.bounds.IntersectRay(mouse);

            if (blocking && hovered) break;
        }

        Fade(blocking && hovered);
    }

    private void Fade(bool fade)
    {
        if (faded == fade || parts == null) return;
        faded = fade;

        for (int i = 0; i < parts.Length; i++)
        {
            if (!parts[i]) continue;

            if (fade)
            {
                var swapped = new Material[originalMaterials[i].Length];
                for (int j = 0; j < swapped.Length; j++) swapped[j] = transparentMaterial;
                parts[i].sharedMaterials = swapped;
            }
            else
            {
                parts[i].sharedMaterials = originalMaterials[i];
            }
        }

        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i])
                colliders[i].gameObject.layer = fade ? Physics.IgnoreRaycastLayer : originalLayers[i];
    }

    private void OnDisable() => Fade(false);
    private void OnDestroy() => Fade(false);
}
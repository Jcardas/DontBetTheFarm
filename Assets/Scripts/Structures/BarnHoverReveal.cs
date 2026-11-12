using UnityEngine;
using UnityEngine.InputSystem;

public class BarnHoverReveal : MonoBehaviour
{
    [SerializeField] private GameObject intactVisual;
    [SerializeField] private Material transparentMaterial;
    [SerializeField] private Camera gameCamera;

    [SerializeField, Min(0.1f)]
    private float creatureRefreshInterval = 0.5f;

    private Renderer[] parts;
    private Material[][] originalMaterials;
    private Collider[] colliders;
    private int[] originalLayers;

    private Creature[] creatures;
    private float nextCreatureRefresh;
    private bool faded;

    private void Awake()
    {
        if (!gameCamera) gameCamera = Camera.main;
        if (!intactVisual || !transparentMaterial)
        {
            enabled = false;
            return;
        }

        parts = intactVisual.GetComponentsInChildren<Renderer>(true);
        originalMaterials = new Material[parts.Length][];

        for (int i = 0; i < parts.Length; i++)
            originalMaterials[i] = parts[i].sharedMaterials;

        colliders = GetComponentsInChildren<Collider>(true);
        originalLayers = new int[colliders.Length];

        for (int i = 0; i < colliders.Length; i++)
            originalLayers[i] = colliders[i].gameObject.layer;

        RefreshCreatures();
    }

    private void RefreshCreatures()
    {
        creatures = FindObjectsByType<Creature>(
            FindObjectsSortMode.None
        );

        nextCreatureRefresh =
            Time.unscaledTime + creatureRefreshInterval;
    }

    private void LateUpdate()
    {
        if (!gameCamera || Mouse.current == null ||
            !intactVisual.activeInHierarchy)
        {
            Fade(false);
            return;
        }

        if (Time.unscaledTime >= nextCreatureRefresh)
            RefreshCreatures();

        Vector3 camPos = gameCamera.transform.position;

        Ray mouse = gameCamera.ScreenPointToRay(
            Mouse.current.position.ReadValue()
        );

        bool hovered = false;

        foreach (Renderer r in parts)
        {
            if (r && r.enabled && r.gameObject.activeInHierarchy &&
                r.bounds.IntersectRay(mouse))
            {
                hovered = true;
                break;
            }
        }

        if (!hovered)
        {
            Fade(false);
            return;
        }

        foreach (Creature creature in creatures)
        {
            if (!creature || !creature.gameObject.activeInHierarchy)
                continue;

            Vector3 toCreature =
                creature.transform.position + Vector3.up - camPos;

            float maxDist = toCreature.magnitude - 0.2f;
            if (maxDist <= 0f) continue;

            Ray sight = new Ray(camPos, toCreature.normalized);

            foreach (Renderer r in parts)
            {
                if (!r || !r.enabled || !r.gameObject.activeInHierarchy)
                    continue;

                if (r.bounds.IntersectRay(sight, out float d) &&
                    d > 0f && d < maxDist)
                {
                    Fade(true);
                    return;
                }
            }
        }

        Fade(false);
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
                var swapped =
                    new Material[originalMaterials[i].Length];

                for (int j = 0; j < swapped.Length; j++)
                    swapped[j] = transparentMaterial;

                parts[i].sharedMaterials = swapped;
            }
            else
            {
                parts[i].sharedMaterials = originalMaterials[i];
            }
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i])
                colliders[i].gameObject.layer = fade
                    ? Physics.IgnoreRaycastLayer
                    : originalLayers[i];
        }
    }

    private void OnDisable() => Fade(false);
    private void OnDestroy() => Fade(false);
}

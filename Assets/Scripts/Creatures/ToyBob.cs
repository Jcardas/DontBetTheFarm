using UnityEngine;

// Makes a unit hop along like a toy being walked across a table, rocking side to side each hop,
// and tip onto its side when knocked out. Works for anything that moves, NavMeshAgent or not.
// Put it on the child that holds the model, not on the object that moves: it owns this transform's
// local position and rotation.
public class ToyBob : MonoBehaviour
{
    public float hopHeight = 0.25f;
    [Tooltip("Hops per metre travelled, so faster units hop faster and a stopped unit stops hopping.")]
    public float hopsPerMetre = 1.2f;
    public float rockDegrees = 10f;
    [Tooltip("How far to raise the model when it lies on its side, about half its width.")]
    public float lyingHeight = 0.3f;

    const float BumpSeconds = 0.2f;

    Vector3 restPosition;
    Vector3 lastPosition;
    Quaternion restRotation;
    Creature creature;
    Transform trackingParent;
    float phase;
    float bumpStart = float.NegativeInfinity;

    void Awake()
    {
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;

        // Fall back to self if there is no parent, although child is recommended
        trackingParent = transform.parent != null ? transform.parent : transform;
        lastPosition = trackingParent.position;

        creature = GetComponentInParent<Creature>();

        if (transform.parent == null)
        {
            Debug.LogWarning($"[ToyBob] On root GameObject '{gameObject.name}'. Re-parent this to a child holding visual mesh to ensure movement doesn't overwrite local adjustments!", this);
        }
    }

    // One hop in place, for an attack or a chop.
    public void Bump() => bumpStart = Time.time;

    void LateUpdate()
    {
        if (creature != null && creature.Down)
        {
            // Tips the visual onto its side (local X/Z) & elevates position so it doesn't clip below ground
            transform.localPosition = restPosition + Vector3.up * lyingHeight;
            
            // Apply a 90-degree tip onto its side along the local X-axis, keeping the Z and Y locally aligned
            Quaternion tippedRotation = restRotation * Quaternion.Euler(90f, 0f, 0f);
            
            transform.localRotation = Quaternion.RotateTowards(
                transform.localRotation, 
                tippedRotation, 
                360f * Time.deltaTime
            );
            return;
        }

        Vector3 moved = trackingParent.position - lastPosition;
        moved.y = 0f;
        lastPosition = trackingParent.position;

        // Calculate phase change based on speed
        if (moved.sqrMagnitude > 1e-8f)
        {
            phase += moved.magnitude * hopsPerMetre * Mathf.PI;
        }
        else
        {
            // If stopped, smoothly bring visual to ground instead of stopping mid-hop
            phase = Mathf.MoveTowards(phase, Mathf.Ceil(phase / Mathf.PI) * Mathf.PI, 4f * Mathf.PI * Time.deltaTime);
        }

        phase = Mathf.Repeat(phase, 2f * Mathf.PI);

        // calculate lift/bounce values
        float wave = Mathf.Sin(phase);
        float lift = Mathf.Abs(wave) * hopHeight;
        float sinceBump = Time.time - bumpStart;

        if (sinceBump < BumpSeconds)
        {
            lift = Mathf.Max(lift, Mathf.Sin((sinceBump / BumpSeconds) * Mathf.PI) * hopHeight);
        }

        transform.localPosition = restPosition + Vector3.up * lift;

        // apply local side-to-side rocking by modifying only the local Z-axis (roll/tilt)
        // Multiplying on the right-hand side of restRotation applies Z-rotation in local coordinates
        transform.localRotation = restRotation * Quaternion.Euler(0f, 0f, wave * rockDegrees);
    }
}
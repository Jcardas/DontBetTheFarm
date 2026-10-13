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

    Vector3 restPosition, lastPosition;
    Quaternion restRotation;
    Health health;
    float phase;
    float bumpStart = float.NegativeInfinity;

    void Awake()
    {
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        lastPosition = transform.parent.position;
        health = GetComponentInParent<Health>();
    }

    // One hop in place, for an attack or a chop.
    public void Bump() => bumpStart = Time.time;

    void LateUpdate()
    {
        if (health && health.Down)
        {
            transform.localPosition = restPosition + Vector3.up * lyingHeight;
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, restRotation * Quaternion.Euler(0f, 0f, 90f), 360f * Time.deltaTime);
            return;
        }

        Vector3 moved = transform.parent.position - lastPosition;
        moved.y = 0f;
        lastPosition = transform.parent.position;

        // One hop is half a sine wave: |sin| touches down between hops, and the sign of sin flips the rock left and right.
        if (moved.sqrMagnitude > 1e-8f) phase += moved.magnitude * hopsPerMetre * Mathf.PI;
        else phase = Mathf.MoveTowards(phase, Mathf.Ceil(phase / Mathf.PI) * Mathf.PI, 4f * Mathf.PI * Time.deltaTime); // land the current hop instead of freezing mid-air
        phase = Mathf.Repeat(phase, 2f * Mathf.PI); // big phases don't round-trip through Ceil(phase / PI) * PI, and a landed toy would hop again

        float wave = Mathf.Sin(phase);
        float lift = Mathf.Abs(wave) * hopHeight;
        float sinceBump = Time.time - bumpStart;
        if (sinceBump < BumpSeconds) lift = Mathf.Max(lift, Mathf.Sin(sinceBump / BumpSeconds * Mathf.PI) * hopHeight);

        transform.localPosition = restPosition + Vector3.up * lift;
        transform.localRotation = restRotation * Quaternion.Euler(0f, 0f, wave * rockDegrees);
    }
}

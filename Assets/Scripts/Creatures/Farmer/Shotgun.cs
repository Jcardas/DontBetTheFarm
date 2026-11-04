using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The farmer's only weapon: a double-barrel that sprays pellets toward a point, in a level fan plus a raised row that can
// reach fliers. He fires it on his own at aliens inside his attack range (Farmer.PerformAttack), and Space (PlayerControls)
// fires it toward the cursor out to the full range. Two shells, then a reload, shared by both.
// It works whether or not he is selected, but a knocked-out farmer can't fire.
[RequireComponent(typeof(Creature), typeof(AudioSource))]
public class Shotgun : MonoBehaviour
{
    const int MaxShells = 2;
    const float ChestHeight = 0.6f;

    enum State { Ready, Reloading }

    [Tooltip("Pellets in the level fan, spread evenly across the cone.")]
    public int pellets = 7;
    [Tooltip("Width of the pellet fan, in degrees.")]
    public float coneDegrees = 30f;
    [Tooltip("Extra pellets angled upward, spread across the same cone, so a shot can hit flying enemies.")]
    public int raisedPellets = 3;
    [Tooltip("How far the raised pellets angle up, in degrees.")]
    public float raisedDegrees = 15f;
    [Tooltip("How far a pellet flies, from the muzzle.")]
    public float range = 12f;
    [Tooltip("Damage each pellet does to the first enemy it hits.")]
    public int pelletDamage = 5;
    [Tooltip("Metres each pellet pushes the enemy it hits, along the pellet's flight.")]
    public float knockback = 1.5f;
    [Tooltip("Seconds from one shot until the next is allowed, while shells are left.")]
    public float shotCooldown = 0.3f;
    [Tooltip("Seconds to reload both shells once they are spent.")]
    public float reloadSeconds = 2f;
    [Tooltip("Barrel tip the pellets fly from. Chest height in front of him if empty.")]
    public Transform muzzle;
    [Tooltip("How long each pellet's streak stays on screen.")]
    public float tracerSeconds = 0.06f;
    [Tooltip("Material for the pellet streaks.")]
    public Material tracer;
    [Tooltip("Played on every shot that fires.")]
    public AudioClip shot;

    Creature self;
    ToyBob toy;
    AudioSource audioSource;
    State state;
    int shells = MaxShells;
    float nextShot, reloadedAt;
    readonly HashSet<Creature> pushed = new();

    public int Shells => shells;
    public bool Reloading => state == State.Reloading;
    public float ReloadProgress => Reloading ? Mathf.Clamp01(1f - (reloadedAt - Time.time) / reloadSeconds) : 1f;

    void Awake()
    {
        self = GetComponent<Creature>();
        toy = GetComponentInChildren<ToyBob>();
        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (state != State.Reloading || Time.time < reloadedAt) return;
        shells = MaxShells;
        state = State.Ready;
    }

    public void Fire(Vector3 aimPoint)
    {
        if (self.Down || state == State.Reloading || Time.time < nextShot) return;

        Vector3 aim = aimPoint - transform.position;
        aim.y = 0f;
        if (aim.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(aim);
        Vector3 from = muzzle ? muzzle.position : transform.position + Vector3.up * ChestHeight;
        // Aim from the barrel, not his middle, so the fan centres on the aim point instead of beside it.
        Vector3 toAim = aimPoint - from;
        toAim.y = 0f;
        Vector3 forward = toAim.sqrMagnitude > 0.25f && Vector3.Dot(toAim, transform.forward) > 0f ? toAim.normalized : transform.forward;
        pushed.Clear();
        Spray(from, forward, pellets, 0f);
        Spray(from, forward, raisedPellets, raisedDegrees);

        if (toy) toy.Bump();
        if (shot) audioSource.PlayOneShot(shot);
        nextShot = Time.time + shotCooldown;
        if (--shells > 0) return;
        state = State.Reloading;
        reloadedAt = Time.time + reloadSeconds;
    }

    // One row of `count` pellets across the cone around `forward`, tilted `upDegrees` above level.
    void Spray(Vector3 from, Vector3 forward, int count, float upDegrees)
    {
        Vector3 tilted = Quaternion.AngleAxis(-upDegrees, Vector3.Cross(Vector3.up, forward)) * forward;
        for (int i = 0; i < count; i++)
        {
            float angle = count == 1 ? 0f : Mathf.Lerp(-coneDegrees / 2f, coneDegrees / 2f, i / (count - 1f));
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * tilted;
            Streak(from, Pellet(from, direction));
        }
    }

    // Flies through the farmer, his own animals and anyone already down; the first standing enemy takes the hit,
    // and anything else solid (tree, barn, any building) stops the pellet. Returns where it ended.
    // Damage stacks per pellet, but an enemy is pushed only once per shot, or point-blank it would fly pellets x knockback.
    Vector3 Pellet(Vector3 from, Vector3 direction)
    {
        var hits = Physics.RaycastAll(from, direction, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            var target = hit.collider.GetComponentInParent<Creature>();
            if (target == null) return hit.point;
            if (target.Team == self.Team || target.Down)
            {
                if (hit.collider.GetComponentInParent<NavMeshAgent>()) continue; // a unit, not a building
                return hit.point;
            }
            target.TakeDamage(pelletDamage);
            if (pushed.Add(target) && target.TryGetComponent(out NavMeshAgent agent) && agent.enabled && agent.isOnNavMesh) agent.Move(direction * knockback);
            return hit.point;
        }
        return from + direction * range;
    }

    void Streak(Vector3 from, Vector3 to)
    {
        var line = new GameObject("Pellet").AddComponent<LineRenderer>();
        line.SetPositions(new[] { from, to });
        line.widthMultiplier = 0.04f;
        line.sharedMaterial = tracer;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(line.gameObject, tracerSeconds);
    }
}
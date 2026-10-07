using UnityEngine;

// The farmer's own attack, on Space (PlayerControls): one sweep that hits every standing enemy in an arc in front of him.
// It works whether or not he is selected, but a knocked-out farmer can't swing.
[RequireComponent(typeof(Health))]
public class Pitchfork : MonoBehaviour
{
    [Tooltip("How far a swing reaches, from his centre to an enemy's nearest edge.")]
    public float reach = 1.8f;
    [Tooltip("Width of the swing in front of him, in degrees. Enemies outside it are not hit.")]
    public float arcDegrees = 140f;
    public int damage = 15;
    [Tooltip("Seconds from one swing until the next one is allowed.")]
    public float cooldown = 1.2f;
    [Tooltip("Pivot of the fork model at his hand. It sweeps across the arc during a swing and rests where it was placed otherwise.")]
    public Transform fork;
    public float swingSeconds = 0.25f;

    Health self;
    ToyBob toy;
    Quaternion rest;
    float swungAt = float.NegativeInfinity;

    void Awake()
    {
        self = GetComponent<Health>();
        toy = GetComponentInChildren<ToyBob>();
        if (fork) rest = fork.localRotation;
    }

    public void Swing()
    {
        if (self.Down || Time.time < swungAt + cooldown) return;
        swungAt = Time.time;
        if (toy) toy.Bump();
        foreach (var enemy in Health.All)
        {
            if (enemy.team == self.team || enemy.Down || Reach.Gap(transform.position, enemy.Body) > reach) continue;
            Vector3 to = enemy.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f || Vector3.Angle(transform.forward, to) <= arcDegrees / 2f) enemy.TakeDamage(damage);
        }
    }

    void Update()
    {
        if (!fork) return;
        float t = (Time.time - swungAt) / swingSeconds;
        fork.localRotation = t < 1f ? Quaternion.Euler(0f, Mathf.Lerp(arcDegrees / 2f, -arcDegrees / 2f, t), 0f) : rest;
    }
}

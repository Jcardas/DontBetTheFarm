using UnityEngine;

// A shot that flies to one target and hurts it on arrival, like the llama's spit. It homes in, so a moving target
// can't dodge it, and it fizzles if the target is knocked out before it lands.
public class Projectile : MonoBehaviour
{
    [Tooltip("Metres per second.")]
    [SerializeField] private float speed = 14f;
    [Tooltip("Seconds before it gives up on a target it can't reach.")]
    [SerializeField] private float lifetime = 3f;

    private IDamageable target;
    private int damage;
    private float diesAt;

    public void Launch(IDamageable target, int damage)
    {
        this.target = target;
        this.damage = damage;
        diesAt = Time.time + lifetime;
    }

    private void Update()
    {
        // Body is a Collider, so == null is Unity's check: it also catches a target destroyed mid-flight.
        if (target == null || target.Body == null || target.Down || Time.time >= diesAt)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 aim = target.Body.bounds.center;
        transform.position = Vector3.MoveTowards(transform.position, aim, speed * Time.deltaTime);
        if ((transform.position - aim).sqrMagnitude > 0.01f) return;

        target.TakeDamage(damage);
        Destroy(gameObject);
    }
}

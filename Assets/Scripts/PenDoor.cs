

using UnityEngine;
using UnityEngine.AI;

public class PenDoor : MonoBehaviour
{
    [Header("Door Settings")]
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float speed = 240f;
    [SerializeField] private float closeDelay = 2f;

    [Header("Detection")]
    [SerializeField] private Vector3 detectionCenter = Vector3.zero;
    [SerializeField] private Vector3 detectionSize = new Vector3(4f, 3f, 8f);

    private Quaternion closedRotation;
    private Quaternion openRotation;

    private bool isOpen;
    private float closeTimer;

    private void Start()
    {
        closedRotation = transform.localRotation;
        openRotation = closedRotation *
            Quaternion.Euler(0f, openAngle, 0f);
    }

    private void Update()
    {
        bool animalApproaching = false;

        Vector3 center =
    transform.position +
    transform.parent.TransformVector(detectionCenter);

        Collider[] nearby = Physics.OverlapBox(
            center,
            detectionSize * 0.5f,
            transform.parent.rotation
        );

        foreach (Collider col in nearby)
        {
            NavMeshAgent agent =
                col.GetComponentInParent<NavMeshAgent>();
            

            if (agent == null || !agent.isOnNavMesh)
                continue;

            if (agent.GetComponent<PenAnimal>() == null) continue;
            bool moving =
                agent.pathPending ||
                (agent.hasPath &&
                 !agent.isStopped &&
                 agent.remainingDistance >
                 agent.stoppingDistance + 0.1f);

            if (moving)
            {
                animalApproaching = true;
                break;
            }
        }

        if (animalApproaching)
        {
            isOpen = true;
            closeTimer = closeDelay;
        }
        else if (isOpen)
        {
            closeTimer -= Time.deltaTime;

            if (closeTimer <= 0f)
                isOpen = false;
        }

        Quaternion targetRotation =
            isOpen ? openRotation : closedRotation;

        transform.localRotation = Quaternion.RotateTowards(
            transform.localRotation,
            targetRotation,
            speed * Time.deltaTime
        );
    }

   
private void OnDrawGizmosSelected()
{
    if (transform.parent == null) return;

    Gizmos.color = Color.yellow;

    Vector3 center =
        transform.position +
        transform.parent.TransformVector(detectionCenter);

    Matrix4x4 oldMatrix = Gizmos.matrix;

    Gizmos.matrix = Matrix4x4.TRS(
        center,
        transform.parent.rotation,
        Vector3.one
    );

    Gizmos.DrawWireCube(Vector3.zero, detectionSize);

    Gizmos.matrix = oldMatrix;
}

}

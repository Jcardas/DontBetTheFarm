
using UnityEngine;
using UnityEngine.InputSystem;

public class PenDoor : MonoBehaviour
{
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float speed = 120f;

    private bool isOpen = false;

    private Quaternion closedRotation;
    private Quaternion openRotation;

    void Start()
    {
        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0, openAngle, 0);
    }

    void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.dKey.wasPressedThisFrame)
        {
            isOpen = !isOpen;
        }

        Quaternion targetRotation = isOpen ? openRotation : closedRotation;

        transform.localRotation = Quaternion.RotateTowards(
            transform.localRotation,
            targetRotation,
            speed * Time.deltaTime
        );
    }
}

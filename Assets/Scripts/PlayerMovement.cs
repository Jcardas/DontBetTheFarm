using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        Vector3 movement = Vector3.zero;

        if (Keyboard.current.wKey.isPressed)
        {
            movement += Vector3.forward;
        }

        if (Keyboard.current.sKey.isPressed)
        {
            movement += Vector3.back;
        }

        if (Keyboard.current.aKey.isPressed)
        {
            movement += Vector3.left;
        }

        if (Keyboard.current.dKey.isPressed)
        {
            movement += Vector3.right;
        }

        transform.position +=
            movement.normalized *
            moveSpeed *
            Time.deltaTime;
    }
}
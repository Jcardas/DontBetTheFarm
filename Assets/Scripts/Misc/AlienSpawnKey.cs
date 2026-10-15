using UnityEngine;
using UnityEngine.InputSystem;

// Sandbox testing aid: press K to drop an alien on the ground under the cursor.
public class AlienSpawnKey : MonoBehaviour
{
    public GameObject alien;

    readonly Plane ground = new(Vector3.up, Vector3.zero);

    void Update()
    {
        if (!alien || Keyboard.current == null || !Keyboard.current.kKey.wasPressedThisFrame || !Camera.main) return;
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (ground.Raycast(ray, out float along)) Instantiate(alien, ray.GetPoint(along), Quaternion.identity);
    }
}

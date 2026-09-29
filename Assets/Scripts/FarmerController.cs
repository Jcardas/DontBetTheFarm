using UnityEngine;
using UnityEngine.InputSystem;

// Left-click the ground to walk there, or click a resource to start harvesting it.
[RequireComponent(typeof(Gatherer))]
public class FarmerController : MonoBehaviour
{
    Gatherer gatherer;

    void Awake() => gatherer = GetComponent<Gatherer>();

    void Update()
    {
        Mouse mouse = Mouse.current;
        Camera cam = Camera.main;
        if (mouse == null || cam == null || !mouse.leftButton.wasPressedThisFrame) return;
        if (!Physics.Raycast(cam.ScreenPointToRay(mouse.position.ReadValue()), out RaycastHit hit)) return;

        ResourceNode node = hit.collider.GetComponentInParent<ResourceNode>();
        if (node) gatherer.Work(node);
        else gatherer.MoveTo(hit.point);
    }
}

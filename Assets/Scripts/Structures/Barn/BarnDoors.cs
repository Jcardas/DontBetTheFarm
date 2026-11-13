using UnityEngine;

// Slides the barn's double doors open and closed in local space. Attached to the active barn visual so swapping
// between intact and broken barn prefabs cleanly enables or disables door control.
public class BarnDoors : MonoBehaviour
{
    [SerializeField] Transform leftDoor;
    [SerializeField] Transform rightDoor;
    [Tooltip("Local offset each door slides when fully open.")]
    [SerializeField] Vector3 leftOpenOffset = new(-1.5f, 0f, 0f);
    [SerializeField] Vector3 rightOpenOffset = new(1.5f, 0f, 0f);
    [SerializeField] float slideSpeed = 2f;

    Vector3 leftClosedPos, rightClosedPos;
    float openAmount;
    bool targetOpen;

    public bool IsFullyOpen => Mathf.Approximately(openAmount, 1f);

    void Awake()
    {
        if (leftDoor) leftClosedPos = leftDoor.localPosition;
        if (rightDoor) rightClosedPos = rightDoor.localPosition;
    }

    public void SetOpen(bool open) => targetOpen = open;

    void Update()
    {
        openAmount = Mathf.MoveTowards(openAmount, targetOpen ? 1f : 0f, slideSpeed * Time.deltaTime);
        if (leftDoor) leftDoor.localPosition = Vector3.Lerp(leftClosedPos, leftClosedPos + leftOpenOffset, openAmount);
        if (rightDoor) rightDoor.localPosition = Vector3.Lerp(rightClosedPos, rightClosedPos + rightOpenOffset, openAmount);
    }
}
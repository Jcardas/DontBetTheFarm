using UnityEngine;
using UnityEngine.UI;

// A small ring over a unit's head that fills while it is busy (chopping, harvesting, reloading).
// Each activity calls Set every frame it runs; the highest priority wins that frame, and a frame with no call hides
// the ring, so an activity that is cut short (new order, a fight, knocked out) can't leave it showing.
public class ProgressRing : MonoBehaviour
{
    public const int Work = 0, Reload = 1;
    const int None = int.MinValue;

    [Tooltip("The filled ring. Its parent is the world-space canvas that is shown or hidden.")]
    public Image fill;

    float fraction;
    Color tint;
    int priority = None;

    public void Set(float fraction, Color tint, int priority)
    {
        if (priority < this.priority) return;
        this.fraction = fraction;
        this.tint = tint;
        this.priority = priority;
    }

    void LateUpdate()
    {
        var view = fill.transform.parent;
        bool busy = priority != None;
        view.gameObject.SetActive(busy);
        priority = None;
        if (!busy) return;
        fill.fillAmount = Mathf.Clamp01(fraction);
        fill.color = tint;
        if (Camera.main) view.rotation = Camera.main.transform.rotation; // faces the screen at any camera angle
    }
}

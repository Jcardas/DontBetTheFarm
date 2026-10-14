using UnityEngine;
using UnityEngine.UI;

// A small ring over a unit's head that fills while it works (chopping, harvesting).
// The work calls Set every frame it runs, and a frame with no call hides the ring, so work that is cut short
// (new order, a fight, knocked out) can't leave it showing. The farmer's reload shows on the cursor instead (PlayerControls).
public class ProgressRing : MonoBehaviour
{
    [Tooltip("The filled ring. Its parent is the world-space canvas that is shown or hidden.")]
    public Image fill;

    float fraction;
    Color tint;
    bool busy;

    public void Set(float fraction, Color tint)
    {
        this.fraction = fraction;
        this.tint = tint;
        busy = true;
    }

    void LateUpdate()
    {
        var view = fill.transform.parent;
        view.gameObject.SetActive(busy);
        if (!busy) return;
        busy = false;
        fill.fillAmount = Mathf.Clamp01(fraction);
        fill.color = tint;
        if (Camera.main) view.rotation = Camera.main.transform.rotation; // faces the screen at any camera angle
    }
}

// SprayButton.cs
// Put this on the full-screen touch area.
// Finger DOWN anywhere = start spraying. Finger UP = stop.

using UnityEngine;
using UnityEngine.EventSystems;   // for finger / pointer events on UI

public class SprayButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Tooltip("Drag the SprayPainter object here")]
    public SprayPainter painter;

    // Finger touches the screen → start spraying
    public void OnPointerDown(PointerEventData eventData)
    {
        if (painter != null) painter.isSpraying = true;
    }

    // Finger leaves the screen → stop spraying
    public void OnPointerUp(PointerEventData eventData)
    {
        if (painter != null) painter.isSpraying = false;
    }

    // Safety: if this gets hidden or disabled, stop spraying
    void OnDisable()
    {
        if (painter != null) painter.isSpraying = false;
    }
}
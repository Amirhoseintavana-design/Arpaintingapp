// ToolButton.cs
// Put this on each colour button and on the eraser button.
// Tap = select that colour (or the eraser). Your own button graphics go on the Image.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ToolButton : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Drag the SprayPainter object here")]
    public SprayPainter painter;

    [Tooltip("Tick for the eraser button")]
    public bool isEraser;

    [Tooltip("The paint colour of this button (ignored for the eraser)")]
    public Color color = Color.red;

    [Tooltip("Optional: an image shown only while this tool is selected (e.g. a ring)")]
    public GameObject selectedMark;

    [Tooltip("Tick on ONE button: the tool selected when the app starts")]
    public bool selectedAtStart;

    // All tool buttons in the scene (shared by all of them, because of 'static')
    static readonly List<ToolButton> allButtons = new List<ToolButton>();

    void OnEnable()  { allButtons.Add(this); }
    void OnDisable() { allButtons.Remove(this); }

    void Start()
    {
        if (selectedAtStart) Select();
        else if (selectedMark != null) selectedMark.SetActive(false);
    }

    // Unity calls this when the button is tapped
    public void OnPointerClick(PointerEventData eventData)
    {
        Select();
    }

    public void Select()
    {
        if (painter != null)
        {
            if (isEraser) painter.SelectEraser();
            else painter.SelectColor(color);
        }

        // Show the "selected" mark on this button only
        foreach (ToolButton b in allButtons)
        {
            if (b.selectedMark != null) b.selectedMark.SetActive(b == this);
        }
    }
}
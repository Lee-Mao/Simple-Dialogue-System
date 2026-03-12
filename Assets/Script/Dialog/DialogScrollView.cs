using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DialogScrollView : ScrollRect
{
    private bool _isDragging;

    public bool IsDragging => _isDragging;

    public override void OnBeginDrag(PointerEventData eventData)
    {
        _isDragging = true;
        base.OnBeginDrag(eventData);
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        _isDragging = false;
        base.OnEndDrag(eventData);
    }
}
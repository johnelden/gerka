using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Forwards drag events from a card UI object to UISystem.</summary>
public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public UISystem ui;
    public int cardIndex;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (ui != null) ui.BeginCardDrag(cardIndex, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (ui != null) ui.UpdateCardDrag(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (ui != null) ui.EndCardDrag(cardIndex, eventData.position, eventData.pointerId);
    }
}

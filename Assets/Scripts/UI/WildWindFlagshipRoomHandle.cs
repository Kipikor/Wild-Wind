using UnityEngine;
using UnityEngine.EventSystems;

public sealed class WildWindFlagshipRoomHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public WildWindFlagshipConstructorScreen owner;
    public string roomId = "";

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.BeginRoomDrag(roomId, eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.MoveDrag(eventData);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.EndDrag(eventData);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.SelectRoom(roomId);
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;

public sealed class WildWindFlagshipCatalogDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public WildWindFlagshipConstructorScreen owner;
    public string definitionId = "";

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.BeginDrag(definitionId, eventData);
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
            owner.SelectCatalogDefinition(definitionId);
        }
    }
}

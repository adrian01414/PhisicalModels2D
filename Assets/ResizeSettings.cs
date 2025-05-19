using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ResizeSettings : MonoBehaviour, IDragHandler
{
    public RectTransform scrollViewRect;
    public float minWidth = 100f;
    public float maxWidth = 500f;

    public void OnDrag(PointerEventData eventData)
    {
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(
            scrollViewRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 currentMousePos
        ))
        {
            Debug.Log(currentMousePos);
            //scrollViewRect.position = new Vector3(currentMousePos.x, scrollViewRect.position.y, scrollViewRect.position.z);
            //scrollViewRect.sizeDelta = new Vector2(currentMousePos.x * -2, scrollViewRect.sizeDelta.y);
        }
    }
}

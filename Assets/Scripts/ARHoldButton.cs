using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class ARHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, ICancelHandler
{
    [SerializeField] private UnityEvent onHoldStart;
    [SerializeField] private UnityEvent onHoldEnd;

    public void AddHoldStartListener(UnityAction listener)
    {
        onHoldStart.AddListener(listener);
    }

    public void AddHoldEndListener(UnityAction listener)
    {
        onHoldEnd.AddListener(listener);
    }

    public void RemoveHoldStartListener(UnityAction listener)
    {
        onHoldStart.RemoveListener(listener);
    }

    public void RemoveHoldEndListener(UnityAction listener)
    {
        onHoldEnd.RemoveListener(listener);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        onHoldStart?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        onHoldEnd?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        onHoldEnd?.Invoke();
    }

    public void OnCancel(BaseEventData eventData)
    {
        onHoldEnd?.Invoke();
    }

    public void TriggerHoldStart()
    {
        onHoldStart?.Invoke();
    }

    public void TriggerHoldEnd()
    {
        onHoldEnd?.Invoke();
    }
}

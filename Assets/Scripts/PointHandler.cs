using UnityEngine;
using UnityEngine.EventSystems;

public class PointHandler : MonoBehaviour, IPointerClickHandler
{
  public Outline Outline;

  public void OnPointerClick(PointerEventData eventData) {
    if (Outline != null) {
      Outline.enabled = !Outline.enabled;
      Debug.Log($"TargetScript toggled. Current state: {Outline.enabled}");
    }
  }
}

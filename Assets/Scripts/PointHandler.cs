using UnityEngine;
using UnityEngine.EventSystems;

public class PointHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
  public Outline Outline;

  void Awake() {
    if (Outline != null) {
      Outline.enabled = false;
    }
  }

  public void OnPointerEnter(PointerEventData eventData) {
    if (Outline != null) {
      Outline.enabled = true;
    }
  }

  public void OnPointerExit(PointerEventData eventData) {
    if (Outline != null) {
      Outline.enabled = false;
    }
  }
}

using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;

public class PointHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
  public Outline Outline;

  [Header("Meta XR")]
  [SerializeField] private RayInteractable _rayInteractable;

  private readonly HashSet<int> _hoveringPointerIds = new HashSet<int>();
  private bool _eventSystemHovering = false;

  void Awake() {
    if (_rayInteractable == null) {
      _rayInteractable = GetComponent<RayInteractable>();
    }
    if (Outline != null) {
      Outline.enabled = false;
    }
  }

  void OnEnable() {
    if (_rayInteractable != null) {
      _rayInteractable.WhenPointerEventRaised += HandleOvrPointerEvent;
    }
    UpdateOutline();
  }

  void OnDisable() {
    if (_rayInteractable != null) {
      _rayInteractable.WhenPointerEventRaised -= HandleOvrPointerEvent;
    }
    _hoveringPointerIds.Clear();
    _eventSystemHovering = false;
    UpdateOutline();
  }

  public void OnPointerEnter(PointerEventData eventData) {
    _eventSystemHovering = true;
    UpdateOutline();
  }

  public void OnPointerExit(PointerEventData eventData) {
    _eventSystemHovering = false;
    UpdateOutline();
  }

  private void HandleOvrPointerEvent(PointerEvent evt) {
    switch (evt.Type) {
      case PointerEventType.Hover:
        _hoveringPointerIds.Add(evt.Identifier);
        break;
      case PointerEventType.Unhover:
      case PointerEventType.Cancel:
        _hoveringPointerIds.Remove(evt.Identifier);
        break;
    }
    UpdateOutline();
  }

  private void UpdateOutline() {
    if (Outline == null) return;
    Outline.enabled = _eventSystemHovering || _hoveringPointerIds.Count > 0;
  }

  public void ForceClearHover() {
    _hoveringPointerIds.Clear();
    _eventSystemHovering = false;
    UpdateOutline();
  }

  public bool IsHovered {
    get { return _eventSystemHovering || _hoveringPointerIds.Count > 0; }
  }
}

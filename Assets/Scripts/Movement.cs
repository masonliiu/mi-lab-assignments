using UnityEngine;

public class Movement : MonoBehaviour
{
  public Transform rigRoot;
  public Transform cursorDot;
  public Transform rayOrigin; 
  [Header("Right Hand Teleportation")]
  public Transform cursorDotRight;
  public Transform rayOriginRight;
  public float backOffDistance = 0.2f;
  
  [Header("Boundary Settings")]
  public BoxCollider boundaryCollider;
  public bool useBoundary = true; 

  void TryTeleport(Transform cursor, Transform ray) {
    if (rigRoot == null || cursor == null) return;
    
    Vector3 cursorPos = cursor.position;
    Vector3 teleportPos = cursorPos;
    
    if (ray != null && backOffDistance > 0) { 
      Vector3 direction = (cursorPos - ray.position).normalized;
      teleportPos = cursorPos - direction * backOffDistance;
    }
    
    if (!IsWithinBoundary(teleportPos)) return;
    
    rigRoot.position = teleportPos;
  }

  bool IsWithinBoundary(Vector3 position) {
    if (!useBoundary || boundaryCollider == null) {
      return true; 
    }
    
    return boundaryCollider.bounds.Contains(position);
  }

  void Update() {
    bool teleportRequested = false;
    Transform useCursor = null;
    Transform useRay = null;

    //left hand and controller
    if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger) || 
        OVRInput.GetDown(OVRInput.Button.Four)) {
      useCursor = cursorDot;
      useRay = rayOrigin;
      teleportRequested = true;
    }
    //right hand
    if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger)) {
      useCursor = cursorDotRight != null ? cursorDotRight : cursorDot;
      useRay = rayOriginRight != null ? rayOriginRight : rayOrigin;
      teleportRequested = true;
    }

    if (teleportRequested && useCursor != null) {
      TryTeleport(useCursor, useRay);
    }
  }

}



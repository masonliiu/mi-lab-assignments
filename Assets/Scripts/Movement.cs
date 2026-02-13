using UnityEngine;

public class Movement : MonoBehaviour
{
  public Transform rigRoot;
  public Transform cursorDot;
  public Transform rayOrigin;
  public float backOffDistance = 0.2f;
  [Header("Hand Tracking (Pinch to Teleport)")]
  public OVRHand ovrHandLeft;
  public OVRHand ovrHandRight;
  [Header("Left Hand Teleportation")]
  public Transform cursorDotLeft;
  public Transform rayOriginLeft; 
  [Header("Right Hand Teleportation")]
  public Transform cursorDotRight;
  public Transform rayOriginRight;
  
  [Header("Boundary Settings")]
  public BoxCollider boundaryCollider;
  public bool useBoundary = true; 

  [Header("Ground Correction")]
  public Collider groundCollider;
  public float floorY = 0f;

  bool _leftPinchLastFrame;
  bool _rightPinchLastFrame;

  void TryTeleport(Transform cursor, Transform ray) {
    if (rigRoot == null || cursor == null) return;
    
    Vector3 cursorPos = cursor.position;
    Vector3 teleportPos = cursorPos;
    
    if (ray != null && backOffDistance > 0) { 
      Vector3 direction = (cursorPos - ray.position).normalized;
      teleportPos = cursorPos - direction * backOffDistance;
    }
    
    if (!IsWithinBoundary(teleportPos)) return;
    
   // raycast downwards to object hit point
    RaycastHit hit;
    float targetSurfaceY = floorY;
    
    if (Physics.Raycast(teleportPos + Vector3.up, Vector3.down, out hit, 5f)) {
      targetSurfaceY = hit.point.y;
    } else if (groundCollider != null) {
      targetSurfaceY = groundCollider.bounds.max.y;
    }
    
    var capsule = rigRoot.GetComponentInChildren<CapsuleCollider>();
    if (capsule != null) {
      float bottomOffset = capsule.center.y - capsule.height * 0.5f;
      teleportPos.y = targetSurfaceY - bottomOffset;
    } else {
      teleportPos.y = targetSurfaceY + 0.1f;
    }
    
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

    // left hand
    if (ovrHandLeft != null) {
      bool leftPinch = ovrHandLeft.GetFingerIsPinching(OVRHand.HandFinger.Index);
      if (leftPinch && !_leftPinchLastFrame) {
        useCursor = cursorDotLeft != null ? cursorDotLeft : cursorDot;
        useRay = rayOriginLeft != null ? rayOriginLeft : rayOrigin;
        teleportRequested = true;
      }
      _leftPinchLastFrame = leftPinch;
    }
    // right hand
    if (ovrHandRight != null) {
      bool rightPinch = ovrHandRight.GetFingerIsPinching(OVRHand.HandFinger.Index);
      if (rightPinch && !_rightPinchLastFrame) {
        useCursor = cursorDotRight != null ? cursorDotRight : cursorDot;
        useRay = rayOriginRight != null ? rayOriginRight : rayOrigin;
        teleportRequested = true;
      }
      _rightPinchLastFrame = rightPinch;
    }
    // controller
    if (!teleportRequested) {
      if (OVRInput.GetDown(OVRInput.Button.Two)) {
        useCursor = cursorDot;
        useRay = rayOrigin;
        teleportRequested = true;
      }
    }

    if (teleportRequested && useCursor != null) {
      TryTeleport(useCursor, useRay);
    }
  }

}



using UnityEngine;

public class Movement : MonoBehaviour
{
  public Transform rigRoot;
  public Transform cursorDot;
  public Transform rayOrigin; 
  public float backOffDistance = 0.2f;
  
  [Header("Boundary Settings")]
  public BoxCollider boundaryCollider;
  public bool useBoundary = true; 

  bool IsWithinBoundary(Vector3 position) {
    if (!useBoundary || boundaryCollider == null) {
      return true; 
    }
    
    return boundaryCollider.bounds.Contains(position);
  }

  void Update() {
    // button.four is X button on left controller
    if (OVRInput.GetDown(OVRInput.Button.Four)) {
      if (rigRoot == null || cursorDot == null) {
        Debug.LogWarning("Rig Root or Cursor Dot is not assigned!");
        return;
      }
      
      Vector3 cursorPos = cursorDot.position;
      
      Vector3 teleportPos = cursorPos;
      
      if (rayOrigin != null && backOffDistance > 0) { 
        Vector3 direction = (cursorPos - rayOrigin.position).normalized;
        teleportPos = cursorPos - direction * backOffDistance;
      }
      
      if (!IsWithinBoundary(teleportPos)) {
        Debug.LogWarning($"Teleport blocked: Position {teleportPos} is outside boundary!");
        return;
      }
      rigRoot.position = teleportPos;
      
      Debug.Log($"Teleported rig to: {rigRoot.position}");
    }
  }

}



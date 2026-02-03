using UnityEngine;

public class ResetRigInput : MonoBehaviour
{
  public ResetRig resetRig;
    void Update()
    {
      if (OVRInput.GetDown(OVRInput.Button.Three)) {
          resetRig.ResetToAnchor();
      }        
    }
}

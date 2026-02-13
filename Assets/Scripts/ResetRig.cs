using UnityEngine;

public class ResetRig: MonoBehaviour
{
  public Transform rigRoot;
  public Transform resetAnchor;
  void Update()
  {
    if (OVRInput.GetDown(OVRInput.Button.Three) && OVRInput.Get(OVRInput.Button.Four)) {
      ResetToAnchor();
    }
  }
    public void ResetToAnchor()
    {
        rigRoot.position = resetAnchor.position;
        rigRoot.rotation = resetAnchor.rotation;
    }
}

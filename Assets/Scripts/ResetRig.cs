using UnityEngine;

public class ResetRig: MonoBehaviour
{
  public Transform rigRoot;
  public Transform resetAnchor;
    public void ResetToAnchor()
    {
        rigRoot.position = resetAnchor.position;
        rigRoot.rotation = resetAnchor.rotation;
    }
}

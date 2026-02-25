using UnityEngine;

public interface IVRMenuOwner
{
    VRMenuManager.InteractableKind Kind { get; }
    Transform Transform { get; }
    void OnMenuOptionSelected(string optionId);
    void OnVolumeChanged(float value);
    void OnIntensityChanged(float value);
}

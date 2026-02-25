 using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;

public class VRAssignment3Setup : MonoBehaviour
{
    void Start()
    {
        EnsureLamp();
        EnsureRadio();
    }

    static void EnsureLamp()
    {
        var lamp = GameObject.Find("Lamp");
        if (lamp == null) return;

        if (lamp.GetComponent<InteractableVR>() == null)
        {
            var iv = lamp.AddComponent<InteractableVR>();
            iv.kind = VRMenuManager.InteractableKind.Lamp;
        }
        else
        {
            var iv = lamp.GetComponent<InteractableVR>();
            iv.kind = VRMenuManager.InteractableKind.Lamp;
        }

        if (lamp.GetComponent<PointHandler>() == null)
        {
            var ph = lamp.AddComponent<PointHandler>();
            var outline = lamp.GetComponent<Outline>();
            if (outline != null) ph.Outline = outline;
        }
        EnsureRayInteractableWithSurface(lamp);
        if (lamp.GetComponentInChildren<Light>() == null && lamp.GetComponent<Light>() == null)
        {
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 5f;
            light.intensity = 1f;
        }

        var ivr = lamp.GetComponent<InteractableVR>();
        if (ivr != null && ivr.lampLight == null)
            ivr.lampLight = lamp.GetComponentInChildren<Light>();
    }

    static void EnsureRadio()
    {
        var radio = GameObject.Find("Radio");
        if (radio == null)
        {
            radio = new GameObject("Radio");
            radio.transform.position = new Vector3(0f, 1f, 2f);
        }

        if (radio.GetComponent<InteractableVR>() == null)
        {
            var iv = radio.AddComponent<InteractableVR>();
            iv.kind = VRMenuManager.InteractableKind.Radio;
        }
        else
        {
            var iv = radio.GetComponent<InteractableVR>();
            iv.kind = VRMenuManager.InteractableKind.Radio;
        }

        if (radio.GetComponent<PointHandler>() == null)
            radio.AddComponent<PointHandler>();
        if (radio.GetComponent<Collider>() == null)
        {
            var col = radio.AddComponent<BoxCollider>();
            col.size = Vector3.one * 0.3f;
        }
        EnsureRayInteractableWithSurface(radio);
        if (radio.GetComponent<AudioSource>() == null)
            radio.AddComponent<AudioSource>();

        var ivr = radio.GetComponent<InteractableVR>();
        if (ivr != null && ivr.audioSource == null)
            ivr.audioSource = radio.GetComponent<AudioSource>();
    }

    static void EnsureRayInteractableWithSurface(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col == null) return;

        ColliderSurface surf = go.GetComponent<ColliderSurface>();
        if (surf == null)
        {
            surf = go.AddComponent<ColliderSurface>();
            surf.InjectCollider(col);
        }

        var rayInteractable = go.GetComponent<RayInteractable>();
        if (rayInteractable == null)
        {
            rayInteractable = go.AddComponent<RayInteractable>();
            rayInteractable.InjectSurface(surf);
        }
        else if (rayInteractable.Surface == null)
        {
            rayInteractable.InjectSurface(surf);
        }
    }
}

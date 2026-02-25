using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Input;

[DefaultExecutionOrder(-1000)]
public class VRPointerPoseWiring : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureRunsOnce()
    {
        var existing = FindFirstObjectByType<VRPointerPoseWiring>();
        if (existing != null) return;
        var go = new GameObject("VRPointerPoseWiring");
        go.AddComponent<VRPointerPoseWiring>();
    }

    void Awake()
    {
        WireControllerPointerPoses();
        WireHandPointerPoses();
    }

    static bool HierarchyContains(Transform t, string part)
    {
        while (t != null)
        {
            if (t.name.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            t = t.parent;
        }
        return false;
    }

    void WireControllerPointerPoses()
    {
        var pointerPoses = FindObjectsByType<ControllerPointerPose>(FindObjectsSortMode.None);
        if (pointerPoses.Length == 0) return;

        var controllers = FindObjectsByType<Controller>(FindObjectsSortMode.None);
        var controllerRefs = FindObjectsByType<ControllerRef>(FindObjectsSortMode.None);

        IController leftController = null;
        IController rightController = null;
        foreach (var c in controllers)
        {
            if (c.Handedness == Handedness.Left) leftController = c;
            else if (c.Handedness == Handedness.Right) rightController = c;
        }
        foreach (var c in controllerRefs)
        {
            if (leftController == null && c.Handedness == Handedness.Left) leftController = c;
            if (rightController == null && c.Handedness == Handedness.Right) rightController = c;
        }

        foreach (var pose in pointerPoses)
        {
            bool isLeft = HierarchyContains(pose.transform, "Left");
            var controller = isLeft ? leftController : rightController;
            if (controller != null)
            {
                pose.InjectController(controller);
            }
        }
    }

    void WireHandPointerPoses()
    {
        var pointerPoses = FindObjectsByType<HandPointerPose>(FindObjectsSortMode.None);
        if (pointerPoses.Length == 0) return;

        var hands = FindObjectsByType<Hand>(FindObjectsSortMode.None);
        var handRefs = FindObjectsByType<HandRef>(FindObjectsSortMode.None);

        IHand leftHand = null;
        IHand rightHand = null;
        foreach (var h in hands)
        {
            if (h.Handedness == Handedness.Left) leftHand = h;
            else if (h.Handedness == Handedness.Right) rightHand = h;
        }
        foreach (var h in handRefs)
        {
            if (leftHand == null && h.Handedness == Handedness.Left) leftHand = h;
            if (rightHand == null && h.Handedness == Handedness.Right) rightHand = h;
        }

        foreach (var pose in pointerPoses)
        {
            bool isLeft = HierarchyContains(pose.transform, "Left");
            var hand = isLeft ? leftHand : rightHand;
            if (hand != null)
            {
                pose.InjectHand(hand);
            }
        }
    }
}

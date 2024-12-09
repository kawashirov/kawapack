using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PortalScaler : UdonSharpBehaviour {
    public float Scale = 1.0f;

    void Start() {
        SendCustomEventDelayedSeconds(nameof(_Timer), 1);
    }

    public void _Timer() {
        SendCustomEventDelayedSeconds(nameof(_Timer), 5);
        _Scale();
    }

    public void _Scale() {
        var curr_scale = transform.localScale;
        var target_scale = Vector3.one * Scale;
        if (Vector3.Distance(curr_scale, target_scale) > Vector3.kEpsilon) {
            Debug.Log($"Rescaling portal {transform}: {curr_scale} -> {target_scale}...");
            transform.localScale = Vector3.one * Scale;
        }
    }
}

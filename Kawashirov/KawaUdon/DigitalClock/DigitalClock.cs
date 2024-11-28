using System;
using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DigitalClock : UdonSharpBehaviour {
    private Material _mat;

    public void Start() {
        _mat = GetComponent<MeshRenderer>().sharedMaterial;
        SendCustomEventDelayedSeconds(nameof(_Timer), 1);
    }

    public void _Timer() {
        SendCustomEventDelayedSeconds(nameof(_Timer), 5);
        SendCustomEventDelayedSeconds(nameof(_RareUpdate), 5);
    }

    public void _RareUpdate() {
        // Debug.Log($"Updating mat...");
        var now = DateTime.Now;
        var hour = now.Hour;
        var minute = now.Minute;
        var digits = new Vector4(hour / 10, hour % 10, minute / 10, minute % 10);
        _mat.SetVector("_Digits", digits);
    }
}

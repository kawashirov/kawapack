using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class SpawnGroup : UdonSharpBehaviour {
    public Transform[] SpawnPoints;

    public void Start() {
        // literally no other code, data class
        if (!Utilities.IsValid(SpawnPoints))
            SpawnPoints = new Transform[0];
    }

    public Transform _PickRandom() {
        if (!Utilities.IsValid(SpawnPoints))
            return null;
        var len = SpawnPoints.Length;
        return len > 1 ? SpawnPoints[Random.Range(0, len)] : len == 1 ? SpawnPoints[0] : null;
    }

}

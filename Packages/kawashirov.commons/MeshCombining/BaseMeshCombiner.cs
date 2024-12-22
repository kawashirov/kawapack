using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kawashirov.MeshCombining {
	public abstract class BaseMeshCombiner : MonoBehaviour {
		public bool IgnoreEditorOnly = true;
		public bool IgnoreDisabled = true;
		public bool ApplyScaleInLightmap = false;

#if UNITY_EDITOR
		public abstract void Run();
#endif
	}
}
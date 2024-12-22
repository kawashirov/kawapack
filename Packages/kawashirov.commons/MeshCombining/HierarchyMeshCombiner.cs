using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kawashirov.MeshCombining {
	public class HierarchyMeshCombiner : BaseMeshCombiner {
		// Start is called before the first frame update
#if UNITY_EDITOR
		public override void Run() {
			var op = new MeshCombineOp();
			IEnumerable<MeshRenderer> sources = GetComponentsInChildren<MeshRenderer>(!IgnoreDisabled);
			if (IgnoreEditorOnly) {
				sources = sources.RuntimeOnly();
			}
			op.Sources = sources.ToList();
			op.Target = gameObject;
			op.RepackLightmapUV = RepackLightmapUV;
			op.ApplyScaleInLightmap = ApplyScaleInLightmap;
			op.Run();
		}
#endif
	}
}
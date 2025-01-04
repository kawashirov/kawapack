#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kawashirov.MeshCombining {
	public readonly struct SubMeshGroup {
		// Здесь сообираются вместе все саб меши одного материала всех меш рендереров

		// Эти 4 для логов и отладки
		public readonly string id;
		public readonly int indexMRG;
		public readonly int indexSMG;
		private readonly string logToken;

		public readonly Material material;
		public readonly List<SubMeshInfo> sources;

		// Временная меш, в которую будут скомбинированы все саб меши одного материала,
		// прежде чем будут скомбинированы в одну большую с разными материалами.
		public readonly Mesh tmpMesh;

		public SubMeshGroup(string id, int mrg_index, int smg_index, Material material, SubMeshInfo init) {
			this.id = id;
			indexMRG = mrg_index;
			indexSMG = smg_index;
			logToken = $"MeshCombine {id}/№{mrg_index}/№{smg_index}";
			this.material = material;
			sources = new List<SubMeshInfo>();
			tmpMesh = new Mesh { name = $"Temp_{id}_{mrg_index}_{smg_index}" };
			sources.Add(init);
			tmpMesh.MarkDynamic();
		}

		public Mesh Combine() {
			// Скомбинировать все саб меши одного материала в одну временную
			Debug.Log($"{logToken}: Combining {sources.Count} sub meshes of same material {material} to temporary mesh...");
			var cis = sources.Select(smi => smi.CombineInstance()).ToArray();
			tmpMesh.CombineMeshes(cis, true, true, false); // TODO hasLightmapData
			tmpMesh.RecalculateBounds();
			tmpMesh.MarkModified();
			EditorUtility.SetDirty(tmpMesh);
			var info = tmpMesh.GetSubMesh(0);
			Debug.Log($"{logToken}: Combined {sources.Count} sub meshes of same material {material} to temporary mesh: {info}");
			return tmpMesh;
		}

		public CombineInstance CombineInstance() {
			return new CombineInstance {
				// TODO lightmapScaleOffset
				mesh = Combine(),
				// TODO realtimeLightmapScaleOffset
				subMeshIndex = 0,
				transform = Matrix4x4.identity, // Уже преобразованы
			};
		}

		public void Destroy() {
			if (tmpMesh != null)
				Object.DestroyImmediate(tmpMesh);
			// Поможем мусорщику
			sources.Clear();
		}

	}

}
#endif
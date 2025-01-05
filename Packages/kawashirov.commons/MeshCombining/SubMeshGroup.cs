#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace Kawashirov.MeshCombining {
	public readonly struct SubMeshGroup {
		// Здесь сообираются вместе все саб меши одного материала всех меш рендереров

		// Эти 4 для логов и отладки
		public readonly MeshCombineGroupMeta group;
		public readonly int indexMRG;
		public readonly int indexSMG;
		private readonly string logToken;

		public readonly Material material;
		public readonly List<SubMeshInfo> originals;

		// Временная меш, в которую будут скомбинированы все саб меши одного материала,
		// прежде чем будут скомбинированы в одну большую с разными материалами.
		public readonly Mesh tmpMesh;

		public SubMeshGroup(MeshCombineGroupMeta group, int mrg_index, int smg_index, Material material, SubMeshInfo init) {
			this.group = group;
			indexMRG = mrg_index;
			indexSMG = smg_index;
			var global_name = group.Combiner.gameObject.name;
			logToken = $"{global_name}/№{mrg_index}/№{smg_index}";
			this.material = material;
			originals = new List<SubMeshInfo>();
			tmpMesh = new Mesh { name = $"Temp_{global_name}_{mrg_index}_{smg_index}" };
			originals.Add(init);
			tmpMesh.MarkDynamic();
		}

		public Mesh Combine() {
			// Скомбинировать все саб меши одного материала в одну временную
			group.LogDebug($"{logToken}: Combining {originals.Count} sub meshes of " +
				$"same material {material} to temporary mesh...");
			var cis = originals.Select(smi => smi.CombineInstance()).ToArray();
			tmpMesh.CombineMeshes(cis, true, true, false); // TODO hasLightmapData
			Assert.IsTrue(tmpMesh.subMeshCount == 1, $"{tmpMesh.subMeshCount}");
			tmpMesh.RecalculateBounds();
			tmpMesh.MarkModified();
			EditorUtility.SetDirty(tmpMesh);
			var info = tmpMesh.GetSubMesh(0);
			group.LogDebug($"{logToken}: Combined {originals.Count} sub meshes of " +
				$"same material {material} to temporary mesh: {info}");
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

		public void DestroyTemp() {
			if (tmpMesh != null)
				Object.DestroyImmediate(tmpMesh);
			// Поможем мусорщику
			originals.Clear();
		}

	}

}
#endif
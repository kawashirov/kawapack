#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

using Object = UnityEngine.Object;

namespace Kawashirov.MaterialCombining {
	public class MaterialSlotItem {
		public readonly AbstaractMaterialCombiner combiner;
		public readonly MaterialGroup matGroup;
		public readonly Renderer renderer;
		public readonly int slot;
		public readonly Mesh meshOriginal;
		public readonly Material matOriginal;

		// С.м. комменты в RendererGroup. Может быть null, если материал не удалось сконверить, 
		// а следовательно применить UV мофдификации, а следовательно создать новцю меш. 
		public Mesh meshUnique = null;

		public MaterialSlotItem(MaterialGroup matGroup, Renderer renderer, int slot, Mesh meshOriginal, Material matOriginal) {
			combiner = matGroup.combiner;
			this.matGroup = matGroup;
			this.renderer = renderer;
			this.slot = slot;
			this.meshOriginal = meshOriginal;
			this.matOriginal = matOriginal;
		}

		public static VertexAttribute UVIndxToAttrib(int uv_idx) {
			return uv_idx switch {
				0 => VertexAttribute.TexCoord0,
				1 => VertexAttribute.TexCoord1,
				2 => VertexAttribute.TexCoord2,
				3 => VertexAttribute.TexCoord3,
				4 => VertexAttribute.TexCoord4,
				5 => VertexAttribute.TexCoord5,
				6 => VertexAttribute.TexCoord6,
				7 => VertexAttribute.TexCoord7,
				_ => throw new IndexOutOfRangeException($"UV with index {uv_idx} can't exist in Unity!")
			};
		}

		public bool EnsureSlotsConsistent(Mesh mesh, bool except) {
			if (slot < mesh.subMeshCount)
				return true;
			var msg = $"{this}: Only have {mesh.subMeshCount} material slots!";
			if (except) {
				combiner.ThrowException(new Exception(msg + " This shouldn't happened."), renderer);
			} else {
				combiner.LogWarning(msg + " This slot will be skipped.", renderer);
			}
			return false;
		}

		public bool EnsureUV2D(Mesh mesh, bool except) {
			var uv_idx = combiner.GetUVChannel();
			var attr = UVIndxToAttrib(uv_idx);
			if (!mesh.HasVertexAttribute(attr)) {
				var msg = $"{this}: The material requires UV №{uv_idx}, but the mesh have no this UV layer!";
				if (except) {
					combiner.ThrowException(new Exception(msg + " This shouldn't happened."), renderer);
				} else {
					combiner.LogWarning(msg + " This slot will be skipped.", renderer);
				}
				return false;
			}

			var dim = mesh.GetVertexAttributeDimension(attr);
			if (dim != 2) {
				var msg = $"{this}: The material requires 2D UV №{uv_idx}, but this UV layer have dimension of {dim}!";
				if (except) {
					combiner.ThrowException(new Exception(msg + " This shouldn't happened."), renderer);
				} else {
					combiner.LogWarning(msg + " This slot will be skipped.", renderer);
				}
				return false;
			}

			return true;
		}

		public Mesh MakeUniqueMesh() {
			Assert.IsNull(meshUnique);
			// Хитровыебанный способ отсоединить одну сабмеш от другой - скомбинировать только одну меш.
			meshUnique = new Mesh() { name = $"Tmp_{matGroup.combiner.gameObject.name}_{meshOriginal.name}" };
			var combine = new CombineInstance() { mesh = meshOriginal, subMeshIndex = slot };
			meshUnique.CombineMeshes(new CombineInstance[1] { combine }, false, false, false);
			MeshUtility.Optimize(meshUnique);
			Assert.IsTrue(meshUnique.subMeshCount == 1);
			return meshUnique;
		}

		public void DestroyUniqueMesh() {
			if (meshUnique != null)
				Object.DestroyImmediate(meshUnique);
		}

		public override string ToString() =>
			$"{nameof(MaterialSlotItem)}({matGroup}, {renderer}, {slot}, {meshOriginal}, {matOriginal})";
	}
}
#endif
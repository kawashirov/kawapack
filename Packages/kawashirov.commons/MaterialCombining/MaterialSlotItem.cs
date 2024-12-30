#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kawashirov.MaterialCombining {
	public readonly struct MaterialSlotItem {
		public readonly MaterialSlotGroup parent;
		public readonly Renderer renderer;
		public readonly int slot;
		public readonly Mesh mesh;
		public readonly Material original;

		public MaterialSlotItem(MaterialSlotGroup parent, Renderer renderer, int slot, Mesh mesh, Material original) {
			this.parent = parent;
			this.renderer = renderer;
			this.slot = slot;
			this.mesh = mesh;
			this.original = original;
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

		public bool EnsureSlotsConsistent(bool except) {
			if (slot < mesh.subMeshCount)
				return true;
			var msg = $"{this}: Only have {mesh.subMeshCount} material slots!";
			if (except)
				throw new Exception(msg + " This shouldn't happened.");
			parent.parent.LogWarning(msg + " This slot will be skipped.");
			return false;
		}

		public bool EnsureUV2D(bool except) {
			var uv_idx = parent.adapted.uvIndex;
			var attr = UVIndxToAttrib(uv_idx);
			if (!mesh.HasVertexAttribute(attr)) {
				var msg = $"{this}: The material requires UV №{uv_idx}, but the mesh have no this UV layer!";
				if (except)
					throw new Exception(msg + " This shouldn't happened.");
				parent.parent.LogWarning(msg + " This slot will be skipped.");
				return false;
			}

			var dim = mesh.GetVertexAttributeDimension(attr);
			if (dim != 2) {
				var msg = $"{this}: The material requires 2D UV №{uv_idx}, but this UV layer have dimension of {dim}!";
				if (except)
					throw new Exception(msg);
				parent.parent.LogWarning(msg + " This slot will be skipped.");
				return false;
			}

			return true;
		}

		public override string ToString()
			=> $"MaterialSlotItem({parent}, {renderer}, {slot}, {mesh}, {original})";
	}
}
#endif
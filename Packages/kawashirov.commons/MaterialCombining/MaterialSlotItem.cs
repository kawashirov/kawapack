#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kawashirov.MaterialCombining {
	public readonly struct MaterialSlotItem {
		public readonly Renderer renderer;
		public readonly int slot;
		public readonly Mesh mesh;
		public readonly Material original;

		public MaterialSlotItem(Renderer renderer, int slot, Mesh mesh, Material original) {
			this.renderer = renderer;
			this.slot = slot;
			this.mesh = mesh;
			this.original = original;
		}
	}
}
#endif
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kawashirov.MaterialCombining {
	public class ShaderIncluder : AbstractMaterialFilter {
		[Header("Include materials with these shaders")]
		public Shader[] Shaders;

		protected void FilterShader() {
			var new_shaders = Shaders.UnityNotNull().Distinct().ToArray();
			if (Shaders.Length != new_shaders.Length) {
				Shaders = new_shaders;
				SetDirty();
			}
		}

		public void ApplyFilters() {
			FilterShader();
		}

		public override void Prepare() => ApplyFilters();

		public override bool CheckInclude(Renderer renderer, Mesh mesh, int index, Material mat) {
			if (Shaders.Length == 0)
				return false;
			return Shaders.Contains(mat.shader);
		}

		public override bool CheckExclude(Renderer renderer, Mesh mesh, int index, Material mat) {
			return false;
		}

		public override void Refresh() => ApplyFilters();
	}
}
#endif
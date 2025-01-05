#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kawashirov.MaterialCombining {
	public class ShaderIncluder : AbstractMaterialFilter {
		[Header("Include materials with these shaders")]
		public List<Shader> Shaders = new List<Shader>();

		protected void FilterShader() {
			var new_shaders = Shaders.UnityNotNull().Distinct().ToArray();
			if (Shaders.Count != new_shaders.Length) {
				Shaders.Clear();
				Shaders.AddRange(new_shaders);
				SetDirty();
			}
		}

		public void ApplyFilters() {
			FilterShader();
		}

		public override void Prepare() => ApplyFilters();

		public override bool CheckInclude(Renderer renderer, Mesh mesh, int index, Material mat) {
			return Shaders.Contains(mat.shader);
		}

		public override bool CheckExclude(Renderer renderer, Mesh mesh, int index, Material mat) {
			return false;
		}

		public override void Refresh() => ApplyFilters();
	}
}
#endif
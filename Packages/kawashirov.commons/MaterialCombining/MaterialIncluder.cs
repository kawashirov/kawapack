#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kawashirov.MaterialCombining {
	public class MaterialIncluder : AbstractMaterialFilter {
		[Header("Include these materials and their cildren")]
		public Material[] Parents;

		protected void FilterParents() {
			var new_parents = Parents
				.UnityNotNull().Distinct().ToArray();
			if (Parents.Length != new_parents.Length) {
				Parents = new_parents;
				SetDirty();
			}
		}

		public void ApplyFilters() {
			FilterParents();
		}

		public override void Prepare() => ApplyFilters();

		public override bool CheckInclude(Renderer renderer, Mesh mesh, int index, Material mat) {
			if (Parents.Length == 0)
				return false;
			while (mat != null) {
				if (Parents.Contains(mat))
					return true;
				mat = mat.parent;
			}
			return false;
		}

		public override bool CheckExclude(Renderer renderer, Mesh mesh, int index, Material mat) {
			return false;
		}

		public override void Refresh() => ApplyFilters();
	}
}
#endif
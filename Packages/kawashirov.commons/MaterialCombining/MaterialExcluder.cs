#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;

namespace Kawashirov.MaterialCombining {
	public class MaterialExcluder : AbstractMaterialFilter {
		[Header("Exclude these materials")]
		public Material[] Materials;
		public string[] MaterialNameWords;

		[Header("Exclude these GameObjects")]
		public GameObject[] Hierarchy;
		public string[] HierarchyNameWords;

		protected void FilterObjArray<T>(ref T[] array) where T : Object {
			var new_exclude = array
				.UnityNotNull().Distinct().ToArray();
			if (array.Length != new_exclude.Length) {
				array = new_exclude;
				SetDirty();
			}
		}

		protected void FilterKeywordsArray(ref string[] array) {
			var new_exclude_kw = array
				.Where(kw => !string.IsNullOrWhiteSpace(kw))
				.Select(kw => kw.Trim()).Distinct().ToArray();
			if (array.Length != new_exclude_kw.Length) {
				array = new_exclude_kw;
				SetDirty();
			}
		}

		protected bool ContainsAnyKeyword(string what, string[] keywords)
			=> keywords.Any(kw => what.Contains(kw, StringComparison.InvariantCultureIgnoreCase));

		public void ApplyFilters() {
			FilterObjArray(ref Materials);
			FilterKeywordsArray(ref MaterialNameWords);

			FilterObjArray(ref Hierarchy);
			FilterKeywordsArray(ref HierarchyNameWords);
		}

		public override void Prepare() => ApplyFilters();

		public override bool CheckInclude(Renderer renderer, Mesh mesh, int slot, Material mat) {
			return false;
		}

		public override bool CheckExclude(Renderer renderer, Mesh mesh, int slot, Material mat) {
			if (Materials.Contains(mat))
				return true;
			if (ContainsAnyKeyword(mat.name, MaterialNameWords))
				return true;

			var t = renderer.transform;
			while (t != null) {
				var gobj = t.gameObject;
				if (Hierarchy.Contains(gobj))
					return true;
				if (ContainsAnyKeyword(gobj.name, HierarchyNameWords))
					return true;
				t = t.parent;
			}

			return false;
		}

		public override void Refresh() => ApplyFilters();
	}
}
#endif
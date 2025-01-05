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
		public List<Material> Materials = new List<Material>();
		public List<string> MaterialNameWords = new List<string>();

		[Header("Exclude these GameObjects")]
		public List<GameObject> Hierarchy = new List<GameObject>();
		public List<string> HierarchyNameWords = new List<string>();

		protected void FilterObjArray<T>(List<T> list) where T : Object {
			var new_exclude = list.UnityNotNull().Distinct().ToArray();
			if (list.Count != new_exclude.Length) {
				list.Clear();
				list.AddRange(new_exclude);
				SetDirty();
			}
		}

		protected void FilterKeywordsArray(List<string> list) {
			var new_exclude_kw = list
				.Where(kw => !string.IsNullOrWhiteSpace(kw))
				.Select(kw => kw.Trim()).Distinct().ToArray();
			if (list.Count != new_exclude_kw.Length) {
				list.Clear();
				list.AddRange(new_exclude_kw);
				SetDirty();
			}
		}

		protected bool ContainsAnyKeyword(string what, List<string> keywords)
			=> keywords.Any(kw => what.Contains(kw, StringComparison.InvariantCultureIgnoreCase));

		public void ApplyFilters() {
			FilterObjArray(Materials);
			FilterKeywordsArray(MaterialNameWords);

			FilterObjArray(Hierarchy);
			FilterKeywordsArray(HierarchyNameWords);
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
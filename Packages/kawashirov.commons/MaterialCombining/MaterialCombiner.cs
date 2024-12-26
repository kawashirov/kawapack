#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kawashirov.MaterialCombining {
	public class MaterialCombiner : KawaEditorBehaviour {
		[Tooltip("Where on scene search objects to atlas.")]
		public GameObject[] Hierarchy;

		[Tooltip("If Checked, \"Hierarchy\" is ignored and hole scene is used.")]
		public bool WholeScene = false;

		[Space]
		[Tooltip("Filter specific Materials for atlassing.")]
		public AbstractMaterialFilter[] Filters;

		[Space]
		public AbstractMaterialAdapter[] MatToDataAdapters;
		public AbstractMaterialAdapter DataToMatAdapter;

		/**/
		protected Dictionary<Material, MaterialSlotGroup> materials;
		protected List<string> textureNames;

		public virtual IEnumerable<Renderer> CollectRenderers() {
			var gobjs = WholeScene ? gameObject.scene.GetRootGameObjects() : Hierarchy;
			return gobjs.SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).Distinct();
		}

		public virtual Mesh GetMesh(Renderer renderer) {
			if (renderer is MeshRenderer mesh_renderer) {
				if (mesh_renderer.TryGetComponent<MeshFilter>(out var filter)) {
					return filter.sharedMesh;
				}
			} else if (renderer is SkinnedMeshRenderer skinned_renderer) {
				return skinned_renderer.sharedMesh;
			}
			// TODO more types
			return null;
		}

		public virtual void ProcessRenderer(Renderer renderer, Mesh mesh, int slot, Material mat) {
			if (Filters.Any(f => f.CheckExclude(renderer, mesh, slot, mat)))
				return;
			if (!Filters.Any(f => f.CheckInclude(renderer, mesh, slot, mat)))
				return;
			// TODO better logs
			var item = new MaterialSlotItem(renderer, slot, mesh, mat);
			if (materials.TryGetValue(mat, out var group)) {
				group.items.Add(item);
			} else {
				materials.Add(mat, new MaterialSlotGroup(this, mat, item));
			}
		}

		public virtual void ProcessRenderer(Renderer renderer) {
			var mesh = GetMesh(renderer);
			if (mesh == null)
				return;
			var slots = renderer.sharedMaterials;
			for (var i = 0; i < slots.Length; ++i) {
				ProcessRenderer(renderer, mesh, i, slots[i]);
				// TODO better logs & exceptions
			}
		}

		public virtual void FilterAndGroup() {
			materials = new Dictionary<Material, MaterialSlotGroup>();
			foreach (var renderer in CollectRenderers()) {
				ProcessRenderer(renderer);
			}
			var slots = materials.Values.Sum(v => v.items.Count);
			Log($"Gathered {slots} material slots in {materials.Count} materials.");
		}

		public virtual bool TryAdaptMat2Data(MaterialSlotGroup group, AbstractMaterialAdapter adapter) {
			if (!adapter.CanAdaptMaterial(group.original))
				return false;
			group.data = adapter.MaterialToData(group.original);
			group.adapter = adapter;
			return true;
		}

		public virtual void AdaptMat2Data() {
			foreach (var group in materials.Values) {
				// TODO logs & errors
				foreach (var adapter in MatToDataAdapters) {
					// TODO logs & errors
					if (TryAdaptMat2Data(group, adapter)) {
						break;
					}
				}
			}

			textureNames = materials.Values.SelectMany(g => g.data.Select(d => d.name)).Distinct().ToList();
			var textureNames_s = string.Join(", ", textureNames.Select(x => $"\"{x}\""));
			Log($"Gathered {textureNames.Count} texture names from {materials.Count} materials: {textureNames_s}");
		}


		public virtual void Run() {
			FilterAndGroup();
			AdaptMat2Data();

		}
	}
}
#endif
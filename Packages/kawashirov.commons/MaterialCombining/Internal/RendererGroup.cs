#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace Kawashirov.MaterialCombining {
	public class RendererGroup {
		// Итак, прикол: Пространство индексов в Mesh общее, 
		// т.е. разные саб-мешы одной меши могут переиспользовать однии теже индексы (и данные на них).
		// Получается, редактировать данные на каждой саб-меши напрямую невозможно, 
		// ведь тогда мы ненароком можем затронуть другие саб меши. По этому, мы:
		// - для каждого слота хитрым трюком создаём меш-копию только с этим слотом,
		// - модифицируем UV на этой временной копии,
		// - собираем все слоты назад (в т.ч. и те которые не атлассировались),
		// Всё это происходит в этом классе. 
		// TODO объединение слотов одинаковых материалов

		public readonly AbstaractMaterialCombiner combiner;
		public readonly int index;
		public readonly Renderer renderer;
		public readonly Dictionary<int, MaterialSlotItem> items;
		public Mesh meshOriginal;

		// Может получиться null.
		public Mesh meshAtlas;

		public RendererGroup(AbstaractMaterialCombiner combiner, int index, Renderer renderer) {
			this.combiner = combiner;
			this.index = index;
			this.renderer = renderer;
			items = new Dictionary<int, MaterialSlotItem>();
		}

		public virtual Mesh GetMesh(Renderer renderer) {
			if (renderer is MeshRenderer mesh_renderer) {
				if (mesh_renderer.TryGetComponent<MeshFilter>(out var filter)) {
					return meshOriginal = filter.sharedMesh;
				}
			} else if (renderer is SkinnedMeshRenderer skinned_renderer) {
				return meshOriginal = skinned_renderer.sharedMesh;
			}
			// TODO more types?
			return null;
		}

		public void Add(MaterialSlotItem item) {
			items[item.slot] = item;
		}

		public virtual void RecombineMeshes() {
			// Может вернуть null, если ни один слот не был модифицирован, 
			// тогда и в создании новой меши нету смысла.
			var N = meshOriginal.subMeshCount;
			var combine = new CombineInstance[N];
			var have_unique = false;
			for (var i = 0; i < N; i++) {
				var cmb_i = new CombineInstance();
				if (items.TryGetValue(i, out var item) && item.meshUnique != null) {
					Assert.IsTrue(item.meshOriginal == meshOriginal);
					Assert.IsTrue(item.meshUnique.subMeshCount == 1);
					cmb_i.mesh = item.meshUnique;
					cmb_i.subMeshIndex = 0;
					have_unique = true;
				} else {
					cmb_i.mesh = meshOriginal;
					cmb_i.subMeshIndex = i;
				}
				combine[i] = cmb_i;
			}
			if (!have_unique)
				return;

			meshAtlas = new Mesh();
			meshAtlas.name = $"Atlas_{combiner.gameObject.name}_mesh_{index}_{meshOriginal.name}";
			meshAtlas.CombineMeshes(combine, false, false, false);
			Assert.IsTrue(meshAtlas.subMeshCount == N);
			MeshUtility.Optimize(meshAtlas);
			meshAtlas.RecalculateBounds();
			meshAtlas.RecalculateUVDistributionMetrics();
			meshAtlas.UploadMeshData(false);
			meshAtlas.MarkModified();
		}

		public virtual void SetMeshAtlas() {
			if (meshAtlas == null)
				return;
			if (renderer is MeshRenderer mesh_renderer) {
				if (mesh_renderer.TryGetComponent<MeshFilter>(out var filter)) {
					filter.sharedMesh = meshAtlas;
					EditorUtility.SetDirty(filter);
				}
			} else if (renderer is SkinnedMeshRenderer skinned_renderer) {
				skinned_renderer.sharedMesh = meshAtlas;
				EditorUtility.SetDirty(skinned_renderer);
			}
			// TODO more types?
		}

		public virtual void ApplyMaterials() {
			if (meshAtlas == null)
				return;
			var materials = renderer.sharedMaterials;
			var N = Mathf.Min(materials.Length, meshAtlas.subMeshCount, items.Count);
			var changed = false;
			for (var i = 0; i < N; i++) {
				if (items.TryGetValue(i, out var item)) {
					var matAtlas = item.matGroup.matAtlas;
					if (matAtlas != null) {
						materials[i] = item.matGroup.matAtlas;
						changed = true;
					}
				}
			}
			if (changed) {
				renderer.sharedMaterials = materials;
				EditorUtility.SetDirty(renderer);
			}
		}

		public virtual void SaveMesh() {
			if (meshAtlas == null)
				return;
			var mesh_atlas_path = meshAtlas.name.SanitizeFileName();
			mesh_atlas_path = $"{combiner.sceneDir}/{mesh_atlas_path}.asset";
			if (combiner.UniqueAssetNames)
				mesh_atlas_path = AssetDatabase.GenerateUniqueAssetPath(mesh_atlas_path);
			AssetDatabase.CreateAsset(meshAtlas, mesh_atlas_path);
			meshAtlas.MarkModified();
		}

		public virtual void AtlasApplyMeshesAndMats() {
			RecombineMeshes();
			SetMeshAtlas();
			ApplyMaterials();
		}
	}
}
#endif
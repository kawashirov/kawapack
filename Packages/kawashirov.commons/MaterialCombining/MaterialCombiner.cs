#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
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
		protected List<UVIslandFlat> islands;
		protected Vector2Int atlasSize = Vector2Int.zero;

		protected struct UVIslandFlat {
			public MaterialSlotGroup group;
			public UVIsland island;
			public int islandIndex;
		}

		protected virtual IEnumerable<Renderer> CollectRenderers() {
			var gobjs = WholeScene ? gameObject.scene.GetRootGameObjects() : Hierarchy;
			return gobjs.SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).Distinct();
		}

		protected virtual Mesh GetMesh(Renderer renderer) {
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

		protected virtual void ProcessRenderer(Renderer renderer, Mesh mesh, int slot, Material mat) {
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

		protected virtual void ProcessRenderer(Renderer renderer) {
			var mesh = GetMesh(renderer);
			if (mesh == null)
				return;
			var slots = renderer.sharedMaterials;
			for (var i = 0; i < slots.Length; ++i) {
				ProcessRenderer(renderer, mesh, i, slots[i]);
				// TODO better logs & exceptions
			}
		}

		protected virtual void FilterAndGroup() {
			materials = new Dictionary<Material, MaterialSlotGroup>();
			foreach (var renderer in CollectRenderers()) {
				ProcessRenderer(renderer);
			}
			var slots = materials.Values.Sum(v => v.items.Count);
			Log($"Gathered {slots} material slots in {materials.Count} materials.");
		}

		protected virtual bool TryAdaptMat2Data(MaterialSlotGroup group, AbstractMaterialAdapter adapter) {
			if (!adapter.CanAdaptMaterial(group.original))
				return false;
			group.data = adapter.MaterialToData(group.original);
			group.adapter = adapter;
			return true;
		}

		protected virtual void AdaptMat2Data() {
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

		protected virtual void CalcMatSizes() {
			foreach (var group in materials.Values) {
				group.CalcTexSize();
			}
		}

		protected virtual void CalcUVIslands() {
			Log($"Calculating UV islands on {materials.Count} materials...");
			foreach (var group in materials.Values) {
				group.CalcIslands();
			}
			var sum = materials.Values.Select(g => g.IslandsCount()).Sum();
			Log($"Got {sum} UV islands from {materials.Count} materials.");
		}

		protected virtual void CalcAtlasLayout() {
			Texture2D[] dull_tex = null;
			Texture2D atlas_tex = null;
			// Используем схему с фейковыми текстурами и Texture2D.PackTextures для вычисления лайаута.
			// Т.к. Texture2D.GenerateAtlas очень баганый и плохо докмументирован
			// https://discussions.unity.com/t/texture2d-generateatlas-has-a-bug-texture2d-generateatlas-has-a-bug/240115
			// https://issuetracker.unity3d.com/issues/texture2d-dot-generateatlas-returns-true-with-a-list-of-returned-rectangles-with-a-size-of-0-when-it-should-return-false-or-return-true-and-downscale-the-sizes-provided-in-the-parameters-to-fit-the-atlas-size
			try {
				// (группа, расширеный остров в коордах ориг тексткры, номер острова в списке группы)
				var islands = materials.Values.SelectMany(
					grp => grp.islandsPadded.Select((isl, idx) => (grp, isl, idx))
				).ToList();
				Log($"Packing {islands.Count} islands total...");
				atlas_tex = new Texture2D(1, 1, TextureFormat.Alpha8, false);
				dull_tex = islands.Select(x => x.isl.MakeDullTex()).ToArray();
				// https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Texture2D.PackTextures.html
				var results = atlas_tex.PackTextures(dull_tex, 0, 8192);
				Log($"Packed {islands.Count} islands to {atlas_tex.width}x{atlas_tex.height} atlas.");
				for (var i = 0; i < results.Length; ++i) {
					var (grp, isl, idx) = islands[i];
					var atlas_island = new UVIsland(results[i]);
					Log($"Packed {islands[i].grp.original}, #{i}: {islands[i].isl} -> {atlas_island}");
					grp.islandsAtlas[idx] = atlas_island;
				}
				atlasSize = new Vector2Int(atlas_tex.width, atlas_tex.height);
			} finally {
				if (dull_tex != null)
					foreach (var dull in dull_tex)
						if (dull != null)
							DestroyImmediate(dull);
				if (atlas_tex)
					DestroyImmediate(atlas_tex);
			}
		}

		protected virtual void BakeAtlasNamed(string data_name) {
			var shader = Shader.Find("Kawashirov/MaterialCombiner/BlitCopy");
			RenderTexture tex_dst1 = null;
			RenderTexture tex_dst2 = null;
			Material mat_blit = null;
			try {
				tex_dst1 = new RenderTexture(atlasSize.x, atlasSize.y, 0, RenderTextureFormat.ARGBHalf);
				tex_dst2 = new RenderTexture(atlasSize.x, atlasSize.y, 0, RenderTextureFormat.ARGBHalf);
				mat_blit = new Material(shader);
				foreach (var group in materials.Values) {
					Log($"Trying to render data \"{data_name}\" of material {group.original}...");
					var datas = group.data.Where(d => data_name.Equals(d.name)).ToArray();
					var mat_original = group.original;
					if (datas.Length < 1) {
						LogWarning($"There is no passes for data \"{data_name}\" material {mat_original}... Not adapted?");
						continue;
					}
					for (var data_i = 0; data_i < datas.Length; data_i++) {
						var data = datas[data_i];
						var tex_src = data.texture;
						var ch = data.textureChannel;
						mat_blit.SetTexture("_MainTex", tex_src);
						mat_blit.SetColor("_Color", data.scaleColor);
						mat_blit.SetFloat("_Scale", data.scale);
						DataToMatAdapter.DataToBlit(data, mat_blit);
						mat_blit.SetTexture("_TargetTex", tex_dst1);
						var values_s = $"{tex_src} * {data.scaleColor} * {data.scale}";
						for (var islands_i = 0; islands_i < group.islandsAtlas.Count; islands_i++) {
							var island_source = group.islandsPadded[islands_i]; // pixel coords
							var island_target = group.islandsAtlas[islands_i]; // 0..1 coords
							var vec_source = island_source.ToVector4Norm(tex_src.width, tex_src.height);
							var vec_target = island_target.ToVector4();
							mat_blit.SetVector("_SourceRect", vec_source);
							mat_blit.SetVector("_TargetRect", vec_target);
							var where_s = $"{mat_original}/{data_i}/{islands_i}";
							Log($"Blitting {where_s}: {values_s} @ {island_source}:{vec_source} -> {tex_dst2} @ {vec_target}");
							Graphics.Blit(tex_src, tex_dst2, mat_blit);
							(tex_dst1, tex_dst2) = (tex_dst2, tex_dst1); // swap buffers
						}
					}
				}
				// Сохранение
				var tex_asset = new Texture2D(tex_dst1.width, tex_dst1.height, TextureFormat.RGBAHalf, true);
				var prev_active = RenderTexture.active;
				try {
					AssetDatabase.StartAssetEditing();

					RenderTexture.active = tex_dst1;
					tex_asset.ReadPixels(new Rect(0, 0, tex_dst1.width, tex_dst1.height), 0, 0, true);
					tex_asset.Apply();
					EditorUtility.SetDirty(tex_asset);

					var png_data = tex_asset.EncodeToPNG();
					var path_png = $"Assets/SavedTexture_{data_name}.png";
					File.WriteAllBytes(path_png, png_data);

					EditorUtility.CompressTexture(tex_asset, TextureFormat.BC7, TextureCompressionQuality.Best);
					tex_asset.Apply();
					EditorUtility.SetDirty(tex_asset);
					var path_asset = $"Assets/SavedTexture_{data_name}.asset";
					AssetDatabase.CreateAsset(tex_asset, path_asset);

					AssetDatabase.SaveAssets();
					AssetDatabase.Refresh();
				} finally {
					RenderTexture.active = prev_active;
					AssetDatabase.StopAssetEditing();
					AssetDatabase.Refresh();
				}
			} finally {
				if (tex_dst1 != null)
					DestroyImmediate(tex_dst1);
				if (tex_dst2 != null)
					DestroyImmediate(tex_dst2);
				if (mat_blit != null)
					DestroyImmediate(tex_dst1);
			}
		}

		protected virtual void BakeAtlas() {
			BakeAtlasNamed("Albedo");
		}

		public virtual void Run() {
			FilterAndGroup();
			AdaptMat2Data();
			CalcMatSizes();
			CalcUVIslands();
			CalcAtlasLayout();
			BakeAtlas();
		}
	}
}
#endif
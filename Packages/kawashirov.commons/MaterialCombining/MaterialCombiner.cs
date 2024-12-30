#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

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
		public List<DataChannelDescriptor> descriptors;
		protected Dictionary<Material, MaterialSlotGroup> materials;
		protected readonly HashSet<Material> unadaptable = new HashSet<Material>();
		protected List<string> textureNames;
		protected Vector2Int atlasSize = Vector2Int.zero;

		protected Material mat_blit;
		protected RenderTexture tex_dst1 = null;
		protected RenderTexture tex_dst2 = null;


		protected virtual void InitData() {
			if (DataToMatAdapter == null) {
				var msg = $"{nameof(DataToMatAdapter)} is not set!";
				LogError(msg);
				throw new NullReferenceException(msg);
			}
			descriptors = DataToMatAdapter.InitDescriptors();
			Log($"Initialized {descriptors.Count} data channels desciptors materials.");
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

		protected virtual bool TryAdaptMaterial(Material mat, out DataAdapted data) {
			Log($"Adapting material {mat}...");
			data = null;
			foreach (var adapter_i in MatToDataAdapters) {
				// TODO logs & errors
				if (!adapter_i.TryAdaptMaterial(mat, descriptors, out data))
					continue;
				Log($"Found adapter {data.adapter} for material {mat} with {data.data.Count} datas and transform {data.texST}.");
				return true;
			}
			Log($"No adapter found for material {mat}.");
			data = null;
			return false;
		}

		protected virtual bool TryGetMaterialGroup(Material mat, out MaterialSlotGroup group) {
			if (materials.TryGetValue(mat, out group)) {
				// Группа уже существует, значит её материал уже адаптирован, а значит доп. проверки не нужны.
				return true;
			} else if (TryAdaptMaterial(mat, out var adapted)) {
				// Создание новый группы, если удалось адаптировать.
				group = new MaterialSlotGroup(this, mat, adapted);
				materials.Add(mat, group);
				return true;
			} else {
				// Если материал не удалось адаптировать, запоминаем это, что бы не проверять ещё раз.
				unadaptable.Add(mat);
			}
			return false;
		}

		protected virtual void ProcessRenderer(Renderer renderer, Mesh mesh, int slot, Material mat) {
			// Если известно, что материал не поддаётся адаптации, 
			// то дальнейшие проверки не имеют смысла.
			if (unadaptable.Contains(mat))
				return;

			// В первую очередь спрашиваем фильтры.
			if (Filters.Any(f => f.CheckExclude(renderer, mesh, slot, mat)))
				return;
			if (!Filters.Any(f => f.CheckInclude(renderer, mesh, slot, mat)))
				return;

			if (TryGetMaterialGroup(mat, out var group)) {
				var item = new MaterialSlotItem(group, renderer, slot, mesh, mat);
				if (!item.EnsureSlotsConsistent(false))
					return;
				if (!item.EnsureUV2D(false))
					return;
				group.items.Add(item);
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

		protected virtual void FilterGroupAndAdapt() {
			Log($"Searching renderers to combine materials on...");
			materials = new Dictionary<Material, MaterialSlotGroup>();
			unadaptable.Clear();
			foreach (var renderer in CollectRenderers()) {
				ProcessRenderer(renderer);
			}
			var slots = materials.Values.Sum(v => v.items.Count);
			var renderers = materials.Values.SelectMany(g => g.items.Select(i => i.renderer)).Distinct().Count();
			unadaptable.Clear(); // Больше метки нам не понадобятся.
			Log($"Gathered {materials.Count} materials and {slots} material slots from {renderers} renderers.");
		}

		protected virtual void CalcMatSizes() {
			foreach (var group in materials.Values) {
				group.CalcTexSize();
			}
		}

		protected virtual IEnumerator CalcUVIslands() {
			Log($"Calculating UV islands on {materials.Count} materials...");
			foreach (var group in materials.Values) {
				group.CalcIslands();
				yield return null;
			}
			var sum = materials.Values.Select(g => g.IslandsCount()).Sum();
			MaterialSlotGroup.ResetBuffers(); // Больше не понадобятся.
			Log($"Got {sum} UV islands from {materials.Count} materials.");
			yield return null;
		}

		protected virtual IEnumerator CalcAtlasLayout() {
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
				yield return null;
				var results = atlas_tex.PackTextures(dull_tex, 0, 4096);
				yield return null;
				atlasSize = new Vector2Int(atlas_tex.width, atlas_tex.height);
				Log($"Packed {islands.Count} islands to {atlasSize.x}x{atlasSize.y} atlas.");
				for (var i = 0; i < results.Length; ++i) {
					var (grp, isl, idx) = islands[i];
					var atlas_island = new UVIsland(results[i]);
					Log($"Packed {islands[i].grp.mat}, #{i}: {islands[i].isl} -> {atlas_island}");
					grp.islandsAtlas[idx] = atlas_island;
				}
			} finally {
				if (dull_tex != null)
					foreach (var dull in dull_tex)
						if (dull != null)
							DestroyImmediate(dull);
				if (atlas_tex)
					DestroyImmediate(atlas_tex);
			}
			yield return null;
		}

		protected virtual RenderTexture MakeRenderTexture_(DataChannelDescriptor descriptor) {
			// Какая-то хуйня, когда через дескриптор инициализирую, то нихуя не работает.
			var rt_desc = new RenderTextureDescriptor();
			// Сначала флажки, порядок имеет значение
			rt_desc.dimension = TextureDimension.Tex2D;
			rt_desc.sRGB = false; // Вначале должен быть false а то в colorFormat дурка.
			rt_desc.enableRandomWrite = false; // Requested anti-aliasing with random write flag. This is not supported.
			rt_desc.autoGenerateMips = false;
			rt_desc.depthBufferBits = 0;
			rt_desc.msaaSamples = 8;
			rt_desc.shadowSamplingMode = ShadowSamplingMode.None;
			rt_desc.useDynamicScale = false;
			rt_desc.useMipMap = false;
			rt_desc.volumeDepth = 1; // Я хуй знает, вроде и не 3д текстура, но без этого крашит.

			rt_desc.colorFormat = RenderTextureFormat.ARGBHalf;
			// rt_desc.sRGB = descriptor.sRGB;
			rt_desc.height = atlasSize.x;
			rt_desc.width = atlasSize.y;

			return RenderTexture.GetTemporary(rt_desc);
		}

		protected virtual RenderTexture MakeRenderTexture(DataChannelDescriptor descriptor) {
			return RenderTexture.GetTemporary(atlasSize.x, atlasSize.y, 0,
				RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.sRGB);
		}

		protected virtual void BakeAtlasBackground(DataChannelDescriptor descriptor) {
			mat_blit.SetTexture("_TexR", descriptor.bgTexture);
			mat_blit.SetTexture("_TexG", descriptor.bgTexture);
			mat_blit.SetTexture("_TexB", descriptor.bgTexture);
			mat_blit.SetTexture("_TexA", descriptor.bgTexture);
			mat_blit.SetVector("_Channels", new Vector4(0, 1, 2, 3));

			mat_blit.SetColor("_Color", descriptor.bgColor);

			mat_blit.SetInteger("_ColorSpace", descriptor.sRGB ? 1 : 0);
			mat_blit.SetInteger("_BumpMode", descriptor.isNormal ? 1 : 0);
			// mat_blit.SetInteger("_BumpBGR", 0);

			var full = new Vector4(0, 0, 1, 1);
			mat_blit.SetVector("_SourceRect", full);
			mat_blit.SetVector("_TargetRect", full);

			mat_blit.SetTexture("_TargetTex", tex_dst1);
			Log($"Blitting \"{descriptor.name}\" default background...");
			Graphics.Blit(descriptor.bgTexture, tex_dst2, mat_blit);
			(tex_dst1, tex_dst2) = (tex_dst2, tex_dst1); // swap buffers
			Selection.SetActiveObjectWithContext(tex_dst1, this);
		}

		protected virtual void BakeAtlasGroup(DataChannelDescriptor descriptor, MaterialSlotGroup group) {
			var dsc_name = descriptor.name;
			if (group.islandsAtlas.Count < 1)
				return; // Группа может быть пустой.

			Log($"Trying to render data \"{dsc_name}\" of material {group.mat}...");
			var datas = group.adapted.data.Where(d => d.descriptor == descriptor).ToArray();
			var mat_original = group.mat;
			if (datas.Length < 1) {
				LogWarning($"There is no passes for data \"{dsc_name}\" material {mat_original}... Not adapted?");
				return;
			} else if (datas.Length > 1) {
				LogError($"There is multiple passes for data \"{dsc_name}\" material {mat_original}!");
				return;
			}
			var data = datas[0];
			var src_tex_size = group.textureSize;

			mat_blit.SetTexture("_TexR", data.dstTex[0]);
			mat_blit.SetTexture("_TexG", data.dstTex[1]);
			mat_blit.SetTexture("_TexB", data.dstTex[2]);
			mat_blit.SetTexture("_TexA", data.dstTex[3]);
			mat_blit.SetVector("_Channels", data.ChannelsAsVector4());
			// mat_blit.SetInteger("_BumpBGR", 1);

			mat_blit.SetColor("_Color", data.color);

			mat_blit.SetInteger("_ColorSpace", descriptor.sRGB ? 1 : 0);
			mat_blit.SetInteger("_BumpMode", descriptor.isNormal ? 1 : 0);

			for (var islands_i = 0; islands_i < group.islandsAtlas.Count; ++islands_i) {
				var island_source = group.islandsPadded[islands_i]; // pixel coords
				var island_target = group.islandsAtlas[islands_i]; // 0..1 coords
				var vec_source = island_source.ToVector4Norm(src_tex_size.x, src_tex_size.y);
				var vec_target = island_target.ToVector4();
				mat_blit.SetVector("_SourceRect", vec_source);
				mat_blit.SetVector("_TargetRect", vec_target);
				mat_blit.SetTexture("_TargetTex", tex_dst1);
				Log($"Blitting {mat_original}/{dsc_name}/{islands_i}: {island_source}/{vec_source} -> {vec_target}");
				var capture = false; // descriptor.isNormal && data.dstTex.Any(t => t != Texture2D.normalTexture);
				try {
					if (capture)
						ExternalGPUProfiler.BeginGPUCapture();
					Graphics.Blit(data.dstTex[0], tex_dst2, mat_blit);
				} finally {
					if (capture)
						ExternalGPUProfiler.EndGPUCapture();
				}
				(tex_dst1, tex_dst2) = (tex_dst2, tex_dst1); // swap buffers
				Selection.SetActiveObjectWithContext(tex_dst1, this);
				// break;
			}
		}

		protected virtual IEnumerator SaveAtlas(DataChannelDescriptor descriptor, RenderTexture atlas) {
			var dsc_name = descriptor.name;
			var path_png = $"Assets/SavedTextureAtlas_{dsc_name}.png";

			var tex_temp = new Texture2D(atlasSize.x, atlasSize.y, TextureFormat.RGBAHalf, true, linear: false);
			var prev_active = RenderTexture.active;
			try {
				AssetDatabase.StartAssetEditing();

				RenderTexture.active = atlas;
				tex_temp.ReadPixels(new Rect(0, 0, atlasSize.x, atlasSize.y), 0, 0, true);
				tex_temp.Apply();
				EditorUtility.SetDirty(tex_temp);
				Selection.SetActiveObjectWithContext(tex_temp, this);
				yield return null;

				//*
				var png_data = tex_temp.EncodeToPNG();
				File.WriteAllBytes(path_png, png_data);
				yield return null;

				AssetDatabase.ImportAsset(path_png, ImportAssetOptions.ForceUpdate |
					ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUncompressedImport);

				var png_asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path_png);
				if (png_asset != null)
					Selection.SetActiveObjectWithContext(png_asset, this);
			} finally {
				RenderTexture.active = prev_active;
				if (tex_temp != null)
					DestroyImmediate(tex_temp);
				AssetDatabase.StopAssetEditing();
			}
			yield return null;

			TextureImporter importer = null;
			for (var i = 0; i < 1000; ++i) {
				importer = AssetImporter.GetAtPath(path_png) as TextureImporter;
				if (importer == null) {
					LogWarning($"Importer of {path_png} is null!");
					yield return null;
				} else {
					break;
				}
			}

			if (importer != null) {
				Selection.SetActiveObjectWithContext(importer, this);
				importer.textureType = descriptor.isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
				importer.alphaIsTransparency = descriptor.alphaIsTransparency;

				importer.mipmapEnabled = true;
				importer.streamingMipmaps = true;
				importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
				importer.mipMapsPreserveCoverage = descriptor.alphaIsTransparency;
				importer.alphaTestReferenceValue = 0.5f;

				importer.filterMode = FilterMode.Trilinear;
				importer.wrapMode = TextureWrapMode.Clamp;

				importer.maxTextureSize = 16384; // TODO
				importer.textureCompression = TextureImporterCompression.CompressedHQ;
				importer.compressionQuality = 100;
				importer.crunchedCompression = false;

				importer.SaveAndReimport();
				yield return null;

				//*/

				/*
				EditorUtility.CompressTexture(tex_asset, TextureFormat.BC7, TextureCompressionQuality.Best);
				tex_asset.Apply();
				EditorUtility.SetDirty(tex_asset);
				var path_asset = $"Assets/SavedTexture_{dsc_name}.asset";
				AssetDatabase.CreateAsset(tex_asset, path_asset);
				yield return null;
				//*/
			}
		}

		protected virtual IEnumerator BakeAtlasNamed(DataChannelDescriptor descriptor) {
			var dsc_name = descriptor.name;
			tex_dst1 = null;
			tex_dst2 = null;
			try {
				// var rt_desc = PrepareRTDescriptor(descriptor);
				// tex_dst1 = new RenderTexture(rt_desc) { name = $"RT_{dsc_name}_A" };
				// tex_dst2 = new RenderTexture(rt_desc) { name = $"RT_{dsc_name}_B" };
				tex_dst1 = MakeRenderTexture(descriptor);
				tex_dst2 = MakeRenderTexture(descriptor);
				Log($"For atlassing \"{dsc_name}\": Created temp buffers: {tex_dst1}, {tex_dst2}");
				Selection.SetActiveObjectWithContext(tex_dst1, this);
				yield return null;

				BakeAtlasBackground(descriptor);
				yield return null;

				foreach (var group in materials.Values) {
					BakeAtlasGroup(descriptor, group);
					yield return null;
				}

				// tex_dst2 больше не будет использоваться, а из tex_dst1 сохраним полученный атлас.
				if (tex_dst2 != null)
					RenderTexture.ReleaseTemporary(tex_dst2); // DestroyImmediate(tex_dst2);
				tex_dst2 = null;
				yield return null;

				var task_save = SaveAtlas(descriptor, tex_dst1);
				while (task_save.MoveNext())
					yield return task_save.Current;
			} finally {
				if (tex_dst1 != null)
					RenderTexture.ReleaseTemporary(tex_dst1); // DestroyImmediate(tex_dst1);
				tex_dst1 = null;
			}
			yield return null;
		}

		protected virtual IEnumerator BakeAtlas() {
			try {
				var shader = Shader.Find("Kawashirov/MaterialCombiner/BlitCopy");
				mat_blit = new Material(shader);
				foreach (var descriptor in descriptors) {
					var task = BakeAtlasNamed(descriptor);
					while (task.MoveNext())
						yield return task.Current;
				}
			} finally {
				if (mat_blit != null)
					DestroyImmediate(mat_blit);
				mat_blit = null;
			}
		}

		public virtual IEnumerator Run() {
			// Инициализируем DataChannelDescriptorы из основного (DataToMatAdapter) адаптера.
			// Эти каналы и будут атлассироваться.
			InitData();
			yield return null;

			// Потом используя фильтры собираем, что мы можем атлассировать в группы.
			// При этом адаптируем материалы.
			FilterGroupAndAdapt();
			yield return null;

			// После этого расчитываем размер каждого материала.
			// Он условный, т.к. текстуры на материале могут быть разных размеров.
			CalcMatSizes();
			yield return null;

			// Затем ишем и расчитываем UV острова.
			// Это делается после выяснения размера текстур, 
			// т.к. острова считаются в пиксельных координатах.
			var task_uv = CalcUVIslands();
			while (task_uv.MoveNext())
				yield return task_uv.Current;

			// Расчёт лайаута аталаса.
			var task_layout = CalcAtlasLayout();
			while (task_layout.MoveNext())
				yield return task_layout.Current;

			// Операции с рендером материалов на атласы.
			var task_bake = BakeAtlas();
			while (task_bake.MoveNext())
				yield return task_bake.Current;
		}
	}
}
#endif
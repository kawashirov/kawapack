#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ICSharpCode.SharpZipLib.Core;
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
		public AbstractMaterialAdapter MainAdapter;
		public AbstractMaterialAdapter[] SecondaryAdapters;
		[Tooltip("Select which texture data channels to atlas. See also OnlyIncludeFilterTextures.")]
		public string[] FilterTextures;
		[Tooltip("When checked only texture data channels from FilterTextures will be used. " +
			"When unchecked only texture data channels that is NOT in FilterTextures will be used.")]
		public bool OnlyIncludeFilterTextures = false;

		[Space]
		[Tooltip("IslandsEpsilonPx and IslandsPaddingPx should be roughly the same and not too differ")]
		public int IslandsEpsilonPx = 8;

		[Tooltip("IslandsEpsilonPx and IslandsPaddingPx should be roughly the same and not too differ")]
		public int IslandsPaddingPx = 8;

		[Tooltip("Powers of 2 recommended (..., 1024, 2048, 4096, ...)")]
		public int MaxAtlasSize = 1024;


		[Tooltip("When checked, will select operating objects and interrupt more frequently for visual feedback.")]
		public bool MoreInfo = false;

		[Header("Properties below are auto-generated")]
		public Texture2D[] OriginalTextures;
		public Material[] OriginalMaterials;
		public Texture2D[] AtlasTextures;
		public Material[] AtlasMaterials;
		public Mesh[] AtlasMeshes;

		/**/
		public List<DataChannelDescriptor> descriptors;
		protected readonly List<RendererGroup> renderers = new List<RendererGroup>();
		protected readonly Dictionary<Material, MaterialSlotGroup> materials = new Dictionary<Material, MaterialSlotGroup>();
		protected readonly HashSet<Material> unadaptable = new HashSet<Material>();
		protected Vector2Int atlasSize = Vector2Int.zero;

		protected Material mat_blit;
		protected RenderTexture tex_dst1 = null;
		protected RenderTexture tex_dst2 = null;

		protected virtual bool DescriptorsPredicate(string name) {
			return OnlyIncludeFilterTextures ? FilterTextures.Contains(name) : !FilterTextures.Contains(name);
		}

		protected virtual void InitData() {
			if (MainAdapter == null) {
				ThrowException(new NullReferenceException($"{nameof(MainAdapter)} is not set!"));
			}

			if (SecondaryAdapters == null || SecondaryAdapters.Length == 0) {
				LogWarning($"{nameof(SecondaryAdapters)} is empty! Auto-adding {nameof(MainAdapter)} {MainAdapter} there.");
				SecondaryAdapters = new AbstractMaterialAdapter[1] { MainAdapter };
			} else if (!SecondaryAdapters.Contains(MainAdapter)) {
				LogWarning($"{nameof(SecondaryAdapters)} doesn't contains {nameof(MainAdapter)}. " +
					$"That's acceptable in specificcases, but might be not that you want.");
			}

			if (1 > MaxAtlasSize || MaxAtlasSize > 16 * 1024) {
				ThrowException(new ArgumentOutOfRangeException($"{nameof(MaxAtlasSize)} must be in range 1 .. {16 * 1024}"));
			}

			if (FilterTextures == null) {
				FilterTextures = new string[0];
			}
			FilterTextures = FilterTextures.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToArray();
			if (OnlyIncludeFilterTextures && FilterTextures.Length < 1) {
				ThrowException(new ArgumentOutOfRangeException(
					$"{nameof(OnlyIncludeFilterTextures)} is ON and {nameof(FilterTextures)} has no elements!"));
			}

			descriptors = MainAdapter.InitDescriptors(s => DescriptorsPredicate(s));
			if (descriptors.Count == 0) {
				ThrowException(new ArgumentException(
					$"{nameof(MainAdapter)} {MainAdapter} returned no data texture descriptors! Nothing to atlas!"));
			}

			Log($"Initialized {descriptors.Count} data channels desciptors materials.");
		}

		protected virtual List<Renderer> CollectRenderers() {
			var gobjs_prime = WholeScene ? gameObject.scene.GetRootGameObjects() : Hierarchy;
			if (gobjs_prime.Length < 1)
				ThrowException(new ArgumentException($"No GameObjects in given scope!"));

			var all_renderers = gobjs_prime.SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).Distinct().ToList();
			if (all_renderers.Count < 1)
				ThrowException(new ArgumentException($"Found no Renderers in given scope!"));

			return all_renderers;
		}

		protected virtual bool TryAdaptMaterial(Material mat, out DataAdapted data) {
			Log($"Adapting material {mat}...");
			data = null;
			foreach (var adapter_i in SecondaryAdapters) {
				// TODO logs & errors
				if (!adapter_i.TryAdaptMaterial(mat, descriptors, out data))
					continue;
				Log($"Found adapter {data.adapter} for material {mat} with {data.data.Count} datas and transform {data.texST}.");
				return true;
			}
			Log($"No adapter found for material {mat}, will be ignored.");
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
				group.epsilonPx = IslandsEpsilonPx;
				group.paddingPx = IslandsPaddingPx;
				materials.Add(mat, group);
				return true;
			} else {
				// Если материал не удалось адаптировать, запоминаем это, что бы не проверять ещё раз.
				unadaptable.Add(mat);
			}
			return false;
		}

		// Возвращает true, если слот был добавлен в группу
		protected virtual MaterialSlotItem ProcessRenderer(RendererGroup group_r, int slot, Material mat) {
			// Если известно, что материал не поддаётся адаптации, 
			// то дальнейшие проверки не имеют смысла.
			if (unadaptable.Contains(mat))
				return null;

			var (renderer, mesh) = (group_r.renderer, group_r.meshOriginal);

			// В первую очередь спрашиваем фильтры.
			if (Filters.Any(f => f.CheckExclude(renderer, mesh, slot, mat)))
				return null;
			if (!Filters.Any(f => f.CheckInclude(renderer, mesh, slot, mat)))
				return null;

			if (TryGetMaterialGroup(mat, out var group)) {
				var item = new MaterialSlotItem(group, renderer, slot, mesh, mat);
				if (!item.EnsureSlotsConsistent(item.meshOriginal, false))
					return null;
				if (!item.EnsureUV2D(item.meshOriginal, false))
					return null;
				group.items.Add(item);
				return item;
			}

			return null;
		}

		protected virtual void ProcessRenderer(Renderer renderer) {
			var group_r = new RendererGroup(this, renderers.Count, renderer);
			if (group_r.GetMesh(renderer) == null) {
				LogWarning($"Renderer {renderer} doesn't have mesh? Skip.", renderer);
				return;
			}
			var slots = renderer.sharedMaterials;
			for (var i = 0; i < slots.Length; ++i) {
				var item = ProcessRenderer(group_r, i, slots[i]);
				if (item != null)
					group_r.Add(item);
				// TODO better logs & exceptions
			}
			if (group_r.items.Count > 0)
				renderers.Add(group_r);
		}

		protected virtual void FilterGroupAndAdapt() {
			Log($"Searching renderers to combine materials on...");
			materials.Clear();
			unadaptable.Clear();
			var all_renderers = CollectRenderers();
			foreach (var renderer in all_renderers) {
				ProcessRenderer(renderer);
			}

			var renderers = materials.Values.SelectMany(g => g.items.Select(i => i.renderer)).Distinct().Count();
			var slots = materials.Values.Sum(v => v.items.Count);
			if (renderers < 1 || slots < 1 || materials.Count < 1) {
				ThrowException(new ArgumentException(
					$"Found {renderers} renderers, {slots} material slots and {materials.Count} materials " +
					$"for atlas after checking {all_renderers.Count} renderers! Nothing to atlas."));
			}

			OriginalTextures = materials.Values.SelectMany(g => g.adapted.data).SelectMany(d => d.dstTex).Distinct().ToArray();
			OriginalMaterials = materials.Keys.ToArray();
			unadaptable.Clear(); // Больше метки нам не понадобятся.
			Log($"Gathered {materials.Count} materials and {slots} material slots from {renderers}/{all_renderers.Count} renderers.");
		}

		protected virtual void CalcMatSizes() {
			foreach (var group in materials.Values) {
				group.CalcTexSize();
			}
			var sizes_s = string.Join("\n", materials.Values.Select(g =>
				$"- {g.matOriginal}: {g.textureSize.x}x{g.textureSize.y} est size, {g.adapted.data.Count} data textures"
			));
			Log($"Detected texture sizes {materials.Count}:\n{sizes_s}");
		}

		protected virtual IEnumerator CalcUVIslands() {
			Log($"Calculating UV islands on {materials.Count} materials...");
			var sw = Stopwatch.StartNew();
			foreach (var group in materials.Values) {
				group.CalcIslands();
				if (MoreInfo || sw.ElapsedMilliseconds > 1000) {
					Selection.SetActiveObjectWithContext(group.matOriginal, this);
					yield return null;
					sw.Restart();
				}
			}
			sw.Stop();

			var sum = materials.Values.Select(g => g.IslandsCount()).Sum();
			MaterialSlotGroup.ResetBuffers(); // Пока не понадобятся.
			var islands_s = string.Join("\n", materials.Values.Select(g =>
				$"- {g.matOriginal}: {g.islandsOriginal.Count} UV islands, " +
				$"{g.debugUVPushes} pushes, {g.debugUVIters} iterations."
			));
			Log($"Got {sum} UV islands total from {materials.Count} materials:\n{islands_s}");
			yield return null; // No MoreInfo
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
				Log($"Packing {islands.Count} islands total into the atlas...");
				var format = TextureFormat.R8;
				atlas_tex = new Texture2D(1, 1, format, false);
				dull_tex = islands.Select(x => x.isl.MakeDullTex(format)).ToArray();
				// https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Texture2D.PackTextures.html
				if (MoreInfo) {
					Selection.SetActiveObjectWithContext(atlas_tex, this);
					yield return null;
				}
				var results = atlas_tex.PackTextures(dull_tex, 0, MaxAtlasSize);
				if (MoreInfo) {
					Selection.SetActiveObjectWithContext(atlas_tex, this);
					yield return null;
				}
				atlasSize = new Vector2Int(atlas_tex.width, atlas_tex.height);
				var islands_str = "";
				// Размеры и индексы islands[], dull_tex[] и results[] совпадают.
				for (var i = 0; i < results.Length; ++i) {
					var (grp, isl, idx) = islands[i];
					var atlas_rect = results[i];
					var atlas_island = new UVIsland(atlas_rect);
					var dt = dull_tex[i]; // ({dt}, {dt.width}x{dt.height})
					islands_str += $"\n- №{i}: {grp.matOriginal}, №{idx}:\n\t" +
						$"{isl} -> (dull {dt.width}x{dt.height}) -> {atlas_rect} -> {atlas_island}";
					grp.islandsAtlas[idx] = atlas_island;
				}
				Log($"Packed {islands.Count} islands to {atlasSize.x}x{atlasSize.y} atlas: {islands_str}");
			} finally {
				if (dull_tex != null)
					foreach (var dull in dull_tex)
						if (dull != null)
							DestroyImmediate(dull);
				if (atlas_tex)
					DestroyImmediate(atlas_tex);
			}
			if (MoreInfo) {
				yield return null;
			}
		}

		protected virtual RenderTexture AtlasMakeRT_(DataChannelDescriptor descriptor) {
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

		protected virtual RenderTexture AtlasMakeRT(DataChannelDescriptor descriptor) {
			return RenderTexture.GetTemporary(atlasSize.x, atlasSize.y, 0,
				RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.sRGB);
		}

		protected virtual void AtlasBlitBackground(DataChannelDescriptor descriptor) {
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

		protected virtual void AtlasBlitGroup(DataChannelDescriptor descriptor, MaterialSlotGroup group) {
			var dsc_name = descriptor.name;
			if (group.islandsAtlas.Count < 1)
				return; // Группа может быть пустой.

			Log($"Trying to render data \"{dsc_name}\" of material {group.matOriginal}...");
			var datas = group.adapted.data.Where(d => d.descriptor == descriptor).ToArray();
			var mat_original = group.matOriginal;
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
				var island_atlas = group.islandsAtlas[islands_i]; // 0..1 coords
				var vec_source = island_source.ToVector4Norm(src_tex_size.x, src_tex_size.y);
				var vec_atlas = island_atlas.ToVector4();
				mat_blit.SetVector("_SourceRect", vec_source);
				mat_blit.SetVector("_TargetRect", vec_atlas);
				mat_blit.SetTexture("_TargetTex", tex_dst1);
				Log($"Blitting {mat_original}/{dsc_name}/{islands_i}: {island_source}/{vec_source} -> {vec_atlas}");
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

		protected virtual Texture2D AtlasRTToTexture2D(DataChannelDescriptor descriptor, RenderTexture atlas) {
			var tex_temp = new Texture2D(atlasSize.x, atlasSize.y, TextureFormat.RGBAHalf, true, linear: false);
			var prev_active = RenderTexture.active;
			try {
				RenderTexture.active = atlas;
				tex_temp.ReadPixels(new Rect(0, 0, atlasSize.x, atlasSize.y), 0, 0, true);
				tex_temp.Apply();
				EditorUtility.SetDirty(tex_temp);
				Selection.SetActiveObjectWithContext(tex_temp, this);
			} finally {
				RenderTexture.active = prev_active;
			}
			return tex_temp;
		}

		protected virtual Texture2D AtlasReImportPNG(string path_png, bool compress) {
			Log($"(Re)importing (compress={compress}) \"{path_png}\"...");

			var flags = ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport;
			if (!compress)
				flags |= ImportAssetOptions.ForceUncompressedImport;
			AssetDatabase.ImportAsset(path_png, flags);

			var png_asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path_png);
			if (png_asset != null)
				Selection.SetActiveObjectWithContext(png_asset, this);
			return png_asset;
		}

		protected virtual bool AtlasConfigureImporter(DataChannelDescriptor descriptor, string path_png) {
			var importer = AssetImporter.GetAtPath(path_png) as TextureImporter;
			if (importer == null) {
				LogWarning($"No TextureImporter at \"{path_png}\", need reimport?");
				return true; // repeat
			}
			Log($"Configuring {importer} at \"{path_png}\"...");

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

			AssetDatabase.WriteImportSettingsIfDirty(path_png);
			Log($"Configured {importer} at \"{path_png}\".");
			return false; // no repeat
		}

		protected virtual IEnumerator AtlasBakeNamed(DataChannelDescriptor descriptor) {
			var dsc_name = descriptor.name;
			tex_dst1 = null;
			tex_dst2 = null;
			EditorUtility.UnloadUnusedAssetsImmediate(true); // save ram
			var sw = Stopwatch.StartNew();
			try {
				// var rt_desc = PrepareRTDescriptor(descriptor);
				// tex_dst1 = new RenderTexture(rt_desc) { name = $"RT_{dsc_name}_A" };
				// tex_dst2 = new RenderTexture(rt_desc) { name = $"RT_{dsc_name}_B" };
				tex_dst1 = AtlasMakeRT(descriptor);
				tex_dst2 = AtlasMakeRT(descriptor);
				Log($"For atlassing \"{dsc_name}\": Created temp buffers: {tex_dst1}, {tex_dst2}");
				if (MoreInfo || sw.ElapsedMilliseconds > 1000) {
					Selection.SetActiveObjectWithContext(tex_dst1, this);
					yield return null;
					sw.Reset();
				}

				AtlasBlitBackground(descriptor);
				if (MoreInfo || sw.ElapsedMilliseconds > 1000) {
					Selection.SetActiveObjectWithContext(tex_dst1, this);
					yield return null;
					sw.Reset();
				}

				foreach (var group in materials.Values) {
					AtlasBlitGroup(descriptor, group);
					if (MoreInfo || sw.ElapsedMilliseconds > 1000) {
						Selection.SetActiveObjectWithContext(tex_dst1, this);
						yield return null;
						sw.Reset();
					}
				}
				Log($"Rendered everything for \"{dsc_name}\"...");
			} finally {
				// tex_dst2 больше не будет использоваться, а из tex_dst1 сохраним полученный атлас.
				if (tex_dst2 != null)
					RenderTexture.ReleaseTemporary(tex_dst2); // DestroyImmediate(tex_dst2);
				tex_dst2 = null;
			}
			if (MoreInfo || sw.ElapsedMilliseconds > 1000) {
				Selection.SetActiveObjectWithContext(tex_dst1, this);
				yield return null;
				sw.Reset();
			}
		}


		protected virtual IEnumerator AtlasBakeSave(DataChannelDescriptor descriptor) {
			var dsc_name = descriptor.name;
			var path_png = $"Assets/SavedTextureAtlas_{dsc_name}.png"; // TODO

			// Сначала записываем в path_png "болванку".
			var sw = Stopwatch.StartNew();
			Texture2D dull_tex = null;
			try {
				dull_tex = new Texture2D(4, 4);
				File.WriteAllBytes(FileUtil.GetPhysicalPath(path_png), dull_tex.EncodeToPNG());
			} catch (Exception exc) {
				LogException($"Failed to save dull texture for \"{dsc_name}\" as \"{path_png}\".", exc);
				throw exc;
			} finally {
				if (dull_tex != null)
					DestroyImmediate(dull_tex);
				dull_tex = null;
			}

			// Убеждаемся, что ассет появился в датабазе, если что - ждём...
			while (AtlasReImportPNG(path_png, false) == null) {
				yield return null;
				sw.Reset();
			}

			// Тут пауза должна быть обязательно иначе срет ошибкой
			// Message: Build asset version error: <файлик> in SourceAssetDB 
			// has modification time of '<раньше>' while content on disk has modification time of '<позже>'
			// И нихуя не делает, не реимпортирует.
			// ХЗ почему, возможно из-за StartAssetEditing / StopAssetEditing, 
			// но без этого оно импортирует несколько раз.
			// ImportPNG всёравно должен быть перед 

			// Настраиваем импортер. Он должен быть к этому моменту, но если нет - переимпортируем и ждём...
			while (AtlasConfigureImporter(descriptor, path_png)) {
				AtlasReImportPNG(path_png, false);
				yield return null;
				sw.Reset();
			}

			// Импортер изменён и можно заимпортировать с корректными настройками.
			AtlasReImportPNG(path_png, false);
			// А теперь можно перезаписать картинку уже норм данными и она сразу заимпортися с норм настройками.

			if (MoreInfo || sw.ElapsedMilliseconds > 1000) {
				yield return null;
				sw.Reset();
			}

			// Но для начала конвертируем RenderTexture -> Texture2D
			var tex_temp = AtlasRTToTexture2D(descriptor, tex_dst1);

			if (MoreInfo || sw.ElapsedMilliseconds > 1000) {
				Selection.SetActiveObjectWithContext(tex_temp, this);
				yield return null;
				sw.Reset();
			}

			// Теперь tex_dst1 больше не нужна.
			RenderTexture.ReleaseTemporary(tex_dst1); // DestroyImmediate(tex_dst1);
			tex_dst1 = null;

			if (MoreInfo || sw.ElapsedMilliseconds > 1000) {
				yield return null;
				sw.Reset();
			}

			// Теперь кодируем Texture2D -> byte[].
			byte[] data = null;
			int data_length = 0;
			try {
				data = tex_temp.EncodeToPNG();
				data_length = data.Length;
			} catch (Exception exc) {
				LogException($"Failed to encode {tex_temp} for \"{dsc_name}\" as PNG.", exc);
				throw exc;
			}

			// Теперь tex_temp больше не нужна.
			DestroyImmediate(tex_temp);
			tex_temp = null;

			if (MoreInfo || sw.ElapsedMilliseconds > 1000) {
				yield return null;
				sw.Reset();
			}

			// Теперь сохраняем byte[] -> path_png.
			try {
				File.WriteAllBytes(FileUtil.GetPhysicalPath(path_png), data);
			} catch (Exception exc) {
				LogException($"Failed to save {tex_temp} for \"{dsc_name}\" encoded as {data_length} bytes as \"{path_png}\".", exc);
				throw exc;
			} finally {
				data = null; // Помогаем сборщику мусора.
			}

			if (MoreInfo || sw.ElapsedMilliseconds > 1000) {
				yield return null;
				sw.Reset();
			}

			// Наконец переимпортируем в последний раз.
			Texture2D png_asset;
			while (true) {
				png_asset = AtlasReImportPNG(path_png, true);
				if (png_asset != null && png_asset.width == atlasSize.x && png_asset.height == atlasSize.y)
					break;
				yield return null; // Принудительная пауза
			}
			descriptor.atlasTexture = png_asset;

			if (MoreInfo || sw.ElapsedMilliseconds > 1000) {
				Selection.SetActiveObjectWithContext(png_asset, this);
				yield return null;
				sw.Reset();
			}
		}

		protected virtual IEnumerator AtlasBake() {
			AtlasTextures = new Texture2D[0];
			var shader = Shader.Find("Kawashirov/MaterialCombiner/BlitCopy");
			try {
				mat_blit = new Material(shader);
				foreach (var descriptor in descriptors) {
					var task_bake = AtlasBakeNamed(descriptor);
					while (task_bake.MoveNext())
						yield return task_bake.Current;
					var task_save = AtlasBakeSave(descriptor);
					while (task_save.MoveNext())
						yield return task_save.Current;
				}
			} finally {
				if (mat_blit != null)
					DestroyImmediate(mat_blit);
				mat_blit = null;
			}
		}

		public virtual Material ConvertMaterial(Material original) {
			foreach (var mat_atlas_ext in AtlasMaterials) {
				if (MainAdapter.IsCompatible(original, mat_atlas_ext))
					return mat_atlas_ext;
			}

			var mat_atlas = MainAdapter.MakeNewAtlasMaterial(original);
			foreach (var mat_atlas_ext in AtlasMaterials) {
				// Может получиться так, что новый 
				if (MainAdapter.IsCompatible(mat_atlas, mat_atlas_ext)) {
					DestroyImmediate(mat_atlas);
					return mat_atlas_ext;
				}
			}
			mat_atlas.name = $"SavedTextureAtlas_{AtlasMaterials.Length}";
			var mat_atlas_path = $"Assets/{mat_atlas.name}.mat"; // TODO
			AssetDatabase.CreateAsset(mat_atlas, mat_atlas_path);
			Log($"Saved new atlas material {mat_atlas} as \"{mat_atlas_path}\"", mat_atlas);
			AtlasMaterials = AtlasMaterials.Append(mat_atlas).ToArray();
			return mat_atlas;
		}

		protected virtual void ConvertMaterials() {
			AtlasMaterials = new Material[0];
			var report = new List<string>(materials.Count);
			foreach (var group in materials.Values) {
				group.matAtlas = ConvertMaterial(group.matOriginal);
				report.Add($"- {group.matOriginal} -> {group.matAtlas}.");
			}
			Log($"Converted {report.Count} materials:\n" + string.Join("\n", report));
		}

		protected virtual IEnumerator AtlasApply() {
			var items_c = materials.Values.Sum(g => g.items.Count);
			Log($"Applying UV transforms to {items_c} temp mat slot meshes...");
			var sw = Stopwatch.StartNew();
			foreach (var mat_group in materials.Values) {
				mat_group.ApplyMatAndUV();
				if (MoreInfo || sw.ElapsedMilliseconds > 1000) {
					yield return null;
					sw.Reset();
				}
			}
			if (MoreInfo || sw.ElapsedMilliseconds > 500) {
				yield return null;
				sw.Reset();
			}

			Log($"Applied UV transforms to {items_c} temp mat slot meshes, processing {renderers.Count} renderers...");

			foreach (var r_group in renderers) {
				r_group.RecombineMeshes();
				r_group.SetMeshAtlas();
				r_group.ApplyMaterials();
				if (MoreInfo || sw.ElapsedMilliseconds > 1000) {
					Selection.SetActiveObjectWithContext(r_group.renderer, this);
					yield return null;
					sw.Reset();
				}
			}
			AtlasMeshes = renderers.Select(rg => rg.meshAtlas).ToArray();
			Log($"Applied atlas to {materials.Count} materials, {items_c} slots, generated {AtlasMeshes.Length} meshes.");
			yield return null;
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
			var task_bake = AtlasBake();
			while (task_bake.MoveNext())
				yield return task_bake.Current;

			// После рендера можно сконверить материалы.
			ConvertMaterials();
			yield return null;

			// И применить на меши.
			var task_apply = AtlasApply();
			while (task_apply.MoveNext())
				yield return task_apply.Current;

		}
	}
}
#endif
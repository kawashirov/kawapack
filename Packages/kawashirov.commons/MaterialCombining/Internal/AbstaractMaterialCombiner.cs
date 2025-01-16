#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

using Object = UnityEngine.Object;

namespace Kawashirov.MaterialCombining {
	public abstract class AbstaractMaterialCombiner : KawaEditorBehaviour {
		public static readonly string BLIT_SHADER_GUID = "fcbb4213b7350534397157f990cd24b7";
		public enum AtlasLayoutBackend {
			PackTextures, GenerateAtlasPerfect, GenerateAtlasDense
		}

		public bool WholeScene = false;
		public List<GameObject> Hierarchy = new List<GameObject>();

		public List<AbstractMaterialFilter> Filters = new List<AbstractMaterialFilter>();

		public AtlasLayoutBackend AtlasLayout = AtlasLayoutBackend.PackTextures;
		[Range(0, 16)] public int IslandsAlignPx = 8;
		[Range(0, 16)] public int IslandsEpsilonPx = 8;
		[Range(0, 16)] public int IslandsPaddingPx = 8;
		[Range(4, 16 * 1024)] public int MaxAtlasSize = 4096;
		public List<Material> AtlasDebugMaterials = new List<Material>();

		public float MaxStallTime = 1;

		public bool SaveMeshes = false;
		public bool SaveMaterials = false;

		public bool UniqueAssetNames = false;

		public List<Texture2D> OriginalTextures = new List<Texture2D>();
		public List<Material> OriginalMaterials = new List<Material>();
		public List<Texture2D> AtlasTextures = new List<Texture2D>();
		public List<Material> AtlasMaterials = new List<Material>();
		public List<Mesh> AtlasMeshes = new List<Mesh>();

		/* * * */

		protected int stallTimeMS = 1000;
		protected readonly List<AbstractAtlasRenderer> atlasRenderers = new List<AbstractAtlasRenderer>();
		protected readonly List<RendererGroup> rendererGroups = new List<RendererGroup>();
		protected readonly Dictionary<Material, MaterialGroup> materialGroups = new Dictionary<Material, MaterialGroup>();
		protected readonly HashSet<Material> unadaptable = new HashSet<Material>();
		protected Vector2Int atlasSize = Vector2Int.zero;

		protected Material matBlit;
		protected RenderTexture texRT1 = null;
		protected RenderTexture texRT2 = null;
		internal string sceneDir = null;

		public bool ShouldYield(Stopwatch sw) {
			return stallTimeMS == 0 || (sw != null && sw.ElapsedMilliseconds > stallTimeMS);
		}

		protected virtual void InitChecks() {
			if (1 > MaxAtlasSize || MaxAtlasSize > 16 * 1024) {
				ThrowException(new ArgumentOutOfRangeException($"{nameof(MaxAtlasSize)} must be in range 1 .. {16 * 1024}"));
			}

			stallTimeMS = MaxStallTime > 0 ? Mathf.RoundToInt(Mathf.Clamp(MaxStallTime, 1f / 60, 10f) * 1000) : 0;

			OriginalTextures.Clear();
			OriginalMaterials.Clear();
			AtlasTextures.Clear();
			AtlasMaterials.Clear();
			AtlasMeshes.Clear();
			SetDirty();
		}

		public virtual int GetUVChannel() => 0;

		protected abstract IEnumerable<AbstractAtlasRenderer> YieldAtlasRenderers();

		public virtual List<AbstractAtlasRenderer> InitAtlasRenderers() {
			atlasRenderers.Clear();
			atlasRenderers.AddRange(YieldAtlasRenderers());
			return atlasRenderers;
		}

		protected virtual void InitSceneDir() {
			var scene = gameObject.scene;
			var scene_path = scene.path;
			if (string.IsNullOrWhiteSpace(scene_path))
				ThrowException(new Exception("Current scene path is null or empty!"));
			var scene_filename = Path.GetFileNameWithoutExtension(scene_path);
			if (!string.Equals(scene_filename, scene.name))
				ThrowException(new Exception($"Current scene name \"{scene.name}\" is not the same as filename \"{scene_filename}\"!"));
			scene_path = Path.GetDirectoryName(scene_path) + "/" + scene_filename;
			sceneDir = FileUtil.GetLogicalPath(scene_path);
			var scene_path_phys = FileUtil.GetPhysicalPath(sceneDir);
			if (!Directory.Exists(scene_path_phys))
				Directory.CreateDirectory(scene_path_phys);
			AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
		}

		protected virtual List<Renderer> CollectRenderers() {
			List<GameObject> gobjs_prime;
			if (WholeScene) {
				gobjs_prime = new List<GameObject>();
				gameObject.scene.GetRootGameObjects(gobjs_prime);
			} else {
				gobjs_prime = Hierarchy;
			}

			if (gobjs_prime.Count < 1) {
				LogWarning($"No GameObjects in given scope! (WholeScene={WholeScene}, Hierarchy={Hierarchy})");
				return new List<Renderer>(0);
			}

			var all_renderers = gobjs_prime.SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).Distinct().ToList();
			if (all_renderers.Count < 1) {
				LogWarning($"Found no Renderers in given scope! (WholeScene={WholeScene}, Hierarchy={Hierarchy})");
				return new List<Renderer>(0);
			}

			LogDebug($"Found {all_renderers.Count} total potential renderers...");
			return all_renderers;
		}

		protected abstract bool CanAdaptMaterial(Material mat);

		protected virtual bool TryGetMaterialGroup(Material mat, out MaterialGroup group) {
			if (materialGroups.TryGetValue(mat, out group)) {
				// Группа уже существует, значит её материал уже адаптирован, а значит доп. проверки не нужны.
				return true;
			} else if (CanAdaptMaterial(mat)) {
				// Создание новый группы, если удалось адаптировать.
				group = new MaterialGroup(this, mat) {
					alignPx = IslandsAlignPx,
					epsilonPx = IslandsEpsilonPx,
					paddingPx = IslandsPaddingPx
				};
				materialGroups.Add(mat, group);
				return true;
			} else {
				// Если материал не удалось адаптировать, запоминаем это, что бы не проверять ещё раз.
				unadaptable.Add(mat);
			}
			return false;
		}

		// Возвращает true, если слот был добавлен в группу
		protected virtual MaterialSlotItem ProcessRenderer(RendererGroup group_r, int slot, Material mat) {
			if (mat == null) {
				LogWarning($"Renderer={group_r.renderer}, slot={slot} have null/destroyed material!", group_r.renderer);
				return null;
			}

			// Если известно, что материал не поддаётся адаптации, 
			// то дальнейшие проверки не имеют смысла.
			if (unadaptable.Contains(mat))
				return null;

			// Пропустить отладочные материалы.
			if (AtlasDebugMaterials.Contains(mat)) {
				unadaptable.Add(mat);
				return null;
			}

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
			var group_r = new RendererGroup(this, rendererGroups.Count, renderer);
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
				rendererGroups.Add(group_r);
		}

		protected virtual void FilterGroupAndAdapt() {
			var begin = Stopwatch.StartNew();
			LogDebug($"Searching renderers to combine materials on...");
			materialGroups.Clear();
			unadaptable.Clear();
			var all_renderers = CollectRenderers();
			if (all_renderers.Count < 1)
				return;

			foreach (var renderer in all_renderers) {
				ProcessRenderer(renderer);
			}

			var renderers = materialGroups.Values.SelectMany(g => g.items.Select(i => i.renderer)).Distinct().Count();
			var slots = materialGroups.Values.Sum(v => v.items.Count);
			if (renderers < 1 || slots < 1 || materialGroups.Count < 1) {
				LogWarning($"Found {renderers} renderers, {slots} material slots and {materialGroups.Count} materials " +
					$"for atlas after checking {all_renderers.Count} renderers! Nothing to atlas.");
				return;
			}

			OriginalTextures.Clear();
			// OriginalTextures.AddRange(materialGroups.Values.SelectMany(g => g.adapted.data)
			// 	.SelectMany(d => d.dstTex).Distinct());
			OriginalMaterials.Clear();
			OriginalMaterials.AddRange(materialGroups.Keys);
			SetDirty();
			unadaptable.Clear(); // Больше метки нам не понадобятся.
			Log($"Gathered {materialGroups.Count} materials and {slots} material slots " +
				$"from {renderers}/{all_renderers.Count} renderers in {begin.Elapsed}.");
		}

		protected abstract Vector2Int CalcMatSize(MaterialGroup mat_group);

		protected abstract Vector4 CalcMatST(MaterialGroup mat_group);

		protected virtual void CalcMatSizesAndST() {
			var begin = Stopwatch.StartNew();

			var min_size = Mathf.Max(IslandsPaddingPx, IslandsEpsilonPx);
			var rem_size = min_size % IslandsAlignPx;
			if (min_size != 0)
				min_size += IslandsAlignPx - rem_size;

			var sizes = new List<string>();
			foreach (var mat_group in materialGroups.Values) {
				var size = CalcMatSize(mat_group);
				var st = CalcMatST(mat_group);
				// TODO это может вызвать диспропорцию
				size.x = Mathf.Max(size.x, min_size);
				size.y = Mathf.Max(size.y, min_size);
				mat_group.textureSize = size;
				mat_group.texST = st;
				sizes.Add($"- {mat_group.matOriginal}: size={size.x}x{size.y}, st={st}");
			}
			var sizes_s = string.Join("\n", sizes);
			Log($"Detected sizes of {materialGroups.Count} materials in {begin.Elapsed}:\n{sizes_s}");
		}

		protected virtual IEnumerator CalcUVIslands() {
			LogDebug($"Calculating UV islands on {materialGroups.Count} materials...");
			var sw = Stopwatch.StartNew();
			foreach (var group in materialGroups.Values) {
				group.CalcIslands();
				if (ShouldYield(sw)) {
					yield return SelectFocus(group.matOriginal);
					sw.Restart();
				}
			}
			sw.Stop();

			var sum = materialGroups.Values.Select(g => g.IslandsCount()).Sum();
			MaterialGroup.ResetBuffers(); // Пока не понадобятся.
			var islands_s = string.Join("\n", materialGroups.Values.Select(g =>
				$"- {g.matOriginal}: {g.islandsOriginal.Count} UV islands, " +
				$"{g.debugUVPushes} pushes, {g.debugUVIters} iterations."
			));
			Log($"Got {sum} UV islands total from {materialGroups.Count} materials:\n{islands_s}");
			yield return null;
		}

		protected virtual IEnumerator CalcAtlasLayout_PackTextures() {
			var begin = Stopwatch.StartNew();
			Texture2D[] dull_tex = null;
			Texture2D atlas_tex = null;
			// (группа, расширеный остров в коордах ориг тексткры, номер острова в списке группы)
			var islands = materialGroups.Values.SelectMany(
				grp => grp.islandsPadded.Select((isl, idx) => (grp, isl, idx))
			).ToList();
			Log($"Packing {islands.Count} islands total into the max {MaxAtlasSize}x{MaxAtlasSize} atlas " +
				$"using {nameof(CalcAtlasLayout_PackTextures)}...");
			var islands_str = "";
			// Используем схему с фейковыми текстурами и Texture2D.PackTextures для вычисления лайаута.
			try {
				var format = TextureFormat.R8;
				atlas_tex = new Texture2D(1, 1, format, false);
				dull_tex = islands.Select(x => x.isl.MakeDullTex(format)).ToArray();
				// https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Texture2D.PackTextures.html
				if (ShouldYield(null))
					yield return SelectFocus(atlas_tex);
				var results = atlas_tex.PackTextures(dull_tex, 0, MaxAtlasSize);
				if (ShouldYield(null))
					yield return SelectFocus(atlas_tex);
				atlasSize = new Vector2Int(atlas_tex.width, atlas_tex.height);
				// Размеры и индексы islands[], dull_tex[] и results[] совпадают.
				for (var i = 0; i < results.Length; ++i) {
					var (grp, isl, idx) = islands[i];
					var atlas_rect = results[i];
					var atlas_island = grp.islandsAtlas[idx] = new UVIsland(atlas_rect);
					// var dt = dull_tex[i]; // ({dt}, {dt.width}x{dt.height})
					islands_str += $"\n- №{i}: {grp.matOriginal}, №{idx}: {isl} -> {atlas_rect} -> {atlas_island}";
				}
			} finally {
				if (dull_tex != null)
					foreach (var dull in dull_tex)
						if (dull != null)
							DestroyImmediate(dull);
				if (atlas_tex)
					DestroyImmediate(atlas_tex);
			}
			if (ShouldYield(null))
				yield return null;
			Log($"Packed {islands.Count} islands to {atlasSize.x}x{atlasSize.y} atlas " +
				$"using {nameof(CalcAtlasLayout_PackTextures)} in {begin.Elapsed}:" +
				islands_str);
		}

		protected virtual IEnumerator CalcAtlasLayout_GenerateAtlas_Dense() {
			var begin = Stopwatch.StartNew();
			var islands = materialGroups.Values.SelectMany(
				grp => grp.islandsPadded.Select((isl, idx) => (grp, isl, idx))
			).ToList();
			Log($"Packing {islands.Count} islands total into the max {MaxAtlasSize}x{MaxAtlasSize} atlas " +
				$"using {nameof(CalcAtlasLayout_GenerateAtlas_Dense)}...");

			var sizes = islands.Select(x => x.isl.Size()).ToArray();
			var size = Mathf.NextPowerOfTwo(sizes.Select(
				v => Mathf.RoundToInt(Mathf.Max(v.x, v.y))
			).Max()) / 2; // Наибольшая степень 2 в которую точно не поместится

			var results = new List<Rect>(sizes.Length);
			var atlas_task = AtlasUtility.GenerateAtlasIterAsync(sizes, 1, size, results, 1.1f, this);
			while (atlas_task.MoveNext()) {
				size = atlas_task.Current;
				if (ShouldYield(null))
					yield return null;
			}

			var size_r = results.Select(r => Mathf.Max(r.xMax, r.yMax)).Max();
			Assert.IsTrue(size_r <= size, $"size_r={size_r}, size={size}");
			atlasSize = Vector2Int.one * MaxAtlasSize;

			var islands_str = "";
			for (var i = 0; i < results.Count; ++i) {
				var (grp, isl, idx) = islands[i];
				var pack_rect = results[i];
				var atlas_island = grp.islandsAtlas[idx] = new UVIsland(pack_rect, size_r);
				islands_str += $"\n- №{i}: {grp.matOriginal}, №{idx}: {isl} -> {pack_rect} -> {atlas_island}";
			}
			Log($"Found packing of {islands.Count} islands in " +
				$"{size_r}x{size_r} / {size}x{size} / {atlasSize.x}x{atlasSize.y} area " +
				$"using {nameof(CalcAtlasLayout_GenerateAtlas_Dense)} in {begin.Elapsed}:" +
				islands_str);
		}

		protected virtual IEnumerator CalcAtlasLayout_GenerateAtlas_PerfectPow2() {
			var begin = Stopwatch.StartNew();
			var islands = materialGroups.Values.SelectMany(
				grp => grp.islandsPadded.Select((isl, idx) => (grp, isl, idx))
			).ToList();
			Log($"Packing {islands.Count} islands total into the max {MaxAtlasSize}x{MaxAtlasSize} atlas " +
				$"using {nameof(CalcAtlasLayout_GenerateAtlas_PerfectPow2)}...");

			var sizes = islands.Select(x => x.isl.Size()).ToArray();
			var size = Mathf.NextPowerOfTwo(sizes.Select(v =>
				Mathf.CeilToInt(Mathf.Max(v.x, v.y))
			).Max()) / 2; // Наибольшая степень 2 в которую точно не поместится

			var results = new List<Rect>(sizes.Length);
			var atlas_task = AtlasUtility.GenerateAtlasIterAsync(sizes, 1, size, results, 2.0f, this);
			while (atlas_task.MoveNext()) {
				size = atlas_task.Current;
				if (ShouldYield(null))
					yield return null;
			}

			var size_r = results.Select(r => Mathf.Max(r.xMax, r.yMax)).Max();
			Assert.IsTrue(size_r <= size, $"size_r={size_r}, size={size}");
			atlasSize = Vector2Int.one * Mathf.Min(MaxAtlasSize, size);

			var islands_str = "";
			for (var i = 0; i < results.Count; ++i) {
				var (grp, isl, idx) = islands[i];
				var pack_rect = results[i];
				var atlas_island = grp.islandsAtlas[idx] = new UVIsland(pack_rect, size);
				islands_str += $"\n- №{i}: {grp.matOriginal}, №{idx}: {isl} -> {pack_rect} -> {atlas_island}";
			}
			Log($"Found packing of {islands.Count} islands in " +
				$"{size_r}x{size_r} / {size}x{size} / {atlasSize.x}x{atlasSize.y} area " +
				$"using {nameof(CalcAtlasLayout_GenerateAtlas_PerfectPow2)} in {begin.Elapsed}:" +
				islands_str);
		}

		protected virtual RenderTexture AtlasMakeRT_() {
			// Какая-то хуйня, когда через RenderTextureDescriptor инициализирую, то нихуя не работает.
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
			// rt_desc.sRGB = desc.sRGB;
			rt_desc.height = atlasSize.x;
			rt_desc.width = atlasSize.y;

			return RenderTexture.GetTemporary(rt_desc);
		}

		protected virtual RenderTexture AtlasMakeRT() {
			return RenderTexture.GetTemporary(atlasSize.x, atlasSize.y, 0,
				RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
		}

		protected virtual void AtlasBlitBackground(AbstractAtlasRenderer renderer) {
			renderer.BlitPrepareReset(matBlit);
			renderer.PrepareRenderBackground(matBlit);

			var full = new Vector4(0, 0, 1, 1);
			matBlit.SetVector("_SourceRect", full);
			matBlit.SetVector("_TargetRect", full);

			matBlit.SetTexture("_TargetTex", texRT1);
			LogDebug($"Blitting \"{renderer.name}\" default background...");
			Graphics.Blit(Texture2D.whiteTexture, texRT2, matBlit);
			(texRT1, texRT2) = (texRT2, texRT1); // swap buffers
			SelectFocus(texRT1);
			LogDebug($"Blitting \"{renderer.name}\" default background...");
		}

		protected virtual void AtlasBlitGroup(AbstractAtlasRenderer atlas_renderer, MaterialGroup group) {
			var dsc_name = atlas_renderer.name;
			if (group.islandsAtlas.Count < 1)
				return; // Группа может быть пустой.

			LogDebug($"Trying to render data \"{dsc_name}\" of material {group.matOriginal}...");
			var mat_original = group.matOriginal;

			atlas_renderer.BlitPrepareReset(matBlit);
			atlas_renderer.PrepareRenderMaterial(group, matBlit);

			for (var islands_i = 0; islands_i < group.islandsAtlas.Count; ++islands_i) {
				var island_source = group.islandsPadded[islands_i]; // pixel coords
				var island_atlas = group.islandsAtlas[islands_i]; // 0..1 coords
				var vec_source = group.PxToNorm(island_source);
				var vec_atlas = island_atlas.ToVector4();
				matBlit.SetVector("_SourceRect", vec_source);
				matBlit.SetVector("_TargetRect", vec_atlas);
				matBlit.SetTexture("_TargetTex", texRT1);
				LogDebug($"Blitting {mat_original}/{dsc_name}/{islands_i}: {island_source}/{vec_source} -> {vec_atlas}");
				Graphics.Blit(Texture2D.whiteTexture, texRT2, matBlit);
				(texRT1, texRT2) = (texRT2, texRT1); // swap buffers
				SelectFocus(texRT1);
			}
		}

		protected virtual IEnumerator AtlasBakeNamed(AbstractAtlasRenderer atlas_renderer) {
			var ar_name = atlas_renderer.name;
			var begin = Stopwatch.StartNew();
			texRT1 = null;
			texRT2 = null;
			EditorUtility.UnloadUnusedAssetsImmediate(true); // save ram
			var sw = Stopwatch.StartNew();
			var capture = ExternalGPUProfiler.IsAttached() && atlas_renderer.BlitDebugCapture();
			try {
				if (capture)
					ExternalGPUProfiler.BeginGPUCapture();

				texRT1 = AtlasMakeRT();
				texRT2 = AtlasMakeRT();
				LogDebug($"For atlassing \"{ar_name}\": Created temp buffers: {texRT1}, {texRT2}");
				if (!capture && ShouldYield(sw)) {
					yield return SelectFocus(texRT1);
					sw.Reset();
				}

				AtlasBlitBackground(atlas_renderer);
				if (!capture && ShouldYield(sw)) {
					yield return SelectFocus(texRT1);
					sw.Reset();
				}

				foreach (var group in materialGroups.Values) {
					AtlasBlitGroup(atlas_renderer, group);
					if (!capture && ShouldYield(sw)) {
						yield return SelectFocus(texRT1);
						sw.Reset();
					}
				}
			} finally {
				if (capture)
					ExternalGPUProfiler.EndGPUCapture();
				// tex_dst2 больше не будет использоваться, а из tex_dst1 сохраним полученный атлас.
				if (texRT2 != null)
					RenderTexture.ReleaseTemporary(texRT2); // DestroyImmediate(tex_dst2);
				texRT2 = null;
			}
			if (ShouldYield(sw)) {
				yield return SelectFocus(texRT1);
				sw.Reset();
			}
			Log($"Rendered everything for \"{ar_name}\" in {begin.Elapsed}.");
		}

		protected virtual Texture2D AtlasReImportPNG(string path, bool compress) {
			LogDebug($"(Re)importing (compress={compress}) \"{path}\"...");

			var flags = ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport;
			if (!compress)
				flags |= ImportAssetOptions.ForceUncompressedImport;
			AssetDatabase.ImportAsset(path, flags);

			var png_asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
			if (png_asset != null)
				SelectFocus(png_asset);
			return png_asset;
		}

		protected virtual bool AtlasConfigureImporter(AbstractAtlasRenderer atlas_renderer, string path) {
			var importer = AssetImporter.GetAtPath(path) as TextureImporter;
			if (importer == null) {
				LogWarning($"No TextureImporter at \"{path}\", need reimport?");
				return true; // repeat
			}
			LogDebug($"Configuring {importer} at \"{path}\"...");

			SelectFocus(importer);
			atlas_renderer.AtlasConfigureImporter(importer);

			AssetDatabase.WriteImportSettingsIfDirty(path);
			LogDebug($"Configured {importer} at \"{path}\".");
			return false; // no repeat
		}

		protected virtual IEnumerator AtlasBakeSave(AbstractAtlasRenderer atlas_renderer) {
			var begin = Stopwatch.StartNew();
			var ar_name = atlas_renderer.name;
			var ext = atlas_renderer.AtlasSaveFormat();
			var is_exr = "exr".Equals(ext);
			var asset_path = $"{sceneDir}/Atlas_{gameObject.name}_tex_{ar_name}.{ext}";
			if (UniqueAssetNames)
				asset_path = AssetDatabase.GenerateUniqueAssetPath(asset_path);
			LogDebug($"Saving \"{ar_name}\" as \"{asset_path}\"...");

			// Раньше тут использовался StartAssetEditing / StopAssetEditing и была ошибка
			// Build asset version error: <файлик> in SourceAssetDB has modification
			// time of '<раньше>' while content on disk has modification time of '<позже>'
			// И нихуя не работало, не реимпортировалось.
			// Оказалось, проще:
			// - записать "болванку" в файл, 
			// - быстро заимпортировать,
			// - настроить импортер,
			// - быстро заимпортировать,
			// - записать корректные данные в файл, 
			// - долго заимпортировать.
			// If it works - it works!

			// Сначала записываем в path_png "болванку".
			var sw = Stopwatch.StartNew();

			LogDebug($"Saving \"{ar_name}\" dull placeholder as \"{asset_path}\"...");
			Texture2D dull_tex = null;
			try {
				var dull_fmt = is_exr ? TextureFormat.RGBAHalf : TextureFormat.RGBA32;
				dull_tex = new Texture2D(4, 4, dull_fmt, 0, is_exr);
				var dull_data = is_exr ? dull_tex.EncodeToEXR() : dull_tex.EncodeToPNG();
				File.WriteAllBytes(FileUtil.GetPhysicalPath(asset_path), dull_data);
			} catch (Exception exc) {
				LogException($"Failed to save dull texture for \"{ar_name}\" as \"{asset_path}\".", exc);
				throw exc;
			} finally {
				if (dull_tex != null)
					DestroyImmediate(dull_tex);
				dull_tex = null;
			}
			LogDebug($"Saved \"{ar_name}\" dull placeholder as \"{asset_path}\".");

			// Убеждаемся, что ассет появился в датабазе, если что - ждём...
			while (AtlasReImportPNG(asset_path, false) == null) {
				yield return null;
				sw.Reset();
			}

			// Настраиваем импортер. Он должен быть к этому моменту, но если нет - переимпортируем и ждём...
			while (AtlasConfigureImporter(atlas_renderer, asset_path)) {
				AtlasReImportPNG(asset_path, false);
				yield return null;
				sw.Reset();
			}

			// Импортер изменён и можно заимпортировать с корректными настройками.
			AtlasReImportPNG(asset_path, false);
			// А теперь можно перезаписать картинку уже норм данными и она сразу заимпортися с норм настройками.

			if (ShouldYield(sw)) {
				yield return null;
				sw.Reset();
			}

			// Но для начала конвертируем RenderTexture -> Texture2D
			Assert.IsTrue(texRT1.width == atlasSize.x, $"atlas.width={texRT1.width}, atlasSize.x={atlasSize.x}");
			Assert.IsTrue(texRT1.height == atlasSize.y, $"atlas.width={texRT1.height}, atlasSize.x={atlasSize.y}");
			var tex_temp = atlas_renderer.AtlasRTToTexture2D(texRT1);
			SelectFocus(tex_temp);

			if (ShouldYield(sw)) {
				yield return SelectFocus(tex_temp);
				sw.Reset();
			}

			// Теперь tex_dst1 больше не нужна.
			RenderTexture.ReleaseTemporary(texRT1); // DestroyImmediate(tex_dst1);
			texRT1 = null;

			if (ShouldYield(sw)) {
				yield return null;
				sw.Reset();
			}

			// Теперь кодируем Texture2D -> byte[].
			byte[] data = null;
			var data_length = 0;
			try {
				var flags = Texture2D.EXRFlags.CompressZIP | Texture2D.EXRFlags.CompressRLE | Texture2D.EXRFlags.CompressPIZ;
				// Я хз как оно работает когда несколько флагов, но оно работает.
				data = is_exr ? tex_temp.EncodeToEXR(flags) : tex_temp.EncodeToPNG();
				data_length = data.Length;
			} catch (Exception exc) {
				LogException($"Failed to encode {tex_temp} for \"{ar_name}\" as PNG.", exc);
				throw exc;
			}
			LogDebug($"Encoded \"{ar_name}\" encoded as {data_length} bytes {ext}.");

			// Теперь tex_temp больше не нужна.
			DestroyImmediate(tex_temp);
			tex_temp = null;

			if (ShouldYield(sw)) {
				yield return null;
				sw.Reset();
			}

			// Теперь сохраняем byte[] -> path_png.
			try {
				File.WriteAllBytes(FileUtil.GetPhysicalPath(asset_path), data);
			} catch (Exception exc) {
				LogException($"Failed to save {tex_temp} for \"{ar_name}\" encoded as {data_length} bytes as \"{asset_path}\".", exc);
				throw exc;
			} finally {
				data = null; // Помогаем сборщику мусора.
			}
			LogDebug($"Saved \"{ar_name}\" encoded as {data_length} bytes as \"{asset_path}\".");

			if (ShouldYield(sw)) {
				yield return null;
				sw.Reset();
			}

			// Наконец переимпортируем в последний раз.
			Texture2D asset_tex;
			while (true) {
				asset_tex = AtlasReImportPNG(asset_path, true);
				if (asset_tex != null && asset_tex.width == atlasSize.x && asset_tex.height == atlasSize.y)
					break;
				yield return null; // Принудительная пауза
			}
			atlas_renderer.atlasTexture = asset_tex;
			AtlasTextures.Add(asset_tex);
			SetDirty();

			if (ShouldYield(sw)) {
				yield return SelectFocus(asset_tex);
				sw.Reset();
			}

			Log($"Saved \"{ar_name}\" as {asset_tex} ({data_length} bytes) at \"{asset_path}\" in {begin.Elapsed}.");
		}

		protected virtual IEnumerator AtlasBake() {
			AtlasTextures.Clear();
			SetDirty();

			var shader_path = AssetDatabase.GUIDToAssetPath(BLIT_SHADER_GUID);
			if (string.IsNullOrWhiteSpace(shader_path))
				ThrowException(new Exception($"Blit shader \"{BLIT_SHADER_GUID}\" not found in asset DB!"));
			var shader = AssetDatabase.LoadAssetAtPath<Shader>(shader_path);
			if (shader == null)
				ThrowException(new Exception($"Was not able to load blit shader at \"{shader_path}\"!"));
			yield return null;
			// Shader.WarmupAllShaders(); // crashes Unity for some reason!
			// yield return null;

			try {
				matBlit = new Material(shader);
				foreach (var atlas_renderer in atlasRenderers) {
					var task_bake = AtlasBakeNamed(atlas_renderer);
					while (task_bake.MoveNext())
						yield return task_bake.Current;
					var task_save = AtlasBakeSave(atlas_renderer);
					while (task_save.MoveNext())
						yield return task_save.Current;
				}
			} finally {
				if (matBlit != null)
					DestroyImmediate(matBlit);
				matBlit = null;
			}
		}


		protected abstract Shader GetDefaultAtlasShader();

		public abstract Shader EnsureAtlasShader();

		public virtual bool DiffFloat(Material left, Material right, string name) {
			if (!left.HasFloat(name) && !right.HasFloat(name))
				return false;
			if (!(left.HasFloat(name) && right.HasFloat(name)))
				return true;
			var left_v = left.GetFloat(name);
			var right_v = right.GetFloat(name);
			return !Mathf.Approximately(left_v, right_v);
		}

		public bool DiffCommons(Material left, Material right, bool gi, bool instancing) {
			if (left.renderQueue != right.renderQueue)
				return true;

			if (left.doubleSidedGI != right.doubleSidedGI)
				return true;

			if (gi) {
				var em_left = left.globalIlluminationFlags & MaterialGlobalIlluminationFlags.AnyEmissive;
				var em_right = right.globalIlluminationFlags & MaterialGlobalIlluminationFlags.AnyEmissive;
				if (em_left != em_right)
					return true;
			}

			if (instancing && left.enableInstancing != right.enableInstancing)
				return true;

			return false;
		}

		// Должен сравнить два материала на совместимость, согласно настройкам этого адаптера.
		public abstract bool IsCompatible(Material left, Material right);

		// Должен настроить данную копию нового материал,
		// изменяя только минимально необходисый набор опций.
		// (заменить текстуры, сбросить ST)
		public abstract void ConfigureAtlasMaterial(Material original);

		public virtual Material ConvertMaterial(Material original) {
			foreach (var mat_atlas_ext in AtlasMaterials) {
				if (IsCompatible(original, mat_atlas_ext))
					return mat_atlas_ext;
			}

			LogDebug($"Orignal material {original} has no matching atlas material, creating one...", original);
			var mat_atlas = Instantiate(original);
			mat_atlas.parent = null;
			ConfigureAtlasMaterial(mat_atlas);
			mat_atlas.name = $"Atlas_{gameObject.name}_mat_new";

			// Может получиться так, что новый начал совпадать с уже существующим.
			foreach (var mat_atlas_ext in AtlasMaterials) {
				if (IsCompatible(mat_atlas, mat_atlas_ext)) {
					LogDebug($"New atlas material {mat_atlas} matching old atlas material {mat_atlas_ext}, reusing...", mat_atlas_ext);
					DestroyImmediate(mat_atlas);
					return mat_atlas_ext;
				}
			}

			mat_atlas.name = $"Atlas_{gameObject.name}_mat_{AtlasMaterials.Count}";
			LogDebug($"Created new atlas material {mat_atlas}.", mat_atlas);
			AtlasMaterials.Add(mat_atlas);
			SetDirty();
			return mat_atlas;
		}

		protected virtual void ConvertMaterials() {
			LogDebug($"Converting {materialGroups.Count} original materials to atlas materials...");
			var begin = Stopwatch.StartNew();
			AtlasMaterials.Clear();
			SetDirty();
			var report = new List<string>(materialGroups.Count);
			foreach (var mat_group in materialGroups.Values) {
				if (mat_group.matOriginal != null) {
					mat_group.matAtlas = ConvertMaterial(mat_group.matOriginal);
				} else {
					mat_group.matAtlas = null;
					LogWarning($"One of groups have no {nameof(mat_group.matOriginal)}... Destroyed? Skip.");
				}
				report.Add($"- {mat_group.matOriginal} -> {mat_group.matAtlas}.");
			}

			Log($"Converted {report.Count} original materials to {AtlasMaterials.Count} " +
				$"atlas materials in {begin.Elapsed}:\n" + string.Join("\n", report));

			foreach (var mat_atlas in AtlasDebugMaterials) {
				if (mat_atlas != null)
					ConfigureAtlasMaterial(mat_atlas);
			}
		}

		protected virtual void AtlasSaveMaterials() {
			// Несмотря на то, что материалы сами по себе простые и маленькие объекты, 
			// CreateAsset() может вызывать фризы, по этому лучше их собрать в один подход.
			// Во время StartAssetEditing / StopAssetEditing не должно быть прерываний.
			if (!SaveMaterials)
				return;
			LogDebug($"Saving {AtlasMaterials.Count} atlas materials...");
			var begin = Stopwatch.StartNew();
			AssetDatabase.StartAssetEditing();
			try {
				foreach (var mat_atlas in AtlasMaterials) {
					if (mat_atlas == null)
						continue;
					var mat_atlas_path = mat_atlas.name.SanitizeFileName();
					mat_atlas_path = $"{sceneDir}/{mat_atlas_path}.mat";
					if (UniqueAssetNames)
						mat_atlas_path = AssetDatabase.GenerateUniqueAssetPath(mat_atlas_path);
					AssetDatabase.CreateAsset(mat_atlas, mat_atlas_path);
					LogDebug($"Saved atlas material {mat_atlas} as \"{mat_atlas_path}\"", mat_atlas);
				}
			} finally {
				AssetDatabase.StopAssetEditing();
			}
			AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
			LogDebug($"Saved {AtlasMaterials.Count} atlas materials in {begin.Elapsed}.");
		}

		protected virtual IEnumerator AtlasApplyUV() {
			var items_c = materialGroups.Values.Sum(g => g.items.Count);
			LogDebug($"Applying UV transforms to {items_c} temp mat slot meshes...");
			var begin = Stopwatch.StartNew();
			var sw = Stopwatch.StartNew();
			foreach (var mat_group in materialGroups.Values) {
				if (mat_group.matAtlas == null)
					continue;
				mat_group.AtlasApplyUV();
				if (ShouldYield(sw)) {
					yield return null;
					sw.Reset();
				}
			}
			Log($"Applied UV transforms to {items_c} temp mat slot meshes in {begin.Elapsed}.");
		}

		protected virtual IEnumerator AtlasApplyMeshesAndMats() {
			LogDebug($"Applying meshes and materials on {rendererGroups.Count} renderers...");
			var begin = Stopwatch.StartNew();
			var sw = Stopwatch.StartNew();
			int atlas_meshes = 0, atlas_slots = 0;
			foreach (var r_group in rendererGroups) {
				r_group.AtlasApplyMeshesAndMats();
				if (r_group.meshAtlas != null)
					++atlas_meshes;
				atlas_slots += r_group.items.Count;
				if (ShouldYield(sw)) {
					yield return SelectFocus(r_group.renderer);
					sw.Reset();
				}
			}
			AtlasMeshes.Clear();
			AtlasMeshes.AddRange(rendererGroups.Select(rg => rg.meshAtlas).UnityNotNull());
			SetDirty();
			Log($"Applied atlas to {atlas_meshes} renderers, {atlas_slots} slots, " +
				$"generated {AtlasMeshes.Count} meshes in {begin.Elapsed}.");
			yield return null;
		}

		protected virtual void AtlasSaveMeshes() {
			// Замечено, что CreateAsset() по отдельности на большом количестве мешей
			// вызывает значительные фризы, по этому лучше их собрать в один подход.
			// Во время StartAssetEditing / StopAssetEditing не должно быть прерываний.
			if (!SaveMeshes)
				return;
			LogDebug($"Saving {AtlasMeshes.Count} meshes...");
			var begin = Stopwatch.StartNew();
			AssetDatabase.StartAssetEditing();
			try {
				foreach (var r_group in rendererGroups) {
					if (r_group.meshAtlas == null)
						continue;
					r_group.SaveMesh();
				}
			} finally {
				AssetDatabase.StopAssetEditing();
			}
			AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
			LogDebug($"Saved {AtlasMeshes.Count} meshes in {begin.Elapsed}.");
		}

		public virtual IEnumerator Run() {
			// Инициализируем DataTexDescы из основного (DataToMatAdapter) адаптера.
			// Эти каналы и будут атлассироваться.
			InitChecks();
			InitSceneDir();
			InitAtlasRenderers();
			yield return null;

			// Потом используя фильтры собираем, что мы можем атлассировать в группы.
			// При этом адаптируем материалы.
			FilterGroupAndAdapt();
			yield return null;

			// После этого расчитываем размер каждого материала.
			// Он условный, т.к. текстуры на материале могут быть разных размеров.
			CalcMatSizesAndST();
			yield return null;

			// Затем ишем и расчитываем UV острова.
			// Это делается после выяснения размера текстур, 
			// т.к. острова считаются в пиксельных координатах.
			var task_uv = CalcUVIslands();
			while (task_uv.MoveNext())
				yield return task_uv.Current;

			// Расчёт лайаута аталаса.
			var task_layout = AtlasLayout switch {
				AtlasLayoutBackend.PackTextures => CalcAtlasLayout_PackTextures(),
				AtlasLayoutBackend.GenerateAtlasPerfect => CalcAtlasLayout_GenerateAtlas_PerfectPow2(),
				AtlasLayoutBackend.GenerateAtlasDense => CalcAtlasLayout_GenerateAtlas_Dense(),
				_ => throw new Exception()
			};
			while (task_layout.MoveNext())
				yield return task_layout.Current;

			// Операции с рендером материалов на атласы.
			var task_bake = AtlasBake();
			while (task_bake.MoveNext())
				yield return task_bake.Current;

			// После рендера можно сконверить материалы.
			ConvertMaterials();
			yield return null;

			AtlasSaveMaterials();
			yield return null;

			// И применить на меши.
			var task_apply_uv = AtlasApplyUV();
			while (task_apply_uv.MoveNext())
				yield return task_apply_uv.Current;

			var task_apply_meshes = AtlasApplyMeshesAndMats();
			while (task_apply_meshes.MoveNext())
				yield return task_apply_meshes.Current;

			AtlasSaveMeshes();
			yield return null;
		}
	}
}
#endif
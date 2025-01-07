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
	public class MaterialCombiner : KawaEditorBehaviour {
		public static readonly string BLIT_SHADER_GUID = "fcbb4213b7350534397157f990cd24b7";
		public enum AtlasLayoutBackend {
			PackTextures, GenerateAtlasPerfect, GenerateAtlasDense
		}

		[Tooltip("Where on scene search objects to atlas.")]
		public List<GameObject> Hierarchy = new List<GameObject>();

		[Tooltip("If Checked, \"Hierarchy\" is ignored and hole scene is used.")]
		public bool WholeScene = false;

		[Space]
		[Tooltip("Filter specific Materials for atlassing.")]
		public List<AbstractMaterialFilter> Filters = new List<AbstractMaterialFilter>();

		[Space]
		public AbstractMaterialAdapter MainAdapter;
		public List<AbstractMaterialAdapter> SecondaryAdapters = new List<AbstractMaterialAdapter>();
		
		[Space]
		public AtlasLayoutBackend AtlasLayout = AtlasLayoutBackend.PackTextures;
		public int IslandsAlignPx = 8;
		public int IslandsEpsilonPx = 8;
		public int IslandsPaddingPx = 8;

		[Tooltip("Powers of 2 recommended (..., 1024, 2048, 4096, ...)")]
		public int MaxAtlasSize = 1024;

		[Tooltip("When checked, will select operating objects and interrupt more frequently for visual feedback.")]
		public float MaxStallTime = 1;

		[Header("Asset saving")]
		public bool SaveMeshes = false;
		public bool SaveMaterials = false;

		[Tooltip("When checked, assets will not be overwriten, but new files with similar names will be created.")]
		public bool UniqueAssetNames = false;

		[Header("Properties below are auto-generated")]
		public List<Texture2D> OriginalTextures = new List<Texture2D>();
		public List<Material> OriginalMaterials = new List<Material>();
		public List<Texture2D> AtlasTextures = new List<Texture2D>();
		public List<Material> AtlasMaterials = new List<Material>();
		public List<Mesh> AtlasMeshes = new List<Mesh>();

		/**/
		protected int stallTimeMS = 1000;
		protected List<DataTexDesc> descriptors;
		protected readonly List<RendererGroup> renderers = new List<RendererGroup>();
		protected readonly Dictionary<Material, MaterialGroup> materials = new Dictionary<Material, MaterialGroup>();
		protected readonly HashSet<Material> unadaptable = new HashSet<Material>();
		protected Vector2Int atlasSize = Vector2Int.zero;

		protected Material matBlit;
		protected RenderTexture texRT1 = null;
		protected RenderTexture texRT2 = null;
		internal string sceneDir = null;

		public object SelectFocus(Object obj) {
			if (obj == null)
				return null;
			Selection.SetActiveObjectWithContext(obj, this);
			if (debugMode)
				EditorGUIUtility.PingObject(obj);
			var view = SceneView.lastActiveSceneView;
			if (view != null && (obj is GameObject || obj is Component))
				view.FrameSelected(false, true);
			return null;
		}

		public bool ShouldYield(Stopwatch sw) {
			return stallTimeMS == 0 || (sw != null && sw.ElapsedMilliseconds > stallTimeMS);
		}

		protected virtual void InitChecks() {
			if (MainAdapter == null) {
				ThrowException(new NullReferenceException($"{nameof(MainAdapter)} is not set!"));
			}

			if (SecondaryAdapters == null || SecondaryAdapters.Count == 0) {
				LogWarning($"{nameof(SecondaryAdapters)} is empty! Auto-adding {nameof(MainAdapter)} {MainAdapter} there.");
				SecondaryAdapters.Add(MainAdapter);
				SetDirty();
			} else if (!SecondaryAdapters.Contains(MainAdapter)) {
				LogWarning($"{nameof(SecondaryAdapters)} doesn't contains {nameof(MainAdapter)}. " +
					$"That's acceptable in specificcases, but might be not that you want.");
			}

			if (1 > MaxAtlasSize || MaxAtlasSize > 16 * 1024) {
				ThrowException(new ArgumentOutOfRangeException($"{nameof(MaxAtlasSize)} must be in range 1 .. {16 * 1024}"));
			}

			stallTimeMS = MaxStallTime > 0 ? Mathf.RoundToInt(Mathf.Clamp(MaxStallTime, 1f / 60, 10f) * 1000) : 0;
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

		protected virtual void InitData() {
			descriptors = MainAdapter.InitDescriptors();
			if (descriptors.Count == 0) {
				ThrowException(new ArgumentException(
					$"{nameof(MainAdapter)} {MainAdapter} returned no data texture descriptors! Nothing to atlas!"));
			}

			var desc_names = string.Join(", ", descriptors.Select(d => $"\"{d.name}\""));
			Log($"Initialized {descriptors.Count} data channels desciptors materials: {desc_names}");
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

		protected virtual bool TryAdaptMaterial(Material mat, out DataAdapted data) {
			LogDebug($"Adapting material {mat}...");
			data = null;
			foreach (var adapter_i in SecondaryAdapters) {
				// TODO logs & errors
				if (!adapter_i.TryAdaptMaterial(mat, descriptors, out data))
					continue;
				LogDebug($"Found adapter {data.adapter} for material {mat} with {data.data.Count} datas and transform {data.texST}.");
				return true;
			}
			LogDebug($"No adapter found for material {mat}, will be ignored.");
			data = null;
			return false;
		}

		protected virtual bool TryGetMaterialGroup(Material mat, out MaterialGroup group) {
			if (materials.TryGetValue(mat, out group)) {
				// Группа уже существует, значит её материал уже адаптирован, а значит доп. проверки не нужны.
				return true;
			} else if (TryAdaptMaterial(mat, out var adapted)) {
				// Создание новый группы, если удалось адаптировать.
				group = new MaterialGroup(this, mat, adapted) {
					alignPx = IslandsAlignPx,
					epsilonPx = IslandsEpsilonPx,
					paddingPx = IslandsPaddingPx
				};
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
			var begin = Stopwatch.StartNew();
			LogDebug($"Searching renderers to combine materials on...");
			materials.Clear();
			unadaptable.Clear();
			var all_renderers = CollectRenderers();
			if (all_renderers.Count < 1)
				return;

			foreach (var renderer in all_renderers) {
				ProcessRenderer(renderer);
			}

			var renderers = materials.Values.SelectMany(g => g.items.Select(i => i.renderer)).Distinct().Count();
			var slots = materials.Values.Sum(v => v.items.Count);
			if (renderers < 1 || slots < 1 || materials.Count < 1) {
				LogWarning($"Found {renderers} renderers, {slots} material slots and {materials.Count} materials " +
					$"for atlas after checking {all_renderers.Count} renderers! Nothing to atlas.");
				return;
			}

			OriginalTextures.Clear();
			OriginalTextures.AddRange(materials.Values.SelectMany(g => g.adapted.data)
				.SelectMany(d => d.dstTex).Distinct());
			OriginalMaterials.Clear();
			OriginalMaterials.AddRange(materials.Keys);
			SetDirty();
			unadaptable.Clear(); // Больше метки нам не понадобятся.
			Log($"Gathered {materials.Count} materials and {slots} material slots " +
				$"from {renderers}/{all_renderers.Count} renderers in {begin.Elapsed}.");
		}

		protected virtual void CalcMatSizes() {
			var begin = Stopwatch.StartNew();
			foreach (var group in materials.Values) {
				group.CalcTexSize();
			}
			var sizes_s = string.Join("\n", materials.Values.Select(g =>
				$"- {g.matOriginal}: {g.textureSize.x}x{g.textureSize.y} est size, {g.adapted.data.Count} data textures"
			));
			Log($"Detected texture sizes {materials.Count} in {begin.Elapsed}:\n{sizes_s}");
		}

		protected virtual IEnumerator CalcUVIslands() {
			LogDebug($"Calculating UV islands on {materials.Count} materials...");
			var sw = Stopwatch.StartNew();
			foreach (var group in materials.Values) {
				group.CalcIslands();
				if (ShouldYield(sw)) {
					yield return SelectFocus(group.matOriginal);
					sw.Restart();
				}
			}
			sw.Stop();

			var sum = materials.Values.Select(g => g.IslandsCount()).Sum();
			MaterialGroup.ResetBuffers(); // Пока не понадобятся.
			var islands_s = string.Join("\n", materials.Values.Select(g =>
				$"- {g.matOriginal}: {g.islandsOriginal.Count} UV islands, " +
				$"{g.debugUVPushes} pushes, {g.debugUVIters} iterations."
			));
			Log($"Got {sum} UV islands total from {materials.Count} materials:\n{islands_s}");
			yield return null;
		}

		protected virtual IEnumerator CalcAtlasLayout_PackTextures() {
			var begin = Stopwatch.StartNew();
			Texture2D[] dull_tex = null;
			Texture2D atlas_tex = null;
			// (группа, расширеный остров в коордах ориг тексткры, номер острова в списке группы)
			var islands = materials.Values.SelectMany(
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

		protected bool CalcAtlasLayout_GenerateAtlas_Try(Vector2[] sizes, int size, List<Rect> results) {
			// Texture2D.GenerateAtlas очень баганый и плохо докмументирован
			// https://discussions.unity.com/t/texture2d-generateatlas-has-a-bug-texture2d-generateatlas-has-a-bug/240115
			// https://issuetracker.unity3d.com/issues/texture2d-dot-generateatlas-returns-true-with-a-list-of-returned-rectangles-with-a-size-of-0-when-it-should-return-false-or-return-true-and-downscale-the-sizes-provided-in-the-parameters-to-fit-the-atlas-size
			// По этому используем цирковые проверки
			results.Clear();
			var result = Texture2D.GenerateAtlas(sizes, 1, size, results) &&
				results.Count == sizes.Length &&
				results.All(r => r.width != 0 && r.height != 0) &&
				results.Any(r => r.x != 0 || r.y != 0);
			var dbg = string.Join("\n", results.Select((r, i) => $"{i}: {r}"));
			LogWarning($"Texture2D.GenerateAtlas: size={size}, result={result}:\n{dbg}");
			return result;
		}

		protected virtual IEnumerator CalcAtlasLayout_GenerateAtlas_Dense() {
			var begin = Stopwatch.StartNew();
			var islands = materials.Values.SelectMany(
				grp => grp.islandsPadded.Select((isl, idx) => (grp, isl, idx))
			).ToList();
			Log($"Packing {islands.Count} islands total into the max {MaxAtlasSize}x{MaxAtlasSize} atlas " +
				$"using {nameof(CalcAtlasLayout_GenerateAtlas_Dense)}...");

			var sizes = islands.Select(x => x.isl.Size()).ToArray();
			var size = Mathf.NextPowerOfTwo(sizes.Select(
				v => Mathf.RoundToInt(Mathf.Max(v.x, v.y))
			).Max()) / 2; // Наибольшая степерь 2 в которую точно не поместится

			var results = new List<Rect>(sizes.Length);
			while (!CalcAtlasLayout_GenerateAtlas_Try(sizes, size, results)) {
				size = Mathf.Max(size + 1, Mathf.RoundToInt(size * 1.1f));
				if (size >= int.MaxValue / 2)
					ThrowException(new Exception($"Atlas size grow too big: {size}!"));
				if (ShouldYield(null))
					yield return null;
			}
			// Есть желание оптимизировать размер бинарным поиском, но он тупо не работает.
			// Похоже, Texture2D.GenerateAtlas кеширует ответ для sizes игнорируя size.
			// Так, что даже если size = 1, то всё равно результат тот же.
			// Именно по этому рост в цикле выше по +10%

			var size_r = results.Select(r => Mathf.Max(r.xMax, r.yMax)).Max();
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
			var islands = materials.Values.SelectMany(
				grp => grp.islandsPadded.Select((isl, idx) => (grp, isl, idx))
			).ToList();
			Log($"Packing {islands.Count} islands total into the max {MaxAtlasSize}x{MaxAtlasSize} atlas " +
				$"using {nameof(CalcAtlasLayout_GenerateAtlas_PerfectPow2)}...");

			var sizes = islands.Select(x => x.isl.Size()).ToArray();
			var size = Mathf.NextPowerOfTwo(sizes.Select(v =>
				Mathf.CeilToInt(Mathf.Max(v.x, v.y))
			).Max()) / 2; // Наибольшая степерь 2 в которую точно не поместится

			var results = new List<Rect>(sizes.Length);
			while (!CalcAtlasLayout_GenerateAtlas_Try(sizes, size, results)) {
				size *= 2;
				if (size >= int.MaxValue / 4)
					ThrowException(new Exception($"Atlas size grow too big: {size}!"));
				if (ShouldYield(null))
					yield return null;
			}

			var size_r = results.Select(r => Mathf.Max(r.xMax, r.yMax)).Max();
			Assert.IsTrue(size_r <= size);
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

		protected virtual RenderTexture AtlasMakeRT_(DataTexDesc desc) {
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

		protected virtual RenderTexture AtlasMakeRT(DataTexDesc desc) {
			return RenderTexture.GetTemporary(atlasSize.x, atlasSize.y, 0,
				RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.sRGB);
		}

		protected virtual void AtlasBlitBackground(DataTexDesc desc) {
			matBlit.SetTexture("_TexR", desc.bgTexture);
			matBlit.SetTexture("_TexG", desc.bgTexture);
			matBlit.SetTexture("_TexB", desc.bgTexture);
			matBlit.SetTexture("_TexA", desc.bgTexture);
			matBlit.SetVector("_Channels", new Vector4(0, 1, 2, 3));

			matBlit.SetColor("_Color", desc.bgColor);

			matBlit.SetInteger("_ColorSpace", desc.sRGB ? 1 : 0);
			matBlit.SetInteger("_BumpMode", desc.isNormal ? 1 : 0);
			// mat_blit.SetInteger("_BumpBGR", 0);

			var full = new Vector4(0, 0, 1, 1);
			matBlit.SetVector("_SourceRect", full);
			matBlit.SetVector("_TargetRect", full);

			matBlit.SetTexture("_TargetTex", texRT1);
			LogDebug($"Blitting \"{desc.name}\" default background...");
			Graphics.Blit(desc.bgTexture, texRT2, matBlit);
			(texRT1, texRT2) = (texRT2, texRT1); // swap buffers
			SelectFocus(texRT1);
		}

		protected virtual void AtlasBlitGroup(DataTexDesc desc, MaterialGroup group) {
			var dsc_name = desc.name;
			if (group.islandsAtlas.Count < 1)
				return; // Группа может быть пустой.

			LogDebug($"Trying to render data \"{dsc_name}\" of material {group.matOriginal}...");
			var datas = group.adapted.data.Where(d => d.desc == desc).ToArray();
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

			matBlit.SetTexture("_TexR", data.dstTex[0]);
			matBlit.SetTexture("_TexG", data.dstTex[1]);
			matBlit.SetTexture("_TexB", data.dstTex[2]);
			matBlit.SetTexture("_TexA", data.dstTex[3]);
			matBlit.SetVector("_Channels", data.ChannelsAsVector4());
			// mat_blit.SetInteger("_BumpBGR", 1);

			matBlit.SetColor("_Color", data.color);

			matBlit.SetInteger("_ColorSpace", desc.sRGB ? 1 : 0);
			matBlit.SetInteger("_BumpMode", desc.isNormal ? 1 : 0);

			for (var islands_i = 0; islands_i < group.islandsAtlas.Count; ++islands_i) {
				var island_source = group.islandsPadded[islands_i]; // pixel coords
				var island_atlas = group.islandsAtlas[islands_i]; // 0..1 coords
				var vec_source = island_source.ToVector4Norm(src_tex_size.x, src_tex_size.y);
				var vec_atlas = island_atlas.ToVector4();
				matBlit.SetVector("_SourceRect", vec_source);
				matBlit.SetVector("_TargetRect", vec_atlas);
				matBlit.SetTexture("_TargetTex", texRT1);
				LogDebug($"Blitting {mat_original}/{dsc_name}/{islands_i}: {island_source}/{vec_source} -> {vec_atlas}");
				var capture = false; // desc.isNormal && data.dstTex.Any(t => t != Texture2D.normalTexture);
				try {
					if (capture)
						ExternalGPUProfiler.BeginGPUCapture();
					Graphics.Blit(data.dstTex[0], texRT2, matBlit);
				} finally {
					if (capture)
						ExternalGPUProfiler.EndGPUCapture();
				}
				(texRT1, texRT2) = (texRT2, texRT1); // swap buffers
				SelectFocus(texRT1);
				// break;
			}
		}

		protected virtual IEnumerator AtlasBakeNamed(DataTexDesc desc) {
			var begin = Stopwatch.StartNew();
			var dsc_name = desc.name;
			texRT1 = null;
			texRT2 = null;
			EditorUtility.UnloadUnusedAssetsImmediate(true); // save ram
			var sw = Stopwatch.StartNew();
			try {
				// var rt_desc = PrepareRTDescriptor(desc);
				// tex_dst1 = new RenderTexture(rt_desc) { name = $"RT_{dsc_name}_A" };
				// tex_dst2 = new RenderTexture(rt_desc) { name = $"RT_{dsc_name}_B" };
				texRT1 = AtlasMakeRT(desc);
				texRT2 = AtlasMakeRT(desc);
				LogDebug($"For atlassing \"{dsc_name}\": Created temp buffers: {texRT1}, {texRT2}");
				if (ShouldYield(sw)) {
					yield return SelectFocus(texRT1);
					sw.Reset();
				}

				AtlasBlitBackground(desc);
				if (ShouldYield(sw)) {
					yield return SelectFocus(texRT1);
					sw.Reset();
				}

				foreach (var group in materials.Values) {
					AtlasBlitGroup(desc, group);
					if (ShouldYield(sw)) {
						yield return SelectFocus(texRT1);
						sw.Reset();
					}
				}
			} finally {
				// tex_dst2 больше не будет использоваться, а из tex_dst1 сохраним полученный атлас.
				if (texRT2 != null)
					RenderTexture.ReleaseTemporary(texRT2); // DestroyImmediate(tex_dst2);
				texRT2 = null;
			}
			if (ShouldYield(sw)) {
				yield return SelectFocus(texRT1);
				sw.Reset();
			}
			Log($"Rendered everything for \"{dsc_name}\" in {begin.Elapsed}...");
		}

		protected virtual Texture2D AtlasRTToTexture2D(DataTexDesc desc, RenderTexture atlas) {
			Assert.IsTrue(atlas.width == atlasSize.x, $"atlas.width={atlas.width}, atlasSize.x={atlasSize.x}");
			Assert.IsTrue(atlas.height == atlasSize.y, $"atlas.width={atlas.height}, atlasSize.x={atlasSize.y}");
			var tex_temp = new Texture2D(atlasSize.x, atlasSize.y, TextureFormat.RGBAHalf, true, linear: false);
			var prev_active = RenderTexture.active;
			try {
				RenderTexture.active = atlas;
				tex_temp.ReadPixels(new Rect(0, 0, atlasSize.x, atlasSize.y), 0, 0, true);
				tex_temp.Apply();
				EditorUtility.SetDirty(tex_temp);
				SelectFocus(tex_temp);
			} finally {
				RenderTexture.active = prev_active;
			}
			return tex_temp;
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

		protected virtual void AtlasConfigureImporter(DataTexDesc desc, string path, TextureImporter importer) {
			importer.textureType = desc.isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
			importer.sRGBTexture = desc.sRGB;
			importer.alphaIsTransparency = desc.alphaIsTransparency;

			importer.mipmapEnabled = true;
			importer.streamingMipmaps = true;
			importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
			importer.mipMapsPreserveCoverage = desc.alphaIsTransparency;
			importer.alphaTestReferenceValue = 0.5f;

			importer.filterMode = FilterMode.Trilinear;
			importer.wrapMode = TextureWrapMode.Clamp;

			var tex_size = Mathf.NextPowerOfTwo(Mathf.Max(atlasSize.x, atlasSize.y));
			importer.maxTextureSize = Mathf.Clamp(tex_size, 32, 16 * 1024);
			importer.textureCompression = TextureImporterCompression.CompressedHQ;
			importer.compressionQuality = 100;
			importer.crunchedCompression = false;
		}

		protected virtual bool AtlasConfigureImporter(DataTexDesc desc, string path) {
			var importer = AssetImporter.GetAtPath(path) as TextureImporter;
			if (importer == null) {
				LogWarning($"No TextureImporter at \"{path}\", need reimport?");
				return true; // repeat
			}
			LogDebug($"Configuring {importer} at \"{path}\"...");

			SelectFocus(importer);
			AtlasConfigureImporter(desc, path, importer);

			AssetDatabase.WriteImportSettingsIfDirty(path);
			LogDebug($"Configured {importer} at \"{path}\".");
			return false; // no repeat
		}

		protected virtual IEnumerator AtlasBakeSave(DataTexDesc desc) {
			var begin = Stopwatch.StartNew();
			var desc_name = desc.name;
			var ext = desc.EXR ? "exr" : "png";
			var asset_path = $"{sceneDir}/Atlas_{gameObject.name}_tex_{desc_name}.{ext}";
			if (UniqueAssetNames)
				asset_path = AssetDatabase.GenerateUniqueAssetPath(asset_path);
			Log($"Saving \"{desc_name}\" as \"{asset_path}\"...");

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
			Texture2D dull_tex = null;
			try {
				var dull_fmt = desc.EXR ? TextureFormat.RGBAHalf : TextureFormat.RGBA32;
				dull_tex = new Texture2D(4, 4, dull_fmt, 0, desc.EXR);
				var dull_data = desc.EXR ? dull_tex.EncodeToEXR() : dull_tex.EncodeToPNG();
				File.WriteAllBytes(FileUtil.GetPhysicalPath(asset_path), dull_data);
			} catch (Exception exc) {
				LogException($"Failed to save dull texture for \"{desc_name}\" as \"{asset_path}\".", exc);
				throw exc;
			} finally {
				if (dull_tex != null)
					DestroyImmediate(dull_tex);
				dull_tex = null;
			}

			// Убеждаемся, что ассет появился в датабазе, если что - ждём...
			while (AtlasReImportPNG(asset_path, false) == null) {
				yield return null;
				sw.Reset();
			}

			// Настраиваем импортер. Он должен быть к этому моменту, но если нет - переимпортируем и ждём...
			while (AtlasConfigureImporter(desc, asset_path)) {
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
			var tex_temp = AtlasRTToTexture2D(desc, texRT1);

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
				data = desc.EXR ? tex_temp.EncodeToEXR() : tex_temp.EncodeToPNG();
				data_length = data.Length;
			} catch (Exception exc) {
				LogException($"Failed to encode {tex_temp} for \"{desc_name}\" as PNG.", exc);
				throw exc;
			}

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
				LogException($"Failed to save {tex_temp} for \"{desc_name}\" encoded as {data_length} bytes as \"{asset_path}\".", exc);
				throw exc;
			} finally {
				data = null; // Помогаем сборщику мусора.
			}

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
			desc.atlasTexture = asset_tex;
			AtlasTextures.Add(asset_tex);
			SetDirty();

			if (ShouldYield(sw)) {
				yield return SelectFocus(asset_tex);
				sw.Reset();
			}

			Log($"Saved \"{desc_name}\" as {asset_tex} at \"{asset_path}\" in {begin.Elapsed}.");
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
				foreach (var desc in descriptors) {
					var task_bake = AtlasBakeNamed(desc);
					while (task_bake.MoveNext())
						yield return task_bake.Current;
					var task_save = AtlasBakeSave(desc);
					while (task_save.MoveNext())
						yield return task_save.Current;
				}
			} finally {
				if (matBlit != null)
					DestroyImmediate(matBlit);
				matBlit = null;
			}
		}

		public virtual Material ConvertMaterial(Material original) {
			foreach (var mat_atlas_ext in AtlasMaterials) {
				if (MainAdapter.IsCompatible(original, mat_atlas_ext))
					return mat_atlas_ext;
			}

			LogDebug($"Orignal material {original} has no matching atlas material, creating one...", original);
			var mat_atlas = MainAdapter.MakeNewAtlasMaterial(original);
			mat_atlas.name = $"Atlas_{gameObject.name}_mat_new";

			// Может получиться так, что новый начал совпадать с уже существующим.
			foreach (var mat_atlas_ext in AtlasMaterials) {
				if (MainAdapter.IsCompatible(mat_atlas, mat_atlas_ext)) {
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
			LogDebug($"Converting {materials.Count} original materials to atlas materials...");
			var begin = Stopwatch.StartNew();
			AtlasMaterials.Clear();
			SetDirty();
			var report = new List<string>(materials.Count);
			foreach (var mat_group in materials.Values) {
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
					var mat_atlas_path = $"{sceneDir}/{mat_atlas.name}.mat";
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
			var items_c = materials.Values.Sum(g => g.items.Count);
			LogDebug($"Applying UV transforms to {items_c} temp mat slot meshes...");
			var begin = Stopwatch.StartNew();
			var sw = Stopwatch.StartNew();
			foreach (var mat_group in materials.Values) {
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
			LogDebug($"Applying meshes and materials on {renderers.Count} renderers...");
			var begin = Stopwatch.StartNew();
			var sw = Stopwatch.StartNew();
			int atlas_meshes = 0, atlas_slots = 0;
			foreach (var r_group in renderers) {
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
			AtlasMeshes.AddRange(renderers.Select(rg => rg.meshAtlas).UnityNotNull());
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
				foreach (var r_group in renderers) {
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
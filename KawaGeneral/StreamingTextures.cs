using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Kawashirov {
	public static class StreamingTextures {
#if UNITY_EDITOR

		[MenuItem("Kawashirov/Mark textures as streaming/In all loaded scenes")]
		public static void MarkScenesTex() {
			var gos = StaticCommons.GetScenesRoots();
			MarkTextures(FindTexturesInHierarchy(gos).ToArray(), false);
		}

		[MenuItem("Kawashirov/Mark textures as streaming/In active scene")]
		public static void MarkSceneTex() {
			var scene = EditorSceneManager.GetActiveScene();
			if (scene.isLoaded && scene.IsValid()) {
				MarkTextures(FindTexturesInHierarchy(scene.GetRootGameObjects()).ToArray(), false);
			}
		}

		[MenuItem("Kawashirov/Mark textures as streaming/In selection (assets, prefabs, hierarchy)")]
		public static void MarkSelectedTex() {
			var objects = Selection.objects;
			Debug.LogFormat("[Kawa|StreamingTextures] Objects selected: {0}", objects.Length);

			var texset = new HashSet<Texture>();
			texset.UnionWith(FindTexturesDirect(objects));
			texset.UnionWith(FindTexturesInHierarchy(objects));

			MarkTextures(texset.ToArray(), false);
		}

		[MenuItem("Kawashirov/Mark lightmaps as streaming/Selected files")]
		public static void MarkSelectedLightmaps() {
			var objects = FindTexturesDirect(Selection.objects).ToArray();
			Debug.LogFormat("[Kawa|StreamingTextures] Lightmaps selected: {0}", objects.Length);
			MarkTextures(objects, true);
		}

		[MenuItem("Kawashirov/Mark lightmaps as streaming/Active lightmap data")]
		public static void MarkActiveLightmaps() {
			var texset = new HashSet<Texture>();
			foreach (var data in LightmapSettings.lightmaps) {
				if (data.lightmapColor != null) {
					texset.Add(data.lightmapColor);
				}
				if (data.lightmapDir != null) {
					texset.Add(data.lightmapDir);
				}
			}
			Debug.LogFormat("[Kawa|StreamingTextures] Active lightmaps textures found: {0}", texset.Count);
			MarkTextures(texset.ToArray(), true);
		}

		private static IEnumerable<Texture> FindTexturesDirect(IEnumerable<UnityEngine.Object> objects) {
			return Selection.objects.Select(x => x as Texture).UnityNotNull();
		}

		private static IEnumerable<Texture> FindTexturesInHierarchy(IEnumerable<UnityEngine.Object> objects) {
			var texset = new HashSet<Texture>();

			// Выбраные GameObject в папках (префабы) и на сцене
			var gos = objects.Select(x => x as GameObject).UnityNotNull().ToArray();
			Debug.LogFormat("[Kawa|StreamingTextures] GameObjects selected: {0}", gos.Length);

			// Renderers
			var renderers = gos.SelectMany(x => x.GetComponentsInChildren<Renderer>(true)).ToArray();
			Debug.LogFormat("[Kawa|StreamingTextures] Renderers found: {0}", renderers.Length);
			var materials = new HashSet<Material>(renderers.SelectMany(x => x.sharedMaterials).UnityNotNull());
			Debug.LogFormat("[Kawa|StreamingTextures] Materials found: {0}", materials.Count);
			foreach (var m in materials) {
				var texnames = m.GetTexturePropertyNames();
				texset.UnionWith(texnames.Select(x => m.GetTexture(x) as Texture).UnityNotNull());
			}

			/*
			// Reflection Probes
			var reflections = gos.SelectMany(x => x.GetComponentsInChildren<ReflectionProbe>(true)).ToArray();
			Debug.LogFormat("[Kawa|StreamingTextures] ReflectionProbe found: {0}", reflections.Length);
			texset.UnionWith(reflections.Select(x => x.bakedTexture as Texture).Where(x => x != null));
			texset.UnionWith(reflections.Select(x => x.customBakedTexture as Texture).Where(x => x != null));
			*/

			Debug.LogFormat("[Kawa|StreamingTextures] Textures found: {0}", texset.Count);
			return texset;
		}


		private static void MarkTextures(Texture[] texarr, bool is_lightmap) {
			try {
				var changes = 0;
				for (var i = 0; i < texarr.Length; ++i) {
					bool changed = false;
					if (MarkTexture(texarr[i], i, texarr.Length, is_lightmap, ref changed)) {
						Debug.LogWarning("[Kawa|StreamingTextures] Cancel by user.");
						return;
					}
					if (changed)
						++changes;
				}
				Debug.LogFormat("[Kawa|StreamingTextures] Done! Updated {0}/{1} textures.", changes, texarr.Length);
			} catch (Exception exc) {
				Debug.LogFormat("[Kawa|StreamingTextures] Error: {0}", exc);
				Debug.LogException(exc);
			} finally {
				EditorUtility.ClearProgressBar();
			}
		}

		private static bool MarkTexture(Texture tex, int progress_i, int progress_n, bool is_lightmap, ref bool changed) {
			changed = false;
			try {
				var progress = 1.0f * progress_i / progress_n;
				var title = string.Format("Mark Streaming {0}/{1}...", progress_i, progress_n);
				var info1 = string.Format("Processing texture {0}/{1}...", progress_i, progress_n);
				if (EditorUtility.DisplayCancelableProgressBar(title, info1, progress))
					return true;

				// Ignore RenderTextures
				if (tex is RenderTexture)
					return false;

				var path = AssetDatabase.GetAssetPath(tex);
				if (path == null || "".Equals(path)) {
					Debug.LogWarningFormat("[Kawa|StreamingTextures] {0} has no asset path! Is it run-time texture? Can not set streaming.", tex);
					return false;
				}
				var importer = TextureImporter.GetAtPath(path) as TextureImporter;
				if (importer == null) {
					Debug.LogWarningFormat("[Kawa|StreamingTextures] {0}:{1} has no TextureImporter! Is it run-time texture? Can not set streaming.", tex, path);
					return false;
				}

				if (!is_lightmap && !importer.mipmapEnabled) {
					Debug.LogWarningFormat("[Kawa|StreamingTextures] {0}:{1} has mipmapEnabled = false Can not set streaming.", tex, path);
					return false;
				}

				var info2 = string.Format("Processing texture {0}/{1} at {2}...", progress_i, progress_n, path);
				if (EditorUtility.DisplayCancelableProgressBar(title, info2, progress))
					return true;

				var size = new Vector2Int(tex.width, tex.height);
				// Стримим только если размер > 256x256 (Для R8G8B8A8 это 256KB, для R32G32B32A32 это 1MB, что довольно мало)
				var should_stream = size.x * size.y > 65536;

				if (is_lightmap) {
					// У lightmap не должно быть mipmap, по этому mipmapEnabled только если нужен streaming
					if (importer.mipmapEnabled != should_stream) { importer.mipmapEnabled = should_stream; changed = true; }
					if (should_stream) // Если нет mipmap и streaming, то какая разница?
					{
						if (importer.mipMapsPreserveCoverage != false) { importer.mipMapsPreserveCoverage = false; changed = true; }
						// Если включаем mipmap на lightmap, то заставляем всегда использовать макс. качество через mipMapBias
						if (importer.mipMapBias != -10) { importer.mipMapBias = -10; changed = true; }
					}
				}
				if (importer.streamingMipmaps != (should_stream & importer.mipmapEnabled)) {
					importer.streamingMipmaps = (should_stream & importer.mipmapEnabled);
					changed = true;
				}
				if (changed) {
					Debug.LogFormat("[Kawa|StreamingTextures] {0}:{1} applying streaming mipmaps settings...", tex, path);
					importer.SaveAndReimport();
				}
				return false;
			} catch (Exception exc) {
				Debug.LogFormat("[Kawa|StreamingTextures] Error {0} ({1}/{2}): {3}", tex, progress_i, progress_n, exc);
				Debug.LogException(exc);
			}
			return false;
		}

#endif
	}
}

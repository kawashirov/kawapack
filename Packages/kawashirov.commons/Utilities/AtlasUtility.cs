using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kawashirov {
	public static class AtlasUtility {
#if UNITY_EDITOR

		public static bool GenerateAtlas(Vector2[] sizes, int size, List<Rect> results, KawaEditorBehaviour keb) {
			// Texture2D.GenerateAtlas очень баганый и плохо докмументирован
			// https://discussions.unity.com/t/texture2d-generateatlas-has-a-bug-texture2d-generateatlas-has-a-bug/240115
			// https://issuetracker.unity3d.com/issues/texture2d-dot-generateatlas-returns-true-with-a-list-of-returned-rectangles-with-a-size-of-0-when-it-should-return-false-or-return-true-and-downscale-the-sizes-provided-in-the-parameters-to-fit-the-atlas-size
			// По этому используем цирковые проверки
			results.Clear();
			var result = Texture2D.GenerateAtlas(sizes, 1, size, results) &&
				results.Count == sizes.Length &&
				results.All(r => r.width != 0 && r.height != 0) &&
				results.Any(r => r.x != 0 || r.y != 0);
#if KAWA_DEBUG
			var dbg = string.Join("\n", results.Select((r, i) => $"{i}: {r}"));
			var dbg_msg = $"Texture2D.GenerateAtlas: size={size}, result={result}:\n{dbg}";
			if (keb == null) {
				Debug.Log(dbg_msg);
			} else {
				keb.LogDebug(dbg_msg);
			}
#endif
			return result;
		}

		public static IEnumerator<int> GenerateAtlasIterAsync(Vector2[] sizes, int size, List<Rect> results, float step, KawaEditorBehaviour keb) {
			// Похоже, Texture2D.GenerateAtlas кеширует ответ для sizes игнорируя size.
			// Так, что если однажды решение было найдено, то потом даже если size = 1,
			// то всё равно результат тот же. По этой причине, если необходимо найти минимальный атлас, 
			// нужно делать и маленький прирост, порядка +10%. Если меньше, то может быть слишком долго.
			// По этой же причине оптимизировать размер бинарным поиском не выйдет.
			while (!GenerateAtlas(sizes, size, results, keb)) {
				size = Mathf.Max(size + 1, Mathf.RoundToInt(size * step));
				if (size < 0 || size >= int.MaxValue / 4) {
					var exc = new Exception($"Atlas size grow too big: {size}!");
					if (keb == null) {
						throw exc;
					} else {
						keb.ThrowException(exc);
					}
				}
				yield return size;
			}
		}

		public static int GenerateAtlasIterSync(Vector2[] sizes, int size, List<Rect> results, float step, KawaEditorBehaviour keb) {
			var task = GenerateAtlasIterAsync(sizes, size, results, step, keb);
			while (task.MoveNext())
				size = task.Current;
			return size;
		}

#endif
	}
}
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace Kawashirov.MaterialCombining {
	public class MaterialGroup {
		// Мета-данные по каждому материалу:
		// - где он используется: на каких рендерерах, на каких слотах
		// - каким адаптером преобразуется в каналы данных,
		// - и каналы данных выдаёт.

		// статические глобальные буферы для быстрого чтения данных из Mesh
		private readonly static List<int> BUFFER_INDICES = new List<int>();
		private readonly static List<Vector2> BUFFER_UV = new List<Vector2>();

		public readonly AbstaractMaterialCombiner combiner;
		public readonly Material matOriginal;
		public readonly List<MaterialSlotItem> items;

		public int alignPx = 8;
		public float epsilonPx = 8;
		public float paddingPx = 8;
		public Vector2Int textureSize = Vector2Int.one;
		public Vector4 texST = new Vector4(1, 1, 0, 0);

		public readonly List<UVIsland> islandsOriginal = new List<UVIsland>(); // tex coords (by textureSize)
		public readonly List<UVIsland> islandsPadded = new List<UVIsland>(); // tex coords (by textureSize)
		public readonly List<UVIsland> islandsAtlas = new List<UVIsland>(); // 0..1 coords

		public int debugUVPushes = 0;
		public int debugUVIters = 0;

		// Может быть null если что-то пошло не так и не получилось заатласить это.
		public Material matAtlas = null;

		public MaterialGroup(AbstaractMaterialCombiner combiner, Material matOriginal) {
			this.combiner = combiner;
			this.matOriginal = matOriginal;
			items = new List<MaterialSlotItem>(1);
		}

		public int IslandsCount() => islandsOriginal.Count;

		public static void ResetBuffers() {
			BUFFER_INDICES.Clear();
			BUFFER_INDICES.Capacity = 1;
			BUFFER_UV.Clear();
			BUFFER_UV.Capacity = 1;
		}

		protected static int TopologyToSteps(MeshTopology topology, int fallback) {
			return topology switch {
				MeshTopology.Triangles => 3,
				MeshTopology.Quads => 4,
				MeshTopology.Lines => 2,
				MeshTopology.Points => 1,
				_ => fallback
			};
		}

		public void CheckIndiciesCount(ref int steps, MeshTopology topology, int count) {
			if (count % steps != 0) {
				steps = count;
				combiner.LogWarning(
					$"{this}: have topology={topology} by {steps} indicies, " +
					$"but have {count} total indicies!"
				);
			}
		}

		protected void PushUVIsland(UVIsland island_px) {
			combiner.LogDebug($"{this}: PushUVIsland: {island_px}");
			++debugUVPushes;
			// Остров уже должен быть в пиксельных коордах.
			if (islandsOriginal.Count < 1) {
				islandsOriginal.Add(island_px);
				return;
			}
			// обходим список с конца, т.к. с конца быстрее работает RemoveAt
			var i = islandsOriginal.Count - 1;
			while (i >= 0) {
				++debugUVIters;
				var island_i = islandsOriginal[i];
				if (UVIsland.TryMerge(island_i, island_px, epsilonPx, out var merged)) {
					// Если два острова соприкасаются, то они объединяются,
					// в таком случае island_px заменяем на полученый новый остров,
					// забираем объединившийся остров из списка,
					// возвращаемся в конец и повторяем обход
					island_px = merged;
					islandsOriginal.RemoveAt(i);
					i = islandsOriginal.Count - 1;
				} else {
					// иначе, идем к следующему вначало.
					--i;
				}
			}
			// Если мы дошли до конца, занчит остров (больше) не пересекается ни с каким другим 
			islandsOriginal.Add(island_px);
			// Прикол этого алгоритма еще и в том, что большие острова оказываются ближе к концу, 
			// а малые ближе к началу. Так что список почти отсортирован.
			// Кроме того, примитивы обычно идут по соседству, так что 
			// каждый новый остров скорее всего сразу увидит своего соседа в конце списка.
			// Так что шанс каждому новому острову наткнуться на пересечение ближе у конца крайне высок, 
			// и полные проходы будут редко. В худшем случае O(n^2), но чаще даже быстрее O(n).
			// Для всех островов вцелом худший сценарий за O(n^3), когда каждый треугольник отдельно, 
			// но в среднем чуть хуже чем O(n). Скорее всего O(nlogn), но мне лень считать.
		}

		protected virtual void CalcIslands(MaterialSlotItem item) {
			var mesh = item.meshOriginal;

			item.EnsureSlotsConsistent(mesh, true);
			item.EnsureUV2D(mesh, true);

			BUFFER_UV.Clear();
			BUFFER_INDICES.Clear();
			mesh.GetUVs(combiner.GetUVChannel(), BUFFER_UV);
			mesh.GetIndices(BUFFER_INDICES, item.slot);

			var topology = mesh.GetTopology(item.slot);
			var steps = TopologyToSteps(topology, BUFFER_INDICES.Count);
			CheckIndiciesCount(ref steps, topology, BUFFER_INDICES.Count);

			for (var i = 0; i < BUFFER_INDICES.Count; i += steps) {
				var island_raw = UVIsland.singual;
				for (var j = 0; j < steps; j++) {
					var v_idx = BUFFER_INDICES[i + j];
					var uv = BUFFER_UV[v_idx];
					island_raw = island_raw.ExpandByUVPoint(uv);
				}
				var island_px = island_raw.TransformST(texST).ToTexCoords(textureSize).RoundToInt();
				PushUVIsland(island_px);
			}

			BUFFER_UV.Clear();
			BUFFER_INDICES.Clear();
		}

		public virtual void CalcIslands() {
			combiner.Log($"Searching UV islands for {matOriginal} on {items.Count} slots...");

			islandsOriginal.Clear();
			debugUVPushes = 0;
			debugUVIters = 0;
			foreach (var item in items)
				CalcIslands(item);

			islandsOriginal.TrimExcess();
			var count = islandsOriginal.Count;
			/*
			var islandsOriginal_l = string.Join("\n", islandsOriginal.Select((isl, idx) => $"- №{idx}: {isl}"));
			parent.Log($"Found {count} UV islands on {matOriginal} " +
				$"for {debugUVPushes} pushes, {debugUVIters} iterations:" +
				$"\n{islandsOriginal_l}");
			*/

			islandsPadded.Clear();
			islandsPadded.Capacity = count;
			islandsPadded.AddRange(islandsOriginal.Select(i => i.Expand(paddingPx).Align(alignPx)));
			islandsPadded.TrimExcess();

			islandsAtlas.Clear();
			islandsAtlas.Capacity = count;
			islandsAtlas.AddRange(islandsOriginal); // alloc indicies same size as islandsOriginal
			islandsAtlas.TrimExcess();
		}

		public Vector4 PxToNorm(UVIsland island) => island.ToVector4Norm(textureSize.x, textureSize.y);

		public virtual void AtlasApplyUV(MaterialSlotItem item) {
			var mesh = item.MakeUniqueMesh();
			combiner.SelectFocus(mesh);

			item.EnsureSlotsConsistent(mesh, true);
			item.EnsureUV2D(mesh, true);

			BUFFER_UV.Clear();
			BUFFER_INDICES.Clear();
			mesh.GetUVs(combiner.GetUVChannel(), BUFFER_UV);
			mesh.GetIndices(BUFFER_INDICES, 0);

			var topology = mesh.GetTopology(0);
			var steps = TopologyToSteps(topology, BUFFER_INDICES.Count);
			CheckIndiciesCount(ref steps, topology, BUFFER_INDICES.Count);

			Assert.IsTrue(islandsOriginal.Count == islandsPadded.Count);
			Assert.IsTrue(islandsOriginal.Count == islandsAtlas.Count);

			var map_idx_to_isl = new int[BUFFER_UV.Count];
			for (var i = 0; i < BUFFER_UV.Count; ++i)
				map_idx_to_isl[i] = -1; // маркер 

			// В одном цикле нельзя, т.к. индексы часто общие между соседними примитивами 
			// и изменение BUFFER_UV ломает поиск островов.
			// Также, все примитивы одного индекса должны пренадлежать одному острову, 
			// так что разрывов быть не должно.
			for (var i = 0; i < BUFFER_INDICES.Count; i += steps) {
				var island_idx = -1;
				// Формируем остров.
				var island_raw = UVIsland.singual;
				for (var j = 0; j < steps; j++) {
					var v_idx = BUFFER_INDICES[i + j];
					if (map_idx_to_isl[v_idx] != -1) {
						// Один из индексов мы уже нашли, значит и другие принадлежат к этому же острову.
						// Можно пропустить дальнейший поиск.
						island_idx = map_idx_to_isl[v_idx];
						break;
					}
					var uv = BUFFER_UV[v_idx];
					island_raw = island_raw.ExpandByUVPoint(uv);
				}

				if (island_idx == -1) {
					// Ни один из индексов не был найден до этого, 
					// так что считерить не получится и будем искать.
					// Округление до целого не обязательно.
					var island_px = island_raw.TransformST(texST).ToTexCoords(textureSize);
					// Поиск такого острова, в который быполностью поместился бы сформированый.
					for (island_idx = 0; island_idx < islandsOriginal.Count; ++island_idx)
						// Точности в 0.1 пикселя должно быть более чем достаточно.
						if (islandsOriginal[island_idx].Inside(island_px, 0.1f))
							break;
					// Такой должен быть всегда т.к. в CalcIslands тот же алгоритм.
					Assert.IsTrue(island_idx < islandsOriginal.Count,
						$"Can't find matching island for \n{island_raw},\n{island_px}\n" +
						$"across {islandsOriginal.Count} islands at {item.meshOriginal}, {item.renderer}.\n" +
						$"BUFFER_INDICES={BUFFER_INDICES.Count}, BUFFER_UV={BUFFER_UV.Count}, steps={steps}");
				}

				// Запоминаем найденый остров.
				for (var j = 0; j < steps; j++) {
					map_idx_to_isl[BUFFER_INDICES[i + j]] = island_idx;
				}
			}

			// К каждому индексу применяем преобразование его острова.
			for (var i = 0; i < BUFFER_UV.Count; ++i) {
				var island_idx = map_idx_to_isl[i];
				Assert.IsFalse(island_idx == -1);
				if (island_idx == -1)
					// Для этого индекса не найден остров. Такое может быть 
					// если меш "не оптимизирована" и на ней есть "мёртвые" индексы.
					continue;

				var island_ppx = islandsPadded[island_idx]; // px coords
				var island_atlas = islandsAtlas[island_idx]; // 0..1 coords

				var uv = BUFFER_UV[i];
				// original 0..1 -> scale/offset 0..1 -> original tex px
				uv.x = (uv.x * texST.x + texST.z) * textureSize.x;
				uv.y = (uv.y * texST.y + texST.w) * textureSize.y;
				// original tex px -> window 0..1 -> atlas 0..1
				uv = island_ppx.InverseLerp(uv);
				uv = island_atlas.Lerp(uv);
				// для отладки
				// uv.x = island_atlas.umin * 0.5f + island_atlas.umax * 0.5f;
				// uv.y = island_atlas.vmin * 0.5f + island_atlas.vmax * 0.5f;
				BUFFER_UV[i] = uv;
			}

			mesh.SetUVs(combiner.GetUVChannel(), BUFFER_UV);

			BUFFER_UV.Clear();
			BUFFER_INDICES.Clear();
		}

		public virtual void AtlasApplyUV() {
			for (var i = 0; i < items.Count; i++) {
				var item = items[i];
				try {
					AtlasApplyUV(item);
				} catch (Exception exc) {
					combiner.LogException($"{nameof(AtlasApplyUV)} failed for group {matOriginal}, item №{i}: {item}", exc);
					throw exc;
				}
			}
		}

		public override string ToString() =>
			$"{nameof(MaterialGroup)}({matOriginal})";
	}
}
#endif
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Algolia.Search.Models.Common;
using UnityEngine;

namespace Kawashirov.MaterialCombining {
	public class MaterialSlotGroup {
		// Мета-данные по каждому материалу:
		// - где он используется: на каких рендерерах, на каких слотах
		// - каким адаптером преобразуется в каналы данных,
		// - и каналы данных выдаёт.

		// статические глобальные буферы для быстрого чтения данных из Mesh
		private readonly static List<int> BUFFER_INDICES = new List<int>();
		private readonly static List<Vector2> BUFFER_UV = new List<Vector2>();

		public readonly MaterialCombiner parent;
		public readonly Material matOriginal;
		public readonly DataAdapted adapted;
		public readonly List<MaterialSlotItem> items;

		public float epsilonPx = 8;
		public float paddingPx = 8;
		public Vector2Int textureSize = Vector2Int.zero;

		public readonly List<UVIsland> islandsOriginal = new List<UVIsland>(); // tex coords (by textureSize)
		public readonly List<UVIsland> islandsPadded = new List<UVIsland>(); // tex coords (by textureSize)
		public readonly List<UVIsland> islandsAtlas = new List<UVIsland>(); // 0..1 coords

		public int debugUVPushes = 0;
		public int debugUVIters = 0;

		public Material matAtlas = null;

		public MaterialSlotGroup(MaterialCombiner parent, Material mat, DataAdapted adapted) {
			this.parent = parent;
			this.matOriginal = mat;
			this.adapted = adapted;
			items = new List<MaterialSlotItem>(1);
		}

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

		public void CalcTexSize() {
			if (adapted.data.Count < 1) {
				textureSize = Vector2Int.zero;
				parent.LogWarning($"Texture size for {matOriginal} is 0, there is no data!");
			} else {
				var ldata = adapted.data.OrderByDescending(d => d.LargestTexSize().sqrMagnitude).First();
				var desc_name = ldata.descriptor.name;
				var ts = textureSize = ldata.LargestTexSize();
				parent.Log($"Texture size for {matOriginal} is {ts.x}x{ts.y} from data \"{desc_name}\".");
			}
		}

		protected void PushUVIsland(UVIsland island_px) {
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

		protected virtual void FindUVIslands(MaterialSlotItem item) {
			item.EnsureSlotsConsistent(true);
			item.EnsureUV2D(true);

			item.mesh.GetUVs(0, BUFFER_UV);

			var topology = item.mesh.GetTopology(item.slot);
			var steps = TopologyToSteps(topology, BUFFER_INDICES.Count);

			BUFFER_INDICES.Clear();
			item.mesh.GetIndices(BUFFER_INDICES, item.slot);
			if (BUFFER_INDICES.Count % steps != 0) {
				steps = 1;
				parent.LogWarning(
					$"{this}: have topology={topology} by {steps} indices, " +
					$"but have {BUFFER_INDICES.Count} total idices!"
				);
			}

			for (var i = 0; i < BUFFER_INDICES.Count; i += steps) {
				var v_idx = BUFFER_INDICES[i];
				var uv = BUFFER_UV[v_idx];
				var island_raw = new UVIsland(uv);
				for (var j = 1; j < steps; j++) {
					v_idx = BUFFER_INDICES[i + j];
					island_raw = island_raw.ExpandByUVPoint(BUFFER_UV[v_idx]);
				}
				var island_px = island_raw.TransformST(adapted.texST).ToTexCoords(textureSize).RoundToInt();
				// parent.Log($"{this}: PushUVIsland: {island_px}");
				PushUVIsland(island_px);
			}
		}

		public virtual void CalcIslands() {
			parent.Log($"Searching UV islands for {matOriginal} on {items.Count} slots...");
			islandsOriginal.Clear();
			debugUVPushes = 0;
			debugUVIters = 0;
			foreach (var item in items) {
				FindUVIslands(item);
			}
			islandsOriginal.TrimExcess();
			var count = islandsOriginal.Count;
			var islandsOriginal_l = string.Join("\n", islandsOriginal.Select((isl, idx) => $"- №{idx}: {isl}"));
			parent.Log($"Found {count} UV islands on {matOriginal} " +
				$"for {debugUVPushes} pushes, {debugUVIters} iterations:" +
				$"\n{islandsOriginal_l}");

			islandsPadded.Clear();
			islandsPadded.Capacity = count;
			islandsPadded.AddRange(islandsOriginal.Select(i => i.Expand(paddingPx)));
			islandsPadded.TrimExcess();

			islandsAtlas.Clear();
			islandsAtlas.Capacity = count;
			islandsAtlas.AddRange(islandsOriginal); // alloc indicies same size as islandsOriginal
			islandsAtlas.TrimExcess();
		}

		public int IslandsCount() {
			return islandsOriginal.Count;
		}

	}
}
#endif
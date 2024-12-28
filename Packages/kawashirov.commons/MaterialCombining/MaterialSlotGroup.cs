#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kawashirov.MaterialCombining {
	public class MaterialSlotGroup {
		// Мета-данные по каждому материалу:
		// - где он используется: на каких рендерерах, на каких слотах
		// - каким адаптером преобразуется в каналы данных,
		// - и каналы данных выдаёт.

		// TODO масштаб материала, размеры UV и т.п.

		public readonly MaterialCombiner parent;
		public readonly Material original;
		public readonly List<MaterialSlotItem> items;

		public AbstractMaterialAdapter adapter = null;
		public List<DataChannel> data = null;

		public float epsilonPx = 8;
		public float paddingPx = 8;

		public Vector2 textureSize = Vector2.zero;
		public readonly List<UVIsland> islandsOriginal = new List<UVIsland>(); // tex coords
		public readonly List<UVIsland> islandsPadded = new List<UVIsland>(); // tex coords
		public readonly List<UVIsland> islandsAtlas = new List<UVIsland>(); // 0..1 coords

		public MaterialSlotGroup(MaterialCombiner parent, Material original, MaterialSlotItem init) {
			this.parent = parent;
			this.original = original;
			items = new List<MaterialSlotItem>() { init };
		}

		public void CalcTexSize() {
			if (data.Count < 1) {
				textureSize = Vector2.zero;
				parent.LogWarning($"Texture size for {original} is 0, there is no data!");
			} else {
				var ldata = data.OrderByDescending(d => d.WeightSqr()).First();
				var t = ldata.texture;
				textureSize = new Vector2(t.width, t.height);
				parent.Log($"Texture size for {original} is {t.width}x{t.height} from data \"{ldata.name}\" {t}.");
			}
		}

		public void CalcIslands() {
			islandsOriginal.Clear();
			// TODO
			islandsOriginal.Add(new UVIsland(0, 0, textureSize.x, textureSize.y));

			islandsPadded.Clear();
			islandsPadded.AddRange(islandsOriginal.Select(i => i.Expand(paddingPx)));
			islandsPadded.TrimExcess();

			islandsAtlas.Clear();
			islandsAtlas.AddRange(islandsOriginal); // alloc same size as islandsOriginal
			islandsAtlas.TrimExcess();
		}

		public int IslandsCount() {
			return islandsOriginal.Count;
		}

	}
}
#endif
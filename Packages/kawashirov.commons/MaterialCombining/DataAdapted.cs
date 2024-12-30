#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Kawashirov.MaterialCombining {
	public class DataAdapted {
		// Адаптированый материал

		public readonly AbstractMaterialAdapter adapter;

		public readonly List<DataChannel> data;

		// _ST (Tiling Offset) преобразование из материала.
		// Материал может иметь только одно такое преобразование и атлас расчитывается согласно ему.
		public readonly Vector4 texST;

		// Немер UV слоя по которому работает комбинирования.
		// Материал может использовать только один UV слой и атлас расчитывается согласно ему.
		public readonly int uvIndex;

		public DataAdapted(AbstractMaterialAdapter adapter, List<DataChannel> data, Vector4 texST, int uvIndex) {
			this.adapter = adapter;
			this.data = data;
			this.texST = texST;
			this.uvIndex = uvIndex;
		}
	}
}
#endif
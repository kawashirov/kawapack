#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
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

		public MaterialSlotGroup(MaterialCombiner parent, Material original, MaterialSlotItem init) {
			this.parent = parent;
			this.original = original;
			items = new List<MaterialSlotItem>() { init };
		}
	}
}
#endif
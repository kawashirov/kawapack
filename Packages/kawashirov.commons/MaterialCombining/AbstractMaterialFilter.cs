#if UNITY_EDITOR
using UnityEngine;

namespace Kawashirov.MaterialCombining {
	public abstract class AbstractMaterialFilter : KawaEditorBehaviour {
		// Материал считается выбраным если из всех селекторов:
		// - хотя бы один Select вернул true
		// И
		// - ни один Deselect не вернул true

		public virtual void Prepare() { }

		public virtual bool CheckInclude(Renderer renderer, Mesh mesh, int slot, Material mat) {
			return false; // default no includes
		}

		public virtual bool CheckExclude(Renderer renderer, Mesh mesh, int slot, Material mat) {
			return false; // default no excludes
		}
	}
}
#endif
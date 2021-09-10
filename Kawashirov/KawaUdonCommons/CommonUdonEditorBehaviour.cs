using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon;
using System.Linq;
using Kawashirov.Refreshables;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kawashirov.Udon {
	/// Extended Refreshable for UdonBehaviours
	public class CommonUdonEditorBehaviour : EditorRefreshableBehaviour {
#if UNITY_EDITOR


		[CanEditMultipleObjects, CustomEditor(typeof(EditorCommonBehaviour))]
		public new class Editor : EditorRefreshableBehaviour.Editor {

			public IEnumerable<UdonBehaviour> GetUdonsNearThisComponents() {
				return targets
						.OfType<Component>().UnityNotNull().Distinct()
						.SelectMany(x => x.GetComponents<UdonBehaviour>()).UnityNotNull().Distinct();
			}

			public void SendCustomEventToUdonsNearThisComponents(string e) {
				GetUdonsNearThisComponents().SendCustomEvent(e, this);
			}

		}

		public IEnumerable<UdonBehaviour> GetUdonsNearThisComponents() {
			return gameObject.GetComponents<UdonBehaviour>();
		}

		public UdonBehaviour GetSingleUdonBehaviour(bool throw_exc = true) {
			var udons = gameObject.GetComponents<UdonBehaviour>();
			if (udons.Length > 1) {
				var error = string.Format("Can not get single UdonBehaviour: More than one UdonBehaviour is attached to {0}!", kawaHierarchyPath);
				Debug.LogErrorFormat(this, "[KawaEditor] {0}", error);
				if (throw_exc)
					throw new InvalidOperationException(error);
				else
					return null;
			}
			if (udons.Length < 1) {
				var error = string.Format("Can not get single UdonBehaviour: No UdonBehaviours is attached to {0}!", kawaHierarchyPath);
				Debug.LogErrorFormat(this, "[KawaEditor] {0}", error);
				if (throw_exc)
					throw new InvalidOperationException(error);
				else
					return null;
			}
			return udons[0];
		}

#endif // UNITY_EDITOR
	}
}

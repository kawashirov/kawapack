using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kawashirov {
	public class ReadOnlyAttribute : PropertyAttribute {
#if UNITY_EDITOR
		[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
		public class ReadOnlyAttributeDrawer : PropertyDrawer {
			public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
				var last_gui = GUI.enabled;
				try {
					GUI.enabled = Application.isPlaying && last_gui;
					if (!Application.isPlaying)
						label = new GUIContent($"(Read-Only) {label.text}", label.image, $"(Read-Only) {label.tooltip}");
					EditorGUI.PropertyField(position, property, label);
				} finally {
					GUI.enabled = last_gui;
				}
			}
		}
#endif // UNITY_EDITOR
	}
}

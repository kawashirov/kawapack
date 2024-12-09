using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kawashirov {
	public class LayerAttribute : PropertyAttribute {
		// https://gist.github.com/sebtoun/d7df89e9cbb56878ac3fcca59b78e560
#if UNITY_EDITOR
		[CustomPropertyDrawer(typeof(LayerAttribute))]
		public class LayerAttributeDrawer : PropertyDrawer {
			public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
				property.intValue = EditorGUI.LayerField(position, label, property.intValue);
			}
		}
#endif // UNITY_EDITOR
	}
}

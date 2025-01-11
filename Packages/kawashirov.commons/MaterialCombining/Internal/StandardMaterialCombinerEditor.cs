#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Kawashirov.Refreshables;

namespace Kawashirov.MaterialCombining {
	[CustomEditor(typeof(StandardMaterialCombiner), true)]
	public class StandardMaterialCombinerEditor : AbstaractMaterialCombinerEditor {

		protected virtual void TextureScaleGUI(SerializedProperty prop) {
			// TODO
			EditorGUILayout.PropertyField(prop);
		}

		protected override void ImplPropertiesGUI() {
			EditorGUILayout.Space();
			GUILayout.Label("Standard shader options", EditorStyles.boldLabel);

			EditorGUILayout.PropertyField(Prop("Workflow"));
			EditorGUILayout.PropertyField(Prop("Gloss"));
			EditorGUILayout.PropertyField(Prop("Blend"));
			EditorGUILayout.PropertyField(Prop("InstancingMatters"));
			EditorGUILayout.PropertyField(Prop("GIFlagsMatters"));
			EditorGUILayout.PropertyField(Prop("OverrideShader"));

			EditorGUILayout.Space();
			GUILayout.Label("Supported texture types to atlas", EditorStyles.boldLabel);

			EditorGUILayout.PropertyField(Prop("AlbedoEnable"));
			TextureScaleGUI(Prop("AlbedoScale"));

			EditorGUILayout.Space();

			EditorGUILayout.PropertyField(Prop("GlossEnable"));
			TextureScaleGUI(Prop("GlossEnable"));

			EditorGUILayout.Space();

			EditorGUILayout.PropertyField(Prop("NormalEnable"));
			TextureScaleGUI(Prop("NormalScale"));

			EditorGUILayout.Space();

			EditorGUILayout.PropertyField(Prop("ParallaxEnable"));
			TextureScaleGUI(Prop("ParallaxScale"));
			using (new EditorGUI.DisabledScope(!IKnowWhatIamDoing)) {
				EditorGUILayout.PropertyField(Prop("ParallaxReference"));
			}

			EditorGUILayout.Space();

			EditorGUILayout.PropertyField(Prop("OcclusionEnable"));
			TextureScaleGUI(Prop("OcclusionScale"));

			EditorGUILayout.Space();

			EditorGUILayout.PropertyField(Prop("EmissionEnable"));
			TextureScaleGUI(Prop("EmissionScale"));

			EditorGUILayout.Space();
			GUILayout.Label("Extra (Standard) shader filters", EditorStyles.boldLabel);

			EditorGUILayout.PropertyField(Prop("ExcludeShadersWithWordsInName"));
			EditorGUILayout.PropertyField(Prop("ExcludeShaders"));
			EditorGUILayout.PropertyField(Prop("IncludeShadersWithWordsInName"));
			EditorGUILayout.PropertyField(Prop("IncludeShaders"));
			EditorGUILayout.PropertyField(Prop("AssumeCompatible"));
		}
	}
}
#endif
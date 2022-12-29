using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;
using System.Collections.Generic;

// Имя файла длжно совпадать с именем типа.
// https://forum.unity.com/threads/solved-blank-scriptableobject-on-import.511527/

namespace Kawashirov.KawaShade {
	[CanEditMultipleObjects]
	[CustomEditor(typeof(KawaShadeGenerator))]
	public partial class KawaShadeGeneratorEditor : BaseGenerator.Editor<KawaShadeGenerator> {

		internal bool error = false;
		internal bool complexity_VGF = false;
		internal bool complexity_VHDGF = false;
		internal Dictionary<Type, bool> folds = new Dictionary<Type, bool>();

		public override void OnInspectorGUI() {
			base.OnInspectorGUI();

			error = false;
			complexity_VGF = false;
			complexity_VHDGF = false;

			EditorGUILayout.Space();
			var debug = serializedObject.FindProperty("debug");
			KawaGUIUtility.DefaultPrpertyField(debug, "Debug Build");
			var debug_bool = !debug.hasMultipleDifferentValues ? debug.boolValue : (bool?)null;
			var debug_true = debug_bool.HasValue && debug_bool.Value;
			if (debug_true) {
				EditorGUILayout.HelpBox(
					"Debug mode is On! In debug mode:\n" +
					"- d3d11 debug symbols included into shader build;\n" +
					"- Some configuration errors are warnings;\n" +
					"- Some checks disabled at generating process, so shader is baked \"as-is\";\n" +
					"Please, do not use debug shaders outside of Unity Editor!",
					MessageType.Warning
				);
			}

			foreach (var feature in AbstractFeature.Features.Value) {
				EditorGUILayout.Space();
				feature.GeneratorEditorGUI(this);
			}

			EditorGUILayout.Space();
			using (new EditorGUI.DisabledScope(error)) {
				if (GUILayout.Button("(Re)Bake Shader")) {
					if (error)
						return;
					foreach (var t in targets) {
						var generator = t as KawaShadeGenerator;
						if (generator)
							generator.Refresh();
					}
					Repaint();
				}
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
}

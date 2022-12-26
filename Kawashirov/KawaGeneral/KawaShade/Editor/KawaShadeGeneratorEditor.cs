using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;

// Имя файла длжно совпадать с именем типа.
// https://forum.unity.com/threads/solved-blank-scriptableobject-on-import.511527/

namespace Kawashirov.KawaShade  
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(KawaShadeGenerator))]
	public partial class KawaShadeGeneratorEditor : BaseGenerator.Editor<KawaShadeGenerator> {

		private bool error = false;
		private bool complexity_VGF = false;
		private bool complexity_VHDGF = false;

		public override void OnInspectorGUI()
		{
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

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("General Shader Options");
			using (new EditorGUI.IndentLevelScope()) {
				PipelineGUI();
				

				BlendingGUI();

			}

			EditorGUILayout.Space();
			TexturesGUI();
			
			EditorGUILayout.Space();
			RandomGUI();

			EditorGUILayout.Space();
			ShadingGUI();

			EditorGUILayout.Space();
			MatcapGUI();

			EditorGUILayout.Space();
			DistanceFadeGUI();

			EditorGUILayout.Space();
			WhiteNoise();

			EditorGUILayout.Space();
			GlitterGUI();

			EditorGUILayout.Space();
			FPSGUI();

			EditorGUILayout.Space();
			PSXGUI();

			EditorGUILayout.Space();
			OutlineGUI();

			EditorGUILayout.Space();
			IWDGUI();
			
			EditorGUILayout.Space();
			PolyColorWaveGUI();

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

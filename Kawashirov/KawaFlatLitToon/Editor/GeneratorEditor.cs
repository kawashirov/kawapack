using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

using GUIL = UnityEngine.GUILayout;
using EGUIL = UnityEditor.EditorGUILayout;

using static UnityEditor.EditorGUI;
using static Kawashirov.KawaGUIUtilities;

// Имя файла длжно совпадать с именем типа.
// https://forum.unity.com/threads/solved-blank-scriptableobject-on-import.511527/

namespace Kawashirov.FLT  
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(Generator))]
	public partial class GeneratorEditor : ShaderBaking.BaseGenerator.Editor<Generator> {

		private bool error = false;
		private bool complexity_VGF = false;
		private bool complexity_VHDGF = false;

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			error = false;
			complexity_VGF = false;
			complexity_VHDGF = false;

			EGUIL.Space();
			var debug = serializedObject.FindProperty("debug");
			DefaultPrpertyField(debug, "Debug Build");
			var debug_bool = !debug.hasMultipleDifferentValues ? debug.boolValue : (bool?)null;
			var debug_true = debug_bool.HasValue && debug_bool.Value;
			if (debug_true) {
				EGUIL.HelpBox(
					"Debug mode is On! In debug mode:\n" +
					"- d3d11 debug symbols included into shader build;\n" +
					"- Some configuration errors are warnings;\n" +
					"- Some checks disabled at generating process, so shader is baked \"as-is\";\n" +
					"Please, do not use debug shaders outside of Unity Editor!",
					MessageType.Warning
				);
			}

			EGUIL.Space();
			EGUIL.LabelField("General Shader Options");
			using (new IndentLevelScope()) {
				PipelineGUI();
				

				BlendingGUI();

			}

			EGUIL.Space();
			TexturesGUI();
			
			EGUIL.Space();
			RandomGUI();

			EGUIL.Space();
			ShadingGUI();

			EGUIL.Space();
			MatcapGUI();

			EGUIL.Space();
			DistanceFadeGUI();

			EGUIL.Space();
			WhiteNoise();

			EGUIL.Space();
			FPSGUI();

			EGUIL.Space();
			PSXGUI();

			EGUIL.Space();
			OutlineGUI();

			EGUIL.Space();
			IWDGUI();
			
			EGUIL.Space();
			PolyColorWaveGUI();

			EGUIL.Space();
			using (new DisabledScope(error)) {
				if (GUIL.Button("(Re)Bake Shader")) {
					if (error)
						return;
					foreach (var t in targets) {
						var generator = t as Generator;
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

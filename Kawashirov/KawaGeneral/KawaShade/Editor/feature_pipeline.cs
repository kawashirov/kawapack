using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;
using Kawashirov.KawaShade;
using System.Diagnostics;

namespace Kawashirov.KawaShade {
	public enum TessDomain { Triangles, Quads }
	public enum ShaderComplexity { VF, VGF, VHDGF }
	public enum TessPartitioning { Integer, FractionalEven, FractionalOdd, Pow2 }

	public class FeaturePipeline : AbstractFeature {
		internal static readonly string F_Debug = "KawaShade_Feature_Debug"; // TODO
		internal static readonly string F_Geometry = "KawaShade_Feature_Geometry";
		internal static readonly string F_Tessellation = "KawaShade_Feature_Tessellation";
		internal static readonly string F_Partitioning = "KawaShade_Feature_Partitioning";
		internal static readonly string F_Domain = "KawaShade_Feature_Domain";
		internal static readonly string F_Instancing = "KawaShade_Feature_Instancing";


		internal static readonly Dictionary<ShaderComplexity, string> shaderComplexityNames = new Dictionary<ShaderComplexity, string> {
			{ ShaderComplexity.VF, "VF Lightweight (Vertex, Fragment)" },
			{ ShaderComplexity.VGF, "VGF Geometry (Vertex, Geometry, Fragment)" },
			{ ShaderComplexity.VHDGF, "VHDGF Tessellation+Geometry (Vertex, Hull, Domain, Geometry, Fragment)" },
		};

		public override int GetOrder() => (int)Order.GENERAL;

		public override void PopulateShaderTags(List<string> tags) {
			tags.Add(F_Debug);
			tags.Add(F_Geometry);
			tags.Add(F_Tessellation);
			tags.Add(F_Partitioning);
			tags.Add(F_Domain);
			tags.Add(F_Instancing);
		}

		public override void ConfigureShaderEarly(KawaShadeGenerator gen, ShaderSetup shader) {

			var f_batching = !gen.disableBatching;
			var f_instancing = gen.instancing;

			shader.Debug(gen.debug);

			if (gen.complexity == ShaderComplexity.VHDGF) {
				shader.TagEnum(F_Partitioning, gen.tessPartitioning);
				switch (gen.tessPartitioning) {
					case TessPartitioning.Integer:
						shader.Define("TESS_P_INT 1");
						break;
					case TessPartitioning.FractionalEven:
						shader.Define("TESS_P_EVEN 1");
						break;
					case TessPartitioning.FractionalOdd:
						shader.Define("TESS_P_ODD 1");
						break;
					case TessPartitioning.Pow2:
						shader.Define("TESS_P_POW2 1");
						break;
				}

				shader.TagEnum(F_Domain, gen.tessDomain);
				switch (gen.tessDomain) {
					case TessDomain.Triangles:
						shader.Define("TESS_D_TRI 1");
						break;
					case TessDomain.Quads:
						shader.Define("TESS_D_QUAD 1");
						break;
				}

				shader.properties.Add(new PropertyFloat() { name = "_Tsltn_Uni", defualt = 1, range = new Vector2(0.9f, 10), power = 2 });
				shader.properties.Add(new PropertyFloat() { name = "_Tsltn_Nrm", defualt = 1, range = new Vector2(0, 20), power = 2 });
				shader.properties.Add(new PropertyFloat() { name = "_Tsltn_Inside", defualt = 1, range = new Vector2(0.1f, 10), power = 10 });

				f_instancing = false;
				f_batching = false;
			}

			if (gen.iwd) { // todo flags
				f_instancing = false;
				f_batching = false;
			}
			if (f_batching == false) {
				f_instancing = false;
			}

			shader.TagBool(F_Instancing, f_instancing);
			shader.TagBool(ShaderTag.DisableBatching, !f_batching);

			shader.forward.multi_compile_instancing = f_instancing;
			shader.forward_add.multi_compile_instancing = f_instancing;
			shader.shadowcaster.multi_compile_instancing = f_instancing;
		}

		public override void GeneratorEditorGUI(KawaShadeGeneratorEditor editor) {
			GUILayout.Label("Pipeline Settings", EditorStyles.boldLabel);

			var complexity = editor.serializedObject.FindProperty("complexity");
			KawaGUIUtility.PropertyEnumPopupCustomLabels(complexity, "DX11 Stages", shaderComplexityNames);

			editor.complexity_VGF = !complexity.hasMultipleDifferentValues && complexity.intValue == (int)ShaderComplexity.VGF;
			editor.complexity_VHDGF = !complexity.hasMultipleDifferentValues && complexity.intValue == (int)ShaderComplexity.VHDGF;

			using (new EditorGUI.DisabledScope(!editor.complexity_VHDGF)) {
				using (new EditorGUI.IndentLevelScope()) {
					KawaGUIUtility.DefaultPrpertyField(editor, "tessPartitioning", "Tessellation Partitioning");
					KawaGUIUtility.DefaultPrpertyField(editor, "tessDomain", "Tessellation Domain (Primitive Topology)");
				}
			}

			KawaGUIUtility.DefaultPrpertyField(editor, "instancing");
			using (new EditorGUI.DisabledScope(true)) {
				KawaGUIUtility.DefaultPrpertyField(editor, "disableBatching");
			}
		}

		public override void ShaderEditorGUI(KawaShadeGUI editor) {
			var debug = editor.shaderTags[F_Debug].IsTrue();
			var instancing = editor.shaderTags[F_Instancing].IsTrue();

			if (instancing && debug) {
				editor.materialEditor.EnableInstancingField();
			} else {
				using (new EditorGUI.DisabledScope(!instancing)) {
					EditorGUILayout.LabelField("Instancing", instancing ? "Enabled" : "Disabled");
				}
				foreach (var m in editor.targetMaterials) {
					if (m && m.enableInstancing != instancing) {
						m.enableInstancing = instancing;
						EditorUtility.SetDirty(m);
					}
				}
			}

			var _Tsltn_Uni = editor.FindProperty("_Tsltn_Uni");
			var _Tsltn_Nrm = editor.FindProperty("_Tsltn_Nrm");
			var _Tsltn_Inside = editor.FindProperty("_Tsltn_Inside");
			var tessellation = KawaUtilities.AnyNotNull(_Tsltn_Uni, _Tsltn_Nrm, _Tsltn_Inside);
			using (new EditorGUI.DisabledScope(!tessellation)) {
				if (tessellation) {
					EditorGUILayout.LabelField("Tessellation", "Enabled");
					using (new EditorGUI.IndentLevelScope()) {
						editor.LabelShaderTagEnumValue<TessPartitioning>(F_Partitioning, "Partitioning", "Unknown");
						editor.LabelShaderTagEnumValue<TessDomain>(F_Domain, "Domain", "Unknown");
						editor.ShaderPropertyDisabled(_Tsltn_Uni, "Uniform factor");
						editor.ShaderPropertyDisabled(_Tsltn_Nrm, "Factor from curvness");
						editor.ShaderPropertyDisabled(_Tsltn_Inside, "Inside multiplier");
					}
				} else {
					EditorGUILayout.LabelField("Tessellation", "Disabled");
				}
			}
		}
	}

	public partial class KawaShadeGenerator {
		public bool debug = false;

		public ShaderComplexity complexity = ShaderComplexity.VF;
		public TessPartitioning tessPartitioning = TessPartitioning.Integer;
		public TessDomain tessDomain = TessDomain.Triangles;

		public bool instancing = true;
		public bool disableBatching = false;
	}
}

using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;

namespace Kawashirov.KawaShade {
	public enum BlendTemplate { Opaque, Cutout, Fade, FadeCutout, Custom = 256 }

	internal static partial class KawaShadeCommons {
		internal static readonly string RenderType = "KawaShade_RenderType";
		internal static readonly string F_Debug = "KawaShade_Feature_Debug"; // TODO
		internal static readonly string F_Instancing = "KawaShade_Feature_Instancing";
	}

	public partial class KawaShadeGenerator {
		public BlendTemplate mode = BlendTemplate.Opaque;
		public int queueOffset = 0;

		private void ConfigureBlending(ShaderSetup shader) {
			string q = null;
			if (mode == BlendTemplate.Opaque) {
				q = "Geometry";
				shader.tags[ShaderTag.RenderType] = "Opaque";
				shader.tags[KawaShadeCommons.RenderType] = "Opaque";
				shader.forward.srcBlend = BlendMode.One;
				shader.forward.dstBlend = BlendMode.Zero;
				shader.forward.zWrite = true;
				shader.forward_add.srcBlend = BlendMode.One;
				shader.forward_add.dstBlend = BlendMode.One;
				shader.forward_add.zWrite = false;
			} else if (mode == BlendTemplate.Cutout) {
				q = "AlphaTest";
				shader.tags[ShaderTag.RenderType] = "TransparentCutout";
				shader.tags[KawaShadeCommons.RenderType] = "Cutout";
				shader.Define("_ALPHATEST_ON 1");
				shader.forward.srcBlend = BlendMode.One;
				shader.forward.dstBlend = BlendMode.Zero;
				shader.forward.zWrite = true;
				shader.forward_add.srcBlend = BlendMode.One;
				shader.forward_add.dstBlend = BlendMode.One;
				shader.forward_add.zWrite = false;
			} else if (mode == BlendTemplate.Fade || mode == BlendTemplate.FadeCutout) {
				q = "Transparent";
				shader.tags[ShaderTag.RenderType] = "Transparent";
				shader.tags[KawaShadeCommons.RenderType] = "Fade";
				shader.Define("_ALPHABLEND_ON 1");
				// Дополнительно CUTOFF_FADE
				shader.forward.srcBlend = BlendMode.SrcAlpha;
				shader.forward.dstBlend = BlendMode.OneMinusSrcAlpha;
				shader.forward.zWrite = false;
				shader.forward_add.srcBlend = BlendMode.SrcAlpha;
				shader.forward_add.dstBlend = BlendMode.One;
				shader.forward_add.zWrite = false;
			}
			shader.tags["Queue"] = string.Format("{0}{1:+#;-#;+0}", q, queueOffset);
			shader.shadowcaster.srcBlend = null;
			shader.shadowcaster.dstBlend = null;
			shader.shadowcaster.zWrite = null;
		}
	}

	public partial class KawaShadeGeneratorEditor {
		private void BlendingGUI() {
			var mode = serializedObject.FindProperty("mode");
			KawaGUIUtility.DefaultPrpertyField(mode, "Blending Mode");
			var mode_int = !mode.hasMultipleDifferentValues ? mode.intValue : (int?)null;
			var mode_Opaque = mode_int.HasValue && mode_int.Value == (int)BlendTemplate.Opaque;
			var mode_Cutout = mode_int.HasValue && mode_int.Value == (int)BlendTemplate.Cutout;
			var mode_Fade = mode_int.HasValue && mode_int.Value == (int)BlendTemplate.Fade;

			if (!mode.hasMultipleDifferentValues && mode.intValue == (int)BlendTemplate.Custom) {
				EditorGUILayout.HelpBox("Custom belding options currently in-dev and not yet supported.", MessageType.Error);
				error = true;
			}

			var queueOffset = serializedObject.FindProperty("queueOffset");
			KawaGUIUtility.DefaultPrpertyField(queueOffset);
			var queueOffset_int = !queueOffset.hasMultipleDifferentValues ? queueOffset.intValue : (int?)null;
			using (new EditorGUI.DisabledScope(true)) {
				using (new EditorGUI.IndentLevelScope()) {
					var queueOffset_str = "Mixed Values";
					if (queueOffset_int.HasValue && mode_int.HasValue) {
						string q = null;
						if (mode_int.Value == (int)BlendTemplate.Opaque) {
							q = "Geometry";
						} else if (mode_int.Value == (int)BlendTemplate.Cutout) {
							q = "AlphaTest";
						} else if (KawaUtilities.AnyEq(mode_int.Value, (int)BlendTemplate.Fade, (int)BlendTemplate.FadeCutout)) {
							q = "Transparent";
						}
						queueOffset_str = string.Format("{0}{1:+#;-#;+0}", q, queueOffset_int.Value);
					}
					EditorGUILayout.TextField("Queue", queueOffset_str);
				}
			}

			var forceNoShadowCasting = serializedObject.FindProperty("forceNoShadowCasting");
			KawaGUIUtility.DefaultPrpertyField(forceNoShadowCasting);
			var forceNoShadowCasting_bool = !forceNoShadowCasting.hasMultipleDifferentValues ? forceNoShadowCasting.boolValue : (bool?)null;
			if (forceNoShadowCasting_bool.HasValue && mode_int.HasValue && !forceNoShadowCasting_bool.Value && mode_int.Value == (int)BlendTemplate.Fade) {
				EditorGUILayout.HelpBox(
					"Blending mode is \"Fade\", but \"Force No Shadow Casting\" is Off.\n" +
					"Usually transparent modes does not cast shadows, but this shader can use \"Cutout\" shadow caster for transparent modes.\n" +
					"It's better to disable shadow casting for \"Fade\" at all, unless you REALLY need it.",
					MessageType.Warning
				);
			}

			KawaGUIUtility.DefaultPrpertyField(this, "cull");

			KawaGUIUtility.DefaultPrpertyField(this, "instancing");

			using (new EditorGUI.DisabledScope(true)) {
				KawaGUIUtility.DefaultPrpertyField(this, "disableBatching");
			}

			KawaGUIUtility.DefaultPrpertyField(this, "ignoreProjector");
		}
	}

	internal partial class KawaShadeGUI {
		protected void OnGUI_BlendMode() {
			var debug = shaderTags[KawaShadeCommons.F_Debug].IsTrue();
			var instancing = shaderTags[KawaShadeCommons.F_Instancing].IsTrue();

			if (instancing && debug) {
				materialEditor.EnableInstancingField();
			} else {
				using (new EditorGUI.DisabledScope(!instancing)) {
					EditorGUILayout.LabelField("Instancing", instancing ? "Enabled" : "Disabled");
				}
				foreach (var m in targetMaterials) {
					if (m && m.enableInstancing != instancing) {
						m.enableInstancing = instancing;
					}
				}
			}
		}
	}
}

using System;
using System.IO;
using System.Text;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;
using Kawashirov.FLT;

using GUIL = UnityEngine.GUILayout;
using EGUIL = UnityEditor.EditorGUILayout;
using EU = UnityEditor.EditorUtility;
using KST = Kawashirov.ShaderTag;
using KSBC = Kawashirov.ShaderBaking.Commons;
using KFLTC = Kawashirov.FLT.Commons;
using SC = Kawashirov.StaticCommons;

using static UnityEditor.EditorGUI;

namespace Kawashirov.FLT {
	public enum BlendTemplate { Opaque, Cutout, Fade, FadeCutout, Custom = 256 }

	internal static partial class Commons {
		internal static readonly string RenderType = "KawaFLT_RenderType";
		internal static readonly string F_Debug = "KawaFLT_Feature_Debug"; // TODO
		internal static readonly string F_Instancing = "KawaFLT_Feature_Instancing";
	}

	public partial class Generator {
		public BlendTemplate mode = BlendTemplate.Opaque;
		public int queueOffset = 0;

		private void ConfigureBlending(ShaderSetup shader) {
			string q = null;
			if (mode == BlendTemplate.Opaque) {
				q = "Geometry";
				shader.tags[KST.RenderType] = "Opaque";
				shader.tags[KFLTC.RenderType] = "Opaque";
				shader.forward.srcBlend = BlendMode.One;
				shader.forward.dstBlend = BlendMode.Zero;
				shader.forward.zWrite = true;
				shader.forward_add.srcBlend = BlendMode.One;
				shader.forward_add.dstBlend = BlendMode.One;
				shader.forward_add.zWrite = false;
			} else if (mode == BlendTemplate.Cutout) {
				q = "AlphaTest";
				shader.tags[KST.RenderType] = "TransparentCutout";
				shader.tags[KFLTC.RenderType] = "Cutout";
				shader.Define("_ALPHATEST_ON 1");
				shader.forward.srcBlend = BlendMode.One;
				shader.forward.dstBlend = BlendMode.Zero;
				shader.forward.zWrite = true;
				shader.forward_add.srcBlend = BlendMode.One;
				shader.forward_add.dstBlend = BlendMode.One;
				shader.forward_add.zWrite = false;
			} else if (mode == BlendTemplate.Fade || mode == BlendTemplate.FadeCutout) {
				q = "Transparent";
				shader.tags[KST.RenderType] = "Transparent";
				shader.tags[KFLTC.RenderType] = "Fade";
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

	public partial class GeneratorEditor {
		private void BlendingGUI() {
			var mode = serializedObject.FindProperty("mode");
			DefaultPrpertyField(mode, "Blending Mode");
			var mode_int = !mode.hasMultipleDifferentValues ? mode.intValue : (int?)null;
			var mode_Opaque = mode_int.HasValue && mode_int.Value == (int)BlendTemplate.Opaque;
			var mode_Cutout = mode_int.HasValue && mode_int.Value == (int)BlendTemplate.Cutout;
			var mode_Fade = mode_int.HasValue && mode_int.Value == (int)BlendTemplate.Fade;

			if (!mode.hasMultipleDifferentValues && mode.intValue == (int)BlendTemplate.Custom) {
				EGUIL.HelpBox("Custom belding options currently in-dev and not yet supported.", MessageType.Error);
				error = true;
			}

			var queueOffset = serializedObject.FindProperty("queueOffset");
			DefaultPrpertyField(queueOffset);
			var queueOffset_int = !queueOffset.hasMultipleDifferentValues ? queueOffset.intValue : (int?)null;
			using (new DisabledScope(true)) {
				using (new IndentLevelScope()) {
					var queueOffset_str = "Mixed Values";
					if (queueOffset_int.HasValue && mode_int.HasValue) {
						string q = null;
						if (mode_int.Value == (int)BlendTemplate.Opaque) {
							q = "Geometry";
						} else if (mode_int.Value == (int)BlendTemplate.Cutout) {
							q = "AlphaTest";
						} else if (SC.AnyEq(mode_int.Value, (int)BlendTemplate.Fade, (int)BlendTemplate.FadeCutout)) {
							q = "Transparent";
						}
						queueOffset_str = string.Format("{0}{1:+#;-#;+0}", q, queueOffset_int.Value);
					}
					EGUIL.TextField("Queue", queueOffset_str);
				}
			}

			var forceNoShadowCasting = serializedObject.FindProperty("forceNoShadowCasting");
			DefaultPrpertyField(forceNoShadowCasting);
			var forceNoShadowCasting_bool = !forceNoShadowCasting.hasMultipleDifferentValues ? forceNoShadowCasting.boolValue : (bool?)null;
			if (forceNoShadowCasting_bool.HasValue && mode_int.HasValue && !forceNoShadowCasting_bool.Value && mode_int.Value == (int)BlendTemplate.Fade) {
				EGUIL.HelpBox(
					"Blending mode is \"Fade\", but \"Force No Shadow Casting\" is Off.\n" +
					"Usually transparent modes does not cast shadows, but this shader can use \"Cutout\" shadow caster for transparent modes.\n" +
					"It's better to disable shadow casting for \"Fade\" at all, unless you REALLY need it.",
					MessageType.Warning
				);
			}

			DefaultPrpertyField("cull");

			DefaultPrpertyField("instancing");

			using (new DisabledScope(true)) {
				DefaultPrpertyField("disableBatching");
			}

			DefaultPrpertyField("ignoreProjector");
		}
	}
}

internal partial class KawaFLTShaderGUI {
	protected void OnGUI_BlendMode() {
		var debug = shaderTags[KFLTC.F_Debug].IsTrue();
		var instancing = shaderTags[KFLTC.F_Instancing].IsTrue();

		if (instancing && debug) {
			materialEditor.EnableInstancingField();
		} else {
			using (new DisabledScope(!instancing)) {
				EGUIL.LabelField("Instancing", instancing ? "Enabled" : "Disabled");
			}
			foreach (var m in targetMaterials) {
				if (m && m.enableInstancing != instancing) {
					m.enableInstancing = instancing;
				}
			}
		}
	}

}

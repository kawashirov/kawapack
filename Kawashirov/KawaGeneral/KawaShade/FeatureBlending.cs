using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using System;
using System.Linq;

namespace Kawashirov.KawaShade {
	public class FeatureBlendMode : AbstractFeature {
		internal static readonly string ShaderTag_Cull = "KawaShade_Cull";

		[Serializable]
		public struct PassBlendMode {
			public bool enabled;
			public BlendMode srcBlend;
			public BlendMode dstBlend;
			public bool zWrite;
		}

		internal static bool deeper_shown = false;

		public override int GetOrder() => (int)Order.GENERAL + 10;

		public override void PopulateShaderTags(List<string> tags) {
			tags.Add(ShaderTag_Cull);
		}

		public override void ConfigureShaderEarly(KawaShadeGenerator gen, ShaderSetup shader) {

			shader.tags["Queue"] = string.Format("{0}{1:+#;-#;+0}", Enum.GetName(typeof(RenderQueue), gen.queue), gen.queueOffset);
			shader.tags[ShaderTag.RenderType] = gen.renderType;

			shader.forward.active = gen.forwardBase.enabled;
			shader.forward.srcBlend = gen.forwardBase.srcBlend;
			shader.forward.dstBlend = gen.forwardBase.dstBlend;
			shader.forward.zWrite = gen.forwardBase.zWrite;
			shader.forward.cullMode = gen.cull;

			shader.forward_add.active = gen.forwardAdd.enabled;
			shader.forward_add.srcBlend = gen.forwardAdd.srcBlend;
			shader.forward_add.dstBlend = gen.forwardAdd.dstBlend;
			shader.forward_add.zWrite = gen.forwardAdd.zWrite;
			shader.forward_add.cullMode = gen.cull;

			shader.TagBool(ShaderTag.ForceNoShadowCasting, !gen.shadowCast);
			shader.shadowcaster.active = gen.shadowCast;
			shader.shadowcaster.srcBlend = null;
			shader.shadowcaster.dstBlend = null;
			shader.shadowcaster.zWrite = null;
			shader.shadowcaster.cullMode = gen.cull;

			shader.TagEnum(ShaderTag_Cull, gen.cull);
			shader.TagBool(ShaderTag.IgnoreProjector, gen.ignoreProjector);
		}

		internal struct GeneratorProps {
			public GeneratorProps(KawaShadeGeneratorEditor editor) {
				queue = editor.serializedObject.FindProperty("queue");
				queueOffset = editor.serializedObject.FindProperty("queueOffset");
				renderType = editor.serializedObject.FindProperty("renderType");
				shadowCast = editor.serializedObject.FindProperty("shadowCast");

				fwB_enabled = editor.serializedObject.FindProperty("forwardBase.enabled");
				fwB_srcBlend = editor.serializedObject.FindProperty("forwardBase.srcBlend");
				fwB_dstBlend = editor.serializedObject.FindProperty("forwardBase.dstBlend");
				fwB_zWrite = editor.serializedObject.FindProperty("forwardBase.zWrite");

				fwA_enabled = editor.serializedObject.FindProperty("forwardAdd.enabled");
				fwA_srcBlend = editor.serializedObject.FindProperty("forwardAdd.srcBlend");
				fwA_dstBlend = editor.serializedObject.FindProperty("forwardAdd.dstBlend");
				fwA_zWrite = editor.serializedObject.FindProperty("forwardAdd.zWrite");

				cutoutFw = editor.serializedObject.FindProperty("cutoutForward.mode");
				cutoutSC = editor.serializedObject.FindProperty("cutoutShadowcaster.mode");
			}

			public SerializedProperty queue;
			public SerializedProperty queueOffset;
			public SerializedProperty renderType;
			public SerializedProperty shadowCast;

			public SerializedProperty fwB_enabled;
			public SerializedProperty fwB_srcBlend;
			public SerializedProperty fwB_dstBlend;
			public SerializedProperty fwB_zWrite;

			public SerializedProperty fwA_enabled;
			public SerializedProperty fwA_srcBlend;
			public SerializedProperty fwA_dstBlend;
			public SerializedProperty fwA_zWrite;

			public SerializedProperty cutoutFw;
			public SerializedProperty cutoutSC;
		}

		internal void GeneratorEditorGUI_PresetOpaque(GeneratorProps props) {
			props.queue.intValue = (int)RenderQueue.Geometry;
			props.queueOffset.intValue = 0;
			props.renderType.stringValue = "Opaque";

			props.fwB_enabled.boolValue = true;
			props.fwB_srcBlend.intValue = (int)BlendMode.One;
			props.fwB_dstBlend.intValue = (int)BlendMode.Zero;
			props.fwB_zWrite.boolValue = true;

			props.fwA_enabled.boolValue = true;
			props.fwA_srcBlend.intValue = (int)BlendMode.One;
			props.fwA_dstBlend.intValue = (int)BlendMode.One;
			props.fwA_zWrite.boolValue = false;

			props.shadowCast.boolValue = true;

			props.cutoutFw.intValue = (int)FeatureCutout.Mode.None;
			props.cutoutSC.intValue = (int)FeatureCutout.Mode.None;
		}
		internal void GeneratorEditorGUI_PresetCutout(GeneratorProps props) {
			props.queue.intValue = (int)RenderQueue.AlphaTest;
			props.queueOffset.intValue = 0;
			props.renderType.stringValue = "TransparentCutout";

			props.fwB_enabled.boolValue = true;
			props.fwB_srcBlend.intValue = (int)BlendMode.One;
			props.fwB_dstBlend.intValue = (int)BlendMode.Zero;
			props.fwB_zWrite.boolValue = true;

			props.fwA_enabled.boolValue = true;
			props.fwA_srcBlend.intValue = (int)BlendMode.One;
			props.fwA_dstBlend.intValue = (int)BlendMode.One;
			props.fwA_zWrite.boolValue = false;

			props.shadowCast.boolValue = true;

			props.cutoutFw.intValue = (int)FeatureCutout.Mode.Classic;
			props.cutoutSC.intValue = (int)FeatureCutout.Mode.Classic;
		}

		internal void GeneratorEditorGUI_PresetFade(GeneratorProps props) {
			props.queue.intValue = (int)RenderQueue.Transparent;
			props.queueOffset.intValue = 0;
			props.renderType.stringValue = "Transparent";

			props.fwB_enabled.boolValue = true;
			props.fwB_srcBlend.intValue = (int)BlendMode.SrcAlpha;
			props.fwB_dstBlend.intValue = (int)BlendMode.OneMinusSrcAlpha;
			props.fwB_zWrite.boolValue = false;

			props.fwA_enabled.boolValue = true;
			props.fwA_srcBlend.intValue = (int)BlendMode.SrcAlpha;
			props.fwA_dstBlend.intValue = (int)BlendMode.One;
			props.fwA_zWrite.boolValue = false;

			props.shadowCast.boolValue = false;

			props.cutoutFw.intValue = (int)FeatureCutout.Mode.None;
			props.cutoutSC.intValue = (int)FeatureCutout.Mode.Classic;
		}

		internal void GeneratorEditorGUI_Deeper(KawaShadeGeneratorEditor editor, GeneratorProps props) {
			EditorGUILayout.PropertyField(props.queue, new GUIContent("Queue"));
			using (new EditorGUI.DisabledScope(true)) {
				using (new EditorGUI.IndentLevelScope()) {
					EditorGUILayout.PropertyField(props.queueOffset, new GUIContent("Offset"));
					var queue_str = "Mixed Values";
					if (!props.queue.hasMultipleDifferentValues && !props.queueOffset.hasMultipleDifferentValues) {
						var q = Enum.GetName(typeof(RenderQueue), props.queue.intValue);
						queue_str = string.Format("{0}{1:+#;-#;+0}", q, props.queueOffset.intValue);
					}
					EditorGUILayout.TextField("Tag", queue_str);
				}
			}

			EditorGUILayout.PropertyField(props.renderType, new GUIContent("Render Type"));

			EditorGUILayout.PropertyField(props.shadowCast, new GUIContent("Cast Shadows"));
			if (!props.shadowCast.hasMultipleDifferentValues && !props.fwB_dstBlend.hasMultipleDifferentValues
				&& props.shadowCast.boolValue == true && props.fwB_dstBlend.intValue != (int)BlendMode.Zero) {
				EditorGUILayout.HelpBox(
					"DstBlend mode is not \"Zero\" and \"Shadow Cast\" is On!\n" +
					"Usually transparent modes does not cast shadows, however this shader can use \"Cutout\" shadow caster for transparent modes.\n" +
					"It's better to disable shadow casting for \"Fade\" at all, unless you REALLY need it.",
					MessageType.Warning
				);
			}

			EditorGUILayout.LabelField("Blending Options");
			using (new EditorGUI.IndentLevelScope()) {
				var rects = EditorGUILayout.GetControlRect(false).RectSplitHorisontal(1, 1, 1).ToArray();
				EditorGUI.LabelField(rects[0], "Pass");
				EditorGUI.LabelField(rects[1], "Forward Base");
				EditorGUI.LabelField(rects[2], "Forward Add");

				rects = EditorGUILayout.GetControlRect(false).RectSplitHorisontal(1, 1, 1).ToArray();
				EditorGUI.LabelField(rects[0], "Enabled");
				EditorGUI.PropertyField(rects[1], props.fwB_enabled, GUIContent.none);
				EditorGUI.PropertyField(rects[2], props.fwA_enabled, GUIContent.none);

				rects = EditorGUILayout.GetControlRect(false).RectSplitHorisontal(1, 1, 1).ToArray();
				EditorGUI.LabelField(rects[0], "SrcBlend");
				EditorGUI.PropertyField(rects[1], props.fwB_srcBlend, GUIContent.none);
				EditorGUI.PropertyField(rects[2], props.fwA_srcBlend, GUIContent.none);

				rects = EditorGUILayout.GetControlRect(false).RectSplitHorisontal(1, 1, 1).ToArray();
				EditorGUI.LabelField(rects[0], "DstBlend");
				EditorGUI.PropertyField(rects[1], props.fwB_dstBlend, GUIContent.none);
				EditorGUI.PropertyField(rects[2], props.fwA_dstBlend, GUIContent.none);

				rects = EditorGUILayout.GetControlRect(false).RectSplitHorisontal(1, 1, 1).ToArray();
				EditorGUI.LabelField(rects[0], "ZWrite");
				EditorGUI.PropertyField(rects[1], props.fwB_zWrite, GUIContent.none);
				EditorGUI.PropertyField(rects[2], props.fwA_zWrite, GUIContent.none);
			}

		}

		public override void GeneratorEditorGUI(KawaShadeGeneratorEditor editor) {
			GUILayout.Label("Render Mode", EditorStyles.boldLabel);
			var props = new GeneratorProps(editor);

			using (new EditorGUILayout.HorizontalScope()) {
				GUILayout.Label("Presets");
				if (GUILayout.Button("Opaque")) {
					GeneratorEditorGUI_PresetOpaque(props);
				}
				if (GUILayout.Button("Cutout")) {
					GeneratorEditorGUI_PresetCutout(props);
				}
				if (GUILayout.Button("Fade")) {
					GeneratorEditorGUI_PresetFade(props);
				}
			}

			deeper_shown = EditorGUILayout.Foldout(deeper_shown, "Deeper Properties");
			if (deeper_shown) {
				using (new EditorGUI.IndentLevelScope()) {
					GeneratorEditorGUI_Deeper(editor, props);
				}
			}

			EditorGUILayout.LabelField("Extra Options");
			using (new EditorGUI.IndentLevelScope()) {
				KawaGUIUtility.DefaultPrpertyField(editor, "cull");
				KawaGUIUtility.DefaultPrpertyField(editor, "ignoreProjector");
			}
		}

		public override void ShaderEditorGUI(KawaShadeGUI editor) {

		}
	}

	public partial class KawaShadeGenerator {
		public RenderQueue queue = RenderQueue.Geometry;
		public int queueOffset = 0;
		public string renderType = "Opaque";
		public FeatureBlendMode.PassBlendMode forwardBase = new FeatureBlendMode.PassBlendMode() {
			enabled = true, srcBlend = BlendMode.One, dstBlend = BlendMode.Zero, zWrite = true
		};
		public FeatureBlendMode.PassBlendMode forwardAdd = new FeatureBlendMode.PassBlendMode() {
			enabled = true, srcBlend = BlendMode.One, dstBlend = BlendMode.One, zWrite = false
		};
		public bool shadowCast = true;

		public CullMode cull = CullMode.Back;
		public bool ignoreProjector = true;
	}
}

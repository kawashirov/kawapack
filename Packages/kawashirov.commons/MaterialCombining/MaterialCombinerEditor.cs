#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Kawashirov.Refreshables;
using UnityEditor;
using System;

namespace Kawashirov.MaterialCombining {
	[CustomEditor(typeof(MaterialCombiner), true)]
	public class MaterialCombinerEditor : KawaEditorBehaviour.KawaEditorBehaviourEditor {
		public SerializedProperty WholeScene;

		public override bool ShowIKnowWhatIamDoing() => true;

		protected override void OnEnable() {
			Prop("AtlasDebugMaterials").isExpanded = false;

			Prop("OriginalTextures").isExpanded = false;
			Prop("OriginalMaterials").isExpanded = false;
			Prop("AtlasTextures").isExpanded = false;
			Prop("AtlasMaterials").isExpanded = false;
			Prop("AtlasMeshes").isExpanded = false;
		}

		protected void HierarchyGUI() {
			var WholeScene = Prop("WholeScene");
			var Hierarchy = Prop("Hierarchy");
			EditorGUI.BeginChangeCheck();
			KawaGUIUtility.ToggleLeft(WholeScene);
			if (EditorGUI.EndChangeCheck() && !WholeScene.hasMultipleDifferentValues) {
				Hierarchy.isExpanded = !WholeScene.boolValue;
			}
			using (new EditorGUI.DisabledScope(!WholeScene.hasMultipleDifferentValues && WholeScene.boolValue)) {
				EditorGUILayout.PropertyField(Prop("Hierarchy"));
			}
		}

		protected void AdaptersGUI() {
			GUILayout.Label("Adapters", EditorStyles.boldLabel);
			var MainAdapter = Prop("MainAdapter");
			var SecondaryAdapters = Prop("SecondaryAdapters");
			EditorGUILayout.PropertyField(MainAdapter, new GUIContent("Main"));
			EditorGUILayout.PropertyField(SecondaryAdapters, new GUIContent("Secondary"));
			if (MainAdapter.hasMultipleDifferentValues || SecondaryAdapters.hasMultipleDifferentValues)
				return;

			var obj = MainAdapter.objectReferenceValue;
			if (obj == null) {
				KawaGUIUtility.HelpBoxRich("<b>Main adapater is not set!</b>");
				return;
			}

			if (KawaGUIUtility.Contains(SecondaryAdapters, obj))
				return;

			if (KawaGUIUtility.HelpBoxWithButton(new GUIContent(
				"<b>Main adapater is not listed in secondary adapters.</b> " +
				"This might be OK in some specific scenarios, but you likely want to " +
				"use main adapter as secondary for decoding original input materials."
			), new GUIContent("Add"))) {
				SecondaryAdapters.InsertArrayElementAtIndex(0);
				SecondaryAdapters.GetArrayElementAtIndex(0).objectReferenceValue = obj;
				SecondaryAdapters.isExpanded = true;
			}
		}

		protected void AtlasSizeGUI() {
			var MaxAtlasSize = Prop("MaxAtlasSize");
			// EditorGUILayout.PropertyField(MaxAtlasSize);
			KawaGUIUtility.PowerSlider(MaxAtlasSize, 4, 16 * 1024, 4);
			if (MaxAtlasSize.hasMultipleDifferentValues)
				return;

			if (4 > MaxAtlasSize.intValue || MaxAtlasSize.intValue > 16 * 1024)
				MaxAtlasSize.intValue = Mathf.Clamp(MaxAtlasSize.intValue, 4, 16 * 1024);

			if (IKnowWhatIamDoing)
				return;

			if (!Mathf.IsPowerOfTwo(MaxAtlasSize.intValue) && KawaGUIUtility.HelpBoxWithButton(new GUIContent(
				$"<b>Atlas size</b> should be a <b>power of 2</b> (.., 1024, 2048 ,4096, ..). " +
				"NPOT values will work too, but might not give expected results."
			), new GUIContent("Fix"))) {
				MaxAtlasSize.intValue = Mathf.Clamp(Mathf.ClosestPowerOfTwo(MaxAtlasSize.intValue), 4, 16 * 1024);
				return;
			}
		}

		protected void AtlasLayoutPxGUI() {
			var Align = Prop("IslandsAlignPx");
			var Epsilon = Prop("IslandsEpsilonPx");
			var Padding = Prop("IslandsPaddingPx");

			// EditorGUILayout.PropertyField(Epsilon);
			// EditorGUILayout.PropertyField(Padding);
			// EditorGUILayout.PropertyField(Align);

			KawaGUIUtility.PowerSlider(Epsilon, 0, 16, 2);
			KawaGUIUtility.PowerSlider(Padding, 0, 16, 2);
			KawaGUIUtility.PowerSlider(Align, 0, 16, 2);

			if (Align.hasMultipleDifferentValues || Epsilon.hasMultipleDifferentValues || Padding.hasMultipleDifferentValues)
				return;

			if (Align.intValue < 0)
				Align.intValue = 0;

			if (Epsilon.intValue < 0)
				Epsilon.intValue = 0;

			if (Padding.intValue < 0)
				Padding.intValue = 0;

			if (IKnowWhatIamDoing)
				return;

			if (!Mathf.IsPowerOfTwo(Align.intValue) && KawaGUIUtility.HelpBoxWithButton(new GUIContent(
				"<b>Align</b> should be a <b>power of 2</b> (.., 4, 8, 16, ..). " +
				"Other values will work too, but might not give expected results."
			), new GUIContent("Fix"))) {
				Align.intValue = Mathf.ClosestPowerOfTwo(Align.intValue);
				return;
			}


			var min = Mathf.Min(Align.intValue, Epsilon.intValue, Padding.intValue);
			var max = Mathf.Max(Align.intValue, Epsilon.intValue, Padding.intValue);
			if (!IKnowWhatIamDoing && 1f * max / min > 3) {
				KawaGUIUtility.HelpBoxRich("<b>Align, Epsilon and Padding</b> should be roughly <b>the same value</b>. " +
				"Spread values will work too, but might not give expected results.");
			}

			if ((Epsilon.intValue < 2 || Padding.intValue < 4) && KawaGUIUtility.HelpBoxWithButton(new GUIContent(
				"<b>Epsilon and Padding</b> shouldn't be too small. " +
				"Small values will work too, but might not give expected results."
			), new GUIContent("Fix"))) {
				Epsilon.intValue = Mathf.Max(Epsilon.intValue, 2);
				Padding.intValue = Mathf.Max(Padding.intValue, 4);
				return;
			}

		}

		protected void PropertiesGUI() {
			ScriptGUI();

			EditorGUILayout.Space();

			HierarchyGUI();

			EditorGUILayout.Space();

			EditorGUILayout.PropertyField(Prop("Filters"));

			EditorGUILayout.Space();

			AdaptersGUI();

			EditorGUILayout.Space();

			EditorGUILayout.PropertyField(Prop("AtlasLayout"));
			AtlasSizeGUI();
			AtlasLayoutPxGUI();
			using (new EditorGUI.DisabledScope(!IKnowWhatIamDoing)) {
				EditorGUILayout.PropertyField(Prop("AtlasDebugMaterials"), new GUIContent("Debug Materials"));
			}

			EditorGUILayout.Space();

			// EditorGUILayout.PropertyField(Prop("MaxStallTime"));
			KawaGUIUtility.PowerSlider(Prop("MaxStallTime"), 0, 10, 1.5f);

			EditorGUILayout.Space();

			EditorGUILayout.PropertyField(Prop("SaveMeshes"));
			EditorGUILayout.PropertyField(Prop("SaveMaterials"));
			EditorGUILayout.PropertyField(Prop("UniqueAssetNames"));

			EditorGUILayout.Space();

			GUILayout.Label("Properties below are auto-generated", EditorStyles.boldLabel);
			using (new EditorGUI.DisabledScope(!IKnowWhatIamDoing)) {
				EditorGUILayout.PropertyField(Prop("OriginalTextures"));
				EditorGUILayout.PropertyField(Prop("OriginalMaterials"));
				EditorGUILayout.PropertyField(Prop("AtlasTextures"));
				EditorGUILayout.PropertyField(Prop("AtlasMaterials"));
				EditorGUILayout.PropertyField(Prop("AtlasMeshes"));
			}
		}

		public override void OnInspectorGUI() {
			IKnowWhatIamDoingGUI();
			DebugModeGUI();

			EditorGUI.BeginChangeCheck();
			serializedObject.UpdateIfRequiredOrScript();

			PropertiesGUI();

			serializedObject.ApplyModifiedProperties();
			EditorGUI.EndChangeCheck();
			
			this.BehaviourRefreshGUI();
		}

	}
}
#endif
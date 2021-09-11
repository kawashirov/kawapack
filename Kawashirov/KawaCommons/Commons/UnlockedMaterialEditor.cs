using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Kawashirov {
	class UnlockedMaterialEditor : MaterialEditor {
		private static BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		private static BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
		public static FieldInfo m_PropertyBlock = typeof(MaterialEditor).GetField("m_PropertyBlock", AnyInstance);
		public static FieldInfo m_InsidePropertiesGUI = typeof(MaterialEditor).GetField("m_InsidePropertiesGUI", AnyInstance);
		public static FieldInfo m_RenderersForAnimationMode = typeof(MaterialEditor).GetField("m_RenderersForAnimationMode", AnyInstance);
		public static MethodInfo DetectTextureStackValidationIssues = typeof(MaterialEditor).GetMethod("DetectTextureStackValidationIssues", AnyInstance);
		public static MethodInfo GetAssociatedRenderersFromInspector = typeof(MaterialEditor).GetMethod("GetAssociatedRenderersFromInspector", AnyStatic);
		public static Type Styles = typeof(MaterialEditor).GetNestedType("Styles", AnyStatic);
		public static FieldInfo Styles_propBlockInfo = Styles.GetField("propBlockInfo", AnyStatic);
		public static MethodInfo Array_PrepareMaterialPropertiesForAnimationMode = typeof(MaterialEditor).GetMethods(AnyStatic)
			.Where(m => m.Name == "PrepareMaterialPropertiesForAnimationMode" && m.ReturnType == typeof(Renderer[])).First();

		public override void OnInspectorGUI() {
			serializedObject.Update();

			// CheckSetup();
			// DetectShaderEditorNeedsUpdate();
			// HasMultipleMixedShaderValues()
			//if (isVisible && m_Shader != null && !HasMultipleMixedShaderValues()) {
			//	// Show Material properties
			//}

			if (isVisible) {
				if (KawaPropertiesGUI()) {
					PropertiesChanged();
				}
			}


			DetectTextureStackValidationIssues?.Invoke(this, new object[0]);
		}

		private bool KawaPropertiesGUI() {
			// Almost full-copy of MaterialEditor.PropertiesGUI()

			// OnInspectorGUI is wrapped inside a BeginVertical/EndVertical block that adds padding,
			// which we don't want here so we could have the VC bar span the entire Material Editor width
			// we stop the vertical block, draw the VC bar, and then start a new vertical block with the same style.
			// var style = GUILayoutUtility.topLevel.style;
			// EditorGUILayout.EndVertical();

			// setting the GUI to enabled where the VC status bar is drawn because it gets disabled by the parent inspector
			// for non-checked out materials, and we need the version control status bar to be always active
			bool wasGUIEnabled = GUI.enabled;
			GUI.enabled = true;

			// Material Editor is the first inspected editor when accessed through the Project panel
			// and this is the scenario where we do not want to redraw the VC status bar
			// since InspectorWindow already takes care of that. Otherwise, the Material Editor
			// is not the first inspected editor (e.g. when it's a part of a GO Inspector)
			// thus we draw the VC status bar
			//if (!firstInspectedEditor) {
			//	InspectorWindow.VersionControlBar(this);
			//}

			GUI.enabled = wasGUIEnabled;
			// EditorGUILayout.BeginVertical(style);

			if ((bool)m_InsidePropertiesGUI.GetValue(this)) {
				Debug.LogWarning("PropertiesGUI() is being called recursively. If you want to render the default gui for shader properties then call PropertiesDefaultGUI() instead");
				return false;
			}

			EditorGUI.BeginChangeCheck();

			MaterialProperty[] props = GetMaterialProperties(targets);

			// In animation mode we are actually animating the Renderer instead of the material.
			// Thus all properties are editable even if the material is not editable.
			// m_RenderersForAnimationMode = PrepareMaterialPropertiesForAnimationMode(props, GetAssociatedRenderersFromInspector(), GUI.enabled);
			var renderers1 = (Renderer[])GetAssociatedRenderersFromInspector.Invoke(null, new object[0]);
			renderers1 = (Renderer[])Array_PrepareMaterialPropertiesForAnimationMode.Invoke(null, new object[] { props, renderers1, GUI.enabled });
			m_RenderersForAnimationMode.SetValue(this, renderers1);

			bool wasEnabled = GUI.enabled;
			if (m_RenderersForAnimationMode != null)
				GUI.enabled = true;

			m_InsidePropertiesGUI.SetValue(this, true);

			// Since ExitGUI is called when showing the Object Picker we wrap
			// properties gui in try/catch to catch the ExitGUIException thrown by ExitGUI()
			// to ensure our m_InsidePropertiesGUI flag is reset
			try {
				CustomPropertiesGUI();

				var renderers2 = (Renderer[])GetAssociatedRenderersFromInspector.Invoke(null, new object[0]);
				if (renderers2 != null && renderers2.Length > 0) {
					var _m_PropertyBlock = (MaterialPropertyBlock)m_PropertyBlock.GetValue(this);
					if (Event.current.type == EventType.Layout) {
						renderers2[0].GetPropertyBlock(_m_PropertyBlock);
					}
					if (_m_PropertyBlock != null && !_m_PropertyBlock.isEmpty)
						EditorGUILayout.HelpBox((string)Styles_propBlockInfo.GetValue(null), MessageType.Info);
				}
			} catch (Exception) {
				GUI.enabled = wasEnabled;
				m_InsidePropertiesGUI.SetValue(this, false);
				m_RenderersForAnimationMode.SetValue(this, null);
				throw;
			}

			GUI.enabled = wasEnabled;
			m_InsidePropertiesGUI.SetValue(this, false);
			m_RenderersForAnimationMode.SetValue(this, null);

			return EditorGUI.EndChangeCheck();
		}

		public virtual void CustomPropertiesGUI() {

		}


	}
}

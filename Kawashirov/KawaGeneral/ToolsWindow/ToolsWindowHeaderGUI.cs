#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using static Kawashirov.KawaGUIUtility;

namespace Kawashirov.ToolsGUI {
	internal class ToolsWindowHeaderGUI {
		internal GUIStyle marginStyle;
		internal GUIStyle labelStyle;

		internal GUILayoutOption minHeightBase;
		internal GUILayoutOption maxHeightBase;

		internal GUILayoutOption expandWidth = GUILayout.ExpandWidth(true);
		internal GUILayoutOption minWidthBase2;
		internal GUILayoutOption minWidthBase3;
		internal GUILayoutOption maxWidthBase4;

		internal GUIContent contentMenu;
		internal GUIContent contentSelectTool;

		public ToolsWindowHeaderGUI() {
			var baseMargin = Mathf.RoundToInt(EditorGUIUtility.singleLineHeight / 4);
			var baseHeight = EditorGUIUtility.singleLineHeight * 2;

			marginStyle = new GUIStyle(GUIStyle.none) {
				margin = new RectOffset(baseMargin, baseMargin, baseMargin, baseMargin)
			};
			labelStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };

			minHeightBase = GUILayout.MinHeight(baseHeight);
			maxHeightBase = GUILayout.MaxHeight(baseHeight);

			minWidthBase2 = GUILayout.MinWidth(baseHeight * 2);
			minWidthBase3 = GUILayout.MinWidth(baseHeight * 3);
			maxWidthBase4 = GUILayout.MaxWidth(baseHeight * 4);

			contentMenu = new GUIContent("Toolbox\nMenu", kawaIcon.Value);
			contentSelectTool = new GUIContent("Select Tool Panel");
		}

		internal void OnHeaderGUI(ToolsWindow window) {
			using (new EditorGUILayout.HorizontalScope(marginStyle, minHeightBase, maxHeightBase, expandWidth)) {
				if (GUILayout.Button(contentMenu, minHeightBase, maxHeightBase, minWidthBase2, maxWidthBase4)) {
					window.currentPanel = null;
					if (!window.panelsLoaded) {
						window.ReloadPanels();
					}
				}

				var labelContent = GUIContent.none;
				if (window.currentPanel == null) {
					labelContent = contentSelectTool;
				} else {
					var headerContent = window.currentPanel.GetMenuHeaderContent();
					if (headerContent != null) {
						labelContent = headerContent;
					}
				}
				GUILayout.Label(labelContent, labelStyle, minHeightBase, maxHeightBase, minWidthBase3, expandWidth);

				if (GUILayout.Button("Reload\nPanels", minHeightBase, maxHeightBase, minWidthBase2, maxWidthBase4)) {
					window.ReloadPanels();
				}
			}
		}

	}
}
#endif // UNITY_EDITOR

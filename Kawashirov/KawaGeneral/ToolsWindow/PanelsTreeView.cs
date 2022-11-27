#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace Kawashirov.ToolsGUI {
	public class PanelsTreeView : TreeView {
		private static Lazy<GUIStyle> leftButtonStyle = new Lazy<GUIStyle>(initLeftButtonStyle);

		public List<AbstractToolPanel> panelInstances;
		public float lineScale = 1.5f;

		private int indexes = 0;

		private class PanelsTreeItem : TreeViewItem {
			public AbstractToolPanel panel;
		}

		private static GUIStyle initLeftButtonStyle() {
			var style = new GUIStyle(GUI.skin.button);
			style.alignment = TextAnchor.MiddleLeft;
			//style.margin.left = 3;
			//style.margin.right = 3;
			//style.margin.top = 3;
			//style.margin.bottom = 3;
			return style;
		}

		public PanelsTreeView() : base(new TreeViewState()) {
			rowHeight = EditorGUIUtility.singleLineHeight * lineScale;
			showAlternatingRowBackgrounds = true;
		}

		private void AddPanel(PanelsTreeItem root, AbstractToolPanel panel) {
			var path = panel.GetMenuPath();
			var item = root;
			for (var i = 0; i < path.Length; ++i) {
				PanelsTreeItem childItem = null;
				if (item.hasChildren) {
					childItem = item.children.Cast<PanelsTreeItem>()
					.Where(x => string.Equals(x.displayName, path[i], StringComparison.InvariantCultureIgnoreCase))
					.FirstOrDefault();
				}
				if (childItem == null) {
					childItem = new PanelsTreeItem { id = ++indexes, displayName = path[i] };
					item.AddChild(childItem);
					childItem.depth = item.depth + 1;
					state.expandedIDs.Add(childItem.id);
					if (item.children.Count > 1) {
						item.children.Sort();
					}
				}

				item = childItem;
			}
			item.panel = panel;

		}

		protected override TreeViewItem BuildRoot() {
			var root = new PanelsTreeItem() { depth = -1, id = ++indexes, };

			foreach (var panel in panelInstances) {
				AddPanel(root, panel);
			}

			return root;
		}

		protected override void RowGUI(RowGUIArgs args) {
			var item = args.item as PanelsTreeItem;
			if (item == null || item.panel == null) {
				var padding = (lineScale - 1.0f) / 2;
				var rowPadded = args.rowRect.RectSplitVertical(padding, 1, padding).ToArray();
				args.rowRect = rowPadded[1];
				base.RowGUI(args);
			} else {
				var indent = GetContentIndent(args.item);
				var rect = args.rowRect;
				rect.x += indent;
				// margin doesnt work for some reason
				rect.y += 2f;
				rect.height -= 3f;
				var guiContent = item.panel.GetMenuButtonContent();
				if (guiContent == null) {
					var image = EditorGUIUtility.IconContent("CustomTool").image;
					guiContent = new GUIContent(args.item.displayName, image);
				}
				if (GUI.Button(rect, guiContent, leftButtonStyle.Value)) {
					ToolsWindow.ActivatePanel(item.panel);
				}
			}
		}

	}
}
#endif // UNITY_EDITOR

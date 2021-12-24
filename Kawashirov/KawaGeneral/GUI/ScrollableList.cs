using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Kawashirov {
	public struct ScrollableList {
		// Устанавливаются через "конструктор"
		private int totalItems;
		private int visibleItems;
		private Rect fullRect;
		private float header;
		private float footer;
		private float scrollWidth;
		private float scrollValue;

		private Rect scrollRect;
		private Rect headerRect;
		private Rect footerRect;
		private Rect itemsRect;
		private Rect[] rows;

		public static ScrollableList AutoLayout(int totalItems, int visibleItems, float? header = null, float? footer = null, float? lineHeight = null, float? scrollWidth = null) {
			if (!lineHeight.HasValue)
				lineHeight = EditorGUIUtility.singleLineHeight;

			// header exist by default in auto 
			if (!header.HasValue)
				header = lineHeight.Value;

			// footer exist by default in auto layout
			if (!footer.HasValue)
				footer = lineHeight.Value;

			// scroll width same as lines height by default in auto layout
			if (!scrollWidth.HasValue)
				scrollWidth = lineHeight;

			var fullRect = EditorGUILayout.GetControlRect(false, (lineHeight.Value + 2) * visibleItems - 2 + header.Value + footer.Value);
			fullRect = EditorGUI.IndentedRect(fullRect);

			return ManualLayout(fullRect, totalItems, visibleItems, scrollWidth.Value, header, footer);
		}

		public static ScrollableList ManualLayout(Rect fullRect, int totalItems, int visibleItems, float scrollWidth, float? header = null, float? footer = null) {
			return new ScrollableList {
				fullRect = fullRect,
				totalItems = totalItems,
				visibleItems = visibleItems,
				scrollWidth = scrollWidth,
				// header is missing by default in manual layout
				header = header ?? 0,
				// footer is missing by default in manual layout
				footer = footer ?? 0,
			};
		}

		public void DrawList(ref float scroll) {
			scrollRect = new Rect(fullRect);
			itemsRect = new Rect(fullRect);
			scrollRect.width = scrollWidth;
			itemsRect.width -= scrollWidth;
			scrollRect.xMin += itemsRect.width;
			itemsRect.width -= 2; // spacing

			headerRect = new Rect(itemsRect);
			headerRect.height = header;
			itemsRect.yMin += header + 2; // spacing

			footerRect = new Rect(itemsRect);
			footerRect.yMin = footerRect.yMax - footer;
			itemsRect.yMax -= footer + 2; // spacing

			rows = itemsRect.RectSplitVerticalUniform(visibleItems).ToArray();

			scroll = GUI.VerticalScrollbar(scrollRect, scroll, visibleItems, 0, totalItems);
			scrollValue = scroll;

			// GUI.Box(itemsRect, GUIContent.none, EditorStyles.helpBox);
		}

		public Rect GetHeader() => headerRect;

		public Rect GetFooter() => footerRect;

		public Rect GetRow(int visibleIndex, out int totalIndex) {
			totalIndex = Mathf.Clamp(Mathf.RoundToInt(scrollValue + visibleIndex), 0, totalItems - 1);
			return rows[visibleIndex];
		}
	}
}

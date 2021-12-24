#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using Kawashirov.ToolsGUI;
using static Kawashirov.KawaUtilities;
using Object = UnityEngine.Object;

namespace Kawashirov {
	[ToolsWindowPanel("Objects/Reveal Hidden")]
	public class HiddenObjectsToolPanel : AbstractToolPanel {

		private struct FoundObject {
			public Object obj;
			public Object pingObject;
		}

		private List<FoundObject> foundObjects = new List<FoundObject>();
		[NonSerialized] public bool ignoreForeignAsset = true;
		[NonSerialized] public int foundObjectsDisplaySize = 20;
		[NonSerialized] public float foundObjectsDisplayOffset = 0;

		public static bool IsHiddenInHierarchy(Object obj) => (obj.hideFlags & HideFlags.HideInHierarchy) != 0;
		public static bool IsHiddenInInspector(Object obj) => (obj.hideFlags & HideFlags.HideInInspector) != 0;
		public static bool IsNotEditable(Object obj) => (obj.hideFlags & HideFlags.NotEditable) != 0;
		public static bool IsHidden(Object obj) => IsHiddenInHierarchy(obj) || IsHiddenInInspector(obj) || IsNotEditable(obj);

		private static StringBuilder GetFlagsString_StringBuilder = new StringBuilder();
		public static string GetFlagsString(Object obj) {
			GetFlagsString_StringBuilder.Clear();
			if (IsHiddenInHierarchy(obj))
				GetFlagsString_StringBuilder.Append("H");
			if (IsHiddenInInspector(obj))
				GetFlagsString_StringBuilder.Append("I");
			if (IsNotEditable(obj))
				GetFlagsString_StringBuilder.Append("E");
			if (GetFlagsString_StringBuilder.Length < 1)
				GetFlagsString_StringBuilder.Append("0");
			return GetFlagsString_StringBuilder.ToString();
		}

		public void FindHiddenInHierarchy() {
			foundObjects.Clear();
			var components = new List<Component>();
			var allGameObjects = IterScenesRoots().SelectMany(KawaUtilities.Traverse).Where(x => x != null);
			foreach (var gobj in allGameObjects) {
				if (IsHidden(gobj)) {
					foundObjects.Add(new FoundObject() { obj = gobj, pingObject = gobj });
				}
				components.Clear();
				gobj.GetComponents(typeof(Component), components);
				foreach (var component in components) {
					if (component != null && IsHidden(component)) {
						foundObjects.Add(new FoundObject() { obj = component, pingObject = gobj });
					}
				}
			}
		}

		private void FindHiddenInProject(string assetPath, ref int totalObjects) {
			var wasLoaded = AssetDatabase.IsMainAssetAtPathLoaded(assetPath);
			var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
			if (mainAsset == null)
				return; // Странная ситуация, но бывет.
			if (ignoreForeignAsset && AssetDatabase.IsForeignAsset(mainAsset)) {
				if (!wasLoaded && !(mainAsset is GameObject || mainAsset is Component)) {
					Resources.UnloadAsset(mainAsset);
				}
				return;
			}
			var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
			for (var j = 0; j < assets.Length; ++j) {
				++totalObjects;
				var asset = assets[j];
				if (asset == null)
					continue; // Странная ситуация, но бывет.
				if (ignoreForeignAsset && AssetDatabase.IsForeignAsset(asset)) {
					if (!wasLoaded && !(asset is GameObject || asset is Component)) {
						// Подразумеваем, что если дочерние ассеты прогружены, то и главный тоже был прогружен,
						// а если главный тоже не был прогружен, то дочерние ассеты тоже не были прогружены
						Resources.UnloadAsset(asset);
					}
					continue;
				}
				if (IsHidden(assets[j])) {
					foundObjects.Add(new FoundObject() { obj = asset, pingObject = mainAsset == null ? asset : mainAsset });
				}
			}
		}

		public void FindHiddenInProject() {
			try {
				EditorUtility.DisplayProgressBar("Searching Hidden Assets...", "Preparing...", 0);
				var totalObjects = 0;
				foundObjects.Clear();
				var assetPaths = AssetDatabase.GetAllAssetPaths();
				var progressBarTime = -1f;
				for (var i = 0; i < assetPaths.Length; ++i) {
					if (progressBarTime < Time.realtimeSinceStartup) {
						//Resources.UnloadUnusedAssets();
						progressBarTime = Time.realtimeSinceStartup + 0.1f;
						var msg = $"Objects: {foundObjects.Count}/{totalObjects}, Files: {i + 1}/{assetPaths.Length}...";
						if (EditorUtility.DisplayCancelableProgressBar("Searching Hidden Assets...", msg, (i + 1f) / (assetPaths.Length + 1f))) {
							foundObjects.Clear();
							break;
						}
					}
					FindHiddenInProject(assetPaths[i], ref totalObjects);
				}
			} finally {
				try {
					EditorUtility.DisplayProgressBar("Searching Hidden Assets...", "Unloading Unused Assets...", 0);
					EditorUtility.UnloadUnusedAssetsImmediate();
				} finally {
					EditorUtility.ClearProgressBar();
				}
			}
		}

		private void RevealObject(Object obj, HideFlags flagsToUnset) {
			obj.hideFlags &= ~flagsToUnset;
			EditorUtility.SetDirty(obj);
		}

		private void RevealObjects(HideFlags flagsToUnset) {
			foundObjects.RemoveAll(x => x.obj == null); // Remove destroyed objects if any
			try {
				foreach (var found in foundObjects) {
					Undo.RegisterFullObjectHierarchyUndo(found.obj, "Reveal Hidden");
					RevealObject(found.obj, flagsToUnset);
				}
			} finally {
				Undo.FlushUndoRecordObjects();
			}
		}

		public static Lazy<GUIStyle> labelCentred = new Lazy<GUIStyle>(() => new GUIStyle(EditorStyles.label) {
			alignment = TextAnchor.MiddleCenter
		});

		public override void ToolsGUI() {

			EditorGUILayout.LabelField("List Hidden Objects:", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope(1)) {
				var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2));
				var rects = rect.RectSplitHorisontal(1, 1).ToArray();
				if (GUI.Button(rects[0], "On Scenes")) {
					FindHiddenInHierarchy();
				}
				if (GUI.Button(rects[1], "In Project")) {
					FindHiddenInProject();
				}
				ignoreForeignAsset = EditorGUILayout.ToggleLeft("Ignore Foreign Assets", ignoreForeignAsset);
			}

			EditorGUILayout.LabelField("Found Hidden Objects:", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope(1)) {
				foundObjects.RemoveAll(x => x.obj == null); // Remove destroyed objects if any
				if (foundObjects.Count < 1) {
					EditorGUILayout.LabelField("No Objects");
				} else {
					foundObjectsDisplaySize = EditorGUILayout.IntField("Max Display Items", foundObjectsDisplaySize);
					var numberOfItemsToShow = Mathf.Min(foundObjectsDisplaySize, foundObjects.Count);
					EditorGUILayout.LabelField($"{numberOfItemsToShow}/{foundObjects.Count}  Objects");
					// var maxOffset = foundObjects.Count - numberOfItemsToShow;
					{
						// Header
						var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
						var rects = rect.RectSplitHorisontal(1, 8, 1, 2, 2).ToArray();
						using (new KawaGUIUtility.ZeroIndentScope()) {
							EditorGUI.LabelField(rects[0], "#");
							EditorGUI.LabelField(rects[1], "Object", labelCentred.Value);
							EditorGUI.LabelField(rects[2], "Flags", labelCentred.Value);
							EditorGUI.LabelField(rects[3], "Reveal", labelCentred.Value);
							EditorGUI.LabelField(rects[4], "Select", labelCentred.Value);
						}
					}
					{
						var listRect = EditorGUILayout.GetControlRect(false, (EditorGUIUtility.singleLineHeight + 2) * numberOfItemsToShow - 2);
						listRect = EditorGUI.IndentedRect(listRect);
						var scrollRect = listRect;
						scrollRect.width = EditorGUIUtility.singleLineHeight;
						listRect.width -= scrollRect.width;
						scrollRect.xMin += listRect.width;
						listRect.width -= 2; // spacing
						var listRects = listRect.RectSplitVerticalUniform(numberOfItemsToShow).ToArray();
						// list scroll
						foundObjectsDisplayOffset = GUI.VerticalScrollbar(scrollRect, foundObjectsDisplayOffset, numberOfItemsToShow, 0, foundObjects.Count);
						// list items
						for (var i = 0; i < numberOfItemsToShow; ++i) {
							var index = Mathf.Clamp(Mathf.RoundToInt(foundObjectsDisplayOffset + i), 0, foundObjects.Count - 1);
							var found = foundObjects[index];
							var rect = listRects[i];
							var rects = rect.RectSplitHorisontal(1, 8, 1, 2, 2).ToArray();
							using (new KawaGUIUtility.ZeroIndentScope()) {
								EditorGUI.LabelField(rects[0], $"{index + 1}");
								using (new EditorGUI.DisabledScope(true)) {
									EditorGUI.ObjectField(rects[1], found.obj, found.obj.GetType(), true);
								}
								EditorGUI.LabelField(rects[2], GetFlagsString(found.obj), labelCentred.Value);
								if (GUI.Button(rects[3], "Reveal")) {
									// Меняя флаги гейм объекта меняются и компоненты.
									Undo.RegisterFullObjectHierarchyUndo(found.obj, $"Reveal {found.GetType()}");
									RevealObject(found.obj, HideFlags.HideInHierarchy | HideFlags.HideInInspector | HideFlags.NotEditable);
									Undo.FlushUndoRecordObjects();
								}
								if (GUI.Button(rects[4], "Select")) {
									Selection.objects = new[] { found.obj };
									Selection.activeObject = found.obj;
									EditorUtility.FocusProjectWindow();
									EditorGUIUtility.PingObject(found.pingObject);
									KawaGUIUtility.OpenInspector();
									EditorGUIUtility.PingObject(found.pingObject);
								}
							}
						}
					}
					EditorGUILayout.HelpBox("Flags: H = HideInHierarchy, I = HideInInspector, E = NotEditable", MessageType.None);
					{
						EditorGUILayout.LabelField("Reveal Everything:");
						var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2));
						var rects = rect.RectSplitHorisontal(2, 2, 2, 3, 1).ToArray();
						if (GUI.Button(rects[0], "In\nHierarchy")) {
							RevealObjects(HideFlags.HideInHierarchy);
						}
						if (GUI.Button(rects[1], "In\nInspector")) {
							RevealObjects(HideFlags.HideInInspector);
						}
						if (GUI.Button(rects[2], "Not\nEditable")) {
							RevealObjects(HideFlags.NotEditable);
						}
						if (GUI.Button(rects[3], "Both\nFlags")) {
							RevealObjects(HideFlags.HideInHierarchy | HideFlags.HideInInspector | HideFlags.NotEditable);
						}
						if (GUI.Button(rects[4], "Clear\nList")) {
							foundObjects.Clear();
							EditorUtility.UnloadUnusedAssetsImmediate();
						}
					}
				}
			}
		}

	}
}
#endif

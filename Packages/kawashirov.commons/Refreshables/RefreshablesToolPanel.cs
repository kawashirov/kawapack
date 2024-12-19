#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Kawashirov.ToolsGUI;

namespace Kawashirov.Refreshables {

	[ToolsWindowPanel("Refreshables")]
	public class RefreshablesToolPanel : AbstractToolPanel {

		private Dictionary<Type, List<Type>> types = null;

		private int where_idx = 0;
		private string[] where_names = { "Components In Loaded Scene", "Scriptables In Project" };

		private static bool IsDirectChild(Type parent, Type child) {
			if (parent == null || child == null || parent == child)
				return false;

			if (parent.IsInterface) {
				var upper_ifaces = child.BaseType?.GetInterfaces() ?? Array.Empty<Type>();
				return child.GetInterfaces().Except(upper_ifaces).Contains(parent);
			} else if (parent.IsClass) {
				return child.BaseType == parent;
			}

			return false;
		}

		private void FillTypes(List<Type> all_types, Type base_type) {
			Debug.Log($"Filling type {base_type}...");
			var sub_types = all_types.Where(t => IsDirectChild(base_type, t)).ToList();
			if (types.TryAdd(base_type, sub_types)) {
				foreach (var sub_type in sub_types)
					FillTypes(all_types, sub_type);
			}
		}

		private void RefreshTypes() {
			types = new Dictionary<Type, List<Type>>();
			var all_types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(KawaUtilities.GetTypesSafe).ToList();
			FillTypes(all_types, typeof(IRefreshable));
		}

		private void RunRefresh(Type type) {
			if (where_idx == 0) {
				KawaUtilities.IterScenesRoots()
					.SelectMany(g => g.GetComponentsInChildren<IRefreshable>(true))
					.Where(c => type.IsAssignableFrom(c.GetType()))
					.RefreshMultiple();
			} else if (where_idx == 1) {
				RefreshableUtility.LoadAllRefreshablesInProject(type).RefreshMultiple();
			}
		}

		private void ToolsGUI_TypeTree(Type base_type) {
			using (new GUILayout.HorizontalScope()) {
				GUILayout.Space(5 + EditorGUI.indentLevel * 5);
				if (GUILayout.Button(base_type.Name)) {
					RunRefresh(base_type);
				}
			}

			if (types.TryGetValue(base_type, out var sub_types) && sub_types.Count > 0) {
				using (new EditorGUI.IndentLevelScope(2)) {
					foreach (var sub_type in sub_types)
						ToolsGUI_TypeTree(sub_type);
				}
			}
		}

		public override void ToolsGUI() {
			if (GUILayout.Button("Refresh Type Registry")) {
				RefreshTypes();
			}

			if (types == null || types.Count < 1) {
				EditorGUILayout.LabelField("Type registry is empty!");
			} else {
				EditorGUILayout.LabelField($"Registred {types.Count} refreshable types.");
				where_idx = EditorGUILayout.Popup("Where", where_idx, where_names);
				EditorGUILayout.LabelField($"Refresh any of (sub) types:");
				ToolsGUI_TypeTree(typeof(IRefreshable));
			}
		}
	}
}
#endif // UNITY_EDITOR

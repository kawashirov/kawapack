using System;
using System.Linq;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UdonSharp;
using Kawashirov;
using Kawashirov.Udon;
using Kawashirov.Refreshables;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEditor;
using UdonSharpEditor;
#endif

public class MaterialQueueFix : UdonSharpBehaviour
#if !COMPILER_UDONSHARP
	, IRefreshable
#endif
{
	public Material[] materials;
	public int[] queues;

	void Start() {
		Debug.Log($"Applying Material.renderQueue fix...", gameObject);
		if (!(Utilities.IsValid(materials) && Utilities.IsValid(queues)))
			return;
		var size = Math.Min(materials.Length, queues.Length);
		for (var i = 0; i < size; ++i) {
			var material = materials[i];
			var queue = queues[i];
			if (Utilities.IsValid(material) && material.renderQueue != queue) {
				Debug.Log($"Changing renderQueue of {material} from {material.renderQueue} to {queue}...", material);
				material.renderQueue = queue;
			}
		}
		Debug.Log($"Applied Material.renderQueue fix.", gameObject);
	}

#if !COMPILER_UDONSHARP && UNITY_EDITOR

	[CustomEditor(typeof(MaterialQueueFix))]
	public class Editor : UnityEditor.Editor {

		public override void OnInspectorGUI() {
			if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(this.target))
				return;

			// DrawDefaultInspector();

			EditorGUILayout.Space();

			var target = this.target as MaterialQueueFix;

			/*

			materials.Clear();
			materials.AddRange(target.materials);
			queues.Clear();
			queues.AddRange(target.queues);

			var size = Math.Min(target.materials.Length, target.queues.Length);
			var modified = false;
			modified |= ResizeList(materials, size);
			modified |= ResizeList(queues, size);
			using (var check = new EditorGUI.ChangeCheckScope()) {
				GUILayout.Label($"Materials ({size}):");
				for (var i = 0; i < size; ++i) {
					using (new GUILayout.HorizontalScope()) {
						GUILayout.Label($"#{i}");
						target.materials[i] = (Material)EditorGUILayout.ObjectField(target.materials[i], typeof(Material), true);
						target.queues[i] = EditorGUILayout.IntField(target.queues[i]);
					}
				}
				if (GUILayout.Button("Add")) {
					modified |= ResizeList(materials, size + 1);
					modified |= ResizeList(queues, size + 1);
				}
				modified |= check.changed;
				Debug.Log($"modified={modified}, changed={check.changed}", this);
			}

			if (modified) {
				target.materials = materials.ToArray();
				target.queues = queues.ToArray();
				target.ApplyProxyModificationsAndSetDirty();
			}
			*/

			using (var check = new EditorGUI.ChangeCheckScope()) {

				using (new GUILayout.HorizontalScope()) {
					GUILayout.Label("Index");
					GUILayout.Label("Material");
					GUILayout.Label("Queue In-Game");
					GUILayout.Label("Current Queue");
				}

				var p_materials = serializedObject.FindProperty("materials");
				var p_queues = serializedObject.FindProperty("queues");
				var size = Math.Max(p_materials.arraySize, p_queues.arraySize);
				var delete_element = -1;
				for (var i = 0; i < size; ++i) {
					if (p_materials.arraySize <= i)
						p_materials.InsertArrayElementAtIndex(i);
					var material = p_materials.GetArrayElementAtIndex(i);
					var ref_material = material.objectReferenceValue as Material;

					if (p_queues.arraySize <= i)
						p_queues.InsertArrayElementAtIndex(i);
					var queue = p_queues.GetArrayElementAtIndex(i);
					var int_queue = queue.intValue;

					var color = GUI.color;
					if (ref_material == null || ref_material.renderQueue != int_queue)
						GUI.color = GUI.color * 0.5f + Color.red * 0.5f;
					using (new GUILayout.HorizontalScope()) {
						GUILayout.Label($"#{i}");
						EditorGUILayout.PropertyField(material, GUIContent.none);
						GUILayout.Label(ref_material ? $"{int_queue}" : "null");
						GUILayout.Label(ref_material ? $"{ref_material.renderQueue}" : "null");
						if (GUILayout.Button("Del"))
							delete_element = i; // don't delete while iterating
					}
					GUI.color = color;
				}
				if (delete_element >= 0) {
					p_materials.DeleteArrayElementAtIndex(delete_element);
					if (p_materials.arraySize < size) // was actually deleted
						p_queues.DeleteArrayElementAtIndex(delete_element);
				}
				if (GUILayout.Button("Add Row")) {
					p_materials.InsertArrayElementAtIndex(size);
					p_queues.InsertArrayElementAtIndex(size);
				}
				if (serializedObject.hasModifiedProperties) {
					serializedObject.ApplyModifiedProperties();
					target.ApplyProxyModificationsAndSetDirty();
				}
			}
			this.EditorRefreshableGUI();
		}
	}

	public void Refresh() {
		KawaUdonUtilities.DistinctArray(this, nameof(materials), ref materials);

		var new_queues = materials.Select(m => m.renderQueue).ToArray();
		var cmp = new KawaUtilities.EquatableComparer<int>();
		KawaUdonUtilities.ModifyArray(this, nameof(queues), ref queues, new_queues, cmp);
	}

	public UnityEngine.Object AsUnityObject() => this;

	public string RefreshablePath() => gameObject.KawaGetFullPath();

#endif
}

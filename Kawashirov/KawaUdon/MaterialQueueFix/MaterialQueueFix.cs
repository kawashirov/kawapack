using System;
using System.Linq;
using UnityEngine;
using VRC.SDKBase;
using UdonSharp;
using Kawashirov;
using Kawashirov.Udon;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEditor;
using UdonSharpEditor;
#endif

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MaterialQueueFix : CommonUSharpBehaviour {
	public Material[] materials;
	public int[] queues;

	public override void Start() {
		base.Start();
		logName = "Kawa|MaterialQueueFix";

		_Info($"Applying Material.renderQueue fix...");

		if (!_EnsureValid(materials, true, "Materials array is invalid!"))
			return;
		if (!_EnsureValid(queues, true, "Queues array is invalid!"))
			return;

		var materials_Length = materials.Length;
		var queues_Length = queues.Length;
		_Ensure(materials_Length == queues_Length, $"materials.Length=={materials_Length}, but queues.Length=={queues_Length}!");
		var size = Math.Min(materials_Length, queues_Length);

		var c_total = 0;
		var c_changed = 0;
		for (var i = 0; i < size; ++i) {
			var material = materials[i];
			var queue = queues[i];
			++c_total;
			if (!Utilities.IsValid(material))
				continue;
			if (material.renderQueue != queue) {
				_Info($"Changing renderQueue of {material}: {material.renderQueue} -> {queue}...");
				material.renderQueue = queue;
				++c_changed;
			}
		}
		_Info($"Applied Material.renderQueue fix on {c_changed}/{c_total}/{size} materials.");
	}

#if !COMPILER_UDONSHARP && UNITY_EDITOR

	[CustomEditor(typeof(MaterialQueueFix))]
	public new class Editor : CommonUSharpBehaviour.Editor {

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

	public override void Refresh() {
		KawaUdonUtilities.DistinctArray(this, nameof(materials), ref materials);

		var new_queues = materials.Select(m => m.renderQueue).ToArray();
		var cmp = new KawaUtilities.EquatableComparer<int>();
		KawaUdonUtilities.ModifyArray(this, nameof(queues), ref queues, new_queues, cmp);
	}
#endif
}

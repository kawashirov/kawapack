using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEditor.SceneManagement;
using System.Reflection;
using System;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR

using UnityEditor;

using SDK2 = VRCSDK2;
using SDK3 = VRC.SDK3.Components;

namespace Kawashirov.VRCSDKMigration {

	public interface IMigration {

		Type GetSourceType();

		Type GetDestinationType();

		int Migrate(Component component);

		int Migrate(IEnumerable<GameObject> roots);

	}

	public class Migration<S, D> : IMigration
		where S : MonoBehaviour
		where D : MonoBehaviour {

		protected string name = "";
		protected Dictionary<string, FieldInfo> fields_src;
		protected Dictionary<string, FieldInfo> fields_dst;
		protected List<string> fields_shaded;

		public Migration(string name) {
			this.name = name;
			fields_src = typeof(S).GetFields().ToDictionary(f => f.Name);
			fields_dst = typeof(D).GetFields().ToDictionary(f => f.Name);
			fields_shaded = fields_src.Keys.Intersect(fields_dst.Keys).ToList();
			var fields_only_src = fields_src.Keys.Except(fields_shaded).ToList();
			var fields_only_dst = fields_dst.Keys.Except(fields_shaded).ToList();

			var msg = string.Format(
				"[KawaVRCSDKMigrate] Loaded {0}:\n\tShared fields: {1}\n\tOnly {2} fields: {3}\n\tOnly {4} fields: {5}",
				GetType(), string.Join(", ", fields_shaded),
				typeof(S), string.Join(", ", fields_only_src),
				typeof(D), string.Join(", ", fields_only_dst)
			);

			if (fields_only_src.Count > 0 || fields_only_dst.Count > 0) {
				Debug.LogWarning(msg);
			} else {
				Debug.Log(msg);
			}

		}

		protected D FindDestination(S source) {
			var destination = source.GetComponent<D>();
			if (destination == null)
				destination = Undo.AddComponent<D>(source.gameObject);
			return destination;
		}

		protected int Migrate(S source, D destination) {
			Undo.RecordObjects(new UnityEngine.Object[] { source, destination }, "Migrate " + name);
			foreach (var key in fields_shaded) {
				var field_a = fields_src[key];
				var field_b = fields_dst[key];
				var data = field_a.GetValue(source);
				field_b.SetValue(destination, data);
			}
			if (fields_shaded.Count > 0) {
				EditorUtility.SetDirty(destination);
				EditorSceneManager.MarkSceneDirty(destination.gameObject.scene);
			}
			return fields_shaded.Count;
		}

		//
		// Implementation

		public Type GetSourceType() => typeof(S);

		public Type GetDestinationType() => typeof(D);

		public int Migrate(Component component) {
			Debug.LogFormat(
				component, "[KawaVRCSDKMigrate] Migrating <b>{1}</b> -> <b>{2}</b>...\n@ <i>{0}</i>",
				component.transform.KawaGetHierarchyPath(), component, typeof(D)
			);
			var source = component as S;
			return source == null ? 0 : Migrate(source, FindDestination(source));
		}

		public int Migrate(IEnumerable<GameObject> roots) {
			var changes = 0;
			var error = false;
			Undo.IncrementCurrentGroup();
			var undo_group = Undo.GetCurrentGroup();
			try {
				var sources = roots.SelectMany(g => g.GetComponentsInChildren<S>(true)).ToList();

				foreach (var source in sources) {

					D destination = null;
					try {
						Debug.LogFormat(
							source, "[KawaVRCSDKMigrate] Migrating <b>{1}</b> -> <b>{2}</b>...\n@ <i>{0}</i>",
							source.transform.KawaGetHierarchyPath(), source, typeof(D)
						);
						destination = FindDestination(source);

						changes += Migrate(source, destination);

					} catch (Exception exc) {
						Debug.LogErrorFormat(
							source, "[KawaVRCSDKMigrate] Error migrating <b>{1}</b> -> <b>{2}</b>: {3}\n@ <i>{0}</i>\n{4}",
							source.transform.KawaGetHierarchyPath(), source, destination, exc.Message, exc.StackTrace
						);
						Debug.LogException(exc);
					}
				}
			} catch (Exception exc) {
				error = true;
				Debug.LogErrorFormat(
					"[KawaVRCSDKMigrate] Error migrating <b>{0}</b> -> <b>{1}</b>: {2}\n{3}",
					typeof(S), typeof(D), exc.Message, exc.StackTrace
				);
				Debug.LogException(exc);
			} finally {
				Undo.CollapseUndoOperations(undo_group);
				Undo.SetCurrentGroupName((error ? "(Error) " : "") + "Migrate " + name);
			}
			return changes;
		}

	}

	public static class MigrationStatic {

		public static IMigration[] migrations = new IMigration[] {
			new Migration<SDK2.VRC_AvatarPedestal, SDK3.VRCAvatarPedestal>("AvatarPedestal"),
			new Migration<SDK2.VRC_MirrorReflection, SDK3.VRCMirrorReflection>("MirrorReflection"),
			new Migration<SDK2.VRC_Pickup, SDK3.VRCPickup>("Pickup"),
			new Migration<SDK2.VRC_PortalMarker, SDK3.VRCPortalMarker>("PortalMarker"),
			new Migration<SDK2.VRC_SceneDescriptor, SDK3.VRCSceneDescriptor>("SceneDescriptor"),
			new Migration<SDK2.VRC_SpatialAudioSource, SDK3.VRCSpatialAudioSource>("SpatialAudioSource"),
			new Migration<SDK2.VRC_Station, SDK3.VRCStation>("Station"),
			new Migration<SDK2.VRC_UiShape, SDK3.VRCUiShape>("UiShape"),
		};  

		public static IEnumerable<IMigration> FindMigrations(MonoBehaviour source) 
			=> migrations.Where(m => m.GetSourceType().IsInstanceOfType(source));

		public static IMigration FindMigration(MonoBehaviour source) {
			var list = FindMigrations(source).ToList();
			if (list.Count < 1) 
				throw new InvalidOperationException(string.Format("Can not find a way to migrate {0}", source));
			if (list.Count > 1)
				throw new InvalidOperationException(string.Format(
					"Found more than one way to migrate {0}: {1}", source, string.Join(", ", list.Select(m => m.GetType().ToString()))
				));
			return list[0];
		}

		[MenuItem("Kawashirov/Migrate VRCSDK2-to-VRCSDK3")]
		public static void MigrateInScene() {
			var changes = 0;
			Undo.IncrementCurrentGroup();
			var undo_group = Undo.GetCurrentGroup();
			try {
				Debug.Log("[KawaVRCSDKMigrate] Migrating VRCSDK2-to-VRCSDK3...");
				var roots = StaticCommons.GetScenesRoots();
				foreach (var migration in migrations) {
					changes += migration.Migrate(roots);
				}
				Debug.LogFormat("[KawaVRCSDKMigrate] Done migrating VRCSDK2-to-VRCSDK3: <b>{0}</b> fields changed.", changes);
			} finally {
				Undo.CollapseUndoOperations(undo_group);
				Undo.SetCurrentGroupName("Migrate VRCSDK2-to-VRCSDK3");
			}
			
		}

	}

	public class AbstractMigrationEditor<A, B> : Editor
		where A : MonoBehaviour
		where B : MonoBehaviour {

		protected void DoMigrate() {
			foreach (var target in targets) {
				var component = target as MonoBehaviour;
				if (component == null)
					continue;
				try {
					MigrationStatic.FindMigration(component).Migrate(component);
				} catch (Exception exc) {
					Debug.LogErrorFormat(
						"[KawaVRCSDKMigrate] Error migrating <b>{1}</b> (<b>{2}</b> -> <b>{3}</b>): {4}\n@ <i>{0}</i>\n{5}",
						component.transform.KawaGetHierarchyPath(), component, typeof(A), typeof(B), exc.Message, exc.StackTrace
					);
					Debug.LogException(exc);
				}
			}
		} 

		public override void OnInspectorGUI() {
			EditorGUILayout.LabelField("THIS IS LEGACY VRCSDK2 COMPONENT");
			EditorGUILayout.LabelField("Contains data:");
			using (new EditorGUI.DisabledGroupScope(true)) {
				using (new EditorGUI.IndentLevelScope()) {
					DrawDefaultInspector();
				}
			}
			if (GUILayout.Button("Migrate to VRCSDK3")) {
				DoMigrate();
				serializedObject.Update();
			}
		}
	}

	[CustomEditor(typeof(SDK2.VRC_AvatarPedestal), false)]
	public class AvatarPedistalEditor : AbstractMigrationEditor<SDK2.VRC_AvatarPedestal, SDK3.VRCAvatarPedestal> { }

	[CustomEditor(typeof(SDK2.VRC_MirrorReflection), false)]
	public class MirrorReflectionEditor : AbstractMigrationEditor<SDK2.VRC_MirrorReflection, SDK3.VRCMirrorReflection> { }

	[CustomEditor(typeof(SDK2.VRC_Pickup), false)]
	public class PickupEditor : AbstractMigrationEditor<SDK2.VRC_Pickup, SDK3.VRCPickup> { }

	[CustomEditor(typeof(SDK2.VRC_PortalMarker), false)]
	public class PortalMarkerEditor : AbstractMigrationEditor<SDK2.VRC_PortalMarker, SDK3.VRCPortalMarker> { }

	[CustomEditor(typeof(SDK2.VRC_SceneDescriptor), false)]
	public class SceneDescriptorEditor : AbstractMigrationEditor<SDK2.VRC_SceneDescriptor, SDK3.VRCSceneDescriptor> { }

	[CustomEditor(typeof(SDK2.VRC_SpatialAudioSource), false)]
	public class SpatialAudioSourceEditor : AbstractMigrationEditor<SDK2.VRC_SpatialAudioSource, SDK3.VRCSpatialAudioSource> { }

	[CustomEditor(typeof(SDK2.VRC_Station), false)]
	public class StationEditor : AbstractMigrationEditor<SDK2.VRC_Station, SDK3.VRCStation> { }

	[CustomEditor(typeof(SDK2.VRC_UiShape), false)]
	public class UiShapeEditor : AbstractMigrationEditor<SDK2.VRC_UiShape, SDK3.VRCUiShape> { }

}

#endif

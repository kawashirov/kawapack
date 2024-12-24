using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
using static VRC.SDKBase.VRC_AnimatorTrackingControl;
using Kawashirov;
using Kawashirov.Refreshables;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

namespace Kawashirov {
	public class PosesRadialPuppetGenerator : ScriptableObject, IRefreshable {
#if UNITY_EDITOR

		public AnimatorController animatorController;
		public string layerName = "Action&AFK";
		public string stateNameIdle = "WaitForActionOrAFK";
		public string stateNameRestore = "Restore Tracking";
		public Motion emptyMotion;
		public Motion[] animations;
		public string parameterForRadialFloat = "Kawa/PoseFloat";
		public string parameterForPoseIndex = "Kawa/PoseIndex";
		public string layerNameForConversion = "PosesFloatToPoseIndex";
		public int VRCEmoteValue = 65;

		[CanEditMultipleObjects]
		[CustomEditor(typeof(PosesRadialPuppetGenerator))]
		public class Editor : UnityEditor.Editor {
			public override void OnInspectorGUI() {
				DrawDefaultInspector();
				var refreshables = targets.OfType<IRefreshable>().ToList();
				if (refreshables.Count > 0 && GUILayout.Button("Refresh")) {
					refreshables.RefreshMultiple();
				}
			}
		}

		[MenuItem("Kawashirov/Create Asset/PosesRadialPuppetGenerator")]
		public static void CreateAsset() {
			var save_path = EditorUtility.SaveFilePanelInProject(
				"New PosesRadialPuppetGenerator Asset", "MyPosesRadialPuppetGenerator.asset", "asset",
				"Please enter a file name to save the PosesRadialPuppetGenerator asset to."
			);
			if (string.IsNullOrWhiteSpace(save_path))
				return;
			var compositor = CreateInstance<PosesRadialPuppetGenerator>();
			AssetDatabase.CreateAsset(compositor, save_path);
		}

		private void EnsureParameter(string name, AnimatorControllerParameterType type, float value) {
			var parameter = animatorController.parameters
				.Where(p => string.Equals(p.name, name)).SingleOrDefault();
			if (parameter.type != type) {
				animatorController.RemoveParameter(parameter); // Undo handled
				animatorController.AddParameter(name, type);
				parameter = animatorController.parameters
					.Where(p => string.Equals(p.name, name)).Single();
			}
			parameter.defaultFloat = value;
			parameter.defaultInt = (int)value;
			parameter.defaultBool = value != 0;
		}

		private void EnsureParameters() {
			EnsureParameter(parameterForPoseIndex, AnimatorControllerParameterType.Int, 0);
			EnsureParameter(parameterForRadialFloat, AnimatorControllerParameterType.Float, 0);
		}

		private AnimatorControllerLayer ReCreateConversionLayer() {
			var retry = true;
			while (retry) {
				var delLayers = animatorController.layers; // copy?
				retry = false;
				for (var i = 0; i < animatorController.layers.Length; ++i) {
					if (string.Equals(delLayers[i].name, layerNameForConversion)) {
						retry = true;
						Debug.LogWarning($"Removing AnimatorControllerLayer: {delLayers[i]} from {animatorController}", animatorController);
						animatorController.RemoveLayer(i);
						break;
					}
				}
			}

			animatorController.AddLayer(layerNameForConversion);
			var layer = animatorController.layers.Where(l => string.Equals(l.name, layerNameForConversion, StringComparison.CurrentCulture)).Single();
			return layer;
		}

		private void CreateConversionLayer() {
			var layer = ReCreateConversionLayer();
			layer.stateMachine.entryPosition = new Vector2(-100, -100);
			layer.stateMachine.exitPosition = new Vector2(100, -100);
			layer.stateMachine.anyStatePosition = Vector2.zero;
			for (var i = 0; i < animations.Length; i++) {
				float poseFrom = 1.0f * i / animations.Length;
				float poseTo = 1.0f * (i + 1) / animations.Length;
				var state_name = $"{i} {poseFrom % .3f} {poseTo % .3f}".Replace(".", "_");
				Debug.Log($"Creating state for {state_name}", layer.stateMachine);

				var angle = Mathf.Lerp(0.05f, 1.95f, 1.0f * i / animations.Length) * Mathf.PI;
				var distance = Mathf.Lerp(800, 1000, (i % 3) / 2.0f);
				var position = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * distance;
				var state = layer.stateMachine.AddState(state_name, position); // Undo handled
				state.motion = emptyMotion;

				var transition = layer.stateMachine.AddAnyStateTransition(state); // Undo handled
				transition.AddCondition(AnimatorConditionMode.Greater, poseFrom, parameterForRadialFloat);
				transition.AddCondition(AnimatorConditionMode.Less, poseTo, parameterForRadialFloat);
				transition.name = state_name;
				transition.canTransitionToSelf = false;
				transition.duration = 0;

				var driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
				Undo.RegisterCreatedObjectUndo(driver, "VRCAvatarParameterDriver Created");
				Undo.RecordObject(driver, "VRCAvatarParameterDriver Configured");
				driver.localOnly = true;
				driver.debugString = null;
				driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
					type = VRC_AvatarParameterDriver.ChangeType.Set,
					name = parameterForPoseIndex,
					value = i,
				});
				EditorUtility.SetDirty(driver);

			}
		}

		private void SetTracking(AnimatorState state, TrackingType type) {
			var tracking = state.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
			Undo.RegisterCreatedObjectUndo(tracking, "VRCAnimatorTrackingControl Created");
			Undo.RecordObject(tracking, "VRCAnimatorTrackingControl Configured");
			tracking.trackingHead = type;
			tracking.trackingLeftHand = type;
			tracking.trackingRightHand = type;
			tracking.trackingHip = type;
			tracking.trackingLeftFoot = type;
			tracking.trackingRightFoot = type;
			tracking.trackingLeftFingers = type;
			tracking.trackingRightFingers = type;
			tracking.trackingEyes = TrackingType.NoChange;
			tracking.trackingMouth = TrackingType.NoChange;
			EditorUtility.SetDirty(tracking);
		}
		private void SetLayerControl(AnimatorState state, float goalWeight) {
			var control = state.AddStateMachineBehaviour<VRCPlayableLayerControl>();
			Undo.RegisterCreatedObjectUndo(control, "VRCPlayableLayerControl Created");
			Undo.RecordObject(control, "VRCPlayableLayerControl Configured");
			control.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
			control.goalWeight = goalWeight;
			control.blendDuration = 0.1f;
			EditorUtility.SetDirty(control);
		}

		private void ApplyPatchForStateMachine() {
			var layer = animatorController.layers
				.Where(l => string.Equals(l.name, layerName, StringComparison.CurrentCulture))
				.Single();

			var state_idle = layer.stateMachine.states
				.Select(s => s.state)
				.Where(s => string.Equals(s.name, stateNameIdle, StringComparison.InvariantCulture))
				.Single();

			var state_restore = layer.stateMachine.stateMachines
				.Select(s => s.stateMachine)
				.Where(s => string.Equals(s.name, stateNameRestore, StringComparison.InvariantCulture))
				.Single();

			var old_poses_list = layer.stateMachine.stateMachines
				.Select(s => s.stateMachine)
				.Where(s => string.Equals(s.name, "Kawa Poses", StringComparison.InvariantCulture)).ToList();
			foreach (var old_poses in old_poses_list)
				layer.stateMachine.RemoveStateMachine(old_poses); // Undo handled

			// States
			var poses = layer.stateMachine.AddStateMachine("Kawa Poses", new Vector2(-50, 50));
			poses.entryPosition = new Vector2(-200, -100);
			poses.anyStatePosition = new Vector2(0, -100);
			poses.exitPosition = new Vector2(200, -100);
			poses.parentStateMachinePosition = new Vector2(0, 100);

			var state_pre = poses.AddState(name, new Vector2(-300, 0)); // Undo handled
			state_pre.name = "Pre-Joint (Disable Tracking)";
			state_pre.motion = emptyMotion;
			SetLayerControl(state_pre, 1);
			SetTracking(state_pre, TrackingType.Animation);

			var state_joint = poses.AddState(name, new Vector2(0, 0)); // Undo handled
			state_joint.name = "Joint";
			state_joint.motion = emptyMotion;

			// Transitions

			var idle2entry = state_idle.AddTransition(poses);
			idle2entry.name = $"Idle -> (VRCEmote={VRCEmoteValue})";
			idle2entry.duration = 0;
			idle2entry.exitTime = 0;
			idle2entry.AddCondition(AnimatorConditionMode.Equals, VRCEmoteValue, "VRCEmote");

			var entry2pre = poses.AddEntryTransition(state_pre);
			entry2pre.name = "Entry -> Pre";

			var pre2joint = state_pre.AddTransition(state_joint);
			pre2joint.name = "Pre -> Joint";
			pre2joint.hasExitTime = true; // Condition less
			pre2joint.duration = 0;
			pre2joint.exitTime = 0;

			var joint2restore = state_joint.AddTransition(state_restore);
			joint2restore.name = $"Joint -> Restore (VRCEmote != {VRCEmoteValue})";
			joint2restore.duration = 0;
			joint2restore.exitTime = 0;
			joint2restore.AddCondition(AnimatorConditionMode.NotEqual, VRCEmoteValue, "VRCEmote"); // Undo handled

			for (var i = 0; i < animations.Length; i++) {
				var pose = animations[i];
				var state_name = $"{i} ({pose.name})".Replace(".", "_");
				Debug.Log($"Creating state \"{i}\" for pose \"{pose.name}\"", poses);

				var angle = Mathf.Lerp(0.05f, 1.95f, 1.0f * i / animations.Length) * Mathf.PI;
				var distance = Mathf.Lerp(800, 1000, (i % 3) / 2.0f);
				var position = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * distance;
				var state_pose = poses.AddState(state_name, position); // Undo handled
				state_pose.motion = pose;

				var joint2pose = state_joint.AddTransition(state_pose); // Undo handled
				joint2pose.name = $"Joint -> {state_name}";
				joint2pose.duration = 0;
				joint2pose.exitTime = 0;
				joint2pose.AddCondition(AnimatorConditionMode.Equals, i, parameterForPoseIndex); // Undo handled
				joint2pose.AddCondition(AnimatorConditionMode.Equals, VRCEmoteValue, "VRCEmote"); // Undo handled

				var pose2joint_index = state_pose.AddTransition(state_joint); // Undo handled
				pose2joint_index.name = $"{state_name} -> Joint ({parameterForPoseIndex} != {i})";
				pose2joint_index.duration = 0;
				pose2joint_index.exitTime = 0;
				pose2joint_index.AddCondition(AnimatorConditionMode.NotEqual, i, parameterForPoseIndex); // Undo handled

				var pose2joint_vrcemote = state_pose.AddTransition(state_joint); // Undo handled
				pose2joint_vrcemote.name = $"{state_name} -> Joint (VRCEmote != {VRCEmoteValue})";
				pose2joint_vrcemote.duration = 0;
				pose2joint_vrcemote.exitTime = 0;
				pose2joint_vrcemote.AddCondition(AnimatorConditionMode.NotEqual, VRCEmoteValue, "VRCEmote"); // Undo handled
			}
		}

		public void Refresh() {
			Undo.IncrementCurrentGroup();
			var undoGroup = Undo.GetCurrentGroup();
			try {
				Undo.SetCurrentGroupName("Refresh PosesRadialPuppetGenerator");

				EnsureParameters();
				CreateConversionLayer();
				ApplyPatchForStateMachine();

				Undo.FlushUndoRecordObjects();
			} catch (Exception exc) {
				Debug.LogError($"Failed to Refresh PosesRadialPuppetGenerator: {exc}. Undoing...");
				Debug.LogException(exc);
				Undo.FlushUndoRecordObjects();
				Undo.CollapseUndoOperations(undoGroup);
				Undo.PerformUndo();
				throw exc;
			} finally {
				Undo.CollapseUndoOperations(undoGroup);
			}
		}
#endif
	}
}

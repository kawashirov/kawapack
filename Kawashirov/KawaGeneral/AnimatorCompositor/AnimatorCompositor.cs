using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using EU = UnityEditor.EditorUtility;
#endif

using Kawashirov.Refreshables;

#if VRC_SDK_VRCSDK3 && !UDON
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
#endif

namespace Kawashirov {
	[Serializable]
	public class AnimatorCompositor : ScriptableObject, IRefreshable {
#if UNITY_EDITOR
		public AnimatorController[] sources;
		public AnimatorController destination;


		[CanEditMultipleObjects]
		[CustomEditor(typeof(AnimatorCompositor))]
		public class Editor : UnityEditor.Editor {

			public override void OnInspectorGUI() {
				base.DrawDefaultInspector();
				var refreshables = targets.OfType<IRefreshable>().ToList();
				if (refreshables.Count > 0 && GUILayout.Button("Refresh (Compose sources into destination)")) {
					refreshables.RefreshMultiple();
				}
			}
		}

		[MenuItem("Kawashirov/Animator Compositor/Create New Animator Compositor Asset")]
		public static void CreateAsset() {
			var save_path = EU.SaveFilePanelInProject(
				"New Animator Compositor Asset", "MyAnimatorCompositor.asset", "asset",
				"Please enter a file name to save the Animator Compositor to."
			);
			if (string.IsNullOrWhiteSpace(save_path))
				return;

			var compositor = CreateInstance<AnimatorCompositor>();
			AssetDatabase.CreateAsset(compositor, save_path);
		}

		private AnimatorControllerLayer CloneLayer(AnimatorControllerLayer old, bool isFirstLayer = false) {
			// Based on VRLabs.AV3Manager.AnimatorCloner.CloneLayer
			var n = new AnimatorControllerLayer {
				avatarMask = old.avatarMask,
				blendingMode = old.blendingMode,
				defaultWeight = isFirstLayer ? 1f : old.defaultWeight,
				iKPass = old.iKPass,
				name = old.name,
				syncedLayerAffectsTiming = old.syncedLayerAffectsTiming,
				stateMachine = CloneStateMachine(old.stateMachine)
			};
			CloneTransitions(old.stateMachine, n.stateMachine);
			return n;
		}

		private AnimatorStateMachine CloneStateMachine(AnimatorStateMachine old) {
			// Based on VRLabs.AV3Manager.AnimatorCloner.CloneStateMachine
			var n = new AnimatorStateMachine {
				anyStatePosition = old.anyStatePosition,
				entryPosition = old.entryPosition,
				exitPosition = old.exitPosition,
				hideFlags = old.hideFlags,
				name = old.name,
				parentStateMachinePosition = old.parentStateMachinePosition,
				stateMachines = old.stateMachines.Select(x => CloneChildStateMachine(x)).ToArray(),
				states = old.states.Select(x => CloneChildAnimatorState(x)).ToArray()
			};

			var _assetPath = AssetDatabase.GetAssetPath(destination);
			AssetDatabase.AddObjectToAsset(n, _assetPath);
			n.defaultState = FindState(old.defaultState, old, n);

			foreach (var oldb in old.behaviours) {
				var behaviour = n.AddStateMachineBehaviour(oldb.GetType());
				CloneBehaviourParameters(oldb, behaviour);
			}
			return n;
		}

		private ChildAnimatorStateMachine CloneChildStateMachine(ChildAnimatorStateMachine old) {
			// Based on VRLabs.AV3Manager.AnimatorCloner.CloneChildStateMachine
			var n = new ChildAnimatorStateMachine {
				position = old.position,
				stateMachine = CloneStateMachine(old.stateMachine)
			};
			return n;
		}

		private ChildAnimatorState CloneChildAnimatorState(ChildAnimatorState old) {
			// Based on VRLabs.AV3Manager.AnimatorCloner.CloneChildAnimatorState
			var n = new ChildAnimatorState {
				position = old.position,
				state = CloneAnimatorState(old.state)
			};
			foreach (var oldb in old.state.behaviours) {
				var behaviour = n.state.AddStateMachineBehaviour(oldb.GetType());
				CloneBehaviourParameters(oldb, behaviour);
			}
			return n;
		}

		private AnimatorState CloneAnimatorState(AnimatorState old) {
			// Based on VRLabs.AV3Manager.AnimatorCloner.CloneAnimatorState
			// Checks if the motion is a blend tree, to avoid accidental blend tree sharing between animator assets

			var _assetPath = AssetDatabase.GetAssetPath(destination);

			var motion = old.motion;
			if (motion is BlendTree oldTree) {
				var tree = CloneBlendTree(null, oldTree);
				motion = tree;
				// need to save the blend tree into the animator
				tree.hideFlags = HideFlags.HideInHierarchy;
				AssetDatabase.AddObjectToAsset(motion, _assetPath);
			}

			var n = new AnimatorState {
				cycleOffset = old.cycleOffset,
				cycleOffsetParameter = old.cycleOffsetParameter,
				cycleOffsetParameterActive = old.cycleOffsetParameterActive,
				hideFlags = old.hideFlags,
				iKOnFeet = old.iKOnFeet,
				mirror = old.mirror,
				mirrorParameter = old.mirrorParameter,
				mirrorParameterActive = old.mirrorParameterActive,
				motion = motion,
				name = old.name,
				speed = old.speed,
				speedParameter = old.speedParameter,
				speedParameterActive = old.speedParameterActive,
				tag = old.tag,
				timeParameter = old.timeParameter,
				timeParameterActive = old.timeParameterActive,
				writeDefaultValues = old.writeDefaultValues
			};
			AssetDatabase.AddObjectToAsset(n, _assetPath);
			return n;
		}

		// Taken from here: https://gist.github.com/phosphoer/93ca8dcbf925fc006e4e9f6b799c13b0
		private BlendTree CloneBlendTree(BlendTree parentTree, BlendTree oldTree) {
			// Based on VRLabs.AV3Manager.AnimatorCloner.CloneBlendTree
			// Create a child tree in the destination parent, this seems to be the only way to correctly 
			// add a child tree as opposed to AddChild(motion)
			var pastedTree = new BlendTree();
			pastedTree.name = oldTree.name;
			pastedTree.blendType = oldTree.blendType;
			pastedTree.blendParameter = oldTree.blendParameter;
			pastedTree.blendParameterY = oldTree.blendParameterY;
			pastedTree.minThreshold = oldTree.minThreshold;
			pastedTree.maxThreshold = oldTree.maxThreshold;
			pastedTree.useAutomaticThresholds = oldTree.useAutomaticThresholds;

			// Recursively duplicate the tree structure
			// Motions can be directly added as references while trees must be recursively to avoid accidental sharing
			foreach (var child in oldTree.children) {
				var children = pastedTree.children;

				var childMotion = new ChildMotion {
					timeScale = child.timeScale,
					position = child.position,
					cycleOffset = child.cycleOffset,
					mirror = child.mirror,
					threshold = child.threshold,
					directBlendParameter = child.directBlendParameter
				};

				if (child.motion is BlendTree tree) {
					var childTree = CloneBlendTree(pastedTree, tree);
					childMotion.motion = childTree;
					// need to save the blend tree into the animator
					childTree.hideFlags = HideFlags.HideInHierarchy;
					var _assetPath = AssetDatabase.GetAssetPath(destination);
					AssetDatabase.AddObjectToAsset(childTree, _assetPath);
				} else {
					childMotion.motion = child.motion;
				}

				ArrayUtility.Add(ref children, childMotion);
				pastedTree.children = children;
			}

			return pastedTree;
		}

		private void CloneBehaviourParameters(StateMachineBehaviour old, StateMachineBehaviour n) {
			// Based on VRLabs.AV3Manager.AnimatorCloner.CloneBehaviourParameters
			if (old.GetType() != n.GetType()) {
				throw new ArgumentException("2 state machine behaviours that should be of the same type are not.");
			}
			switch (n) {

#if VRC_SDK_VRCSDK3 && !UDON
				case VRCAnimatorLayerControl l: {
					var o = old as VRCAnimatorLayerControl;
					l.ApplySettings = o.ApplySettings;
					l.blendDuration = o.blendDuration;
					l.debugString = o.debugString;
					l.goalWeight = o.goalWeight;
					l.layer = o.layer;
					l.playable = o.playable;
					break;
				}
				case VRCAnimatorLocomotionControl l: {
					var o = old as VRCAnimatorLocomotionControl;
					l.ApplySettings = o.ApplySettings;
					l.debugString = o.debugString;
					l.disableLocomotion = o.disableLocomotion;
					break;
				}
				case VRCAnimatorTemporaryPoseSpace l: {
					var o = old as VRCAnimatorTemporaryPoseSpace;
					l.ApplySettings = o.ApplySettings;
					l.debugString = o.debugString;
					l.delayTime = o.delayTime;
					l.enterPoseSpace = o.enterPoseSpace;
					l.fixedDelay = o.fixedDelay;
					break;
				}
				case VRCAnimatorTrackingControl l: {
					var o = old as VRCAnimatorTrackingControl;
					l.ApplySettings = o.ApplySettings;
					l.debugString = o.debugString;
					l.trackingEyes = o.trackingEyes;
					l.trackingHead = o.trackingHead;
					l.trackingHip = o.trackingHip;
					l.trackingLeftFingers = o.trackingLeftFingers;
					l.trackingLeftFoot = o.trackingLeftFoot;
					l.trackingLeftHand = o.trackingLeftHand;
					l.trackingMouth = o.trackingMouth;
					l.trackingRightFingers = o.trackingRightFingers;
					l.trackingRightFoot = o.trackingRightFoot;
					l.trackingRightHand = o.trackingRightHand;
					break;
				}
				case VRCAvatarParameterDriver l: {
					var d = old as VRCAvatarParameterDriver;
					l.debugString = d.debugString;
					l.localOnly = d.localOnly;
					l.isLocalPlayer = d.isLocalPlayer;
					l.initialized = d.initialized;
					l.parameters = d.parameters.ConvertAll(p => {
						var name = p.name;
						return new VRC_AvatarParameterDriver.Parameter {
							name = name,
							value = p.value,
							chance = p.chance,
							valueMin = p.valueMin,
							valueMax = p.valueMax,
							type = p.type,
							source = p.source,
							convertRange = p.convertRange,
							destMax = p.destMax,
							destMin = p.destMin,
							destParam = p.destParam,
							sourceMax = p.sourceMax,
							sourceMin = p.sourceMin,
							sourceParam = p.sourceParam
						};
					});
					break;
				}
				case VRCPlayableLayerControl l: {
					var o = old as VRCPlayableLayerControl;
					l.ApplySettings = o.ApplySettings;
					l.blendDuration = o.blendDuration;
					l.debugString = o.debugString;
					l.goalWeight = o.goalWeight;
					l.layer = o.layer;
					l.outputParamHash = o.outputParamHash;
					break;
				}
#endif // VRC_SDK_VRCSDK3 && !UDON
			}
		}

		private AnimatorState FindState(AnimatorState original, AnimatorStateMachine old, AnimatorStateMachine n) {
			// Based on VRLabs.AV3Manager.AnimatorCloner.FindState
			var oldStates = GetStatesRecursive(old).ToArray();
			var newStates = GetStatesRecursive(n).ToArray();
			for (var i = 0; i < oldStates.Length; i++)
				if (oldStates[i] == original)
					return newStates[i];

			return null;
		}

		private static AnimatorStateMachine FindStateMachine(AnimatorStateTransition transition, AnimatorStateMachine sm) {
			// Based on VRLabs.AV3Manager.AnimatorCloner.FindStateMachine
			var childrenSm = sm.stateMachines.Select(x => x.stateMachine).ToArray();
			var dstSm = Array.Find(childrenSm, x => x.name == transition.destinationStateMachine.name);
			if (dstSm != null)
				return dstSm;

			foreach (var childSm in childrenSm) {
				dstSm = FindStateMachine(transition, childSm);
				if (dstSm != null)
					return dstSm;
			}

			return null;
		}

		private static List<AnimatorState> GetStatesRecursive(AnimatorStateMachine sm) {
			// Based on VRLabs.AV3Manager.AnimatorCloner.GetStatesRecursive
			var childrenStates = sm.states.Select(x => x.state).ToList();
			foreach (var child in sm.stateMachines.Select(x => x.stateMachine))
				childrenStates.AddRange(GetStatesRecursive(child));

			return childrenStates;
		}

		private static List<AnimatorStateMachine> GetStateMachinesRecursive(AnimatorStateMachine sm,
				IDictionary<AnimatorStateMachine, AnimatorStateMachine> newAnimatorsByChildren = null) {
			// Based on VRLabs.AV3Manager.AnimatorCloner.GetStateMachinesRecursive
			var childrenSm = sm.stateMachines.Select(x => x.stateMachine).ToList();

			var gcsm = new List<AnimatorStateMachine>();
			gcsm.Add(sm);
			foreach (var child in childrenSm) {
				newAnimatorsByChildren?.Add(child, sm);
				gcsm.AddRange(GetStateMachinesRecursive(child, newAnimatorsByChildren));
			}

			return gcsm;
		}

		private static AnimatorState FindMatchingState(List<AnimatorState> old, List<AnimatorState> n, AnimatorTransitionBase transition) {
			// Based on VRLabs.AV3Manager.AnimatorCloner.FindMatchingState
			for (var i = 0; i < old.Count; i++)
				if (transition.destinationState == old[i])
					return n[i];

			return null;
		}

		private static AnimatorStateMachine FindMatchingStateMachine(List<AnimatorStateMachine> old, List<AnimatorStateMachine> n, AnimatorTransitionBase transition) {
			// Based on VRLabs.AV3Manager.AnimatorCloner.FindMatchingStateMachine
			for (var i = 0; i < old.Count; i++)
				if (transition.destinationStateMachine == old[i])
					return n[i];

			return null;
		}

		private void CloneTransitions(AnimatorStateMachine old, AnimatorStateMachine n) {
			// Based on VRLabs.AV3Manager.AnimatorCloner.CloneTransitions
			var oldStates = GetStatesRecursive(old);
			var newStates = GetStatesRecursive(n);
			var newAnimatorsByChildren = new Dictionary<AnimatorStateMachine, AnimatorStateMachine>();
			var oldAnimatorsByChildren = new Dictionary<AnimatorStateMachine, AnimatorStateMachine>();
			var oldStateMachines = GetStateMachinesRecursive(old, oldAnimatorsByChildren);
			var newStateMachines = GetStateMachinesRecursive(n, newAnimatorsByChildren);
			// Generate state transitions
			for (var i = 0; i < oldStates.Count; i++) {
				foreach (var transition in oldStates[i].transitions) {
					AnimatorStateTransition newTransition = null;
					if (transition.isExit && transition.destinationState == null && transition.destinationStateMachine == null) {
						newTransition = newStates[i].AddExitTransition();
					} else if (transition.destinationState != null) {
						var dstState = FindMatchingState(oldStates, newStates, transition);
						if (dstState != null)
							newTransition = newStates[i].AddTransition(dstState);
					} else if (transition.destinationStateMachine != null) {
						var dstState = FindMatchingStateMachine(oldStateMachines, newStateMachines, transition);
						if (dstState != null)
							newTransition = newStates[i].AddTransition(dstState);
					}

					if (newTransition != null) {
						ApplyTransitionSettings(transition, newTransition);
						if (string.IsNullOrWhiteSpace(newTransition.name)) {
							newTransition.name = newTransition.GetDisplayName(n);
						}
					}
				}
			}

			for (var i = 0; i < oldStateMachines.Count; i++) {
				if (oldAnimatorsByChildren.ContainsKey(oldStateMachines[i]) && newAnimatorsByChildren.ContainsKey(newStateMachines[i])) {
					foreach (var transition in oldAnimatorsByChildren[oldStateMachines[i]].GetStateMachineTransitions(oldStateMachines[i])) {
						AnimatorTransition newTransition = null;
						if (transition.isExit && transition.destinationState == null && transition.destinationStateMachine == null) {
							newTransition = newAnimatorsByChildren[newStateMachines[i]].AddStateMachineExitTransition(newStateMachines[i]);
						} else if (transition.destinationState != null) {
							var dstState = FindMatchingState(oldStates, newStates, transition);
							if (dstState != null)
								newTransition = newAnimatorsByChildren[newStateMachines[i]].AddStateMachineTransition(newStateMachines[i], dstState);
						} else if (transition.destinationStateMachine != null) {
							var dstState = FindMatchingStateMachine(oldStateMachines, newStateMachines, transition);
							if (dstState != null)
								newTransition = newAnimatorsByChildren[newStateMachines[i]].AddStateMachineTransition(newStateMachines[i], dstState);
						}

						if (newTransition != null)
							ApplyTransitionSettings(transition, newTransition);
					}
				}
				// Generate AnyState transitions
				GenerateStateMachineBaseTransitions(oldStateMachines[i], newStateMachines[i], oldStates, newStates, oldStateMachines, newStateMachines);
			}
		}

		private void GenerateStateMachineBaseTransitions(AnimatorStateMachine old, AnimatorStateMachine n, List<AnimatorState> oldStates,
				List<AnimatorState> newStates, List<AnimatorStateMachine> oldStateMachines, List<AnimatorStateMachine> newStateMachines) {
			// Based on VRLabs.AV3Manager.AnimatorCloner.GenerateStateMachineBaseTransitions
			foreach (var transition in old.anyStateTransitions) {
				AnimatorStateTransition newTransition = null;
				if (transition.destinationState != null) {
					var dstState = FindMatchingState(oldStates, newStates, transition);
					if (dstState != null)
						newTransition = n.AddAnyStateTransition(dstState);
				} else if (transition.destinationStateMachine != null) {
					var dstState = FindMatchingStateMachine(oldStateMachines, newStateMachines, transition);
					if (dstState != null)
						newTransition = n.AddAnyStateTransition(dstState);
				}

				if (newTransition != null)
					ApplyTransitionSettings(transition, newTransition);
			}

			// Generate EntryState transitions
			foreach (var transition in old.entryTransitions) {
				AnimatorTransition newTransition = null;
				if (transition.destinationState != null) {
					var dstState = FindMatchingState(oldStates, newStates, transition);
					if (dstState != null)
						newTransition = n.AddEntryTransition(dstState);
				} else if (transition.destinationStateMachine != null) {
					var dstState = FindMatchingStateMachine(oldStateMachines, newStateMachines, transition);
					if (dstState != null)
						newTransition = n.AddEntryTransition(dstState);
				}

				if (newTransition != null)
					ApplyTransitionSettings(transition, newTransition);
			}
		}

		private void ApplyTransitionSettings(AnimatorStateTransition transition, AnimatorStateTransition newTransition) {
			// Based on VRLabs.AV3Manager.AnimatorCloner.ApplyTransitionSettings
			newTransition.canTransitionToSelf = transition.canTransitionToSelf;
			newTransition.duration = transition.duration;
			newTransition.exitTime = transition.exitTime;
			newTransition.hasExitTime = transition.hasExitTime;
			newTransition.hasFixedDuration = transition.hasFixedDuration;
			newTransition.hideFlags = transition.hideFlags;
			newTransition.isExit = transition.isExit;
			newTransition.mute = transition.mute;
			newTransition.name = transition.name;
			newTransition.offset = transition.offset;
			newTransition.interruptionSource = transition.interruptionSource;
			newTransition.orderedInterruption = transition.orderedInterruption;
			newTransition.solo = transition.solo;
			foreach (var condition in transition.conditions)
				newTransition.AddCondition(condition.mode, condition.threshold, condition.parameter);

		}

		private void ApplyTransitionSettings(AnimatorTransition transition, AnimatorTransition newTransition) {
			// Based on VRLabs.AV3Manager.AnimatorCloner.ApplyTransitionSettings
			newTransition.hideFlags = transition.hideFlags;
			newTransition.isExit = transition.isExit;
			newTransition.mute = transition.mute;
			newTransition.name = transition.name;
			newTransition.solo = transition.solo;
			foreach (var condition in transition.conditions)
				newTransition.AddCondition(condition.mode, condition.threshold, condition.parameter);

		}

		private void CleanupDest() {
			var layers = destination.layers;
			ArrayUtility.Clear(ref layers);
			destination.layers = layers;

			var parameters = destination.parameters;
			ArrayUtility.Clear(ref parameters);
			destination.parameters = parameters;

			var assetPath = AssetDatabase.GetAssetPath(destination);
			var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
			AssetDatabase.SetMainObject(destination, assetPath);
			foreach (var asset in allAssets) {
				if (asset != destination) {
					AssetDatabase.RemoveObjectFromAsset(asset);
				}
			}
		}

		private void ComposeParameters() {
			var parameters = new Dictionary<string, AnimatorControllerParameter>();
			foreach (var source in sources) {
				if (source == null)
					continue;
				foreach (var srcp in source.parameters) {
					if (parameters.TryGetValue(srcp.name, out var dstp)) {
						if (dstp.type != srcp.type)
							throw new ArgumentException($"Value type does not match for {srcp.name}"); // TODO
						string srcpValue = null;
						string dstpValue = null;
						if (dstp.type == AnimatorControllerParameterType.Float && dstp.defaultFloat != srcp.defaultFloat) {
							srcpValue = $"{srcp.defaultFloat}";
							dstpValue = $"{dstp.defaultFloat}";
						}
						if (dstp.type == AnimatorControllerParameterType.Int && dstp.defaultInt != srcp.defaultInt) {
							srcpValue = $"{srcp.defaultInt}";
							dstpValue = $"{dstp.defaultInt}";
						}
						if ((dstp.type == AnimatorControllerParameterType.Bool || dstp.type == AnimatorControllerParameterType.Trigger) && dstp.defaultBool != srcp.defaultBool) {
							srcpValue = $"{srcp.defaultBool}";
							dstpValue = $"{dstp.defaultBool}";
						}
						if (srcpValue != null || dstpValue != null) {
							var msg = $"Default value for {srcp.name} does not match: {dstpValue} vs {srcpValue} ({source.name})";
							Debug.LogWarning(msg, this);
						}

					} else {
						dstp = new AnimatorControllerParameter() {
							name = srcp.name,
							type = srcp.type,
							defaultBool = srcp.defaultBool,
							defaultFloat = srcp.defaultFloat,
							defaultInt = srcp.defaultInt
						};
						parameters.Add(srcp.name, dstp);
					}
				}
			}
			destination.parameters = parameters.Values.ToArray();
		}

		private void ComposeLayers() {
			foreach (var source in sources) {
				if (source == null)
					continue;
				for (var i = 0; i < source.layers.Length; i++) {
					var newL = CloneLayer(source.layers[i], i == 0);
					newL.name = destination.MakeUniqueLayerName(newL.name);
					newL.stateMachine.name = newL.name;
					destination.AddLayer(newL);
				}
			}
		}

		public void Refresh() {
			try {
				AssetDatabase.StartAssetEditing();
				CleanupDest();
				ComposeParameters();
				ComposeLayers();
				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();
			} finally {
				AssetDatabase.StopAssetEditing();
			}

			var assetPath = AssetDatabase.GetAssetPath(destination);
			var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
			var counter = new Dictionary<Type, int>();
			foreach (var asset in allAssets) {
				var t = asset.GetType();
				counter.TryGetValue(t, out var c);
				counter[t] = c + 1;
			}
			var log = $"Composed {sources.Length} animators into {allAssets.Length} objects:\n";
			foreach (var pair in counter) {
				log += $"- {pair.Key.Name}: {pair.Value}\n";
			}
			Debug.Log(log, this);
		}

		public UnityEngine.Object AsUnityObject()
			=> this;

		public string RefreshablePath()
			=> AssetDatabase.GetAssetPath(this);

#endif // UNITY_EDITOR
	}
}


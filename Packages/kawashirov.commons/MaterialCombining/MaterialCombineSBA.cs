#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Kawashirov.MaterialCombining;

namespace Kawashirov.SceneBuilding {
	public class MaterialCombineSBA : BaseSBA {
		[Tooltip("Run only eanbled mesh combiners, ignore disabled")]
		public bool OnlyEnabled = true;
		[Tooltip("Grab and apply literaly every mesh combiner on scene")]
		public bool CombineEveryithingOnScene = false;

		[Space]
		[Tooltip("What combiners to run.\nUsed only if CombineEveryithingOnScene is off")]
		public List<AbstaractMaterialCombiner> Combiners = new List<AbstaractMaterialCombiner>();
		[Tooltip("Exclude those combiners.\nUseful when CombineEveryithingOnScene is on, but applies always.")]
		public List<AbstaractMaterialCombiner> Except = new List<AbstaractMaterialCombiner>();

		protected virtual List<AbstaractMaterialCombiner> GetCombiners() {
			var allow_disabled = !OnlyEnabled;
			var except = Except.Distinct().UnityNotNull();
			if (CombineEveryithingOnScene) {
				return gameObject.scene.GetRootGameObjects()
					.SelectMany(gobj => gobj.GetComponentsInChildren<AbstaractMaterialCombiner>())
					.Where(mc => allow_disabled || (mc.enabled && mc.gameObject.activeSelf))
					.Except(except).ToList();
			} else {
				return Combiners.Distinct().UnityNotNull()
					.Where(mc => allow_disabled ||(mc.enabled && mc.gameObject.activeSelf))
					.Except(except).ToList();
			}
		}

		public override IEnumerator RunAsync(BuildingScenario scenario) {
			Log($"Searching combiners to run...");
			var combiners = GetCombiners();
			Log($"Found {combiners.Count}, running...");
			foreach (var combiner in combiners) {
				if (debugMode)
					combiner.debugMode = true;
				Log($"Running combiner {combiner}...", combiner);
				var task = combiner.Run();
				while (true) {
					try {
						if (!task.MoveNext())
							break;
					} catch (Exception exc) {
						LogException("Combiner {combiner} failed!", exc, combiner);
						throw exc;
					}
					yield return task.Current;
				}
			}
			Log($"Done {combiners.Count} combiners.");
		}

		[CustomEditor(typeof(MaterialCombineSBA), true)]
		public class MaterialCombineSBAEditor : BaseSBAEditor {
			// TODO
		}
	}
}
#endif

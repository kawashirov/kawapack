#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Kawashirov.SceneBuilding;
using System.Collections;
using System;

namespace Kawashirov.MeshCombining {
	public class MeshCombineSBA : BaseSBA {
		[Tooltip("Run only eanbled mesh combiners, ignore disabled")]
		public bool OnlyEnabled = true;
		[Tooltip("Grab and apply literaly every mesh combiner on scene")]
		public bool CombineEveryithingOnScene = false;

		[Space]
		[Tooltip("What combiners to run.\nUsed only if CombineEveryithingOnScene is off")]
		public List<MeshCombiner> Combiners = new List<MeshCombiner>();
		[Tooltip("Exclude those combiners.\nUseful when CombineEveryithingOnScene is on, but applies always.")]
		public List<MeshCombiner> Except = new List<MeshCombiner>();

		protected virtual List<MeshCombiner> GetCombiners() {
			var allow_disabled = !OnlyEnabled;
			var except = Except.Distinct().UnityNotNull();
			if (CombineEveryithingOnScene) {
				return gameObject.scene.GetRootGameObjects()
					.SelectMany(gobj => gobj.GetComponentsInChildren<MeshCombiner>())
					.Where(mc => allow_disabled || mc.enabled)
					.Except(except).ToList();
			} else {
				return Combiners.Distinct().UnityNotNull()
					.Where(mc => allow_disabled || mc.enabled)
					.Except(except).ToList();
			}
		}

		public override IEnumerator RunAsync(BuildingScenario scenario) {
			LogDebug($"Searching mesh combiners to run...");
			var combiners = GetCombiners();
			Log($"Found mesh combiners {combiners.Count}, running...");
			foreach (var combiner in combiners) {
				Log($"Running mesh combiner {combiner}...", combiner);
				var task = combiner.Run();
				while (true) {
					try {
						if (!task.MoveNext())
							break;
					} catch (Exception exc) {
						LogException($"Mesh combiner {combiner} failed!", exc, combiner);
						throw exc;
					}
					yield return task.Current;
				}
			}
			Log($"Done {combiners.Count} mesh combiners.");
		}

		// TODO MeshCombineSBAEditor
	}
}
#endif

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Kawashirov.SceneBuilding;

namespace Kawashirov.MeshCombining {
	public class MeshCombineSBA : BaseSBA {
		[Tooltip("Run only eanbled mesh combiners, ignore disabled")]
		public bool OnlyEnabled = true;
		[Tooltip("Grab and apply literaly every mesh combiner on scene")]
		public bool CombineEveryithingOnScene = false;

		[Space]
		[Tooltip("What combiners to run.\nUsed only if CombineEveryithingOnScene is off")]
		public BaseMeshCombiner[] Combiners;
		[Tooltip("Exclude those combiners.\nUseful when CombineEveryithingOnScene is on, but applies always.")]
		public BaseMeshCombiner[] Except;

		protected virtual List<BaseMeshCombiner> GetCombiners() {
			var allow_disabled = !OnlyEnabled;
			var except = Except.Distinct().UnityNotNull();
			if (CombineEveryithingOnScene) {
				return gameObject.scene.GetRootGameObjects()
					.SelectMany(gobj => gobj.GetComponentsInChildren<BaseMeshCombiner>())
					.Where(mc => allow_disabled || mc.enabled)
					.Except(except).ToList();
			} else {
				return Combiners.Distinct().UnityNotNull()
					.Where(mc => allow_disabled || mc.enabled)
					.Except(except).ToList();
			}
		}

		public override void RunSync(BuildingScenario scenario) {
			Log($"Searching combiners to run...");
			var combiners = GetCombiners();
			Log($"Found {combiners.Count}, running...");
			foreach (var combiner in combiners) {
				Log($"Running combiner at {combiner.gameObject.KawaGetFullPath()}...", combiner);
				combiner.Run();
			}
			Log($"Done {combiners.Count} combiners.");
		}

		// TODO MeshCombineSBAEditor
	}
}
#endif

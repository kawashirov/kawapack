using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Kawashirov.MaterialCombining;
using Kawashirov.SceneBuilding;
using Kawashirov;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MaterialCombineBuildingAction : BaseBuildingAction
{
#if UNITY_EDITOR
		[Tooltip("Run only eanbled mesh combiners, ignore disabled")]
		public bool OnlyEnabled = true;
		[Tooltip("Grab and apply literaly every mesh combiner on scene")]
		public bool CombineEveryithingOnScene = false;

		[Space]
		[Tooltip("What combiners to run.\nUsed only if CombineEveryithingOnScene is off")]
		public MaterialCombiner[] Combiners;
		[Tooltip("Exclude those combiners.\nUseful when CombineEveryithingOnScene is on, but applies always.")]
		public MaterialCombiner[] Except;

		protected virtual List<MaterialCombiner> GetCombiners() {
			var allow_disabled = !OnlyEnabled;
			var except = Except.Distinct().UnityNotNull();
			if (CombineEveryithingOnScene) {
				return gameObject.scene.GetRootGameObjects()
					.SelectMany(gobj => gobj.GetComponentsInChildren<MaterialCombiner>())
					.Where(mc => allow_disabled || mc.enabled)
					.Except(except).ToList();
			} else {
				return Combiners.Distinct().UnityNotNull()
					.Where(mc => allow_disabled || mc.enabled)
					.Except(except).ToList();
			}
		}

		public override void RunSync() {
			Debug.Log($"Searching combiners to run...", this);
			var combiners = GetCombiners();
			Debug.Log($"Found {combiners.Count}, running...", this);
			foreach (var combiner in combiners) {
				Debug.Log($"Running combiner at {combiner.gameObject.KawaGetFullPath()}...", combiner);
				combiner.Run();
			}
			Debug.Log($"Done {combiners.Count} combiners.", this);
		}

		// TODO MeshCombineBuildingActionEditor

#endif
}

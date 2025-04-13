#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

using Kawashirov.SceneBuilding;
using Kawashirov.Refreshables;

namespace Kawashirov.CollidersFromMaterials {
	public class CollidersFromMaterialsSBA : BaseSBA {
		public bool EveryRendererOnScene = false;
		public List<GameObject> RenderersHierarchy = new List<GameObject>();
		public bool IgnoreEditorOnlyRenderers = true;

		[Space]
		public bool EveryTransformerOnScene = false;
		public List<CollidersFromMaterial> Transformers = new List<CollidersFromMaterial>();
		public List<CollidersFromMaterial> Except = new List<CollidersFromMaterial>();
		public bool IgnoreDisabledTransformers = true;

		[Header("Properties below are auto-generated")]
		public List<CollidersFromMaterial> ResolvedTransformers = new List<CollidersFromMaterial>();
		public List<MeshRenderer> OriginalRenderers = new List<MeshRenderer>();

		protected virtual List<CollidersFromMaterial> ResolveTransformers() {
			LogDebug($"Looking for {nameof(CollidersFromMaterial)}s for {gameObject.name}...");
			ResolvedTransformers.Clear();

			IEnumerable<CollidersFromMaterial> transformers = null;
			if (EveryTransformerOnScene) {
				transformers = gameObject.scene.GetRootGameObjects().SelectMany(
					g => g.GetComponentsInChildren<CollidersFromMaterial>(!IgnoreDisabledTransformers)
				);
			}
			if (EveryTransformerOnScene == true || RenderersHierarchy == null || RenderersHierarchy.Count < 1) {
				// При пустом Hierarchy ищем в самом себе
				transformers = gameObject.GetComponentsInChildren<CollidersFromMaterial>(!IgnoreDisabledTransformers);
			} else {
				transformers = Transformers;
			}

			transformers = transformers.UnityNotNull().Distinct();
			if (IgnoreDisabledTransformers)
				transformers = transformers.Where(r => r.enabled && r.gameObject.activeInHierarchy);

			ResolvedTransformers.Clear();
			ResolvedTransformers.AddRange(transformers);
			if (OriginalRenderers.Count > 0) {
				LogDebug($"Found {ResolvedTransformers.Count} {nameof(CollidersFromMaterial)}s for {gameObject.name}.");
			} else {
				LogWarning($"Found {ResolvedTransformers.Count} {nameof(CollidersFromMaterial)}s for {gameObject.name}.");
			}
			return ResolvedTransformers;
		}

		protected virtual List<MeshRenderer> ResolveHierarchy() {
			LogDebug($"Looking for original mesh renderers for {gameObject.name}...");
			OriginalRenderers.Clear();

			IEnumerable<MeshRenderer> hrs = null;
			if (EveryRendererOnScene) {
				hrs = gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true));
			} else if (RenderersHierarchy == null || RenderersHierarchy.Count < 1) {
				// При пустом Hierarchy ищем в самом себе
				hrs = gameObject.GetComponentsInChildren<MeshRenderer>(true);
			} else {
				hrs = RenderersHierarchy.UnityNotNull().Distinct().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true));
			}

			hrs = hrs.UnityNotNull().Distinct();
			if (IgnoreEditorOnlyRenderers)
				hrs = hrs.RuntimeOnly();

			OriginalRenderers.Clear();
			OriginalRenderers.AddRange(hrs);
			if (OriginalRenderers.Count > 0) {
				LogDebug($"Found {OriginalRenderers.Count} mesh renderers for {gameObject.name}.");
			} else {
				LogWarning($"Found {OriginalRenderers.Count} mesh renderers for {gameObject.name}.");
			}
			return OriginalRenderers;
		}

		public override void Refresh() {
			ResolveTransformers();
			ResolveHierarchy();

			Log($"Processing colliders on {OriginalRenderers.Count} renderers with {ResolvedTransformers.Count} transformers...");

			foreach (var transformer in ResolvedTransformers) {
				transformer.ClearOutputs();
				transformer.SetDirty();
			}

			var counter = 0;
			var match = new List<CollidersFromMaterial>(ResolvedTransformers.Count);
			foreach (var renderer in OriginalRenderers) {
				match.Clear();
				var any_partial = false;
				foreach (var transformer in ResolvedTransformers) {
					var match_result = transformer.MatchMeshRenderer(renderer);
					if (match_result == MatchResult.Match) {
						match.Add(transformer);
					} else if (match_result == MatchResult.Partial) {
						any_partial = true;
					}
				}
				if (match.Count > 1) {
					var match_s = string.Join("\n", match.Select((m, i) => $"- №{i}: {m.KawaGetFullPath()}"));
					LogWarning($"MeshRenderer match <b>{match.Count}</b> {nameof(CollidersFromMaterial)}s:\n" +
						$"{match_s}\nOnly first one will be used!", renderer);
				}
				if (any_partial) {
					LogWarning($"MeshRenderer has both matching and missmatching materials for colliders!", renderer);
				}

				if (match.Count > 0 && match[0].TryApply(renderer)) {
					++counter;
				}
			}
			CollidersFromMaterial.FreeBuffers();
			var count = ResolvedTransformers.SelectMany(t => t.ConfiguredColliders).Distinct().Count();
			Log($"Configured {count} colliders from materials on {OriginalRenderers.Count} renderers " +
				$"with {ResolvedTransformers.Count} transformers.");
		}

		public override void RunSync(BuildingScenario scenario) => Refresh();

		[CustomEditor(typeof(CollidersFromMaterialsSBA), true)]
		public class CollidersFromMaterialsSBAEditor : BaseSBAEditor {

			public override void OnInspectorGUI() {
				IKnowWhatIamDoingGUI();
				DebugModeGUI();
				DrawDefaultInspector();
				this.BehaviourRefreshGUI();
			}
		}
	}
}
#endif
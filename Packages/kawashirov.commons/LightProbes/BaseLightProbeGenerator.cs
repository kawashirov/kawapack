using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Kawashirov.Refreshables;

#if UNITY_EDITOR
using UnityEditor;
#endif


namespace Kawashirov {
	public class BaseLightProbeGenerator : KawaEditorBehaviour {
		protected static bool _debug_fold_renderers = false;

		// Кастомные типы данных и поля не спрятаны под UNITY_EDITOR
		// что бы настройки могли сереализоваться/сохраняться.

		public enum KawaRaycastAxis { X = 0, Y = 1, Z = 2 }

		public enum HitDirection { Forward, Backward }

		[Serializable]
		public struct KawaRaycastHit {
			[SerializeField] public RaycastHit hit;
			public HitDirection direction;
			public float radius; // Чисто дебажная инфа

			public KawaRaycastHit LocalToWorld(Transform local_space) {
				var hit = this.hit;
				hit.point = local_space.TransformPoint(hit.point);
				hit.normal = local_space.TransformDirection(hit.normal);
				return new KawaRaycastHit { hit = hit, direction = direction, radius = radius, };
			}

			public KawaRaycastHit WorldToLocal(Transform local_space) {
				var hit = this.hit;
				hit.point = local_space.InverseTransformPoint(hit.point);
				hit.normal = local_space.InverseTransformDirection(hit.normal);
				return new KawaRaycastHit { hit = hit, direction = direction, radius = radius, };
			}


		}

		[Serializable]
		public struct KawaRaycastSegment {
			// Это как UnityEngine.Ray, только отрезок: имеет начало, конец и пару вспомогательных методов.

			public Vector3 begin;
			public Vector3 end;

			public Vector3 normal_at_begin;
			public Vector3 normal_at_end;

			public float radius; // Чисто дебажная инфа

#if UNITY_EDITOR
			public float Length() => Vector3.Distance(begin, end);

			public Ray GetForwardRay() => new Ray(begin, Vector3.Normalize(end - begin));

			public Ray GetBackwardRay() => new Ray(end, Vector3.Normalize(begin - end));

			public KawaRaycastSegment LocalToWorld(Transform local_space) => new KawaRaycastSegment {
				begin = local_space.TransformPoint(begin),
				end = local_space.TransformPoint(end),
				normal_at_begin = local_space.TransformDirection(normal_at_begin),
				normal_at_end = local_space.TransformDirection(normal_at_end),
				radius = radius, // TODO: Scale
			};

			public KawaRaycastSegment WorldToLocal(Transform local_space) => new KawaRaycastSegment {
				begin = local_space.InverseTransformPoint(begin),
				end = local_space.InverseTransformPoint(end),
				normal_at_begin = local_space.InverseTransformDirection(normal_at_begin),
				normal_at_end = local_space.InverseTransformDirection(normal_at_end),
				radius = radius, // TODO: Scale
			};
#endif // UNITY_EDITOR
		}

		// Общие поля

		[Tooltip("Save debug data and draw extra gizmos. You need to regenarate to collect data.")]
		public bool debug = false;

		[NonSerialized] public HashSet<GameObject> _tmp_game_objects_ = new HashSet<GameObject>();

		[SerializeField, HideInInspector] protected KawaRaycastHit[] _debug_hits_ = new KawaRaycastHit[0];
		[SerializeField, HideInInspector] protected KawaRaycastSegment[] _debug_segments_ = new KawaRaycastSegment[0];
		[SerializeField, HideInInspector] protected MeshRenderer[] _debug_renderrers_ = new MeshRenderer[0];
		[SerializeField, HideInInspector] protected int _debug_initial_segments_ = 0;

#if UNITY_EDITOR

		[CanEditMultipleObjects, CustomEditor(typeof(BaseLightProbeGenerator), true)]
		public class Editor : UnityEditor.Editor {
			[MenuItem("Kawashirov/Lightprobe volumes/Refresh in loaded scenes")]
			public static void RefreshLP() {
				KawaUtilities.IterScenesRoots()
					.SelectMany(g => g.GetComponentsInChildren<BaseLightProbeGenerator>())
					.ToList().RefreshMultiple();
			}

			public override void OnInspectorGUI() {
				var targets = this.targets.Select(t => t as BaseLightProbeGenerator).ToArray();
				var debug = targets.Where(t => t.debug).Any();

				base.DrawDefaultInspector();

				if (debug) {
					EditorGUILayout.Space();
					var style = GUI.skin.box;
					using (var v = new EditorGUILayout.VerticalScope(style, GUILayout.MaxWidth(200))) {
						EditorGUIUtility.labelWidth = v.rect.width * 0.75f;
						OnInspectorDebugInfoGUI();
						EditorGUIUtility.labelWidth = 0;
					}
				}

				EditorGUILayout.Space();

				if (debug && GUILayout.Button("Reset debug data")) {
					foreach (var target in targets)
						target.ResetDebug();
				}

				this.BehaviourRefreshGUI();

				EditorGUILayout.LabelField("Globals:");
				KawaGizmos.DrawEditorGizmosGUI();
			}

			public virtual void OnInspectorDebugInfoGUI() {
				var targets = this.targets.Select(t => t as BaseLightProbeGenerator).Where(t => t != null).ToArray();

				var renderers = targets.SelectMany(t => t._debug_renderrers_).Distinct().ToList();
				var segments_initial = targets.Select(t => t._debug_initial_segments_).Sum();
				var segments_raycasted = targets.Select(t => t._debug_segments_.Length).Sum();
				var raycast_hits = targets.Select(t => t._debug_hits_.Length).Sum();
				var raycast_hits_f = targets.Select(t => t._debug_hits_.Where(x => x.direction == HitDirection.Forward).Count()).Sum();
				var raycast_hits_b = targets.Select(t => t._debug_hits_.Where(x => x.direction == HitDirection.Backward).Count()).Sum();

				var probes_count = targets.Select(x => x.GetComponent<LightProbeGroup>()).Where(x => x != null)
						.Select(x => x.probePositions.Length).Sum();

				EditorGUILayout.LabelField("Debug info:");
				EditorGUILayout.LabelField("Selected generators:", targets.Length.ToString());
				EditorGUILayout.LabelField("Initial segments:", segments_initial.ToString());
				using (new EditorGUI.IndentLevelScope()) {
					_debug_fold_renderers = EditorGUILayout.Foldout(
						_debug_fold_renderers, string.Format("MeshRenderers ({0}) used: ", renderers.Count.ToString())
					);
					if (_debug_fold_renderers && renderers.Count > 0)
						for (var i = 0; i < renderers.Count; ++i)
							EditorGUILayout.ObjectField(renderers[i], typeof(MeshRenderer), true);
				}
				EditorGUILayout.LabelField("Total raycast hits:", raycast_hits.ToString());
				EditorGUILayout.LabelField("Forward raycast hits:", raycast_hits_f.ToString());
				EditorGUILayout.LabelField("Backward raycast hits:", raycast_hits_b.ToString());
				EditorGUILayout.LabelField("Generated segments:", segments_raycasted.ToString());
				EditorGUILayout.LabelField("Total light probes:", probes_count.ToString());
			}

		}

		public virtual void ResetDebug() {
			_debug_hits_ = new KawaRaycastHit[0];
			_debug_segments_ = new KawaRaycastSegment[0];
			_debug_renderrers_ = new MeshRenderer[0];
			_debug_initial_segments_ = 0;
		}

		public override void Refresh() { /* */ }

		protected virtual Bounds GetBounds() => throw new NotImplementedException();

		private List<MeshRenderer> GetMeshRenderersToRaycast() {
			// На тоже сцене, где и этот компонент, ищет все MeshRenderer, со следующими условиями:
			// - MeshRenderer.gameObject.isStatic
			// - MeshRenderer.bounds пересекается с this.GetBounds()
			// - MeshRenderer имеет MeshFilter с привязаной Mesh
			// Эти MeshRenderer используются для Raycast

			var self_bounds = GetBounds();
			// TODO FIXME Почему-то Intersects не всегда срабатывает
			var near_renderers = gameObject.scene.GetRootGameObjects()
					.SelectMany(x => x.GetComponentsInChildren<MeshRenderer>())
					.Where(mr => GameObjectUtility.AreStaticEditorFlagsSet(mr.gameObject, StaticEditorFlags.BatchingStatic) && mr.shadowCastingMode != ShadowCastingMode.Off)
					.Select(mr => new { mr, b = mr.bounds, f = mr.GetComponent<MeshFilter>() })
					.Where(x => x.f != null && x.f.sharedMesh != null) //  && x.b.Intersects(self_bounds)
					.Select(x => x.mr)
					.ToList();
			if (debug) {
				Debug.LogFormat("near_renderers: {0}", near_renderers.Count);
				_debug_renderrers_ = near_renderers.ToArray();
			}
			return near_renderers;
		}

		protected void DestroyTempGameObject(GameObject tmp_obj) {
			if (tmp_obj != null && _tmp_game_objects_.Contains(tmp_obj)) {
				_tmp_game_objects_.Remove(tmp_obj);
				DestroyImmediate(tmp_obj);
			}
		}

		protected void DestroyAllTempGameObjects() {
			foreach (var tmp_obj in _tmp_game_objects_.ToArray()) {
				_tmp_game_objects_.Remove(tmp_obj);
				DestroyImmediate(tmp_obj);
			}
		}

		protected GameObject AllocTempGameObject(string name = null) {
			var new_obj = new GameObject(string.IsNullOrWhiteSpace(name) ? "__TEMP__" : name);
			new_obj.transform.parent = gameObject.transform;
			new_obj.transform.localPosition = Vector3.zero;
			new_obj.transform.localRotation = Quaternion.identity;
			new_obj.transform.localScale = Vector3.one;
			_tmp_game_objects_.Add(new_obj);
			return new_obj;
		}

		private List<MeshCollider> AllocTempMeshColliders() {
			// Создает временные MeshCollider из GetMeshRenderersToRaycast()
			// Эти MeshRenderer используются для Raycast

			var renderers = GetMeshRenderersToRaycast();
			var tmp_colliders = new List<MeshCollider>();

			for (var i = 0; i < renderers.Count; ++i) {
				var renderer = renderers[i];

				var filter = renderer.GetComponent<MeshFilter>();
				if (filter == null) {
					Debug.LogWarningFormat(renderer, "[KawaLPG] Renderer has no MeshFilter! @ <i>{0}</i>", renderer.transform.KawaGetHierarchyPath());
					continue;
				}

				var mesh = filter.sharedMesh;
				if (mesh == null) {
					Debug.LogWarningFormat(filter, "[KawaLPG] MeshFilter has no attached Mesh! @ <i>{0}</i>", filter.transform.KawaGetHierarchyPath());
					continue;
				}

				var renderer_t = renderer.transform;
				var tmp_obj = AllocTempGameObject(string.Format("__TEMP__{0}__", i));
				var tmp_obj_t = tmp_obj.transform;
				tmp_obj_t.position = renderer_t.position;
				tmp_obj_t.rotation = renderer_t.rotation;
				tmp_obj_t.localScale = renderer_t.lossyScale;
				tmp_obj.tag = "EditorOnly";
				//tmp_obj.layer = LayerMask.NameToLayer("reserved3");
				tmp_obj.isStatic = true;
				var tmp_collider = tmp_obj.GetOrAddComponent<MeshCollider>();
				tmp_collider.sharedMesh = mesh;
				tmp_collider.convex = false;
				tmp_collider.isTrigger = false;

				tmp_colliders.Add(tmp_collider);
			}

			return tmp_colliders;
		}

		protected void RaycastSegments(
				ICollection<KawaRaycastSegment> results, ICollection<KawaRaycastSegment> world_segments,
				float step_world = 0, bool fake_hits = false
		) {
			var tmp_colliders = AllocTempMeshColliders();
			try {
				if (debug) {
					_debug_initial_segments_ = world_segments.Count;
					_debug_hits_ = new KawaRaycastHit[0];
				}

				Debug.LogFormat(this, "[KawaLPG] Using <b>{1}</b> meshes for raycasting. @ <i>{0}</i>", kawaHierarchyPath, tmp_colliders.Count);
				var conv_colliders = tmp_colliders.ConvertAll(c => (Collider)c);
				foreach (var world_segment in world_segments) {
					RaycastSegment(results, conv_colliders, world_segment, step_world, fake_hits);
				}

				if (debug)
					_debug_segments_ = results.Select(x => x.WorldToLocal(transform)).ToArray();
			} finally {
				tmp_colliders.ForEach(x => DestroyTempGameObject(x.gameObject));
			}
		}

		private void RaycastSegment(
				ICollection<KawaRaycastSegment> results, List<Collider> colliders, KawaRaycastSegment world_segment,
				float step = 0, bool fake_hits = false
				) {
			// Делает Raycast по отрезку и разбивает на под-отрезки.

			var world_ray_fwd = world_segment.GetForwardRay();
			var world_ray_back = world_segment.GetBackwardRay();
			var world_dst = world_segment.Length();

			var hits_fwd = Raymulticast(colliders, world_ray_fwd, world_dst, step);
			var hits_back = Raymulticast(colliders, world_ray_back, world_dst, step);

			// Трюкачество: т.к. barycentricCoordinate нам не нужен, мы там храним флаги: 
			// X: 0 - Raycast вперед, 1 - Raycast назад


			var hits_mixed = new List<KawaRaycastHit>(hits_fwd.Count + hits_back.Count);
			hits_mixed.AddRange(hits_fwd.Select(h => {
				h.point -= world_ray_fwd.direction * step;
				return new KawaRaycastHit { hit = h, direction = HitDirection.Forward, radius = step /* Debug */ };
			}));
			hits_mixed.AddRange(hits_back.Select(h => {
				h.point -= world_ray_back.direction * step;
				return new KawaRaycastHit { hit = h, direction = HitDirection.Backward, radius = step /* Debug */ };
			}));

			hits_mixed = hits_mixed.OrderBy(x => (world_segment.begin - x.hit.point).sqrMagnitude).ToList();

			if (fake_hits) {
				var fake_hit_begin = new KawaRaycastHit {
					hit = new RaycastHit {
						point = world_segment.begin,
						distance = 0,
						normal = world_ray_fwd.direction,
					},
					direction = HitDirection.Backward,
					radius = step, // Дебажная инфа
				}; // Фейковое начало, как будто в точке, из которой делается Raycast, что-то есть
				var fake_hit_end = new KawaRaycastHit {
					hit = new RaycastHit {
						point = world_segment.end,
						distance = world_dst,
						normal = -world_ray_fwd.direction,
					},
					direction = HitDirection.Forward,
					radius = step, // Дебажная инфа
				}; // Фейковый конец, как будто в точке, в которой оканчивается Raycast, что-то есть
				hits_mixed.Insert(0, fake_hit_begin);
				hits_mixed.Add(fake_hit_end);
			}

			for (var hit_i = 1; hit_i < hits_mixed.Count; ++hit_i) {
				// Перебор всех пар RaycastHit и просмотр флага:
				// Если первый RaycastHit.barycentricCoordinate.x == 1 и RaycastHit.barycentricCoordinate.x == 0
				// То между этими двумя точками - воздух, сохраняем его в отрезок.
				KawaRaycastHit hit_a = hits_mixed[hit_i - 1], hit_b = hits_mixed[hit_i];
				if (hit_a.direction == HitDirection.Backward && hit_b.direction == HitDirection.Forward) {
					var vec_back = hit_b.hit.point - hit_a.hit.point;
					var dst_back = vec_back.magnitude;
					results.Add(new KawaRaycastSegment {
						begin = hit_a.hit.point,
						end = hit_b.hit.point,
						normal_at_begin = hit_a.hit.normal,
						normal_at_end = hit_b.hit.normal,
						radius = step,
					});
				}
			}

			Debug.LogFormat(this, "[KawaLPG] Raycasted <b>{1}</b> forwardwd and <b>{2}</b> back hits. @ <i>{0}</i>", kawaHierarchyPath, hits_fwd.Count, hits_back.Count);
			if (debug)
				_debug_hits_ = _debug_hits_.Concat(hits_mixed.Select(x => x.WorldToLocal(transform))).ToArray();
		}

		private List<RaycastHit> Raymulticast(List<Collider> colliders, Ray world_ray, float world_dst, float step = 0) {
			var hits = new List<RaycastHit>();
			colliders.ForEach(x => Raymulticast(x, world_ray, world_dst, hits, step));
			// hits.Sort((a, b) => a.distance.CompareTo(b.distance));
			return hits;
		}

		private void Raymulticast(Collider collider, Ray world_ray, float world_dst, ICollection<RaycastHit> store, float step = 0) {
			// как collider.Raycast, только находит все пересечения в случае меш коллайдера 

			world_ray.direction = Vector3.Normalize(world_ray.direction);
			while (world_dst > 0) {
				var hit = new RaycastHit();
				var result = collider.Raycast(world_ray, out hit, world_dst);
				if (!result)
					return;
				store.Add(hit);
				var mv_fwd = hit.distance + step;
				world_ray.origin += world_ray.direction * mv_fwd;
				world_dst -= mv_fwd;
			}
		}

		protected void OnDrawGizmosLPG() {
			var lpg = GetComponent<LightProbeGroup>();
			if (lpg != null) {
				Gizmos.color = Color.yellow.Alpha(KawaGizmos.GizmosAplha);
				foreach (var pos_local in lpg.probePositions) {
					Gizmos.DrawWireSphere(transform.TransformPoint(pos_local), 0.01f);
				}
			}
			Gizmos.color = Color.white;
		}

		protected void OnDrawGizmosDebug() {
			if (debug) {
				var this_t = transform;
				foreach (var segment_l in _debug_segments_) {
					var segment_w = segment_l.LocalToWorld(this_t);
					Gizmos.color = Color.green.Alpha(KawaGizmos.GizmosAplha);
					Gizmos.DrawLine(segment_w.begin, segment_w.end);

					var radius = Mathf.Max(segment_w.radius, 0.01f);
					Gizmos.color = Color.green.Alpha(KawaGizmos.GizmosAplha);
					Gizmos.DrawWireSphere(segment_w.begin, radius);
					Gizmos.DrawWireSphere(segment_w.end, radius);
				}
				foreach (var hit_l in _debug_hits_) {
					var hit_w = hit_l.LocalToWorld(this_t);
					Gizmos.color = (hit_w.direction == HitDirection.Backward ? Color.red : Color.blue).Alpha(KawaGizmos.GizmosAplha);
					Gizmos.DrawWireSphere(hit_w.hit.point, Mathf.Max(hit_w.radius, 0.01f));
				}
			}
		}

		protected virtual void OnDrawGizmosSelected() {
			OnDrawGizmosLPG();
			OnDrawGizmosDebug();
		}

#endif // UNITY_EDITOR
	}
}

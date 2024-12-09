using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kawashirov {
	[RequireComponent(typeof(BoxCollider))]
	public class BoxLightProbeGenerator : BaseLightProbeGenerator {
		[Tooltip("Direction of raycasts. Local. Y is vertical.")]
		public KawaRaycastAxis raycast_axis = KawaRaycastAxis.Y;

		[Tooltip("Padding around box. Axis and sizes are local.")]
		public Vector3 padding = new Vector3(0.05f, 0.05f, 0.05f);
		[Tooltip("Minimal space between light probes. Segments shorter than this will be collapsed to a single point. Axis and sizes are local.")]
		public Vector3 spacing_min = new Vector3(0.1f, 0.1f, 0.1f);
		[Tooltip("Maximum space between light probes. Segments longer than this will be subdivided. Axis and sizes are local.")]
		public Vector3 spacing_max = new Vector3(2, 2, 2);
		[Tooltip("Add fake raycast hits at begin and end of rays. Like box surface have a mesh.")]
		public bool fake_edges_hits = false;

		[Tooltip("Do raycasting only around box bounds. Cage-like mode.")]
		public bool only_bounds = false;

#if UNITY_EDITOR

		protected override Bounds GetBounds() {
			var box = GetComponent<BoxCollider>();
			if (box == null) {
				Debug.LogErrorFormat(this, "[KawaLPG] <b>{0}</b> does not have BoxCollider, can not get bounds!", kawaHierarchyPath);
				throw new NullReferenceException(string.Format("{0} does not have BoxCollider, can not get bounds!", kawaHierarchyPath));
			}
			return box.bounds;
		}

		protected override void OnDrawGizmosSelected() {
			base.OnDrawGizmosSelected();

		}

		public float[] MakeGrid(float left, float right, float spacing_min, float spacing_max) {
			var length = Mathf.Abs(left - right);
			if (length < spacing_min)
				return new float[] { (left * 0.5f) + (right * 0.5f) }; // Если размер маленький, то возвращаем центр.
			var subdivs = Mathf.CeilToInt(length / spacing_max); // На сколько частей нужно делать отрезок
			var grid = new float[subdivs + 1];
			for (var i = 0; i <= subdivs; ++i)
				grid[i] = Mathf.Lerp(left, right, 1.0f * i / subdivs);
			return grid;
		}

		public override void Refresh() {
			gameObject.tag = "EditorOnly";
			gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
			var raxis = raycast_axis;
			var raxis_vector = new Vector3(raxis == KawaRaycastAxis.X ? 1 : 0, raxis == KawaRaycastAxis.Y ? 1 : 0, raxis == KawaRaycastAxis.Z ? 1 : 0);
			var raxis_vector_inv = Vector3.one - raxis_vector;

			var spacing_min = Vector3.Min(this.spacing_min, this.spacing_max);
			var spacing_max = Vector3.Max(this.spacing_min, this.spacing_max);
			spacing_max = Vector3.Max(spacing_max, Vector3.one * 0.01f);

			// Вместо тысячи if else
			var spacing_min_axis = Vector3.Scale(spacing_min, raxis_vector).magnitude;
			var spacing_max_axis = Vector3.Scale(spacing_max, raxis_vector).magnitude;
			var padding_axis = Vector3.Scale(Vector3.Max(padding, Vector3.zero), raxis_vector).magnitude;

			var box = gameObject.GetOrAddComponent<BoxCollider>();
			box.enabled = false;
			box.isTrigger = true;
			var padding_cut = Vector3.Scale(padding * 2, raxis_vector_inv);
			var bound_local = new Bounds(box.center, Vector3.Max(Vector3.zero, box.size - padding_cut));

			// Разбивка объема купа на сетку по "свободным" осям.
			var grid_x = raxis == KawaRaycastAxis.X
					? new float[] { 0 }
					: MakeGrid(bound_local.min.x, bound_local.max.x, spacing_min.x, spacing_max.x);
			var grid_y = raxis == KawaRaycastAxis.Y
					? new float[] { 0 }
					: MakeGrid(bound_local.min.y, bound_local.max.y, spacing_min.y, spacing_max.y);
			var grid_z = raxis == KawaRaycastAxis.Z
					? new float[] { 0 }
					: MakeGrid(bound_local.min.z, bound_local.max.z, spacing_min.z, spacing_max.z);

			// Генерация отрезков для рэйкаста из сетки
			var segments_raw = new List<KawaRaycastSegment>(grid_x.Length * grid_y.Length * grid_z.Length);
			for (var ix = 0; ix < grid_x.Length; ++ix) {
				for (var iy = 0; iy < grid_y.Length; ++iy) {
					for (var iz = 0; iz < grid_z.Length; ++iz) {
						if (only_bounds) {
							// Пропуск точки внутри сетки, если this.only_bounds
							var bound_x = raxis != KawaRaycastAxis.X && (ix == 0 || ix == grid_x.Length - 1);
							var bound_y = raxis != KawaRaycastAxis.Y && (iy == 0 || iy == grid_y.Length - 1);
							var bound_z = raxis != KawaRaycastAxis.Z && (iz == 0 || iz == grid_z.Length - 1);
							if (bound_x || bound_y || bound_z)
								continue;
						}
						// Локлаьные координаты точки сетки, но ось raxis занулена.
						var grid_point = Vector3.Scale(new Vector3(grid_x[ix], grid_y[iy], grid_z[iz]), Vector3.one - raxis_vector);
						;
						// Замена координаты raxis на bound_local min и max без тыячи if else
						var begin = Vector3.Scale(bound_local.min, raxis_vector) + grid_point;
						var end = Vector3.Scale(bound_local.max, raxis_vector) + grid_point;
						// Для каждой точки создаем отрезок в направлении оси raxis.
						segments_raw.Add((new KawaRaycastSegment { begin = begin, end = end }).LocalToWorld(transform));
					}
				}
			}

			// Собственно, рэйкасты.
			var segments_raycasted = new List<KawaRaycastSegment>();
			RaycastSegments(segments_raycasted, segments_raw, padding_axis, fake_edges_hits);


			// Разбивка отрезков на точки
			var points = new List<Vector3>();
			foreach (var segment in segments_raycasted) {
				var length = segment.Length();
				var points_on_length = MakeGrid(0f, length, spacing_min_axis, spacing_max_axis);
				foreach (var p in points_on_length) {
					var a = p / length; // [0..1]
					points.Add((segment.begin * a) + (segment.end * (1 - a)));
				}
			}

			var lpg = gameObject.GetComponent<LightProbeGroup>();
			//if (lpg != null) DestroyImmediate(lpg); // Почему-то просто переписать probePositions не канает, нужно пересоздавать компонент.
			//lpg = this.gameObject.AddComponent<LightProbeGroup>();
			var lpg_positions = points.Select(x => transform.InverseTransformPoint(x)).ToArray();
			Undo.RegisterCompleteObjectUndo(lpg, Undo.GetCurrentGroupName());
			lpg.probePositions = lpg_positions;
			EditorUtility.SetDirty(lpg);
			gameObject.GetComponent<BoxCollider>().enabled = false;
			Debug.LogFormat(this, "[KawaLPG] Placed <b>{0}</b> probes for <i>{1}</i>.", lpg_positions.Length, kawaHierarchyPath);
		}
#endif
	}
}

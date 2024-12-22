#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kawashirov.MeshCombining {
	public class MeshCombineOp {
		// Уникальный ИД операции, все созданные ассеты будут иметь его в названии.
		public string ID = null;
		private string logToken = null;

		// Возможность кастомного сравнивания MeshRendererов на объединяемость.
		public MeshRendererEquality mre = null;

		// Исходные MeshRendererы которые нужно объеденить.
		public List<MeshRenderer> Sources = new List<MeshRenderer>();

		// Гейм-объект в который будут объеденённые меши.
		public GameObject Target = null;

		// Применять или нет базовую перепаковку второго UV слоая, если он есть. 
		// Перепаковка имеет базовую, не очень эффективную реализацию, 
		// но необходима для корректной работы лайтмап на объединённой меши.
		public bool RepackLightmapUV = true;

		// false - рендереры с разным scaleInLightmap считаются не комбинируемыми
		// true - рендереры с разным scaleInLightmap кобминируются и UV1 корректируется на это масштаб
		// Может быть переопределено в кастомном MeshRendererEquality,
		// но корректировка будет применяться как указано тут.
		public bool ApplyScaleInLightmap = true;

		/* internals */

		protected List<MeshRendererGroup> groups = new List<MeshRendererGroup>();

		protected virtual void GroupMeshRenderers() {
			Debug.Log($"{logToken}: Trying to group {Sources.Count} source MeshRenderers...");
			groups.Clear();

			var sources = Sources.Distinct().UnityNotNull().ToList();
			if (Sources.Count != sources.Count) {
				Debug.LogWarning($"{logToken}: Sources reduced {Sources.Count} -> {sources.Count}, there is might be empty/dupe elements.");
			}

			foreach (var mr in sources) {
				// Тут по сути сложность N^3, но я не думаю что будет много мешей.
				var matched = groups.Where(g => g.Match(mr)).ToList();
				if (matched.Count == 0) {
					groups.Add(new MeshRendererGroup(this, groups.Count, mr));
				} else if (matched.Count == 1) {
					matched[0].sources.Add(mr);
				} else {
					// TODO подробнее
					var mr_str = mr.gameObject.KawaGetFullPath();
					var msg = $"{logToken}: Matches {mr_str} with {matched.Count} groups, but must be one or nothing.";
					Debug.LogError(msg, mr);
					throw new Exception(msg);
				}
			}

			Debug.Log($"{logToken}: Created {groups.Count} groups from {sources.Count} source MeshRenderers.");
			/*
			for (var i = 0; i < groups.Count; ++i) {
				var group = groups[i];
				var items = string.Join("\n", group.sources.Select(mr => mr.gameObject.KawaGetFullPath()));
				Debug.Log($"{logToken}: Group #{i}:\n{items}");
				// TODO убрать это
			}
			*/
		}

		public virtual void Run() {
			mre ??= new MeshRendererEquality(this);

			ID ??= new System.Random().Next().ToString();

			logToken = $"MeshCombine {ID}";

			GroupMeshRenderers();

			foreach (var group in groups)
				group.Process();
		}

	}
}
#endif

#if UNITY_EDITOR
using UnityEngine;

namespace Kawashirov.MeshCombining {
	public readonly struct SubMeshInfo {
		// Самый атомарный элемент - часть меши меш рендерера
		// Здесь нет ничего для отладки т.к. ничего не делает, просто данные.
		public readonly MeshRenderer meshRenderer;
		public readonly Mesh mesh;
		public readonly int subMeshIndex;
		// Матрица для перехода из локальных координат оригинального MeshRenderer в новый целевой.
		public readonly Matrix4x4 transform;

		public SubMeshInfo(MeshRenderer meshRenderer, Mesh mesh, int subMeshIndex, Matrix4x4 transform) {
			this.meshRenderer = meshRenderer;
			this.mesh = mesh;
			this.subMeshIndex = subMeshIndex;
			this.transform = transform;
		}

		public CombineInstance CombineInstance() {
			// Подготовить инфу для комбинирования всех саб мешей одного материала в одну временную
			return new CombineInstance {
				// TODO lightmapScaleOffset
				mesh = mesh,
				// TODO realtimeLightmapScaleOffset
				subMeshIndex = subMeshIndex,
				transform = transform,
			};
		}
	}
}
#endif
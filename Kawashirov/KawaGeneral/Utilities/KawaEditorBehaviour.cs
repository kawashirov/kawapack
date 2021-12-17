using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using Kawashirov.Refreshables;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kawashirov {
	public abstract class KawaEditorBehaviour : MonoBehaviour, IRefreshable {
#if UNITY_EDITOR

		private string _hierarchy_path = null;
		private string _full_path = null;

		public string kawaHierarchyPath {
			get {
				if (string.IsNullOrWhiteSpace(_hierarchy_path))
					_hierarchy_path = transform.KawaGetHierarchyPath();
				return _hierarchy_path;
				// Possible bug: hierarchy change may not update path
			}
		}

		public string kawaFullPath {
			get {
				if (string.IsNullOrWhiteSpace(_full_path))
					_full_path = transform.gameObject.KawaGetFullPath();
				return _full_path;
				// Possible bug: hierarchy change may not update path
			}
		}

		public virtual void Refresh() { }

		public UnityEngine.Object AsUnityObject() => this;

		public string RefreshablePath() => kawaFullPath;

#endif
	}
}

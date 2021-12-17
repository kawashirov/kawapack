#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace Kawashirov.ToolsGUI {
	public abstract class AbstractToolPanel : ScriptableObject {

		[NonSerialized] public bool active;

		public virtual void ToolsGUI() { }

		public virtual void DrawGizmos() { }

		public virtual void Update() { }

	}
}

#endif // UNITY_EDITOR

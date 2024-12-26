#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kawashirov.MaterialCombining {
	public class DataChannel {
		public Material parent;
		public string name; // "Albedo", "Normal", ...

		public Texture2D texture;
		public string textureChannel; // "R", "G", "B", "A", "RGB", "RGBA", ...
		public Color scaleColor = Color.white;
		public float scale = 1;
	}
}
#endif
#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kawashirov.MaterialCombining {
	public class DataChannel {
		public Material parent;
		public string name; // "Albedo", "Normal", ...

		public Texture2D texture;
		public string textureChannel = "0000"; // Всегда 4 символа: "RGBA", "RGB0", "000A", ...
		public Color scaleColor = Color.white;
		public float scale = 1;

		public float WeightSqr() => 1f * texture.height * texture.width;

	}
}
#endif
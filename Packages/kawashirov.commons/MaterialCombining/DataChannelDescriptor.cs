#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kawashirov.MaterialCombining {
	public class DataChannelDescriptor {
		public AbstractMaterialAdapter parent;
		public string name; // "Albedo", "Normal", ...

		public int scaleFactor = 1;

		public Texture2D bgTexture = Texture2D.whiteTexture;
		public string textureChannels = "RGBA"; // TODO
		public Color bgColor = Color.white;
		public bool alphaIsTransparency = false;
		public bool isNormal = false;
		public bool sRGB = true;
		public bool HDR = false;

		public DataChannelDescriptor(AbstractMaterialAdapter parent, string name) {
			this.parent = parent;
			this.name = name;
		}

	}
}
#endif
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kawashirov.MaterialCombining {
	public class DataTexDesc : IEquatable<DataTexDesc> {
		// Data Texture Descriptor
		public readonly AbstractMaterialAdapter adapter;
		public readonly string name; // "Albedo", "Normal", ...

		public float scaleFactor = 1;

		public Texture2D bgTexture = Texture2D.whiteTexture;
		public string textureChannels = "RGBA"; // TODO
		public Color bgColor = Color.white;
		public bool alphaIsTransparency = false;
		public bool isNormal = false;
		public bool sRGB = true;
		public bool EXR = false;

		public Texture2D atlasTexture = null;

		public DataTexDesc(AbstractMaterialAdapter adapter, string name) {
			this.adapter = adapter;
			this.name = name;
		}

		public bool Equals(DataTexDesc other) => other != null && string.Equals(name, other.name);
		public override int GetHashCode() => name.GetHashCode();
	}
}
#endif
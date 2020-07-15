using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using librsync.net;
using Kawashirov.Refreshables;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kawashirov.ShaderBaking {

	[Serializable]
	public class Generator : ScriptableObject, IRefreshable {

		public string shaderName = "";
		public bool debug = false;
		// [NonSerialized] public string _guid = "";

#if UNITY_EDITOR

		[MenuItem("Kawashirov/Shader Baking/Refresh every shader in project")]
		public static void RefreshEverytingInLoadedScenes() {
			RefreshableExtensions.RefreshEverytingInProject<Generator>(true);
		}

		public static void DeleteGeneratedAtPath(string path) {
			Debug.LogFormat("[KawaShaderBaking] Searching and removing all generated shader files at <i>{0}</i>...", path);
			var shaders = AssetDatabase.FindAssets("t:Shader", new string[] { path })
				.Select(s => AssetDatabase.GUIDToAssetPath(s))
				.Where(s => Path.GetFileName(s).StartsWith("_generated_", StringComparison.InvariantCultureIgnoreCase))
				.ToList();
			try {
				AssetDatabase.DisallowAutoRefresh();
				foreach (var sh in shaders) {
					Debug.LogFormat("[KawaShaderBaking] Removing generated shader file: <i>{0}</i>...", sh);
					AssetDatabase.DeleteAsset(sh);
				}
			} finally {
				AssetDatabase.AllowAutoRefresh();
				AssetDatabase.Refresh();
			}
			Debug.LogFormat("[KawaShaderBaking] Removed <b>{0}</b> generated shader files.", shaders.Count);
		}

		public virtual void Refresh() {
			throw new NotImplementedException();
		}

		public UnityEngine.Object AsUnityObject()
			=> this;

		public string RefreshablePath() 
			=> AssetDatabase.GetAssetPath(this);

#endif
	}
}

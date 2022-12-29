using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using Kawashirov;
using Kawashirov.Refreshables;

using GUIL = UnityEngine.GUILayout;

#if UNITY_EDITOR
using UnityEditor;
using EGUIL = UnityEditor.EditorGUILayout;
using static UnityEditor.EditorGUI;
#endif

namespace Kawashirov.ShaderBaking {

	[Serializable]
	public class BaseGenerator : ScriptableObject, IRefreshable {

		public string shaderName = "";

		public Shader result = null;

#if UNITY_EDITOR

		[CanEditMultipleObjects]
		[CustomEditor(typeof(BaseGenerator))]
		public class Editor<G> : Editor where G : BaseGenerator {

			protected readonly List<Shader> results_shaders = new List<Shader>();

			protected SerializedProperty result = null;

			protected void OnEnable() {
				result = serializedObject.FindProperty("result");
				if (result.hasMultipleDifferentValues) {
					results_shaders.Clear();
					results_shaders.AddRange(
						 serializedObject.targetObjects.OfType<G>().Select(g => g.result)
					);
				}
			}

			public override void OnInspectorGUI() {

				EGUIL.LabelField("Shader");
				using (new IndentLevelScope()) {
					KawaGUIUtility.DefaultPrpertyField(this, "shaderName", "Name");

					if (!result.hasMultipleDifferentValues) {
						var value = result.objectReferenceValue;
						using (new EGUIL.HorizontalScope()) {
							EGUIL.ObjectField("Shader Asset", value, typeof(UnityEngine.Object), false);
							if (GUIL.Button("Delete")) {
								DestroyImmediate(value, true);
								Repaint();
							}
						}
					} else {
						// несколько
						if (results_shaders.Count > 0) {
							EGUIL.LabelField("Shader Assets:");
						} else {
							EGUIL.LabelField("No Shader Assets");
						}
						using (new IndentLevelScope()) {
							foreach (var value in results_shaders) {
								using (new EGUIL.HorizontalScope()) {
									EGUIL.ObjectField(value, typeof(UnityEngine.Object), false);
									if (GUIL.Button("Delete")) {
										DestroyImmediate(value, true);
										Repaint();
									}
								}
							}
							
						}
					}
				}

			}
		}

		[MenuItem("Kawashirov/Shader Baking/Refresh every shader in project")]
		public static void RefreshEverytingInLoadedScenes() {
			RefreshableUtility.RefreshEverytingInProject<BaseGenerator>(true);
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

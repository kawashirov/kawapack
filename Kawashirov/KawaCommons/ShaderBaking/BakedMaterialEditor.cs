using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using static UnityEditor.EditorGUI;

namespace Kawashirov.ShaderBaking {
	public class MaterialEditor<G> : Kawashirov.MaterialEditor where G : BaseGenerator {

		protected readonly GUIContent gui_sh_gen = new GUIContent(
			"Shader Generator", "Shader Generator asset used to generate shader of this material."
		);
		protected readonly GUIContent gui_sh_gens = new GUIContent(
		 "Shader Generators:", "Shader Generators assets used to generate shaders of these materials."
	 );

		// Сохраняем списки глобально что бы было меньше аллокаций
		protected readonly List<Tuple<Material, G>> material_generators = new List<Tuple<Material, G>>();
		protected readonly List<G> generators = new List<G>();
		protected readonly List<Material> materials_with_no_generators = new List<Material>();

		protected G GeneratorFromMaterial(Material m) {
			try {
				var guid = m.GetTag(Commons.GenaratorGUID, false);
				if (string.IsNullOrWhiteSpace(guid))
					return null;
				var generator_path = AssetDatabase.GUIDToAssetPath(guid);
				var generator_obj = AssetDatabase.LoadAssetAtPath<G>(generator_path);
				return generator_obj;
			} catch (Exception) { }
			return null;
		}

		public override void OnEnable() {
			base.OnEnable();

			material_generators.Clear();
			material_generators.AddRange(
				targetMaterials.Select(m => new Tuple<Material, G>(m, GeneratorFromMaterial(m)))
			);

			generators.Clear();
			generators.AddRange(
				material_generators.Select(x => x.Item2).UnityNotNull().Distinct()
			);

			materials_with_no_generators.Clear();
			materials_with_no_generators.AddRange(
				material_generators.Where(g => g.Item2 == null).Select(x => x.Item1)
			);
		}

		protected bool GenaratorGUIDFields() {
			try {
				if (materials_with_no_generators.Count == 1 && targetMaterials.Length == 1) {
					EditorGUILayout.HelpBox(
						"This material has shader with no bound generator object!\n" + 
						"It's recommended to delete this shader and generate new one with new generator object.",
						MessageType.Error, true
					);
				} else if (materials_with_no_generators.Count > 0) {
					// EditorGUILayout.LabelField("Following materials has no bound generator objects:");
					EditorGUILayout.HelpBox(string.Format(
						"Following materials ({0}) has shaders with no bound generator objects of type {1}:",
						materials_with_no_generators.Count, typeof(G).FullName
					), MessageType.Error, true);
					using (new IndentLevelScope()) {
						foreach (var m in materials_with_no_generators) {
							EditorGUILayout.ObjectField(m, typeof(Material), false);
						}
					}
					EditorGUILayout.HelpBox(string.Format(
						"It's recommended to review shaders of these materials and not edit multiple materials at the same time.",
						materials_with_no_generators.Count, typeof(G).FullName
					), MessageType.None, true);
				}

				if (generators.Count == 1 && materials_with_no_generators.Count < 1) {
					// кода один генератор и нет материалов без генераторов - нормальные условия
					EditorGUILayout.ObjectField(gui_sh_gen, generators[0], typeof(Material), false);
				} else if (generators.Count > 0) {
					EditorGUILayout.LabelField(gui_sh_gens);
					using (new IndentLevelScope()) {
						foreach (var g in generators) {
							EditorGUILayout.ObjectField(g, typeof(G), false);
						}
					}
				}

				return materials_with_no_generators.Count < 1 && generators.Count > 0;
			} catch (Exception exc) { // TODO
				EditorGUILayout.LabelField("Shader Generator error : " + exc.Message);
				Debug.LogErrorFormat(this, "GenaratorGUIDFields error: {0}\n{1}", exc.Message, exc.StackTrace);
				Debug.LogException(exc, this);
			}
			return true;
		}

	}
}
#endif // UNITY_EDITOR

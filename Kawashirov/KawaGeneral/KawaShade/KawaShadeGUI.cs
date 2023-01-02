using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;

namespace Kawashirov.KawaShade {
	public partial class KawaShadeGUI : BakedShaderGUI<KawaShadeGenerator> {

		public override IEnumerable<string> GetShaderTagsOfIntrest() {
			var list = new List<string>();
			list.Add(Commons.GenaratorGUID);
			foreach (var feature in AbstractFeature.Features.Value)
				feature.PopulateShaderTags(list);
			return list;
		}

		public override void CustomBakedGUI() {
			foreach (var feature in AbstractFeature.Features.Value) {
				EditorGUILayout.Space();
				feature.ShaderEditorGUI(this);
			}
		}
	}
}


// Имя файла длжно совпадать с именем типа.
// https://forum.unity.com/threads/solved-blank-scriptableobject-on-import.511527/
// Тип не включен в неймспейс Kawashirov.KawaShade, т.к. эдитор указывается в файле .shader без указания неймспейса.

class KawaShadeGUI : Kawashirov.KawaShade.KawaShadeGUI { }

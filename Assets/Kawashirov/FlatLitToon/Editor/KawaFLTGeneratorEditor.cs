using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kawashirov.FLT  
{
	[CustomEditor(typeof(Generator))]
	public class GeneratorEditor : Editor {
		
		public override void OnInspectorGUI()
		{
			

			this.DrawDefaultInspector();


			if( GUILayout.Button("(Re)Generate Shader") ) {
				foreach(var t in this.targets){
					var generator = t as Generator;
					if (generator)
						generator.Generate();
				}
			}
		}
	}
}
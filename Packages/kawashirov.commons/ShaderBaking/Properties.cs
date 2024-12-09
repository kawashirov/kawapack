using System.Text;

namespace Kawashirov.ShaderBaking {

	public abstract class Property {
		public string name = null;
		public bool hidden = false; // TODO
		public abstract void Bake(StringBuilder sb);
	}

	public abstract class Property<T> : Property {
		public T defualt = default;

		static Property() {
			var a = typeof(PropertyFloat);
			var b = typeof(PropertyVector);
			var c = typeof(Property<object>);
			var x = a.IsSubclassOf(c);
			UnityEngine.Debug.LogWarning("  PropertyFloat IsSubclassOf Property<object> " + x);
		}
	}




}

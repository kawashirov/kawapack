using Kawashirov;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kawashirov {
	public class ShaderTag {
		// Тоже, что и MaterialProperty, но для тегов шейдеров при работе над материалом

		// Облегчает трекинг сыылок и навигацию по коду.
		public static readonly string DisableBatching = "DisableBatching";
		public static readonly string ForceNoShadowCasting = "ForceNoShadowCasting";
		public static readonly string IgnoreProjector = "IgnoreProjector";
		public static readonly string RenderType = "RenderType";

		public class TagMulitipleValuesException : ArgumentException {

			private static string _Message(IEnumerable<Material> materials, string tag, IEnumerable<string> values) {
				var mats_array = materials.OfType<Material>().Distinct().Select(m => m.ToString()).ToArray();
				var values_array = values.Distinct().ToArray();
				return string.Format(
					"Materials ({0}: {1}) have multiple values for tag \"{2}\": ({3}: {4}). Can not operate with this tag. Try oprate on a single material.",
					mats_array.Length, string.Join(", ", mats_array), tag, values_array.Length, string.Join(", ", values_array)
				);
			}

			public TagMulitipleValuesException(IEnumerable<Material> materials, string tag, IEnumerable<string> values)
				: base(_Message(materials, tag, values)) { }

		}

		private readonly Material[] materials;
		private readonly string tag;

		public ShaderTag(IEnumerable<Material> materials, string tag) {
			if (tag == null)
				throw new ArgumentNullException("No tag provided!");
			if (string.IsNullOrWhiteSpace(tag))
				throw new ArgumentException("No tag provided!");
			this.tag = tag.Trim();

			if (materials == null)
				throw new ArgumentNullException(string.Format("tag {0}: No materials provided!", tag));
			this.materials = materials.OfType<Material>().Distinct().ToArray();
			if (this.materials.Length < 1)
				throw new ArgumentException(string.Format("tag {0}: No materials provided!", tag));
		}

		public IEnumerable<string> GetMultipleValues() {
			return materials
				.Select(m => m.GetTag(tag, false))
				.Select(s => string.IsNullOrWhiteSpace(s) ? string.Empty : s) // пуыстые -> string.Empty
				.Distinct();
		}

		public string GetValue() {
			var values_array = materials.Select(m => m.GetTag(tag, false)).Distinct().ToArray();

			if (values_array.Length > 1)
				throw new TagMulitipleValuesException(materials, tag, values_array);

			return string.IsNullOrWhiteSpace(values_array[0]) ? null : values_array[0];
		}

		public HashSet<string> GetItems() {
			// Разбивает на части 
			var value = GetValue();
			var result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
			if (value != null)
				result.UnionWith(value.Split(',').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
			return result;
		}

		public bool IsSet() => GetValue() != null;

		public bool ValueEquals(string value) {
			// Установлен ли tag на материале равным value ?
			if (string.IsNullOrWhiteSpace(value))
				throw new ArgumentException(string.Format("tag {0}: Comparing with empty value: {1}", tag, value));
			var tag_v = GetValue();
			return tag_v != null && string.Equals(value, tag_v, StringComparison.InvariantCultureIgnoreCase);
		}

		public bool IsTrue() => ValueEquals("True");

		public bool ContainsItem(string item) {
			if (string.IsNullOrWhiteSpace(item))
				throw new ArgumentException(string.Format("tag {0}: Comparing with empty item: {1}", tag, item));
			var items = GetItems();
			return items.Contains(item);
		}

		public bool IsEnumValue<E>(E value) where E : Enum
			=> ValueEquals(Enum.GetName(typeof(E), value));

		public E GetEnumValueUnsafe<E>() where E : Enum {
			var tag_v = GetValue();
			if (string.IsNullOrWhiteSpace(tag_v)) // todo detailed exc
				throw new ArgumentException(string.Format("tag {0}: Enum value is not set!", tag));
			return (E)Enum.Parse(typeof(E), tag_v, true);
		}

		public E GetEnumValueSafe<E>(E defualt) where E : Enum {
			try {
				return GetEnumValueUnsafe<E>();
			} catch (ArgumentException) {
				return defualt;
			}
		}

		public bool GetEnumValueSafe<E>(ref E value) where E : Enum {
			try {
				value = GetEnumValueUnsafe<E>();
				return true;
			} catch (ArgumentException) {
			}
			return false;
		}
		

	}
}

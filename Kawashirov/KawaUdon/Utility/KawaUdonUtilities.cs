using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Udon;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using UdonSharp;
using Kawashirov.Refreshables;

#if UNITY_EDITOR
using UnityEditor;
using UdonSharpEditor;
#endif

namespace Kawashirov.Udon {
	public static partial class KawaUdonUtilities {
#if UNITY_EDITOR

		public static void ApplyProxyModificationsAndSetDirty(this UdonSharpBehaviour proxy) {
			// proxy.ApplyProxyModifications();
			EditorUtility.SetDirty(proxy);
			// Почему-то юнити не видит изменений, внесенных в UdonBehaviour, по этому метим его.
			// EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy));
			Debug.Log($"Modified <b>{proxy.name}</b> @ <i>{proxy.gameObject.KawaGetFullPath()}</i>", proxy);
		}

		public static void EnsureIsValid(System.Object obj, string paramName = null) {
			if (!Utilities.IsValid(obj))
				throw new ArgumentNullException(paramName, "Object is not valid!");
		}

		private static string ExtraLogDescResolver(Func<string> extraLogDesc) {
			var extra_desc = extraLogDesc?.Invoke();
			if (string.IsNullOrWhiteSpace(extra_desc))
				extra_desc = "(no extra log description)";
			return extra_desc;
		}

		private static string IndexedExtraLogDescResolver(int index, Func<string> extraLogDesc) {
			return $"#{index}: {ExtraLogDescResolver(extraLogDesc)}";
		}

		public static void ValidateSafeForEach<T>(T[] array, Action<T> validation, Component source, string propName, Func<string> extraLogDesc = null) {
			for (var i = 0; i < array.Length; ++i)
				ValidateSafe(() => validation.Invoke(array[i]), source, propName, () => IndexedExtraLogDescResolver(i, extraLogDesc));
		}

		public static bool ValidateSafeForEach<T>(T[] array, Func<T, bool> validation, Component source, string propName, Func<string> extraLogDesc = null) {
			var modified = false;
			for (var i = 0; i < array.Length; ++i)
				modified |= ValidateSafe(() => validation.Invoke(array[i]), source, propName, () => IndexedExtraLogDescResolver(i, extraLogDesc));
			return modified;
		}

		public static void ValidateSafe(Action validation, Component source, string propName, Func<string> extraLogDesc = null) {
			ValidateSafe(() => { validation.Invoke(); return false; }, source, propName, extraLogDesc);
		}

		public static bool ValidateSafe(Func<bool> validation, Component source, string propName, Func<string> extraLogDesc = null) {
			var usharp = source as UdonSharpBehaviour;
			var modified = false;
			try {
				modified = validation.Invoke(); // return is modified
			} catch (Exception exc) {
				var source_msg = $"{source.GetType()}.{propName} @ <i>{source.gameObject.KawaGetFullPath()}</i>";
				Debug.LogError($"Error validating {source_msg}\n{ExtraLogDescResolver(extraLogDesc)}\n{exc.GetType()}: {exc.Message}\n{exc.StackTrace}", source);
				Debug.LogException(exc, source);
			}
			if (modified)
				usharp?.ApplyProxyModificationsAndSetDirty();
			return modified;
		}

		public static bool EnsureAppendedAsUdonBehaviour(UdonSharpBehaviour proxy, string name, ref Component[] array, UdonSharpBehaviour item) {
			var items = UdonSharpEditorUtility.GetBackingUdonBehaviour(item).ToEnumerable();
			return EnsureAppended(proxy, name, ref array, items, KawaUtilities.UnityEquality);
		}

		public static bool EnsureAppended<T>(UdonSharpBehaviour proxy, string name, ref T[] array, T item) where T : UnityEngine.Object {
			// Убеждается, что массив array который proxy.name содержит в себе item.
			// Если item нету в array, добавляет его помечая изменения.
			return EnsureAppended(proxy, name, ref array, item.ToEnumerable(), KawaUtilities.UnityEquality);
		}

		public static bool EnsureAppended<T>(UdonSharpBehaviour proxy, string name, ref T[] array, T item, IEqualityComparer<T> cmp) {
			// Убеждается, что массив array который proxy.name содержит в себе item.
			// Если item нету в array, добавляет его помечая изменения.
			return EnsureAppended(proxy, name, ref array, item.ToEnumerable(), cmp);
		}

		public static bool EnsureAppended<T>(UdonSharpBehaviour proxy, string name, ref T[] array, IEnumerable<T> items) where T : UnityEngine.Object {
			return EnsureAppended(proxy, name, ref array, items, KawaUtilities.UnityEquality);
		}

		public static bool EnsureAppended<T>(UdonSharpBehaviour proxy, string name, ref T[] array, IEnumerable<T> items, IEqualityComparer<T> cmp) {
			// Убеждается, что массив array который proxy.name содержит в себе все объекты из items.
			// Если каких-то объектов нету в array, добавляет их помечая изменения.
			// Порядок не гарантируется.

			if (!Utilities.IsValid(proxy))
				throw new ArgumentException("U# proxy behaviour is not valid!", nameof(proxy));

			if (!UdonSharpEditorUtility.IsProxyBehaviour(proxy))
				throw new ArgumentException($"U# behaviour {proxy} ({proxy.gameObject.KawaGetFullPath()}) is not a proxy!", nameof(proxy));

			if (!Utilities.IsValid(array)) {
				var message = $"Array from U# proxy behaviour {proxy} ({proxy.gameObject.KawaGetFullPath()}) is not valid!";
				throw new ArgumentNullException(name, message);
			}

			// Берем все объекты, которые нужно добавить, кроме тех, что уже добавлены.
			var items_to_add = items.Where(x => x != null).Distinct().Except(array).ToList();
			if (items_to_add.Count < 1)
				// Если их нету, то похую.
				return false;

			var new_array = array.Concat(items_to_add).ToArray();
			if (!ModifyArray(proxy, name, ref array, new_array, cmp))
				// По идее такого не должно быть, но ладно.
				return false;

			return true;
		}

		public static IEnumerable<UdonBehaviour> ConvertToUdonBehaviour(Component component) {
			// Убеждается, что последовательность компонентов состоит только из UdonBehaviour.
			// Если есть Proxy UdonSharpBehaviour, то заменяется на его UdonBehaviour
			// Если есть иной Component, то заменяется на все UdonBehaviour из его объекта
			if (!Utilities.IsValid(component)) {
				yield break;
			} else if (component is UdonBehaviour udon) {
				yield return udon;
			} else if (component is UdonSharpBehaviour usharp && UdonSharpEditorUtility.IsProxyBehaviour(usharp)) {
				yield return UdonSharpEditorUtility.GetBackingUdonBehaviour(usharp);
			} else {
				foreach (var subudon in component.gameObject.GetComponents<UdonBehaviour>())
					yield return subudon;
			}
			yield break;
		}

		public static bool ValidateComponentsArrayOfUdonSharpBehaviours(UdonSharpBehaviour usb, string name, ref Component[] array) {
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			var new_array = array.SelectMany(ConvertToUdonBehaviour).Distinct().ToArray();
			return ModifyArray(usb, name, ref array, new_array, KawaUtilities.UnityEquality);
		}

		public static bool DistinctArray<T>(UdonSharpBehaviour usb, string name, ref T[] array, IEqualityComparer<T> cmp) {
			var new_array = array.Where(x => x != null).Distinct().ToArray();
			return ModifyArray(usb, name, ref array, new_array, cmp);
		}

		public static bool DistinctArray<T>(UdonSharpBehaviour usb, string name, ref T[] array) where T : UnityEngine.Object {
			var new_array = array.Where(Utilities.IsValid).Distinct().ToArray();
			return ModifyArray(usb, name, ref array, new_array, KawaUtilities.UnityEquality);
		}

		public static bool ModifyArray<T>(ref T[] target_array, T[] new_array, IEqualityComparer<T> cmp) {
			// Модифицирует target_array, значениями из new_array, если они различаются согласно cmp.
			// Если массивы одинаковой длины, то меняет различающиеся элементы.
			// Если разные, то в target_array сохраняется копия new_array.
			// Возвращает true, если target_array был изменен.
			// Смысл данного метода в том, что бы увидеть реальные изменения в массиве,
			// что бы потом при необходимости вызвать EditorUtility.SetDirty(...);

			if (target_array == null)
				throw new ArgumentNullException(nameof(target_array));
			if (new_array == null)
				throw new ArgumentNullException(nameof(new_array));
			if (cmp == null)
				throw new ArgumentNullException(nameof(cmp));

			if (target_array.Length != new_array.Length) {
				target_array = (T[])new_array.Clone();
				return true;
			}

			var modified = false;
			for (var i = 0; i < target_array.Length; ++i) {
				if (!cmp.Equals(target_array[i], new_array[i])) {
					modified = true;
					target_array[i] = new_array[i];
				}
			}

			return modified;
		}

		public static bool ModifyArray<T>(UdonSharpBehaviour usb, string name, ref T[] array, T[] new_array) where T : UnityEngine.Object {
			return ModifyArray(usb, name, ref array, new_array, KawaUtilities.UnityEquality);
		}

		public static bool ModifyArray<T>(UdonSharpBehaviour usb, string name, ref T[] array, T[] new_array, IEqualityComparer<T> cmp) {
			if (!Utilities.IsValid(usb))
				throw new ArgumentNullException(nameof(usb));
			if (!Utilities.IsValid(name))
				throw new ArgumentNullException(nameof(name));

			if (!ModifyArray(ref array, new_array, cmp))
				return false;

			if (Utilities.IsValid(usb)) {
				Debug.Log($"Modified array <b>{name}</b> @ <i>{usb.gameObject.KawaGetFullPath()}</i>", usb);
				EditorUtility.SetDirty(usb);
			}
			return true;
		}

		public static bool HasEntryPoint(this UdonBehaviour udon, string event_name)
				=> udon != null && udon.programSource != null && udon.programSource.HasEntryPoint(event_name);

		public static bool HasEntryPoint(this AbstractUdonProgramSource source, string event_name) {
			if (source == null)
				return false;
			var spa = source.SerializedProgramAsset; // get
			return spa != null && spa.HasEntryPoint(event_name);
		}

		public static bool HasEntryPoint(this AbstractSerializedUdonProgramAsset asset, string event_name) {
			if (asset == null)
				return false;
			var program = asset.RetrieveProgram();
			return program != null && program.HasEntryPoint(event_name);
		}

		public static bool HasEntryPoint(this IUdonProgram program, string event_name) {
			if (program == null)
				return false;
			var ep = program.EntryPoints; // get
			return ep != null && ep.HasEntryPoint(event_name);
		}

		public static bool HasEntryPoint(this IUdonSymbolTable table, string event_name)
				=> table != null && table.GetExportedSymbols().Any(s => event_name.Equals(s));

		public static readonly string[] LAGGY_SYMBOLS = new string[] {
			"_update", "Update", "_lateUpdate", "LateUpdate", "_fixedUpdate", "FixedUpdate", "_postLateUpdate", "PostLateUpdate",
			"_onAnimatorMove", "OnAnimatorMove",
			"_onPlayerCollisionStay", "_onCollisionStay", "OnCollisionStay",
			"_onRenderObject", "OnRenderObject",
			"_onPlayerTriggerStay", "_onTriggerStay", "OnTriggerStay"
		};

		[MenuItem("Kawashirov/Udon/Report (to console) program instances with potentially laggy entry points")]
		public static void ReportUdonScriptsWithUpdates() {
			var laggy_symbols = new List<string>(LAGGY_SYMBOLS.Length);
			var udons = KawaUtilities.IterScenesRoots()
					.SelectMany(g => g.GetComponentsInChildren<UdonBehaviour>(true))
					.RuntimeOnly();
			foreach (var udon in udons) {
				laggy_symbols.Clear();
				laggy_symbols.AddRange(LAGGY_SYMBOLS.Where(s => udon.HasEntryPoint(s)));
				if (laggy_symbols.Count > 0) {
					Debug.LogWarningFormat(udon,
						"UdonBehaviour (activeInHierarchy=<b>{0}</b>) @ <b>{1}</b> have <b>{2}</b> potentially laggy entry points: <b>{3}</b>",
						udon.gameObject.activeInHierarchy, udon.transform.KawaGetHierarchyPath(), laggy_symbols.Count, string.Join(", ", laggy_symbols)
					);
				}
			}
		}

		[MenuItem("Kawashirov/Udon/Report (to console) program instances")]
		public static void ReportUdonScriptInstances() {
			var instances = 0;
			var udons = KawaUtilities.IterScenesRoots()
					.SelectMany(g => g.GetComponentsInChildren<UdonBehaviour>(true))
					.GroupBy(u => u.programSource)
					.Select(grp => new { grp.Key, Value = grp.ToList() })
					.OrderByDescending(x => x.Value.Count);
			var laggy_symbols = new List<string>(LAGGY_SYMBOLS.Length);
			foreach (var pair in udons) {
				if (pair.Value.Count < 1)
					continue;
				var warn = false;
				var builder = new StringBuilder();
				var source = pair.Key == null ? "<i>null</i>" : string.Format("<b>{0}</b> (<i>{1}</i>)", pair.Key.name, pair.Key.GetType().Name);
				builder.AppendFormat("Udon program source {0} have <b>{1}</b> instances:", source, pair.Value.Count);
				var paths = pair.Value
						.GroupBy(u => u.transform.KawaGetHierarchyPath())
						.Select(grp => new { grp.Key, Value = grp.ToList() })
						.OrderByDescending(x => x.Value.Count);
				foreach (var path in paths) {
					var full_path = path.Key;
					builder.AppendFormat("\n - <i>{0}</i> x <b>{1}</b>", full_path, path.Value.Count);
					instances += path.Value.Count;
				}

				laggy_symbols.Clear();
				laggy_symbols.AddRange(LAGGY_SYMBOLS.Where(s => pair.Key.HasEntryPoint(s)));
				if (laggy_symbols.Count > 0) {
					builder.AppendFormat(
							"\nNote: this program have <b>{0}</b> potentially laggy entry points: <b>{1}</b>",
							laggy_symbols.Count, string.Join(", ", laggy_symbols)
					);
					warn = true;
				}

				var msg = builder.Append("\n").ToString();
				if (warn)
					Debug.LogWarning(msg, pair.Key);
				else
					Debug.Log(msg, pair.Key);
			}
			Debug.LogFormat("Total <b>{0}</b> Udon program instances in loaded scenes.", instances);
		}

		private static void RefreshUdonBehavioursOnScene(Scene scene, UdonSharpBehaviour ushrp) {
			var program = UdonSharpEditorUtility.GetBackingUdonBehaviour(ushrp).programSource;
			scene.GetRootGameObjects()
				.SelectMany(g => g.GetComponentsInChildren<UdonBehaviour>(true))
				.Where(UdonSharpEditorUtility.IsUdonSharpBehaviour)
				.Where(u => u.programSource == program)
				.Select(UdonSharpEditorUtility.GetProxyBehaviour)
				.OfType<IRefreshable>().RefreshMultiple();
		}

		private static void RefreshUdonBehavioursOnScene(Scene scene) {
			scene.GetRootGameObjects()
				.SelectMany(g => g.GetComponentsInChildren<UdonBehaviour>(true))
				.Where(UdonSharpEditorUtility.IsUdonSharpBehaviour)
				.Select(UdonSharpEditorUtility.GetProxyBehaviour)
				.OfType<IRefreshable>().RefreshMultiple();
		}

		public static void EditorRefreshableGUI(this Editor editor) {
			// USharp-specific GUI for Refreshables

			var ushrp = editor.target as UdonSharpBehaviour;
			var refreshable = editor.target as IRefreshable;
			if (ushrp == null || refreshable == null)
				return;

			GUILayout.Label("Refresh:");
			using (new GUILayout.HorizontalScope()) {
				if (GUILayout.Button("This")) {
					refreshable.RefreshSafe();
				}

				var type = editor.target.GetType();
				if (GUILayout.Button($"Every {type} on scene")) {
					RefreshUdonBehavioursOnScene(ushrp.gameObject.scene, ushrp);
				}

				if (GUILayout.Button("Every UdonBehaviour on scene")) {
					// Ищем все UdonBehaviour на сценах, конвертируем их в Proxy, выбираем те что IRefreshable
					RefreshUdonBehavioursOnScene(ushrp.gameObject.scene);
				}
			}
		}

#endif
	}
}

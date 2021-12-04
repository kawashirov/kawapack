using Kawashirov;
using Kawashirov.Refreshables;
using System.Linq;
using UdonSharp;
using UnityEngine;
using VRC.Udon;
using System;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UdonSharpEditor;
#endif

namespace Kawashirov.Udon {
	public static partial class KawaUdonUtilities {
#if UNITY_EDITOR

		public static bool IsUdonic(object obj) => obj is UdonBehaviour || obj is UdonSharpBehaviour;
		public static bool IsNotUdonic(object obj) => !IsUdonic(obj);

		public static void ApplyProxyModificationsAndSetDirty(this UdonSharpBehaviour usharp) {
			usharp.ApplyProxyModifications();
			// Почему-то юнити не видит изменений, внесенных в UdonBehaviour, по этому метим его.
			EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(usharp));
			Debug.LogFormat(usharp, "Modified {0} @ {1}", usharp, usharp.gameObject.KawaGetFullPath());
		}

		public static void EnsureIsValid(System.Object obj, string paramName = null) {
			if (!Utilities.IsValid(obj))
				throw new ArgumentNullException(paramName, "Object is not valid!");
		}

		private static string ExtraLogDescResolver(Func<string> extraLogDesc) {
			var extra_desc = extraLogDesc?.Invoke();
			if (string.IsNullOrWhiteSpace(extra_desc))
				extra_desc = "(no extra log desxription)";
			return extra_desc;
		}

		private static string IndexedExtraLogDescResolver(int index, Func<string> extraLogDesc) {
			return string.Format("#{0}: {1}", index, ExtraLogDescResolver(extraLogDesc));
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
			usharp?.UpdateProxy();
			var modified = false;
			try {
				modified = validation.Invoke(); // return is modified
			} catch (Exception exc) {
				var source_msg = string.Format("{0}.{1} @ {2}", source.GetType(), propName, source.gameObject.KawaGetFullPath());
				Debug.LogErrorFormat(source, "Error validating {0}\n{1}\n{2}: {3}\n{4}", source_msg, ExtraLogDescResolver(extraLogDesc), exc.GetType(), exc.Message, exc.StackTrace);
				Debug.LogException(exc, source);
			}
			if (modified)
				usharp?.ApplyProxyModificationsAndSetDirty();
			return modified;
		}

		private static void EnsureValidUdonSharpProxy(UdonSharpBehaviour proxy, string paramName = null) {
			if (!Utilities.IsValid(proxy))
				throw new ArgumentException("U# proxy behaviour is not valid!", paramName);
			if (!UdonSharpEditorUtility.IsProxyBehaviour(proxy))
				throw new ArgumentException(string.Format("U# behaviour {0} ({1}) is not a proxy!", proxy, proxy.gameObject.KawaGetFullPath()), paramName);
		}

		public static bool EnsureAppendedAsUdonBehaviour(UdonSharpBehaviour proxy, ref Component[] array, UdonSharpBehaviour item, string paramName = null) {
			return EnsureAppended(proxy, ref array, UdonSharpEditorUtility.GetBackingUdonBehaviour(item).ToEnumerable(), paramName);
		}

		public static bool EnsureAppended<T>(UdonSharpBehaviour proxy, ref T[] array, T item, string paramName = null) where T : UnityEngine.Object {
			return EnsureAppended(proxy, ref array, item.ToEnumerable(), paramName);
		}

		public static bool EnsureAppended<T>(UdonSharpBehaviour proxy, ref T[] array, IEnumerable<T> items, string paramName = null) where T : UnityEngine.Object {
			EnsureValidUdonSharpProxy(proxy, nameof(proxy));
			if (!Utilities.IsValid(array)) {
				var message = string.Format("Array from U# proxy behaviour {0} ({1}) is not valid!", proxy, proxy.gameObject.KawaGetFullPath());
				if (string.IsNullOrWhiteSpace(paramName))
					paramName = nameof(array);
				throw new ArgumentNullException(paramName, message);
			}

			proxy.UpdateProxy();

			var new_array = array.Concat(items).Distinct().ToArray();
			if (!ModifyArray(ref array, new_array, UnityInequality))
				return false;

			proxy.ApplyProxyModificationsAndSetDirty();
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

		public static bool ValidateComponentsArrayOfUdonSharpBehaviours(ref Component[] array, string paramName = null) {
			if (!Utilities.IsValid(array))
				throw new ArgumentNullException(string.IsNullOrWhiteSpace(paramName) ? nameof(array) : paramName, "Array of Components is not valid!");
			return ModifyArray(ref array, array.SelectMany(ConvertToUdonBehaviour).Distinct().ToArray(), UnityInequality);
		}

		public static bool DistinctArray<T>(ref T[] array, string paramName = null) where T : UnityEngine.Object {
			if (!Utilities.IsValid(array))
				throw new ArgumentNullException(string.IsNullOrWhiteSpace(paramName) ? nameof(array) : paramName, "Array is not valid!");

			var resized = array.Where(x => x != null).Distinct().ToArray();

			if (array.Length == resized.Length)
				return false;

			array = resized;
			return true;
		}

		public static bool UnityInequality(UnityEngine.Object a, UnityEngine.Object b) => a != b;

		public static bool ModifyArray<T>(ref T[] array, T[] new_array, Func<T, T, bool> noteq) {
			if (array.Length != new_array.Length) {
				array = new_array;
				return true;
			}
			var modified = false;
			for (var i = 0; i < array.Length; ++i) {
				if (noteq.Invoke(array[i], new_array[i])) {
					modified = true;
					array[i] = new_array[i];
				}
			}
			return modified;
		}

		private static readonly object[] DUMMY_OBJECT_ARRAY = new object[0];

		private static readonly MethodInfo UdonBehaviour_SerializePublicVariables = typeof(UdonBehaviour)
			.GetMethod("SerializePublicVariables", BindingFlags.Instance | BindingFlags.NonPublic);

		public static void KawaForceSerialize(this UdonBehaviour udon) {
			// Уебки из врчата не сделали что бы при изменении publicVariables он сериализовался.
			if (udon != null) {
				UdonBehaviour_SerializePublicVariables.Invoke(udon, DUMMY_OBJECT_ARRAY);
				EditorUtility.SetDirty(udon);
			}
		}

		public static void SendCustomEvent(this IEnumerable<UdonBehaviour> udons, string event_name, UnityEngine.Object context = null) {
			// LINQ-like SendCustomEvent
			Debug.LogFormat(context, "[KawaEditor] Sending event <b>{0}</b> to multiple udons...", event_name);
			var count = 0;
			foreach (var u in udons) {
				// TODO: try/catch
				var local_context = context != null ? context : u;
				Debug.LogFormat(context, "[KawaEditor] Sending event <b>{0}</b> to <b>{1}</b> (<b>{2}</b>)", event_name, u, u.programSource);
				u.SendCustomEvent(event_name);
				++count;
			}
			Debug.LogFormat(context, "[KawaEditor] Sent event <b>{0}</b> to <b>{1}</b> Udons.", event_name, count);
		}

		public static bool SetPubVarValue<T>(this UdonBehaviour udon, string symbol, T value, bool serialize = true) {
			var path = udon.transform.KawaGetHierarchyPath();
			var value_type = value == null ? "null" : value.GetType().FullName + " : " + value.ToString();

			bool result;
			try {
				if (value != null && value.GetType().IsArray) {
					var array = value as Array;
					value_type += string.Format(" (Length = <b>{0}</b>)", array.Length);
				}

				result = udon.publicVariables.TrySetVariableValue(symbol, value);

				if (serialize && result) {
					udon.KawaForceSerialize();
				}

				if (result) {
					Debug.LogFormat(udon, "[KawaEditor] Sucessfully set Udon variable <b>{1}</b> = <b>{2}</b> @ <i>{0}</i>", path, symbol, value_type);
				} else {
					Debug.LogErrorFormat(udon, "[KawaEditor] Failed to set Udon variable<b>{1}</b> = <b>{2}</b> @ <i>{0}</i>", path, symbol, value_type);
				}
			} catch (Exception exc) {
				Debug.LogErrorFormat(udon, "[KawaEditor] Failed to set Udon variable <b>{1}</b> = <b>{2}</b>: <i>{3}</i> @ <i>{0}</i>\n{4}", path, symbol, value_type, exc.Message, exc.StackTrace);
				Debug.LogException(exc, udon);
				result = false;
			}
			return result;
		}

		public static T GetPubVarValue<T>(this UdonBehaviour udon, string symbol, T def) {
			return udon.publicVariables.TryGetVariableValue(symbol, out T value) ? value : def;
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

		private static readonly string[] LAGGY_SYMBOLS = new string[] {
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

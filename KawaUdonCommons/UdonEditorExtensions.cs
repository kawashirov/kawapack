using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using System.Text;
using System.Reflection;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Kawashirov.Udon {
	public static class UdonEditorExtensions {
#if UNITY_EDITOR
		private static readonly object[] DUMMY_OBJECT_ARRAY = new object[0];
		private static readonly string[] LAGGY_SYMBOLS = new string[] { "_update", "Update", "_lateUpdate", "LateUpdate", "_fixedUpdate", "FixedUpdate" };
		private static readonly MethodInfo UdonBehaviour_SerializePublicVariables;

		static UdonEditorExtensions() {
			UdonBehaviour_SerializePublicVariables = typeof(UdonBehaviour)
					.GetMethod("SerializePublicVariables", BindingFlags.Instance | BindingFlags.NonPublic);
		}

		[MenuItem("Kawashirov/Udon/Report (to console) program instances with potentially laggy entry points")]
		public static void ReportUdonScriptsWithUpdates() {
			var laggy_symbols = new List<string>(LAGGY_SYMBOLS.Length);
			var udons = StaticCommons.IterScenesRoots()
					.SelectMany(g => g.GetComponentsInChildren<UdonBehaviour>(true))
					.Where(u => !u.transform.IsEditorOnly());
			foreach (var udon in udons) {
				laggy_symbols.Clear();
				laggy_symbols.AddRange(LAGGY_SYMBOLS.Where(s => udon.HasEntryPoint(s)));
				if (laggy_symbols.Count < 1)
					continue;
				var builder = new StringBuilder();
				var path = udon.transform.KawaGetHierarchyPath();
				builder.AppendFormat(
						"UdonBehaviour (activeInHierarchy=<b>{0}</b>) @ <b>{1}</b> have <b>{2}</b> potentially laggy entry points: <b>{3}</b>",
						udon.gameObject.activeInHierarchy, path, laggy_symbols.Count, string.Join(", ", laggy_symbols)
				);
				Debug.LogWarning(builder.ToString(), udon);
			}
		}

		[MenuItem("Kawashirov/Udon/Report (to console) program instances")]
		public static void ReportUdonScriptInstances() {
			var instances = 0;
			var udons = StaticCommons.IterScenesRoots()
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

		public static bool AppendItemToPubVarArray<T>(this UdonBehaviour udon, string symbol, T item, bool distinct = true, bool serialize = true) {
			var path = udon.transform.KawaGetHierarchyPath();
			bool result;
			try {
				Debug.LogFormat(udon, "[KawaEditor] Adding item <b>{1}</b> to Udon public variable array <b>{2}</b>... @ <i>{0}</i>", path, item, symbol);
				var receivers = GetPubVarValue<T[]>(udon, symbol, null).Append(item);
				if (distinct)
					receivers = receivers.Distinct();
				result = SetPubVarValue(udon, symbol, receivers.ToArray(), serialize);
			} catch (Exception exc) {
				Debug.LogErrorFormat(udon, "[KawaEditor] Error adding item <b>{1}</b> to Udon public variable array <b>{2}</b>: <i>{2}</i> @ <i>{0}</i>\n{3}", path, item, symbol, exc.Message, exc.StackTrace);
				Debug.LogException(exc, udon);
				result = false;
			}
			return result;
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

#endif
	}
}

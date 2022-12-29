using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Kawashirov;
using Kawashirov.ShaderBaking;

namespace Kawashirov.KawaShade {

	public abstract class AbstractFeature {

		public static readonly Lazy<List<AbstractFeature>> Features = new Lazy<List<AbstractFeature>>(LoadFeatures);

		private static List<AbstractFeature> LoadFeatures() {
			var type = typeof(AbstractFeature);
			return AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(s => s.GetTypesSafe())
				.Where(p => type.IsAssignableFrom(p) && !p.IsAbstract)
				.Select(p => p.GetConstructor(Type.EmptyTypes))
				.Where(c => c != null)
				.Select(c => c.Invoke(new object[0]))
				.Cast<AbstractFeature>()
				.OrderBy(f => f, Comparer)
				.ToList();
		}

		public enum Order {
			GENERAL = 1000,
			SHADING = 2000,
			VF = 3000,
			VGF = 4000,
			VVHDGF = 5000,
		}

		public class ComparerClass : IComparer<AbstractFeature> {
			public int Compare(AbstractFeature x, AbstractFeature y) {
				return x == y ? string.Compare(x.GetType().FullName, y.GetType().FullName) : x.GetOrder() < y.GetOrder() ? -1 : 1;
			}
		}

		public static ComparerClass Comparer = new ComparerClass();

		public void IncludeFeatureDirect(ShaderSetup shader, string filename) {
			var path = KawaShadeGenerator.GetCGIncPath(filename);
			shader.Include(ShaderInclude.Direct((int)KawaShadeGenerator.IncludeOrders.FEATURES, path));
		}

		public virtual int GetOrder() => (int)Order.VF;

		public virtual void PopulateShaderTags(List<string> tags) { }

		public virtual void ConfigureShaderEarly(KawaShadeGenerator generator, ShaderSetup shader) { }

		public virtual void ConfigureShader(KawaShadeGenerator generator, ShaderSetup shader) { }

		public virtual void ConfigureShaderLate(KawaShadeGenerator generator, ShaderSetup shader) { }

		public virtual void GeneratorEditorGUI(KawaShadeGeneratorEditor editor) { }

		public virtual void ShaderEditorGUI(KawaShadeGUI editor) { }

	}

}

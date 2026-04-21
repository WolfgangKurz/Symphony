using System;
using System.Linq;
using System.Reflection;

namespace Symphony.Utils {
	public static class SymphonyUtils {
		private static bool initialized = false;

		public static void Initialize(Func<Assembly, string, Assembly> loader) {
			if (initialized) return;
			initialized = true;

			if (loader == null) throw new ArgumentNullException(nameof(loader));

			var asm = typeof(SymphonyUtils).Assembly;
			var res = asm.GetManifestResourceNames()
				.Where(x => x.StartsWith("Symphony.Utils.Dependencies/", StringComparison.Ordinal));
			foreach (var name in res) loader(asm, name);
		}
	}
}

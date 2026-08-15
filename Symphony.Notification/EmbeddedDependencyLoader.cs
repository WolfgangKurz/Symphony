using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Symphony.Notification {
	internal static class EmbeddedDependencyLoader {
		private const string ResourcePrefix = "Symphony.Notification.Dependencies/";
		private static int initialized;

		internal static void Initialize() {
			if (Interlocked.Exchange(ref initialized, 1) != 0)
				return;

			AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
		}

		private static Assembly ResolveAssembly(object sender, ResolveEventArgs args) {
			var requestedName = new AssemblyName(args.Name).Name;
			if (requestedName != "Microsoft.Toolkit.Uwp.Notifications" &&
				requestedName != "System.ValueTuple")
				return null;

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				if (string.Equals(assembly.GetName().Name, requestedName, StringComparison.Ordinal))
					return assembly;
			}

			var resourceName = ResourcePrefix + requestedName + ".dll";
			using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)) {
				if (stream == null)
					return null;

				using (var buffer = new MemoryStream()) {
					stream.CopyTo(buffer);
					return Assembly.Load(buffer.ToArray());
				}
			}
		}
	}
}

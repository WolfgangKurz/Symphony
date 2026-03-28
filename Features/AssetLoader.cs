using AssetBundles;

using System;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using UnityEngine;

namespace Symphony.Features {
	internal class AssetLoader {
		private const string DependencyResourcePrefix = "Symphony.Dependencies/";

		public static int FilesFound = 0;
		public static int FilesLoaded = 0;
		public static int FilesError = 0;

		private static bool Loaded = false;
		private static bool Inited = false;

		public static void Init() {
			if (Inited) {
				Plugin.Logger.LogWarning("[Symphony::AssetLoader] AssetLoader's Inited method already called. Current call ignored");
				return;
			}

			Inited = true;

			AppDomain.CurrentDomain.AssemblyResolve += AssetLoader_AssemblyResolve;

			var asm = Assembly.GetExecutingAssembly();
			foreach (var resourceName in asm.GetManifestResourceNames().Where(x => x.StartsWith(DependencyResourcePrefix, StringComparison.Ordinal))) {
				try {
					LoadAssemblyFromResource(asm, resourceName);
				} catch (Exception e) {
					Plugin.Logger.LogError($"[Symphony::AssetLoader] Failed to load embedded dependency '{resourceName}': {e}");
				}
			}
		}

		private static Assembly AssetLoader_AssemblyResolve(object sender, ResolveEventArgs args) {
			var requestedName = new AssemblyName(args.Name).Name;
			var asm = Assembly.GetExecutingAssembly();
			var resourceName = asm.GetManifestResourceNames()
				.FirstOrDefault(x =>
					x.StartsWith(DependencyResourcePrefix, StringComparison.Ordinal) &&
					string.Equals(
						Path.GetFileNameWithoutExtension(x.Replace('/', Path.DirectorySeparatorChar)),
						requestedName,
						StringComparison.OrdinalIgnoreCase
					)
				);

			return resourceName == null ? null : LoadAssemblyFromResource(asm, resourceName);
		}
		private static Assembly LoadAssemblyFromResource(Assembly owner, string resourceName) {
			using var stream = owner.GetManifestResourceStream(resourceName);
			if (stream == null)
				throw new FileNotFoundException($"Embedded dependency resource '{resourceName}' not found");

			using var memory = new MemoryStream();
			stream.CopyTo(memory);
			var buffer = memory.ToArray();

			var loadedName = Path.GetFileNameWithoutExtension(resourceName.Replace('/', Path.DirectorySeparatorChar));
			var alreadyLoaded = AppDomain.CurrentDomain.GetAssemblies()
				.FirstOrDefault(x => string.Equals(x.GetName().Name, loadedName, StringComparison.OrdinalIgnoreCase));
			if (alreadyLoaded != null) return alreadyLoaded;

			var assembly = Assembly.Load(buffer);
			Plugin.Logger.LogMessage($"[Symphony::AssetLoader] Loaded embedded dependency '{resourceName}'");
			return assembly;
		}

		public static void Load() {
			if (Loaded) {
				Plugin.Logger.LogWarning("[Symphony::AssetLoader] AssetLoader's Load method already called. Current call ignored");
				return;
			}

			Loaded = true;

			var dir = Path.Combine(Plugin.GameDir, "AssetLoader");
			if (!Directory.Exists(dir)) {
				Plugin.Logger.LogWarning("[Symphony::AssetLoader] AssetLoader directory not exists, skip loading");
				return;
			}

			var _pathRegex = new Regex(@"[\\/]", RegexOptions.Compiled);
			var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
				.Where(x => Path.GetFileName(x) != "__info")
				.ToArray();

			FilesFound = files.Length;
			Plugin.Logger.LogInfo($"[Symphony::AssetLoader] Found {files.Length} files to load in AssetLoader directory");

			var loaded = 0;
			var errors = 0;
			foreach (var file in files) {
				var fname = Path.GetFileName(file);

				if (fname == "__data")
					fname = _pathRegex.Replace(Path.GetDirectoryName(Path.GetRelativePath(dir, file)), "__");

				byte[] memory = null;
				try {
					Plugin.Logger.LogMessage($"[Symphony::AssetLoader] Trying to load '{fname}'");

					memory = File.ReadAllBytes(file);
					var bundle = AssetBundle.LoadFromMemory(memory);
					if (bundle == null) {
						if (AssetLoaderPatch.AssetLoader_BundlePatch.TryPatchToWindows(memory, out var patchedMemory, out var patchError)) {
							bundle = AssetBundle.LoadFromMemory(patchedMemory);
							if (bundle == null)
								throw new PlatformNotSupportedException($"Platform-independent patch succeeded but failed to load bundle '{fname}'");

							AssetBundleManager.LoadedAssetBundles.Add(fname, new LoadedAssetBundle(bundle));
							loaded++;
							Plugin.Logger.LogMessage($"[Symphony::AssetLoader] Loaded '{fname}' with platform-independent patch");
							continue;
						}

						Plugin.Logger.LogWarning($"[Symphony::AssetLoader] Platform-independent load failed for '{fname}': {patchError}");
						continue;
					}

					AssetBundleManager.LoadedAssetBundles.Add(fname, new LoadedAssetBundle(bundle));
					loaded++;
				} catch (PlatformNotSupportedException e) {
					Plugin.Logger.LogError($"[Symphony::AssetLoader] {e.Message}");
					errors++;

				} catch (Exception e) {
					Plugin.Logger.LogError(e);
					errors++;
				}
			}
			FilesLoaded = loaded;
			FilesError = errors;
			Plugin.Logger.LogInfo($"[Symphony::AssetLoader] Loaded {loaded} files!");
		}
	}
}

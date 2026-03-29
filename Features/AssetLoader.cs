using AssetBundles;

using HarmonyLib;

using Symphony.Utils;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEngine.Networking;

namespace Symphony.Features {
	internal class AssetLoader {
		public struct Statistics {
			public readonly int Found;
			public readonly int Loaded;
			public readonly int Error;

			internal Statistics(int Found, int Loaded, int Error) {
				this.Found = Found;
				this.Loaded = Loaded;
				this.Error = Error;
			}
		}
		public static Statistics AssetStatistics { get; private set; }

		public static string AssetLoaderDirectory { get; } = Path.Combine(Plugin.GameDir, "AssetLoader");

		private static bool Loaded = false;
		private static bool Inited = false;
		private static Shader[] PreloadedShaders = [];

		public static void Init() {
			if (Inited) {
				Plugin.Logger.LogWarning("[Symphony::AssetLoader] AssetLoader's Inited method already called. Current call ignored");
				return;
			}
			Inited = true;

			var harmony = new Harmony("Symphony.AssetLoader");

			// AssetBundle loading fix
			harmony.Patch(
				AccessTools.Method(typeof(DownloadHandlerAssetBundle), nameof(DownloadHandlerAssetBundle.GetContent)),
				prefix: new HarmonyMethod(typeof(AssetLoader), nameof(AssetLoader.Patch_Initial_AssetBundle_Loading))
			);

			// Broken shader patch
			harmony.Patch(
				AccessTools.Method(typeof(ResourceManager), nameof(ResourceManager.LoadObject)),
				prefix: new HarmonyMethod(typeof(AssetLoader), nameof(AssetLoader.Patch_ResourceManager_LoadObject))
			);

			try {
				var ab = AssetBundle.LoadFromMemory(Resource.AssetBundle_SpineShaders);
				PreloadedShaders = ab.LoadAllAssets<Shader>();
				Plugin.Logger.LogInfo($"[Symphony::AssetLoader] {PreloadedShaders.Length} Spine shaders loaded");
			} catch {
				Plugin.Logger.LogError("[Symphony::AssetLoader] Failed to load Spine shaders");
			}

			SymphonyUtils.Initialize(Helper.RegisterAssemblyFromResource);
		}

		private static bool TryPatchToWindows(byte[] bundleData, out byte[] patchedData, out string error)
			=> AssetBundleCompatibilityPatcher.TryPatchToWindows(bundleData, out patchedData, out error, Plugin.Logger.LogInfo, Plugin.Logger.LogWarning);

		public static void Load() {
			if (Loaded) {
				Plugin.Logger.LogWarning("[Symphony::AssetLoader] AssetLoader's Load method already called. Current call ignored");
				return;
			}
			Loaded = true;

			if (!Directory.Exists(AssetLoaderDirectory)) {
				Plugin.Logger.LogWarning("[Symphony::AssetLoader] AssetLoader directory not exists, skip loading");
				return;
			}

			var _pathRegex = new Regex(@"[\\/]", RegexOptions.Compiled);
			var _patchedPostfixRegex = new Regex(@"\.patched$", RegexOptions.Compiled);
			var files = Directory.GetFiles(AssetLoaderDirectory, "*", SearchOption.AllDirectories)
				.Where(x => {
					if (Path.GetFileName(x) == "__info") return false;
					if (File.Exists(x + ".patched")) return false; // Already patched cache

					try {
						using var stream = File.OpenRead(x);
						var buffer = new byte[7];
						stream.Read(buffer, 0, 7);

						var str = Encoding.UTF8.GetString(buffer);
						return str == "UnityFS";
					} catch {
						return false;
					}
				})
				.ToArray();

			Plugin.Logger.LogInfo($"[Symphony::AssetLoader] Found {files.Length} files to load in AssetLoader directory");

			var loaded = 0;
			var errors = 0;
			foreach (var file in files) {
				var fname = _patchedPostfixRegex.Replace(Path.GetFileName(file), "");

				if (fname == "__data")
					fname = _pathRegex.Replace(Path.GetDirectoryName(Path.GetRelativePath(AssetLoaderDirectory, file)), "__");

				byte[] memory = null;
				try {
					Plugin.Logger.LogInfo($"[Symphony::AssetLoader] Trying to load '{fname}'");

					memory = File.ReadAllBytes(file);
					var bundle = AssetBundle.LoadFromMemory(memory);
					if (bundle == null) {
						if (TryPatchToWindows(memory, out var patchedMemory, out var patchError)) {
							bundle = AssetBundle.LoadFromMemory(patchedMemory);
							if (bundle == null)
								throw new PlatformNotSupportedException($"Patch succeeded but failed to load bundle '{fname}'");

							// Save as cache
							File.WriteAllBytes(file + ".patched", patchedMemory);

							AssetBundleManager.LoadedAssetBundles.Add(fname, new LoadedAssetBundle(bundle));
							loaded++;
							Plugin.Logger.LogMessage($"[Symphony::AssetLoader] Loaded '{fname}' with patch");
							continue;
						}

						Plugin.Logger.LogWarning($"[Symphony::AssetLoader] Failed to load '{fname}' with patch: {patchError}");
						continue;
					} else {
						Plugin.Logger.LogMessage($"[Symphony::AssetLoader] Loaded '{fname}'");
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

			AssetLoader.AssetStatistics = new Statistics(files.Length, loaded, errors);
			Plugin.Logger.LogInfo($"[Symphony::AssetLoader] Loaded {loaded} files!");
		}

		#region AssetBundle loading fix
		private static bool Patch_Initial_AssetBundle_Loading(UnityWebRequest www, ref AssetBundle __result) {
			var target_name = Path.GetFileName(www.url);
			__result = AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault(x => x.name == target_name);

			if (__result == null)
				__result = DownloadHandler.GetCheckedDownloader<DownloadHandlerAssetBundle>(www).assetBundle;
			else
				Plugin.Logger.LogInfo($"[Symphony::AssetLoader] Tried to load AssetBundle '{target_name}' that already loaded, return it from memory");

			return false;
		}
		#endregion

		#region Broken shader patch
		private static void Patch_ResourceManager_LoadObject(GameObject obj) {
			if (obj == null) return;

			var renderers = obj.GetComponentsInChildren<Renderer>(true);
			foreach (var ren in renderers) {
				foreach (var mat in ren.sharedMaterials) {
					if (mat?.shader == null) continue;
					if (mat.shader.isSupported) continue;
					if (string.IsNullOrEmpty(mat.shader.name)) continue;

					var s = PreloadedShaders.FirstOrDefault(x => x.name == mat.shader.name);
					if (s != null) mat.shader = s;
				}
			}
		}
		#endregion
	}
}

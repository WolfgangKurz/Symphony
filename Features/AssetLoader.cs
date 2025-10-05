using AssetBundles;

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

using UnityEngine;

namespace Symphony.Features {
	internal class AssetLoader {
		private static bool Loaded = false;
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

			var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
			Plugin.Logger.LogInfo($"[Symphony::AssetLoader] Found {files.Length} files in AssetLoader directory");

			var loaded = 0;
			foreach (var file in files) {
				try {
					var fname = Path.GetFileName(file);
					Plugin.Logger.LogMessage($"[Symphony::AssetLoader] Trying to load '{fname}'");

					var bundle = AssetBundle.LoadFromMemory(File.ReadAllBytes(file));
					AssetBundleManager.LoadedAssetBundles.Add(fname, new LoadedAssetBundle(bundle));
					loaded++;
				} catch (Exception e) {
					Plugin.Logger.LogError(e);
				}
			}
			Plugin.Logger.LogInfo($"[Symphony::AssetLoader] Loaded {loaded} files!");
		}
	}
}

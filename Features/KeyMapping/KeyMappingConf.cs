using BepInEx;

using LitJson;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Symphony.Features.KeyMapping {
	internal class KeyMappingConf {
		private static string ConfigPath = Path.Combine(Paths.GameRootPath, "Symphony_KeyMapping.json");

		public static KeyMappingData[] KeyMaps { get; private set; } = [];

		public static void Load() {
			if (!File.Exists(ConfigPath)) KeyMaps = [];

			try {
				KeyMaps = JsonMapper.ToObject<KeyMappingData[]>(File.ReadAllText(ConfigPath));
			} catch {
				KeyMaps = [];
			}
		}
		public static void Save(KeyMappingData[] keyMaps) {
			KeyMaps = keyMaps;

			File.WriteAllText(ConfigPath, JsonMapper.ToJson(keyMaps));
		}
	}
}

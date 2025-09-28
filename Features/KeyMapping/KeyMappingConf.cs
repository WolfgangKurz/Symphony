using BepInEx;

using LitJson;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Symphony.Features.KeyMapping {
	internal class KeyMappingConf {
		private static string ConfigPath = Path.Combine(Paths.GameRootPath, "Symphony_KeyMapping.json");

		public static Dictionary<string, KeyMappingData[]> KeyMaps { get; private set; } = new();

		public static void Load() {
			if (!File.Exists(ConfigPath)) KeyMaps = [];

			KeyMaps.Clear();
			try {
				KeyMaps = JsonMapper.ToObject<Dictionary<string, KeyMappingData[]>>(File.ReadAllText(ConfigPath));
			} catch {
				try {
					var lst = JsonMapper.ToObject<KeyMappingData[]>(File.ReadAllText(ConfigPath));
					KeyMaps.Add("Default", lst);
				} catch {
				}
			}
		}
		public static void RemoveGroup(string group) {
			if (KeyMaps.ContainsKey(group)) {
				KeyMaps.Remove(group);
				File.WriteAllText(ConfigPath, JsonMapper.ToJson(KeyMaps));
			}
		}
		public static void Save(string group, KeyMappingData[] keyMaps) {
			if (!KeyMaps.ContainsKey(group))
				KeyMaps.Add(group, keyMaps);
			else
				KeyMaps[group] = keyMaps;

			File.WriteAllText(ConfigPath, JsonMapper.ToJson(KeyMaps));
		}
	}
}

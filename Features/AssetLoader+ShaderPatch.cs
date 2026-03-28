using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

using UnityEngine;
using UnityEngine.UI;

namespace Symphony.Features.AssetLoaderPatch {
	internal class AssetLoader_ShaderPatch {
		public static void Patch() {
			var harmony = new Harmony("Symphony.AssetLoader");

			harmony.Patch(
				AccessTools.Method(typeof(ResourceManager), nameof(ResourceManager.LoadObject)),
				prefix: new HarmonyMethod(typeof(AssetLoader_ShaderPatch), nameof(AssetLoader_ShaderPatch.Patch_ResourceManager_LoadObject))
			);
		}

		private static bool Patch_ResourceManager_LoadObject(GameObject obj) {
			if (obj == null) return true;

			Plugin.Logger.LogMessage($"[Symphony::AssetLoader::ShaderPatch] Trying to patch '{obj.name}'");
			foreach (var ren in obj.GetComponentsInChildren<Renderer>(true)) {
				var materials = ren.sharedMaterials;

				for (var i = 0; i < materials.Length; i++) {
					var material = materials[i];
					if (material == null || material.shader == null) continue;

					Plugin.Logger.LogMessage($"[Symphony::AssetLoader::ShaderPatch] shader '{material.shader.name}'");

					Shader replacement;
					if (material.shader.name == "Hidden/InternalErrorShader")
						continue;

					replacement = Shader.Find(material.shader.name);
					if (replacement == material.shader) continue;

					material.shader = replacement;
				}
			}
			return true;
		}
	}
}

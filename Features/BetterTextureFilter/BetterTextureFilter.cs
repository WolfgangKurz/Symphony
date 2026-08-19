using AssetBundles;

using HarmonyLib;

using System;
using System.Collections;

using UnityEngine;

using BTF = Symphony.Features.BetterTextureFilter;

namespace Symphony.Features {
	internal partial class Experimental {
		internal static class BetterTextureFilter {
			private static Experimental coroutineRunner;

			internal static void Patch(Experimental experimental, Harmony harmony) {
				coroutineRunner = experimental;
				BTF.SpinePatcher.SetCoroutineRunner(experimental);
				BTF.Cache.Initialize(experimental);

				#region 2DModel & Creature(SD)
				harmony.Patch(
					AccessTools.Method(typeof(ResourceManager), nameof(ResourceManager.InstantiateGameObjectAsyncCallback)),
					postfix: new HarmonyMethod(typeof(BetterTextureFilter), nameof(Patch_InstantiateGameObjectAsyncCallback))
				);
				harmony.Patch(
					AccessTools.Method(typeof(ResourceManager), nameof(ResourceManager.InstantiateGameObjectAsync)),
					postfix: new HarmonyMethod(typeof(BetterTextureFilter), nameof(Patch_InstantiateGameObjectAsync))
				);
				var character2DArguments = new[] {
					typeof(string),
					typeof(Transform),
					typeof(bool)
				};
				harmony.Patch(
					AccessTools.Method(
						typeof(ResourceManager),
						nameof(ResourceManager.LoadCharacter2D),
						character2DArguments
					),
					postfix: new HarmonyMethod(typeof(BetterTextureFilter), nameof(Patch_LoadCharacter2D))
				);
				harmony.Patch(
					AccessTools.Method(typeof(Actor), "LoadModel", Type.EmptyTypes),
					postfix: new HarmonyMethod(typeof(BetterTextureFilter), nameof(Patch_Actor_LoadModel))
				);
				harmony.Patch(
					AccessTools.Method(
						typeof(ObjectPools),
						"SpawnGameSkinObject",
						[typeof(string), typeof(Transform)]
					),
					postfix: new HarmonyMethod(typeof(BetterTextureFilter), nameof(Patch_SpawnGameSkinObject))
				);
				harmony.Patch(
					AccessTools.Method(
						typeof(NGUITools),
						nameof(NGUITools.AddChild),
						[typeof(GameObject), typeof(GameObject), typeof(string)]
					),
					postfix: new HarmonyMethod(typeof(BetterTextureFilter), nameof(Patch_NGUIAddChild))
				);
				harmony.Patch(
					AccessTools.Method(
						typeof(ResourceManager),
						nameof(ResourceManager.LoadCharacter2DView),
						character2DArguments
					),
					postfix: new HarmonyMethod(typeof(BetterTextureFilter), nameof(Patch_LoadCharacter2D))
				);

				harmony.Patch(
					AccessTools.Method(typeof(CommonUtils), nameof(CommonUtils.GetRenderTexture)),
					prefix: new HarmonyMethod(typeof(BetterTextureFilter), nameof(Patch_CommonUtils_GetRenderTexture))
				);
				#endregion

				harmony.Patch(
					AccessTools.Method(typeof(ResourceManager), nameof(ResourceManager.LoadNovelFaceSprite)),
					postfix: new HarmonyMethod(typeof(BetterTextureFilter), nameof(Patch_LoadNovelFaceSprite))
				);
				harmony.Patch(
					AccessTools.Method(typeof(ResourceManager), nameof(ResourceManager.LoadPcFormationTexture)),
					postfix: new HarmonyMethod(typeof(BetterTextureFilter), nameof(Patch_LoadPcFormationTexture))
				);
				harmony.Patch(
					AccessTools.Method(typeof(ResourceManager), nameof(ResourceManager.LoadPcInvenTexture)),
					postfix: new HarmonyMethod(typeof(BetterTextureFilter), nameof(Patch_LoadPcInvenTexture))
				);
				harmony.Patch(
					AccessTools.Method(typeof(ResourceManager), nameof(ResourceManager.LoadPcInvenSprite)),
					postfix: new HarmonyMethod(typeof(BetterTextureFilter), nameof(Patch_LoadPcInvenSprite))
				);
				harmony.Patch(
					AccessTools.Method(typeof(ResourceManager), "LoadTroopsIcon"),
					postfix: new HarmonyMethod(typeof(BetterTextureFilter), nameof(Patch_LoadTroopsIconTexture))
				);

				harmony.Patch(
					AccessTools.Method(typeof(ResourceManager), nameof(ResourceManager.LoadAtlas)),
					postfix: new HarmonyMethod(typeof(BetterTextureFilter), nameof(Patch_LoadAtlas))
				);
				harmony.Patch(
					AccessTools.Method(typeof(Panel_MaterialWarehouse), nameof(Panel_MaterialWarehouse.Start)),
					prefix: new HarmonyMethod(typeof(BetterTextureFilter), nameof(Patch_MaterialWarehouse_Start))
				);

				harmony.Patch(
					AccessTools.Method(typeof(PrefabLoader), nameof(PrefabLoader.GetBundlePrefab)),
					postfix: new HarmonyMethod(typeof(BetterTextureFilter), nameof(Patch_GetBundlePrefab))
				);

				#region Facility
				var facilityAwakePostfix = new HarmonyMethod(
					typeof(BetterTextureFilter),
					nameof(Patch_FacilityAwake)
				);
				harmony.Patch(
					AccessTools.Method(typeof(FixedFacility), "Awake", Type.EmptyTypes),
					postfix: facilityAwakePostfix
				);
				harmony.Patch(
					AccessTools.Method(typeof(InstallationFacility), "Awake", Type.EmptyTypes),
					postfix: facilityAwakePostfix
				);
				#endregion
			}

			private static void Patch_InstantiateGameObjectAsyncCallback(
				ResourceManager __instance,
				ref IEnumerator __result,
				string assetBundleName,
				string assetName,
				Transform parent,
				Action<GameObject> callback
			) {
				if (!Conf.Experimental.Use_BetterTextureFilter.Value) return;

				IEnumerator Replacement() {
					assetBundleName = assetBundleName.ToLowerInvariant();

					var request = AssetBundleManager.LoadAssetAsync(
						assetBundleName,
						assetName,
						typeof(GameObject)
					);
					if (request == null) yield break;

					yield return __instance.StartCoroutine(request);

					var asset = request.GetAsset<GameObject>();
					asset = __instance.LoadObject(asset, parent);
					BTF.SpinePatcher.AttachAll(assetBundleName, assetName, asset);

					if (callback != null) {
						callback(asset);
						callback = null;
					}

					var actor = asset?.GetComponentInChildren<Actor>(true);
					if (actor) {
						__instance.StartCoroutine(
							BTF.TexturePatchService.PatchActorAsync(assetBundleName, assetName, actor)
						);
					}
				}

				__result = Replacement();
			}

			private static void Patch_InstantiateGameObjectAsync(
				ResourceManager __instance,
				ref IEnumerator __result,
				string assetBundleName,
				string assetName,
				Transform parent,
				bool reset_scale
			) {
				if (!Conf.Experimental.Use_BetterTextureFilter.Value) return;

				IEnumerator Replacement() {
					assetBundleName = assetBundleName.ToLowerInvariant();

					var request = AssetBundleManager.LoadAssetAsync(
						assetBundleName,
						assetName,
						typeof(GameObject)
					);
					if (request == null) yield break;

					yield return __instance.StartCoroutine(request);

					var prefab = request.GetAsset<GameObject>();
					var asset = __instance.LoadObject(prefab, parent, reset_scale);
					if (asset) {
						BTF.SpinePatcher.AttachAll(assetBundleName, assetName, asset);
						__instance.StartCoroutine(
							BTF.TexturePatchService.PatchRendererTexturesAsync(
								assetBundleName,
								assetName,
								asset,
								asset.GetComponentsInChildren<Renderer>(true)
							)
						);
					}
				}

				__result = Replacement();
			}

			private static void Patch_LoadCharacter2D(GameObject __result, string assetname) {
				if (!Conf.Experimental.Use_BetterTextureFilter.Value || !__result) return;

				BTF.SpinePatcher.AttachAll(assetname.ToLowerInvariant(), assetname, __result);
			}

			private static void Patch_Actor_LoadModel(Actor __instance) {
				if (
					!Conf.Experimental.Use_BetterTextureFilter.Value || !__instance ||
					!__instance.model || string.IsNullOrEmpty(__instance.actor_prefabname)
				) return;

				BTF.SpinePatcher.AttachAll(
					__instance.actor_prefabname.ToLowerInvariant(),
					__instance.actor_prefabname,
					__instance.model
				);
			}

			private static void Patch_SpawnGameSkinObject(Transform __result, string prefabName) {
				if (
					!Conf.Experimental.Use_BetterTextureFilter.Value || !__result ||
					string.IsNullOrEmpty(prefabName)
				) return;

				BTF.SpinePatcher.AttachAll(prefabName.ToLowerInvariant(), prefabName, __result.gameObject);
			}

			private static void Patch_NGUIAddChild(GameObject __result, GameObject parent, string name) {
				if (
					!Conf.Experimental.Use_BetterTextureFilter.Value || !__result || !parent ||
					string.IsNullOrEmpty(name)
				) return;

				var scene = Scene_DialogNovel.GetClass();
				if (scene == null || parent != scene.StaticImageCamera) return;

				BTF.SpinePatcher.AttachAll(name.ToLowerInvariant(), name, __result);
			}

			private static void Patch_LoadNovelFaceSprite(ResourceManager __instance, ref Sprite __result, string assetname) {
				if (!Conf.Experimental.Use_BetterTextureFilter.Value || !__result) return;

				var separator = assetname.LastIndexOf("_", StringComparison.Ordinal);
				if (separator <= 0) return;

				var bundleName = $"face_{assetname.Substring(0, separator).ToLowerInvariant()}";
				if (!BTF.TexturePatchService.CanPatch(__result.texture)) return;

				Plugin.Logger.LogDebug($"[Symphony::Experimental] BTF LoadNovelFaceSprite: {bundleName}:{assetname}");
				__result = BTF.TexturePatchService.CreateSpriteSync(bundleName, assetname, __result);
			}
			private static void Patch_LoadPcFormationTexture(ResourceManager __instance, ref Texture __result, string assetname) {
				if (!Conf.Experimental.Use_BetterTextureFilter.Value) return;
				if (!(__result is Texture2D texture) || !BTF.TexturePatchService.CanPatch(texture)) return;

				const string bundleName = "formationicon";
				Plugin.Logger.LogDebug($"[Symphony::Experimental] BTF LoadPcFormationTexture: {bundleName}:{assetname}");
				__result = BTF.TexturePatchService.CreateTextureSync(bundleName, assetname, texture);
			}
			private static void Patch_LoadPcInvenTexture(ResourceManager __instance, ref Texture __result, string assetname) {
				if (!Conf.Experimental.Use_BetterTextureFilter.Value) return;
				if (!(__result is Texture2D texture) || !BTF.TexturePatchService.CanPatch(texture)) return;

				const string bundleName = "pcinvenicon";
				Plugin.Logger.LogDebug($"[Symphony::Experimental] BTF LoadPcInvenTexture: {bundleName}:{assetname}");
				__result = BTF.TexturePatchService.CreateTextureSync(bundleName, assetname, texture);
			}
			private static void Patch_LoadPcInvenSprite(ResourceManager __instance, ref Sprite __result, string assetname) {
				if (!Conf.Experimental.Use_BetterTextureFilter.Value || !__result) return;

				var texture = __result.texture;
				if (!BTF.TexturePatchService.CanPatch(texture)) return;

				const string bundleName = "pcinvenicon";
				Plugin.Logger.LogDebug($"[Symphony::Experimental] BTF LoadPcInvenSprite: {bundleName}:{assetname}");
				__result = BTF.TexturePatchService.CreateSpriteSync(bundleName, assetname, __result);
			}
			private static void Patch_LoadTroopsIconTexture(ResourceManager __instance, ref Texture __result, string assetname) {
				if (!Conf.Experimental.Use_BetterTextureFilter.Value) return;
				if (!(__result is Texture2D texture) || !BTF.TexturePatchService.CanPatch(texture)) return;

				const string bundleName = "troopicon";
				Plugin.Logger.LogDebug($"[Symphony::Experimental] BTF LoadTroopsIconTexture: {bundleName}:{assetname}");
				__result = BTF.TexturePatchService.CreateTextureSync(bundleName, assetname, texture);
			}

			private static void Patch_LoadAtlas(ResourceManager __instance, ref UIAtlas __result, string assetname) {
				if (!Conf.Experimental.Use_BetterTextureFilter.Value || __result == null) return;

				var material = __result.spriteMaterial;
				if (!material || !(material.mainTexture is Texture2D texture)) return;
				if (!BTF.TexturePatchService.CanPatch(texture)) return;

				const string bundleName = "atlas";
				Plugin.Logger.LogDebug($"[Symphony::Experimental] BTF LoadAtlas: {bundleName}:{assetname}");
				material.mainTexture = BTF.TexturePatchService.CreateTextureSync(bundleName, assetname, texture);
				__result.MarkAsChanged(true);
			}
			private static void Patch_MaterialWarehouse_Start() {
				if (!Conf.Experimental.Use_BetterTextureFilter.Value) return;

				var resourceManager = SingleTon<ResourceManager>.Instance;
				resourceManager.LoadAtlas(GlobalDefines.Const.Atlas_Consumable);
				resourceManager.LoadAtlas(GlobalDefines.Const.Atlas_Consumable_V2);
			}

			private static void Patch_GetBundlePrefab(ref GameObject __result, string name) {
				if (!Conf.Experimental.Use_BetterTextureFilter.Value || !__result) return;
				if (!coroutineRunner) return;

				string bundleName;
				switch (name) {
					case "UIPcWarehouse":
					case "UIInvenSquad":
						bundleName = "mainui";
						break;
					case "UICharacterBook":
					case "UICharacterBookTroopsPcIcon":
						bundleName = "atlas";
						break;
					default:
						return;
				}

				BTF.TexturePatchService.PatchUIPrefabTextures(coroutineRunner, bundleName, name, __result);
			}

			private static void Patch_FacilityAwake(FacilityModel __instance) {
				if (
					!Conf.Experimental.Use_BetterTextureFilter.Value || !coroutineRunner ||
					!__instance || !__instance.gameObject
				) return;

				var facility = __instance.gameObject;
				var assetName = facility.name ?? "";
				const string cloneSuffix = "(Clone)";
				if (assetName.EndsWith(cloneSuffix, StringComparison.Ordinal))
					assetName = assetName.Substring(0, assetName.Length - cloneSuffix.Length);

				try {
					coroutineRunner.StartCoroutine(PatchFacilityAsync(facility, assetName));
				} catch (Exception exception) {
					Plugin.Logger.LogWarning(
						$"[Symphony::Experimental] Failed to start facility texture patch for '{assetName}': {exception}"
					);
				}
			}

			private static IEnumerator PatchFacilityAsync(GameObject facility, string assetName) {
				yield return null;
				if (!facility) yield break;

				yield return BTF.TexturePatchService.PatchRendererTexturesAsync(
					"facility",
					assetName,
					facility,
					facility.GetComponentsInChildren<Renderer>(true)
				);
			}

			private static bool Patch_CommonUtils_GetRenderTexture(
				int width, int height,
				int depthBuffer,
				RenderTextureFormat format,
				ref RenderTexture __result
			) {
				if (!Conf.Experimental.Use_BetterTextureFilter.Value) return true;

				var res = (Application.platform == RuntimePlatform.IPhonePlayer) ? 2048 : 4096;
				var r = Mathf.Min(1f, res / (float)Mathf.Max(width, height));
				width = Mathf.Max(128, Mathf.RoundToInt(width * r));
				height = Mathf.Max(128, Mathf.RoundToInt(height * r));

				var descriptor = new RenderTextureDescriptor(width, height, format, depthBuffer) {
					useMipMap = true,
					autoGenerateMips = true,
					msaaSamples = 1
				};
				var renderTexture = RenderTexture.GetTemporary(descriptor);
				renderTexture.filterMode = FilterMode.Trilinear;
				renderTexture.mipMapBias = -0.25f;

				__result = renderTexture;
				return false;
			}
		}

		private void OnApplicationQuit() => BTF.Cache.Shutdown();
		private void OnDestroy() => BTF.Cache.Shutdown();
	}
}

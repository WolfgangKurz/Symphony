using AssetBundles;

using GlobalDefines;

using HarmonyLib;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace Symphony.Features {
	[Feature("Experimental")]
	internal class Experimental : MonoBehaviour {
		private static bool FastLoaded = false;

		public void Start() {
			var harmony = new Harmony("Symphony.Experimental");

			#region Freezing fixers
			harmony.Patch(
				AccessTools.Method(typeof(Creature), nameof(Creature.DisappearBuffEffectParticleAll)),
				prefix: new HarmonyMethod(typeof(Experimental), nameof(Experimental.Patch_Creature_DisappearBuffEffectParticleAll))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Creature), nameof(Creature.PlayAnimation)),
				postfix: new HarmonyMethod(typeof(Experimental), nameof(Experimental.Patch_Creature_PlayAnimation))
			);
			#endregion

			#region FastLoading(LazyLoad)
			harmony.Patch(
				AccessTools.Method(typeof(ResourceManager), nameof(ResourceManager.CoLoadAssetBundles)),
				prefix: new HarmonyMethod(
					typeof(Experimental),
					nameof(Experimental.Patch_ResourceManager_CoLoadAssetBundles)
				)
			);

			LoadedAssetBundlesTarget = AssetBundleManager.LoadedAssetBundles;
			harmony.Patch(
				AccessTools.PropertyGetter(typeof(Dictionary<string, LoadedAssetBundle>), "Item"),
				prefix: new HarmonyMethod(typeof(Experimental), nameof(Experimental.Patch_AssetBundleManager_DictGetter))
			);
			harmony.Patch(
				AccessTools.Method(
					typeof(Dictionary<string, LoadedAssetBundle>),
					nameof(Dictionary<string, LoadedAssetBundle>.TryGetValue),
					[typeof(string), typeof(LoadedAssetBundle).MakeByRefType()]
				),
				prefix: new HarmonyMethod(typeof(Experimental), nameof(Experimental.Patch_AssetBundleManager_DictTryGet))
			);
			#endregion
		}

		#region Freezing fixers
		#region DisappearBuffEffectParticleAll "Collection was mutated while being enumerated" exception fix
		private static bool Patch_Creature_DisappearBuffEffectParticleAll(Creature __instance) {
			if (!Conf.Experimental.Fix_BattleFreezing.Value) return true;

			// Make copy to prevent collection changed exception
			var lst = __instance.XGetFieldValue<List<ulong>>("_ListDisappearDelayParticle").ToArray();
			foreach (ulong item in lst)
				__instance.DestroyAttachBuffEffectParticle(item);

			Plugin.Logger.LogDebug("[Symphony::Experimental] Freezing Patched (DisappearBuffEffectParticleAll)");
			return false;
		}
		#endregion

		#region AnimationClip Event missing fix
		private static void Patch_XXXstep_Animation(Creature creature) {
			if (creature == null) return;

			if (creature is Character) {
				var _animator = creature.Animator;

				eAndroidAnimationType[] animTypes = [
					eAndroidAnimationType.backstep,
					eAndroidAnimationType.frontstep
				];
				foreach (var animType in animTypes) {
					var clip = _animator.GetClip(animType);
					if (clip == null) return;
					if (!clip.events.Any(x => x.functionName == "EventJumpMoveStart")) {
						Plugin.Logger.LogInfo($"[Symphony::Experimental] EventJumpMoveStart missing patched, for {animType.ToString()} animation");

						var ev = new AnimationEvent();
						ev.functionName = "EventJumpMoveStart";
						ev.time = 0;
						clip.AddEvent(ev);
					}
				}
			}
		}
		private static void Patch_Nabi_NS1_Skill2(ref AnimationEvent[] events) {
			if (!events.Any(x => x.functionName == "EventHit" && x.stringParameter == "vfx_Nabi_ns1_skill1_hit")) return;
			Plugin.Logger.LogDebug("[Symphony::Experimental] Fix Freezing for Nabi NS1 Skill2");

			var audio = events.FirstOrDefault(x => x.objectReferenceParameter != null).objectReferenceParameter as AudioClip;

			#region Patch
			events = [ // Copy of non-skin
				new() {
					time = 0.016666667f,
					functionName = "EventDynamicBone",
					stringParameter = "Bone_Hair",
				},
				new() {
					time = 0.033333333f,
					functionName = "EventDynamicBone",
					stringParameter = "Bone_Breast",
				},
				new() {
					time = 0.050000000f,
					functionName = "EventDynamicBone",
					stringParameter = "Bone_Skirt",
				},
				new() {
					time = 0.166666667f,
					functionName = "EventCameraControlSelfMultiParam",
					stringParameter = "0.2,25,0,40",
				},
				new() {
					time = 0.5f,
					functionName = "EventSoundPlaySFXPrefab",
					objectReferenceParameter = null,
				},
				new() {
					time = 1.0f,
					functionName = "EventSoundPlaySFXPrefab",
					objectReferenceParameter=audio,
				},
				new() {
					time = 1.25f,
					functionName = "EventSoundPlaySFXPrefab",
					objectReferenceParameter = audio,
				},
				new() {
					time = 3.0f,
					functionName = "EventEvadeCheck",
				},
				new() {
					time = 3.333333333f,
					functionName = "EventSoundPlaySFXPrefab",
					objectReferenceParameter = null,
				},
				new() {
					time = 3.55f,
					functionName = "EventCameraShake",
					stringParameter = "0.15",
					floatParameter = 0.2f,
				},
				new() {
					time = 3.55f,
					functionName = "EventHit",
					stringParameter = "",
				},
				new() {
					time = 3.75f,
					functionName = "EventSoundPlaySFXPrefab",
					objectReferenceParameter = null,
				},
				new() {
					time = 3.75f,
					functionName = "EventHit",
					stringParameter = "noHit_m",
				},
				new() {
					time = 4.0f,
					functionName = "EventHit",
					stringParameter = "noHit_m",
				},
				new() {
					time = 4.0f,
					functionName = "EventCameraShake",
					stringParameter = "0.2",
					floatParameter = 0.2f,
				},
				new() {
					time = 4.25f,
					functionName = "EventHit",
					stringParameter = "noHit_m",
				},
				new() {
					time = 4.333333333f,
					functionName = "EventSoundPlaySFXPrefab",
					objectReferenceParameter = null,
				},
				new() {
					time = 4.333333333f,
					functionName = "EventThrowProjectileFire1",
					stringParameter = "eff_nabi_ns1_skill2_bullet", // Change name
				},
				new() {
					time = 4.333333333f,
					functionName = "EventCameraShake",
					stringParameter = "0.7",
					floatParameter = 1.0f,
				},
				new() {
					time = 4.5f,
					functionName = "EventHit",
					stringParameter = "noHit_m",
				},
				new() {
					time = 4.5f,
					functionName = "EventHit",
					stringParameter = "",
				},
				new() {
					time = 4.75f,
					functionName = "EventHit",
					stringParameter = "noHit_m",
				},
				new() {
					time = 4.75f,
					functionName = "EventSoundPlaySFXPrefab",
					objectReferenceParameter = null,
				},
				new() {
					time = 5.0f,
					functionName = "EventCameraShake",
					stringParameter = "0.7",
					floatParameter = 1.0f,
				},
				new() {
					time = 5.0f,
					functionName = "EventHit",
					stringParameter = "noHit_m",
				}
			];
			#endregion
		}
		private static void Patch_Creature_PlayAnimation(Creature __instance, eAndroidAnimationType aniType) {
			var creature = __instance;
			Plugin.Logger.LogInfo($"[Symphony::Experimental] Creature_PlayAnimation : {__instance?.name}, {aniType}");

			eAndroidAnimationType[] animTypes_step = [
				eAndroidAnimationType.frontstep,
				eAndroidAnimationType.frontstep_protect,
				eAndroidAnimationType.backstep,
				eAndroidAnimationType.backstep_protect,
			];
			if (animTypes_step.Contains(aniType)) {
				// Fix, fill missing jump start event for AnimationClip
				Experimental.Patch_XXXstep_Animation(creature);
			}

			// Patch LC_Nabi NS1 Skill2
			if (aniType == eAndroidAnimationType.skill2 && (creature as Character)?.TablePC.Key == "Char_LC_Nabi_N") {
				var clip = __instance.Animator.GetClip(eAndroidAnimationType.skill2);
				var events = clip.events;
				Experimental.Patch_Nabi_NS1_Skill2(ref events);
				clip.events = events;
			}
		}
		#endregion
		#endregion

		#region FastLoading
		private static bool Patch_ResourceManager_CoLoadAssetBundles(ResourceManager __instance, ref IEnumerator __result) {
			if (!Conf.Experimental.Use_FastLoading.Value) return true;

			Experimental.FastLoaded = true;

			IEnumerator Fn() {
				var resourceManager = __instance;

				var labelUpdater = new FrameLimit(0.1f);

				var serverURL = SingleTon<DataManager>.Instance.BundleAddress;
				Debug.Log("serverURL : " + serverURL);
				resourceManager.chkServerURLFlag = true;
				Debug.Log("Waiting.. AssetBundleManifestObject...");

				while (AssetBundleManager.AssetBundleManifestObject == null)
					yield return null;

				Debug.Log("Waiting.. Caching...");
				while (!Caching.ready)
					yield return null;

				Debug.Log("Download.. Bundle List...");
				var bundleWWWManifest = AssetBundleManager.AssetBundleManifestObject;

				var uriAssetVersion = serverURL + "assetVersion.json";
				Debug.Log("assetVersion URL Path : " + uriAssetVersion);
				var request = UnityWebRequestAssetBundle.GetAssetBundle(uriAssetVersion);

				request.downloadHandler = new DownloadHandlerBuffer();
				yield return request.SendWebRequest();

				if (request.isDone)
					resourceManager.XSetFieldValue(
						"AssetPlatformManifestObject",
						JsonUtility.FromJson<AssetPlatformManifest>(request.downloadHandler.text)
					);

				var AssetPlatformManifestObject = resourceManager.XGetFieldValue<AssetPlatformManifest>("AssetPlatformManifestObject");

				request = null;

				#region For lazy-pre-load
				var lazyLoadList = new List<string>();
				IEnumerator LazyLoadAssets() {
					var assetList = new List<UnityWebRequest>();
					foreach (var bundleName in lazyLoadList) {
						var uriBundle = serverURL + bundleName;
						var hash = bundleWWWManifest.GetAssetBundleHash(bundleName);

						var result = 0U;
						var assetFileManifest = AssetPlatformManifestObject.list.Find(x => x.fileName == bundleName);
						if (assetFileManifest != null && assetFileManifest.fileName.StartsWith("char_"))
							uint.TryParse(assetFileManifest.crc, out result);

						var assetBundle = UnityWebRequestAssetBundle.GetAssetBundle(uriBundle, hash, result);
						assetBundle.SendWebRequest();

						assetList.Add(assetBundle);

						if (labelUpdater.Valid())
							yield return null;
					}

					var isLoadingComplete = true;
					var assetSet = new HashSet<UnityWebRequest>(assetList);
					while (assetSet.Count > 0) {
						var itemsToRemove = new HashSet<UnityWebRequest>();

						foreach (var req in assetSet) {
							if (req.error != null) {
								__instance.needDownloadDelegate(true, 0, 0L, null);
								isLoadingComplete = false;
								Debug.LogError("Request Error!");
								break;
							}

							if (!req.isDone) continue; // next asset

							var loadedAssetBundle = new LoadedAssetBundle(DownloadHandlerAssetBundle.GetContent(req));
							AssetBundleManager.LoadedAssetBundles.TryAdd(loadedAssetBundle.m_AssetBundle.name, loadedAssetBundle);

							yield return null;

							itemsToRemove.Add(req);
							req.Dispose();
						}

						if (!isLoadingComplete) break;

						foreach (var r in itemsToRemove) assetSet.Remove(r);
						yield return null;
					}

					__instance.XSetFieldValue<AssetPlatformManifest>("AssetPlatformManifestObject", null);
				}
				#endregion

				var noneDownloadList = new List<string>();
				var needPatchFileList = new List<AssetFileManifest>();
				var bundlerVersion = AssetPlatformManifestObject.version;
				CrashReporter.SetBundleMetaData(bundlerVersion);

				var uePatchCheck = new UnityEvent<string, float>();
				uePatchCheck.AddListener((label, ratio) => resourceManager.onUpdateAssetLoading.Invoke("업데이트 검사중...", ratio));

				var ueLazyUpdateLoading = new UnityEvent<string, float>();
				ueLazyUpdateLoading.AddListener((label, ratio) => {
					if (labelUpdater.Valid())
						resourceManager.onUpdateAssetLoading.Invoke(label, ratio);
				});

				IEnumerator CheckPatchCoroutine() {
					var en = AssetPlatformManifestObject.PatchCheckCoroutine(
						serverURL,
						bundleWWWManifest,
						needPatchFileList,
						noneDownloadList,
						uePatchCheck
					);
					while (en.MoveNext()) {
						if (labelUpdater.Valid())
							yield return en.Current;
					}
				}

				yield return resourceManager.StartCoroutine(CheckPatchCoroutine());
				resourceManager.XSetFieldValue("networkBroken", false);

				var Wait0_5 = new WaitForSeconds(0.5f);

				if (needPatchFileList.Count > 0) {
					var totalsize = AssetBundles.Utility.GetPatchFileSizeTotal(needPatchFileList);

					resourceManager.isConfirmDownload = false;
					resourceManager.XSetFieldValue<long>("totalsize", totalsize);
					resourceManager.needDownloadDelegate(false, needPatchFileList.Count, totalsize, null);

					while (!resourceManager.isConfirmDownload)
						yield return Wait0_5;

					var maxConcurrentDownload = SingleTon<DataManager>.Instance.BundleMaxDownloadCount;
					Debug.Log($"<color=cyan>BundleMaxCount : {maxConcurrentDownload}</color>");
					if (maxConcurrentDownload < 1) maxConcurrentDownload = 1;

					var runningCount = 0;
					var downloadQueue = new Queue<AssetFileManifest>(needPatchFileList);
					while (downloadQueue.Count > 0 || runningCount > 0) {
						while (runningCount < maxConcurrentDownload && downloadQueue.Count > 0) {
							var manifestItem = downloadQueue.Dequeue();
							var bundleName = manifestItem.fileName;
							var savePath = serverURL + bundleName;

							runningCount++;
							resourceManager.StartCoroutine(
								(IEnumerator)AccessTools.Method(typeof(ResourceManager), "DownloadBundleWithRetry")
									.Invoke(resourceManager, [
										bundleWWWManifest,
										savePath,
										bundleName,
										(Action<bool>)(isSuccess => {
											--runningCount;
											if (!isSuccess)
												downloadQueue.Enqueue(manifestItem);
											else
												Debug.Log("다운로드 완료: " + bundleName);
										})
									])
							);
						}
						yield return null;
					}
				}

				// Clean up
				var _coDownLoad = resourceManager.XGetFieldValue<List<UnityWebRequest>>("_coDownLoad");
				foreach (var req in _coDownLoad) req.Dispose();
				_coDownLoad.Clear();

				var _coNoneDownloadList = resourceManager.XGetFieldValue<List<UnityWebRequest>>("_coNoneDownloadList");
				if (!resourceManager.IsCoDownloadPaused) {
					lazyLoadList = noneDownloadList
						// Lobby BG, Player/Enemy SD characters dependency
						.Where(x => x.StartsWith("novelbgtexture_ui_") || x.StartsWith("char_"))
						.Concat(["lastoneshader", "atlas", "sfx", "fxeffect", "bulleteffect"]) // Common assets
						.ToList();

					noneDownloadList = noneDownloadList
						.Where(x => x.StartsWith("table_") || x == "localization") // DB
						.ToList();

					for (var i = 0; i < noneDownloadList.Count; ++i) {
						var bundleName = noneDownloadList[i];
						var uriBundle = serverURL + bundleName;
						var hash = bundleWWWManifest.GetAssetBundleHash(bundleName);

						var result = 0U;
						var assetFileManifest = AssetPlatformManifestObject.list.Find(x => x.fileName == bundleName);
						if (assetFileManifest != null && assetFileManifest.fileName.StartsWith("char_"))
							uint.TryParse(assetFileManifest.crc, out result);

						var assetBundle = UnityWebRequestAssetBundle.GetAssetBundle(uriBundle, hash, result);
						assetBundle.SendWebRequest();

						_coNoneDownloadList.Add(assetBundle);

						var progress = i / (float)noneDownloadList.Count;
						ueLazyUpdateLoading.Invoke($"필수 에셋 요청중... ({i} / {noneDownloadList.Count})", progress);

						if (labelUpdater.Valid())
							yield return null;
					}

					var _panel_puzzle = resourceManager.XGetFieldValue<Panel_Puzzle>("_panel_puzzle");
					_panel_puzzle.GetProgressLb().text = Localization.Get("10");

					var isLoadingComplete = true;
					var noneDownloadSet = new HashSet<UnityWebRequest>(_coNoneDownloadList);
					while (noneDownloadSet.Count > 0) {
						var itemsToRemove = new HashSet<UnityWebRequest>();

						foreach (var req in noneDownloadSet) {
							if (req.error != null) {
								resourceManager.needDownloadDelegate(true, 0, 0L, null);
								isLoadingComplete = false;
								Debug.LogError("Request Error!");
								break;
							}

							if (!req.isDone) continue; // next asset

							var loadedAssetBundle = new LoadedAssetBundle(DownloadHandlerAssetBundle.GetContent(req));
							AssetBundleManager.LoadedAssetBundles.TryAdd(loadedAssetBundle.m_AssetBundle.name, loadedAssetBundle);

							var loaded = noneDownloadList.Count - (noneDownloadSet.Count - itemsToRemove.Count);
							var progress = (float)loaded / noneDownloadList.Count;
							ueLazyUpdateLoading.Invoke($"필수 에셋 로드중... ({loaded} / {noneDownloadList.Count})", progress);
							yield return null;

							itemsToRemove.Add(req);
							req.Dispose();
						}

						if (!isLoadingComplete) break;

						foreach (var r in itemsToRemove) noneDownloadSet.Remove(r);
						yield return null;
					}

					if (isLoadingComplete) {
						yield return new WaitForEndOfFrame();

						resourceManager.loadComplete = true;

						Localization.LoadExcel(resourceManager.LoadLocalizationPatch("LocalizationPatch"), true);
						SingleTon<DataManager>.Instance.LoadTable();
						SaveManager.BundleVersionSave(bundlerVersion);
						resourceManager.XSetFieldValue("init", true);
					}
				}

				resourceManager.XSetFieldValue<Panel_Puzzle>("_panel_puzzle", null);
				resourceManager.XSetPropertyValue<NeedDownloadDelegate>("needDownloadDelegate", null);
				resourceManager.onUpdateAssetLoading.Invoke("네트워크 통신중...", -1f);

				resourceManager.StartCoroutine(LazyLoadAssets());
			}
			__result = Fn();
			return false;
		}

		private static Dictionary<string, LoadedAssetBundle> LoadedAssetBundlesTarget = null;
		private static bool Patch_AssetBundleManager_DictGetter(
			Dictionary<string, LoadedAssetBundle> __instance,
			ref LoadedAssetBundle __result,
			string key
		) {
			if (__instance != LoadedAssetBundlesTarget) return true;
			if (!Experimental.FastLoaded) return true;

			LoadedAssetBundle Internal_Get(string key) {
				var i = __instance.XGetMethod<string, int>("FindEntry").Invoke(key);
				if (i >= 0) { // Dictionary raw indexer getter implement
					var entries = AccessTools.Field(__instance.GetType(), "_entries").GetValue(__instance);
					var get = AccessTools.Method(entries.GetType(), "GetValue", [typeof(int)]);
					var entry = get.Invoke(entries, [i]);
					return AccessTools.Field(entry.GetType(), "value").GetValue<LoadedAssetBundle>(entry);
				}
				return null;
			}

			if (__instance.ContainsKey(key)) {
				// Plugin.Logger.LogWarning($"[Symphony::LazyLoad] Cached {key}, return it");
				__result = Internal_Get(key);
				return false;
			}

			if (AssetBundleManager.AssetBundleManifestObject == null) {
				__result = null;
				return false;
			}

			Plugin.Logger.LogInfo($"[Symphony::LazyLoad] Requesting {key}");

			var url = SingleTon<DataManager>.Instance.BundleAddress + key;
			var hash = AssetBundleManager.AssetBundleManifestObject.GetAssetBundleHash(key);
			if (!Caching.IsVersionCached(url, hash)) {
				Plugin.Logger.LogWarning($"[Symphony::LazyLoad] {key} is not version-cached");
				__result = null;
				return false;
			}

			using (UnityWebRequest uwr = UnityWebRequestAssetBundle.GetAssetBundle(url, hash, 0)) {
				var asyncOperation = uwr.SendWebRequest();

				while (!asyncOperation.isDone)
					Thread.Sleep(0);

				if (uwr.result == UnityWebRequest.Result.Success) {
					var ret = new LoadedAssetBundle(DownloadHandlerAssetBundle.GetContent(uwr));
					__instance[key] = ret;
					__result = ret;
					return false;
				}
				else {
					Plugin.Logger.LogError($"[Symphony::LazyLoad] Failed to async-load AssetBundle that cached: {uwr.error}");
					__result = null;
					return false;
				}
			}
		}
		private static bool Patch_AssetBundleManager_DictTryGet(
			Dictionary<string, LoadedAssetBundle> __instance,
			ref bool __result,
			string key,
			ref LoadedAssetBundle value
		) {
			if (__instance != LoadedAssetBundlesTarget) return true;
			if (!Experimental.FastLoaded) return true;

			LoadedAssetBundle ret = null;
			Patch_AssetBundleManager_DictGetter(__instance, ref ret, key);
			value = ret;
			__result = value != null;
			return false;
		}
		#endregion
	}
}

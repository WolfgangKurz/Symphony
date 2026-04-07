using AssetBundles;

using GlobalDefines;

using HarmonyLib;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

using static Panel_Cheat;

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

			var loadedBundles = AssetBundle.GetAllLoadedAssetBundles().ToDictionary(x => x.name);
			var maxConcurrentDownload = SingleTon<DataManager>.Instance.BundleMaxDownloadCount;

			IEnumerator Fn() {
				var resourceManager = __instance;

				var labelUpdater = new FrameLimit(0.05f);

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

				AssetPlatformManifest AssetPlatformManifestObject;
				{
					var request = UnityWebRequest.Get(uriAssetVersion);
					yield return request.SendWebRequest();

					if (request.result == UnityWebRequest.Result.Success)
						resourceManager.XSetFieldValue(
							"AssetPlatformManifestObject",
							JsonUtility.FromJson<AssetPlatformManifest>(request.downloadHandler.text)
						);

					AssetPlatformManifestObject = resourceManager.XGetFieldValue<AssetPlatformManifest>("AssetPlatformManifestObject");
				}
				var AssetPlatformManifestTable = AssetPlatformManifestObject.list.ToDictionary(x => x.fileName);

				uint GetBundleCrc(string bundleName) {
					if (!AssetPlatformManifestTable.TryGetValue(bundleName, out var assetFileManifest)) return 0U;
					if (!assetFileManifest.fileName.StartsWith("char_")) return 0U;
					return uint.TryParse(assetFileManifest.crc, out var result) ? result : 0U;
				}

				IEnumerator LoadAssetBundlesConcurrent(
					IEnumerable<string> bundleNames,
					Func<int, int, bool, IEnumerator> onProgress = null,
					Action onError = null,
					Func<string, uint> crcSelector = null,
					bool stopOnError = true
				) {
					var loadQueue = new Queue<string>(bundleNames);
					var totalCount = loadQueue.Count;
					var loadedCount = 0;
					var errored = false;

					if (onProgress != null)
						yield return onProgress(loadedCount, totalCount, true);

					IEnumerator LoadFn(string bundleName, Action onComplete) {
						LoadedAssetBundle loadedAssetBundle;

						if (loadedBundles.TryGetValue(bundleName, out var cachedBundle)) {
							loadedAssetBundle = new LoadedAssetBundle(cachedBundle);
						} else {
							var req = UnityWebRequestAssetBundle.GetAssetBundle(
								serverURL + bundleName,
								bundleWWWManifest.GetAssetBundleHash(bundleName),
								crcSelector?.Invoke(bundleName) ?? 0U
							);
							yield return req.SendWebRequest();

							if (req.error != null) {
								Debug.LogError("Request Error!");
								req.Dispose();
								if (stopOnError)
									errored = true;
								onError?.Invoke();
								onComplete?.Invoke();
								yield break;
							}

							loadedAssetBundle = new LoadedAssetBundle(DownloadHandlerAssetBundle.GetContent(req));
							req.Dispose();
						}

						AssetBundleManager.LoadedAssetBundles.TryAdd(loadedAssetBundle.m_AssetBundle.name, loadedAssetBundle);
						loadedCount++;

						if (onProgress != null)
							yield return onProgress(loadedCount, totalCount, false);

						onComplete?.Invoke();
					}

					var runningCount = 0;
					while (loadQueue.Count > 0 || runningCount > 0) {
						while (!errored && runningCount < maxConcurrentDownload && loadQueue.Count > 0) {
							var bundleName = loadQueue.Dequeue();

							runningCount++;
							resourceManager.StartCoroutine(LoadFn(bundleName, () => {
								runningCount--;
							}));
						}

						if (errored) break;
						yield return null;
					}

					if (onProgress != null)
						yield return onProgress(loadedCount, totalCount, true);
				}

				var bundlerVersion = AssetPlatformManifestObject.version;
				CrashReporter.SetBundleMetaData(bundlerVersion);

				var noneDownloadList = new List<string>();
				var needPatchFileList = new List<AssetFileManifest>();

				var ueUpdateLabel = new UnityEvent<string, float>();
				ueUpdateLabel.AddListener((label, ratio) => resourceManager.onUpdateAssetLoading.Invoke(label, ratio));

				yield return resourceManager.StartCoroutine(
					Patch_AssetPlatformManifestObject_PatchCheckCoroutine(
						AssetPlatformManifestObject,
						serverURL,
						bundleWWWManifest,
						needPatchFileList,
						noneDownloadList,
						ueUpdateLabel
					)
				);
				resourceManager.XSetFieldValue("networkBroken", false);

				var Wait0_5 = new WaitForSeconds(0.5f);

				if (needPatchFileList.Count > 0) {
					var totalsize = AssetBundles.Utility.GetPatchFileSizeTotal(needPatchFileList);

					resourceManager.isConfirmDownload = false;
					resourceManager.XSetFieldValue<long>("totalsize", totalsize);
					resourceManager.needDownloadDelegate(false, needPatchFileList.Count, totalsize, null);

					while (!resourceManager.isConfirmDownload)
						yield return Wait0_5;

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
					#region For lazy-pre-load
					IEnumerator LazyLoadAssets(IEnumerable<string> bundleNames) {
						yield return new WaitUntil(() => resourceManager.loadComplete);
						yield return LoadAssetBundlesConcurrent(bundleNames, crcSelector: GetBundleCrc, stopOnError: false);

						__instance.XSetFieldValue<AssetPlatformManifest>("AssetPlatformManifestObject", null);
					}

					resourceManager.StartCoroutine(LazyLoadAssets(
						noneDownloadList
							// Lobby BG, Player/Enemy SD characters dependency
							.Where(x => x.StartsWith("novelbgtexture_ui_") || x.StartsWith("char_"))
							.Concat(["lastoneshader", "atlas", "sfx", "fxeffect", "bulleteffect"]) // Common assets
					));
					#endregion

					IEnumerator UpdateLabel(int loadedCount, int totalCount, bool force = false) {
						if ((force || labelUpdater.Valid()) && totalCount > 0) {
							var progress = loadedCount / (float)totalCount;
							ueUpdateLabel.Invoke($"필수 에셋 로드중... ({loadedCount} / {totalCount})", progress);
							yield return null;
						}
					}
					var errored = false;
					yield return LoadAssetBundlesConcurrent(
						noneDownloadList.Where(x => x.StartsWith("table_") || x == "localization"), // DB,
						onProgress: UpdateLabel,
						onError: () => {
							resourceManager.needDownloadDelegate(true, 0, 0L, null);
							errored = true;
						}
					);

					if (!errored) {
						resourceManager.onUpdateAssetLoading.Invoke("데이터 불러오는 중...", -1f);
						yield return new WaitForEndOfFrame();

						resourceManager.loadComplete = true;

						Localization.LoadExcel(resourceManager.LoadLocalizationPatch("LocalizationPatch"), true);

						var tableLoaded = false;
						Task.Run(() => {
							SingleTon<DataManager>.Instance.LoadTable();
							tableLoaded = true;
						});
						yield return new WaitUntil(() => tableLoaded);

						SaveManager.BundleVersionSave(bundlerVersion);
						resourceManager.XSetFieldValue("init", true);
					}
				}

				resourceManager.XSetFieldValue<Panel_Puzzle>("_panel_puzzle", null);
				resourceManager.XSetPropertyValue<NeedDownloadDelegate>("needDownloadDelegate", null);
				resourceManager.onUpdateAssetLoading.Invoke("네트워크 통신중...", -1f);
			}
			__result = Fn();
			return false;
		}

		private static IEnumerator Patch_AssetPlatformManifestObject_PatchCheckCoroutine(
			AssetPlatformManifest manifest,
			string serverURL,
			AssetBundleManifest bundleWWWManifest,
			List<AssetFileManifest> needPatchFileList,
			List<string> noneDownloadList,
			UnityEvent<string, float> progressCallback
		) {
			var labelUpdater = new FrameLimit(1f / 30);

			{
				var manifestQueue = new ConcurrentQueue<AssetFileManifest>(manifest.list);
				var count = manifest.list.Count;
				var counter = 0;

				var conNeedPatchList = new ConcurrentBag<AssetFileManifest>();
				var conNoneDownloadList = new ConcurrentBag<string>();
				var conLoadedAssetBundles = new ConcurrentDictionary<string, LoadedAssetBundle>(AssetBundleManager.LoadedAssetBundles);
				var unloadBag = new ConcurrentBag<string>();
				var unloadCount = 0;

				var maxConcurrent = SingleTon<DataManager>.Instance.BundleMaxDownloadCount;
				var runningCount = maxConcurrent;
				for (var i = 0; i < maxConcurrent; i++) {
					Task.Run(() => {
						try {
							while (manifestQueue.TryDequeue(out var item)) {
								try {
									var assetBundleHash = bundleWWWManifest.GetAssetBundleHash(item.fileName);

									Caching.ClearOtherCachedVersions(item.fileName, assetBundleHash);

									var loaded = conLoadedAssetBundles.ContainsKey(item.fileName);
									if (!Caching.IsVersionCached(item.fileName, assetBundleHash)) {
										if (loaded) {
											if (conLoadedAssetBundles.TryRemove(item.fileName, out _)) {
												Interlocked.Increment(ref unloadCount);
												unloadBag.Add(item.fileName);
											}
										}

										Caching.ClearAllCachedVersions(item.fileName); // Version mismatch
										conNeedPatchList.Add(item);
										Debug.Log("<color=cyan> NeedPatch Bundle Name : " + item.fileName + "</color>");
									}
									else if (!loaded) {
										conNoneDownloadList.Add(item.fileName);
										Caching.MarkAsUsed(item.fileName, assetBundleHash);
									}
								} catch (Exception e) {
									if (conLoadedAssetBundles.TryRemove(item.fileName, out _)) {
										Interlocked.Increment(ref unloadCount);
										unloadBag.Add(item.fileName);
									}

									conNeedPatchList.Add(item);

									Plugin.Logger.LogError($"[Symphony::Experimental] FastLoading Cache-Control got error: {e}");
								} finally {
									Interlocked.Increment(ref counter);
								}
							}
						} finally {
							Interlocked.Decrement(ref runningCount);
						}
					});
				}

				while (Volatile.Read(ref counter) < count) {
					if (Volatile.Read(ref runningCount) == 0) {
						Plugin.Logger.LogError("[Sympony::Experimental] FastLoading Cache-Control got error: Workers stopped before all items processed");
						break;
					}

					if (labelUpdater.Valid() && count > 0) {
						var ratio = (float)Volatile.Read(ref counter) / count;
						progressCallback?.Invoke("캐시 검사중...", ratio);
					}

					if (unloadBag.TryTake(out var target))
						AssetBundleManager.UnloadAssetBundle(target);

					yield return null;
				}

				while (unloadBag.TryTake(out var target)) { // flush remaining bag when exception
					AssetBundleManager.UnloadAssetBundle(target);

					if (labelUpdater.Valid()) {
						var ratio = (float)(unloadCount - unloadBag.Count) / unloadCount;
						progressCallback?.Invoke("캐시 처리중...", ratio);
						yield return null;
					}
				}

				needPatchFileList.AddRange(conNeedPatchList);
				noneDownloadList.AddRange(conNoneDownloadList);
			}

			IEnumerable<string> cachedBundleNames;
			string cachedRootPath;
			{
				var list = new List<string>();
				Caching.GetAllCachePaths(list);

				cachedRootPath = list.FirstOrDefault();
				if (cachedRootPath != null && Directory.Exists(cachedRootPath))
					cachedBundleNames = Directory.EnumerateDirectories(cachedRootPath)
						.Select(Path.GetFileName);
				else
					cachedBundleNames = [];
			}

			var fileNameSet = new HashSet<string>(manifest.list.Select(x => x.fileName));
			Task.Run(() => {
				foreach (var cachedName in cachedBundleNames) {
					if (!fileNameSet.Contains(cachedName)) {
						var path = Path.Combine(cachedRootPath, cachedName);
						if (Directory.Exists(path) && !Directory.EnumerateFiles(path).Any()) {
							try {
								Directory.Delete(path, recursive: true);
							} catch { } // Ignore errors
						}
					}
				}
			});

			Debug.Log("<color=cyan> NeedPatch Bundle Total Count : " + needPatchFileList.Count + "</color>");
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
			if (!Caching.IsVersionCached(key, hash)) {
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

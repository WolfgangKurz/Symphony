using AssetBundles;

using GlobalDefines;

using HarmonyLib;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace Symphony.Features {
	[Feature("Experimental")]
	internal partial class Experimental : MonoBehaviour {
		public void Start() {
			LazyLoadMainThreadId = Thread.CurrentThread.ManagedThreadId;
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
				AccessTools.Method(typeof(AssetBundleManager), "LoadAssetBundleInternal"),
				prefix: new HarmonyMethod(typeof(Experimental), nameof(Experimental.Patch_AssetBundleManager_InternalDictionary_Prefix)),
				finalizer: new HarmonyMethod(typeof(Experimental), nameof(Experimental.Patch_AssetBundleManager_InternalDictionary_Finalizer))
			);
			harmony.Patch(
				AccessTools.Method(typeof(AssetBundleManager), nameof(AssetBundleManager.GetLoadedAssetBundle)),
				prefix: new HarmonyMethod(typeof(Experimental), nameof(Experimental.Patch_AssetBundleManager_InternalDictionary_Prefix)),
				finalizer: new HarmonyMethod(typeof(Experimental), nameof(Experimental.Patch_AssetBundleManager_InternalDictionary_Finalizer))
			);
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
			harmony.Patch(
				AccessTools.Method(
					typeof(Dictionary<string, LoadedAssetBundle>),
					nameof(Dictionary<string, LoadedAssetBundle>.ContainsKey),
					[typeof(string)]
				),
				prefix: new HarmonyMethod(typeof(Experimental), nameof(Experimental.Patch_AssetBundleManager_DictContainsKey))
			);
			#endregion

			BetterTextureFilter.Patch(this, harmony);
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
		private static bool FastLoaded = false;
		private static bool FastLoadingInProgress = false;
		private static bool LazyLoadDictionaryEnabled = false;
		private static int LazyLoadMainThreadId;
		private static int LazyLoadGeneration;
		private static readonly ConcurrentDictionary<string, string[]> LazyLoadDependencyCache = new();

		[ThreadStatic]
		private static int LazyLoadDictionaryBypassDepth = 0;
		private static bool TryGetLoadedAssetBundleRaw(Dictionary<string, LoadedAssetBundle> bundles, string key, out LoadedAssetBundle value) {
			LazyLoadDictionaryBypassDepth++;
			try {
				return bundles.TryGetValue(key, out value);
			} finally {
				LazyLoadDictionaryBypassDepth--;
			}
		}

		private static readonly FieldInfo ABM_DownloadingWebRequest = AccessTools.Field(typeof(AssetBundleManager), "m_DownloadingWebRequest");
		private static readonly FieldInfo ABM_DownloadErrors = AccessTools.Field(typeof(AssetBundleManager), "m_DownloadingErrors");
		private static readonly FieldInfo ABM_IsRequestError = AccessTools.Field(typeof(AssetBundleManager), "m_IsRequestError");
		private static readonly FieldInfo ABM_Dependencies = AccessTools.Field(typeof(AssetBundleManager), "m_Dependencies");
		private static readonly	MethodInfo ABM_RemapVariantName = AccessTools.Method(typeof(AssetBundleManager), "RemapVariantName", [typeof(string)]);

		private static Dictionary<string, UnityWebRequest> GetAssetBundleManagerDownloads()
			=> (Dictionary<string, UnityWebRequest>)ABM_DownloadingWebRequest.GetValue(null);
		private static Dictionary<string, string> GetAssetBundleManagerDownloadErrors()
			=> (Dictionary<string, string>)ABM_DownloadErrors.GetValue(null);
		private static Dictionary<string, string[]> GetAssetBundleManagerDependencies()
			=> (Dictionary<string, string[]>)ABM_Dependencies.GetValue(null);
		private static void SetAssetBundleManagerRequestError() 
			=> ABM_IsRequestError.SetValue(null, true);

		private static string[] GetAssetBundleDependencies(string bundleName) {
			if (GetAssetBundleManagerDependencies().TryGetValue(bundleName, out var dependencies))
				return dependencies;

			return LazyLoadDependencyCache.GetOrAdd(bundleName, key => {
				var manifest = AssetBundleManager.AssetBundleManifestObject;
				if (manifest == null) return [];

				var manifestDependencies = manifest.GetAllDependencies(key);
				for (var i = 0; i < manifestDependencies.Length; i++)
					manifestDependencies[i] = (string)ABM_RemapVariantName.Invoke(null, [manifestDependencies[i]]);

				return manifestDependencies;
			});
		}

		private static bool AreAssetBundleDependenciesLoaded(string[] dependencies) {
			foreach (var dependency in dependencies) {
				if (!TryGetLoadedAssetBundleRaw(LoadedAssetBundlesTarget, dependency, out var loadedDependency)) return false;
				if (loadedDependency == null) return false;
			}
			return true;
		}

		private static bool TryGetAssetBundleManagerError(string bundleName, out string error, string[] dependencies = null) {
			var errors = GetAssetBundleManagerDownloadErrors();
			if (errors.TryGetValue(bundleName, out error)) return true;

			foreach (var dependency in dependencies ?? GetAssetBundleDependencies(bundleName)) {
				if (errors.TryGetValue(dependency, out error))
					return true;
			}

			error = null;
			return false;
		}

		private static void PumpCompletedAssetBundleManagerDownloads() {
			var downloads = GetAssetBundleManagerDownloads();
			var errors = GetAssetBundleManagerDownloadErrors();
			List<KeyValuePair<string, UnityWebRequest>> completed = null;

			foreach (var pair in downloads) {
				if (!pair.Value.isDone && pair.Value.error == null) continue;
				(completed ??= []).Add(pair);
			}
			if (completed == null) return;

			foreach (var pair in completed) {
				var request = pair.Value;
				try {
					if (request.result != UnityWebRequest.Result.Success) {
						SetAssetBundleManagerRequestError();
						errors[pair.Key] = $"Failed downloading bundle {pair.Key}, {request.error}";
						continue;
					}

					if (TryGetLoadedAssetBundleRaw(LoadedAssetBundlesTarget, pair.Key, out _)) continue;

					var assetBundle = DownloadHandlerAssetBundle.GetContent(request);
					if (assetBundle == null) {
						SetAssetBundleManagerRequestError();
						errors[pair.Key] = $"{pair.Key} is not a valid AssetBundle";
						continue;
					}

					LoadedAssetBundlesTarget.TryAdd(pair.Key, new LoadedAssetBundle(assetBundle));
				} catch (Exception e) {
					SetAssetBundleManagerRequestError();
					errors[pair.Key] = e.ToString();
				} finally {
					if (downloads.TryGetValue(pair.Key, out var current) && current == request)
						downloads.Remove(pair.Key);

					request.Dispose();
				}
			}
		}

		private static bool Patch_ResourceManager_CoLoadAssetBundles(ResourceManager __instance, ref IEnumerator __result) {
			if (!Conf.Experimental.Use_FastLoading.Value) return true;
			if (FastLoadingInProgress) {
				IEnumerator WaitForCurrentLoad() {
					while (FastLoadingInProgress)
						yield return null;
				}

				Plugin.Logger.LogWarning("[Symphony::LazyLoad] CoLoadAssetBundles is already running; joining it");
				__result = WaitForCurrentLoad();
				return false;
			}

			FastLoadingInProgress = true;
			Experimental.FastLoaded = true;
			LazyLoadDictionaryEnabled = false;
			var lazyLoadGeneration = ++LazyLoadGeneration;
			LazyLoadDependencyCache.Clear();
			__instance.loadComplete = false;
			LoadedAssetBundlesTarget = AssetBundleManager.LoadedAssetBundles;

			var loadedBundles = AssetBundle.GetAllLoadedAssetBundles().ToDictionary(x => x.name);
			var maxConcurrentDownload = Math.Max(1, SingleTon<DataManager>.Instance.BundleMaxDownloadCount);

			IEnumerator Fn() {
				var resourceManager = __instance;

				var labelUpdater = new FrameLimit(0.05f);

				var serverURL = SingleTon<DataManager>.Instance.BundleAddress;
				Debug.Log("serverURL : " + serverURL);
				AssetBundleManager.SetSourceAssetBundleURL(serverURL);
				resourceManager.chkServerURLFlag = true;
				Debug.Log("Waiting.. AssetBundleManifestObject...");

				while (AssetBundleManager.AssetBundleManifestObject == null) {
					if (lazyLoadGeneration != LazyLoadGeneration) yield break;
					yield return null;
				}

				Debug.Log("Waiting.. Caching...");
				while (!Caching.ready) {
					if (lazyLoadGeneration != LazyLoadGeneration) yield break;
					yield return null;
				}

				Debug.Log("Download.. Bundle List...");
				var bundleWWWManifest = AssetBundleManager.AssetBundleManifestObject;

				var uriAssetVersion = serverURL + "assetVersion.json";
				Debug.Log("assetVersion URL Path : " + uriAssetVersion);

				AssetPlatformManifest AssetPlatformManifestObject;
				{
					using (var request = UnityWebRequest.Get(uriAssetVersion)) {
						yield return request.SendWebRequest();
						if (lazyLoadGeneration != LazyLoadGeneration) yield break;

						if (request.result == UnityWebRequest.Result.Success)
							resourceManager.XSetFieldValue(
								"AssetPlatformManifestObject",
								JsonUtility.FromJson<AssetPlatformManifest>(request.downloadHandler.text)
							);
					}

					AssetPlatformManifestObject = resourceManager.XGetFieldValue<AssetPlatformManifest>("AssetPlatformManifestObject");
				}
				var AssetPlatformManifestTable = AssetPlatformManifestObject.list.ToDictionary(x => x.fileName);

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
						try {
							LoadedAssetBundle loadedAssetBundle = null;

							if (TryGetLoadedAssetBundleRaw(LoadedAssetBundlesTarget, bundleName, out loadedAssetBundle)) {
								// Already completed by another loader.
							} else if (loadedBundles.TryGetValue(bundleName, out var cachedBundle)) {
								loadedAssetBundle = new LoadedAssetBundle(cachedBundle);
							} else {
								UnityWebRequest request = null;
								UnityWebRequestAsyncOperation operation = null;
								string requestError = null;
								try {
									try {
										request = UnityWebRequestAssetBundle.GetAssetBundle(
											serverURL + bundleName,
											bundleWWWManifest.GetAssetBundleHash(bundleName),
											crcSelector?.Invoke(bundleName) ?? 0U
										);
										operation = request.SendWebRequest();
									} catch (Exception e) {
										requestError = e.ToString();
									}

									if (operation != null) {
										yield return operation;
										if (lazyLoadGeneration != LazyLoadGeneration) yield break;
										try {
											if (request.result == UnityWebRequest.Result.Success)
												loadedAssetBundle = new LoadedAssetBundle(DownloadHandlerAssetBundle.GetContent(request));
											else
												requestError = request.error;
										} catch (Exception e) {
											requestError = e.ToString();
										}
									}
								} finally {
									request?.Dispose();
								}

								if (requestError != null || loadedAssetBundle == null) {
									Debug.LogError($"Request Error! ({bundleName}: {requestError})");
									if (stopOnError)
										errored = true;
									onError?.Invoke();
									yield break;
								}
							}

							if (lazyLoadGeneration != LazyLoadGeneration) yield break;

							AssetBundleManager.LoadedAssetBundles.TryAdd(loadedAssetBundle.m_AssetBundle.name, loadedAssetBundle);
							loadedCount++;

							if (onProgress != null)
								yield return onProgress(loadedCount, totalCount, false);
						} finally {
							onComplete?.Invoke();
						}
					}

					var runningCount = 0;
					while (loadQueue.Count > 0 || runningCount > 0) {
						if (lazyLoadGeneration != LazyLoadGeneration) yield break;
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
				if (lazyLoadGeneration != LazyLoadGeneration) yield break;
				resourceManager.XSetFieldValue("networkBroken", false);

				var Wait0_5 = new WaitForSeconds(0.5f);

				if (needPatchFileList.Count > 0) {
					var totalsize = AssetBundles.Utility.GetPatchFileSizeTotal(needPatchFileList);

					resourceManager.isConfirmDownload = false;
					resourceManager.XSetFieldValue<long>("totalsize", totalsize);
					resourceManager.needDownloadDelegate(false, needPatchFileList.Count, totalsize, null);

					while (!resourceManager.isConfirmDownload) {
						if (lazyLoadGeneration != LazyLoadGeneration) yield break;
						yield return Wait0_5;
					}

					Debug.Log($"<color=cyan>BundleMaxCount : {maxConcurrentDownload}</color>");
					if (maxConcurrentDownload < 1) maxConcurrentDownload = 1;

					var runningCount = 0;
					var downloadQueue = new Queue<AssetFileManifest>(needPatchFileList);
					while (downloadQueue.Count > 0 || runningCount > 0) {
						if (lazyLoadGeneration != LazyLoadGeneration) yield break;
						while (runningCount < maxConcurrentDownload && downloadQueue.Count > 0) {
							var manifestItem = downloadQueue.Dequeue();
							runningCount++;

							var DownloadBundleWithRetry = resourceManager
								.XGetMethod<AssetBundleManifest, string, AssetFileManifest, Action<bool>, IEnumerator>("DownloadBundleWithRetry");

							resourceManager.StartCoroutine(
								DownloadBundleWithRetry(
									bundleWWWManifest,
									serverURL,
									manifestItem,
									delegate (bool isSuccess) {
										--runningCount;
										if (!isSuccess)
											downloadQueue.Enqueue(manifestItem);
										else
											Debug.Log("다운로드 완료: " + manifestItem.fileName);
									}
								)
							);
						}
						yield return null;
					}
				}

				// Clean up
				if (lazyLoadGeneration != LazyLoadGeneration) yield break;
				var _coDownLoad = resourceManager.XGetFieldValue<List<UnityWebRequest>>("_coDownLoad");
				foreach (var req in _coDownLoad) req.Dispose();
				_coDownLoad.Clear();

				var _coNoneDownloadList = resourceManager.XGetFieldValue<List<UnityWebRequest>>("_coNoneDownloadList");
				if (!resourceManager.IsCoDownloadPaused) {
					#region For lazy-pre-load
					IEnumerator LazyLoadAssets(IEnumerable<string> bundleNames) {
						var loadQueue = new Queue<string>(bundleNames);
						var running = new List<string>();
						var startedAt = new Dictionary<string, DateTime>();
						var lastProgressAt = DateTime.UtcNow;
						while (
							lazyLoadGeneration == LazyLoadGeneration &&
							(loadQueue.Count > 0 || running.Count > 0)
						) {
							var madeProgress = false;
							while (
								// Finish one root (including its dependencies) before starting the next.
								// AssetBundleManager does not ref-count joins to an in-flight dependency.
								running.Count == 0 &&
								GetAssetBundleManagerDownloads().Count == 0 &&
								loadQueue.Count > 0
							) {
								var bundleName = loadQueue.Dequeue();
								madeProgress = true;
								if (TryGetLoadedAssetBundleRaw(LoadedAssetBundlesTarget, bundleName, out _)) continue;
								if (TryGetAssetBundleManagerError(bundleName, out var previousError)) {
									Plugin.Logger.LogError($"[Symphony::LazyLoad] Skipping failed preload {bundleName}: {previousError}");
									continue;
								}

								try {
									AssetBundleManager.LoadAssetBundle(bundleName, false);
									running.Add(bundleName);
									startedAt[bundleName] = DateTime.UtcNow;
								} catch (Exception e) {
									Plugin.Logger.LogError($"[Symphony::LazyLoad] Failed to preload {bundleName}: {e}");
								}
							}

							for (var i = running.Count - 1; i >= 0; i--) {
								var bundleName = running[i];
								var loaded = AssetBundleManager.GetLoadedAssetBundle(bundleName, out var error);
								if (loaded != null) {
									running.RemoveAt(i);
									startedAt.Remove(bundleName);
									madeProgress = true;
									continue;
								}

								if (error == null)
									TryGetAssetBundleManagerError(bundleName, out error);

								if (error != null) {
									Plugin.Logger.LogError($"[Symphony::LazyLoad] Failed to preload {bundleName}: {error}");
									running.RemoveAt(i);
									startedAt.Remove(bundleName);
									madeProgress = true;
									continue;
								}

								if (DateTime.UtcNow - startedAt[bundleName] >= TimeSpan.FromSeconds(30)) {
									Plugin.Logger.LogError($"[Symphony::LazyLoad] Timed out while preloading {bundleName}");
									running.RemoveAt(i);
									startedAt.Remove(bundleName);
									madeProgress = true;
								}
							}

							if (madeProgress)
								lastProgressAt = DateTime.UtcNow;
							else if (DateTime.UtcNow - lastProgressAt >= TimeSpan.FromSeconds(30)) {
								Plugin.Logger.LogError("[Symphony::LazyLoad] Aborting lazy preload because its queue made no progress for 30 seconds");
								break;
							}

							yield return null;
						}

						if (lazyLoadGeneration == LazyLoadGeneration)
							__instance.XSetFieldValue<AssetPlatformManifest>("AssetPlatformManifestObject", null);
					}

					string[] commonBundlePriority = [
						"mainui", "formationicon", "lastoneshader", "atlas", "texture", "pcinvenicon",
						"lobbybgmodel", "sfx", "fxeffect", "bulleteffect", "novelcutimg", "voice",
						"bgm_02 bgm_lobby", // The bundle name intentionally contains a space.
					];
					var lazyLoadBundleNames =
						commonBundlePriority
							.Where(AssetPlatformManifestTable.ContainsKey)
							// Lobby BG, Player/Enemy SD characters dependency
							.Concat(noneDownloadList.Where(x => x.StartsWith("novelbgtexture_ui_") || x.StartsWith("char_")))
							.Distinct()
							.ToArray();
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

						if (lazyLoadGeneration != LazyLoadGeneration) yield break;
						resourceManager.loadComplete = true;

						Exception localizationError = null;
						try {
							Localization.LoadExcel(resourceManager.LoadLocalizationPatch("LocalizationPatch"), true);
						} catch (Exception e) {
							localizationError = e;
						}

						if (localizationError != null) {
							Plugin.Logger.LogError($"[Symphony::LazyLoad] Failed to load localization: {localizationError}");
							resourceManager.loadComplete = false;
							resourceManager.needDownloadDelegate(true, 0, 0L, null);
						} else {
							var tableLoadTask = Task.Run(() => SingleTon<DataManager>.Instance.LoadTable());
							yield return new WaitUntil(() => tableLoadTask.IsCompleted);
							if (lazyLoadGeneration != LazyLoadGeneration) {
								Plugin.Logger.LogInfo("[Symphony::LazyLoad] Ignoring a superseded table load");
							} else if (tableLoadTask.IsFaulted) {
								Plugin.Logger.LogError($"[Symphony::LazyLoad] Failed to load tables: {tableLoadTask.Exception}");
								resourceManager.loadComplete = false;
								resourceManager.needDownloadDelegate(true, 0, 0L, null);
							} else {
								LazyLoadDictionaryEnabled = true;
								resourceManager.StartCoroutine(LazyLoadAssets(lazyLoadBundleNames));

								SaveManager.BundleVersionSave(bundlerVersion);
								resourceManager.XSetFieldValue("init", true);
							}
						}
					}
				}

				if (lazyLoadGeneration == LazyLoadGeneration) {
					resourceManager.XSetFieldValue<Panel_Puzzle>("_panel_puzzle", null);
					resourceManager.XSetPropertyValue<NeedDownloadDelegate>("needDownloadDelegate", null);
					resourceManager.onUpdateAssetLoading.Invoke("네트워크 통신중...", -1f);
				}
			}
			IEnumerator GuardedFn() {
				try {
					yield return Fn();
				} finally {
					FastLoadingInProgress = false;
				}
			}

			__result = GuardedFn();
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
						progressCallback?.Invoke("설치된 파일 검사중...", ratio);
					}

					if (unloadBag.TryTake(out var target))
						AssetBundleManager.UnloadAssetBundle(target);

					yield return null;
				}

				while (unloadBag.TryTake(out var target)) { // flush remaining bag when exception
					AssetBundleManager.UnloadAssetBundle(target);

					if (labelUpdater.Valid()) {
						var ratio = (float)(unloadCount - unloadBag.Count) / unloadCount;
						progressCallback?.Invoke("설치된 파일 정리중...", ratio);
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

		private static void Patch_AssetBundleManager_InternalDictionary_Prefix() {
			LazyLoadDictionaryBypassDepth++;
		}
		private static Exception Patch_AssetBundleManager_InternalDictionary_Finalizer(Exception __exception) {
			if (LazyLoadDictionaryBypassDepth > 0)
				LazyLoadDictionaryBypassDepth--;
			return __exception;
		}

		private static LoadedAssetBundle LoadAssetBundleSynchronously(string key, bool startRequest) {
			if (Thread.CurrentThread.ManagedThreadId != LazyLoadMainThreadId) {
				Plugin.Logger.LogError($"[Symphony::LazyLoad] Refusing to load {key} from a worker thread");
				return null;
			}

			var manifest = AssetBundleManager.AssetBundleManifestObject;
			if (manifest == null) return null;

			var hash = manifest.GetAssetBundleHash(key);
			if (!Caching.IsVersionCached(key, hash)) {
				Plugin.Logger.LogWarning($"[Symphony::LazyLoad] {key} is not version-cached");
				return null;
			}

			var downloads = GetAssetBundleManagerDownloads();
			var wasInFlight = downloads.ContainsKey(key);
			if (startRequest) {
				try {
					AssetBundleManager.LoadAssetBundle(key, false);
				} catch (Exception e) {
					Plugin.Logger.LogError($"[Symphony::LazyLoad] Failed to start loading {key}: {e}");
					return null;
				}
			}

			Plugin.Logger.LogInfo(
				wasInFlight || !startRequest
					? $"[Symphony::LazyLoad] Waiting for in-flight AssetBundleManager request {key}"
					: $"[Symphony::LazyLoad] Requesting {key}"
			);

			// A bundle can already be in flight as another root's dependency. In that case
			// AssetBundleManager does not create m_Dependencies[key], so verify the
			// manifest's flattened dependency list before returning the target bundle.
			var expectedDependencies = GetAssetBundleDependencies(key);
			var startedAt = DateTime.UtcNow;
			var timeoutAt = startedAt.AddSeconds(10);
			while (true) {
				PumpCompletedAssetBundleManagerDownloads();

				var result = AssetBundleManager.GetLoadedAssetBundle(key, out var error);
				var dependenciesReady = AreAssetBundleDependenciesLoaded(expectedDependencies);
				if (result != null && dependenciesReady) {
					var elapsed = DateTime.UtcNow - startedAt;
					Plugin.Logger.LogInfo($"[Symphony::LazyLoad] Synchronous wait for {key}: {elapsed.TotalMilliseconds:F0} ms");
					return result;
				}

				if (error == null)
					TryGetAssetBundleManagerError(key, out error, expectedDependencies);

				if (error != null) {
					Plugin.Logger.LogError($"[Symphony::LazyLoad] Failed to load cached AssetBundle {key}: {error}");
					return null;
				}

				if (DateTime.UtcNow >= timeoutAt) {
					Plugin.Logger.LogError($"[Symphony::LazyLoad] Timed out after 10 seconds while loading {key}");
					return null;
				}

				Thread.Sleep(1);
			}
		}

		private static bool TryGetOrLoadAssetBundle(
			Dictionary<string, LoadedAssetBundle> bundles,
			string key,
			out LoadedAssetBundle value
		) {
			if (Thread.CurrentThread.ManagedThreadId != LazyLoadMainThreadId)
				return TryGetLoadedAssetBundleRaw(bundles, key, out value);

			value = AssetBundleManager.GetLoadedAssetBundle(key, out var loadError);
			if (value != null) {
				// A non-null manager result already verified dependencies when it owns a map.
				if (GetAssetBundleManagerDependencies().ContainsKey(key)) return true;
				if (AreAssetBundleDependenciesLoaded(GetAssetBundleDependencies(key))) return true;
				value = LoadAssetBundleSynchronously(key, false);
				return value != null;
			}

			if (loadError != null) {
				Plugin.Logger.LogError($"[Symphony::LazyLoad] AssetBundleManager previously failed to load {key}: {loadError}");
				return false;
			}

			var isBundleLoaded = TryGetLoadedAssetBundleRaw(bundles, key, out var rawValue);
			if (isBundleLoaded && rawValue == null) return true;

			value = LoadAssetBundleSynchronously(key, !isBundleLoaded);
			return value != null;
		}

		private static bool Patch_AssetBundleManager_DictGetter(
			Dictionary<string, LoadedAssetBundle> __instance,
			ref LoadedAssetBundle __result,
			string key
		) {
			if (__instance != LoadedAssetBundlesTarget) return true;
			if (!Experimental.FastLoaded || !LazyLoadDictionaryEnabled || LazyLoadDictionaryBypassDepth > 0) return true;

			if (TryGetOrLoadAssetBundle(__instance, key, out __result))
				return false;

			return true; // will throw KeyNotFoundException
		}

		private static bool Patch_AssetBundleManager_DictTryGet(
			Dictionary<string, LoadedAssetBundle> __instance,
			ref bool __result,
			string key,
			ref LoadedAssetBundle value
		) {
			if (__instance != LoadedAssetBundlesTarget) return true;
			if (!Experimental.FastLoaded || !LazyLoadDictionaryEnabled || LazyLoadDictionaryBypassDepth > 0) return true;

			__result = TryGetOrLoadAssetBundle(__instance, key, out value);
			return false;
		}

		private static bool Patch_AssetBundleManager_DictContainsKey(
			Dictionary<string, LoadedAssetBundle> __instance,
			ref bool __result,
			string key
		) {
			if (__instance != LoadedAssetBundlesTarget) return true;
			if (!Experimental.FastLoaded || !LazyLoadDictionaryEnabled || LazyLoadDictionaryBypassDepth > 0) return true;

			__result = TryGetOrLoadAssetBundle(__instance, key, out _);
			return false;
		}
		#endregion
	}
}

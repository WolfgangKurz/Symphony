using AssetBundles;

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
	internal class Experimental : MonoBehaviour {
		public void Start() {
			var harmony = new Harmony("Symphony.SimpleTweaks.Loading");
			if (Conf.Experimental.Use_FastLoading.Value) {
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
			}
		}

		private static bool Patch_ResourceManager_CoLoadAssetBundles(ResourceManager __instance, ref IEnumerator __result) {
			IEnumerator Fn() {
				var resourceManager = __instance;

				Plugin.Logger.LogWarning(AssetBundleManager.LoadedAssetBundles.GetType());

				var serverURL = SingleTon<DataManager>.Instance.BundleAddress;
				Plugin.Logger.LogMessage("serverURL : " + serverURL);
				resourceManager.chkServerURLFlag = true;
				Plugin.Logger.LogMessage("Waiting.. AssetBundleManifestObject...");

				while (AssetBundleManager.AssetBundleManifestObject == null)
					yield return null;

				Plugin.Logger.LogMessage("Waiting.. Caching...");
				while (!Caching.ready)
					yield return null;

				Plugin.Logger.LogMessage("Download.. Bundle List...");
				var bundleWWWManifest = AssetBundleManager.AssetBundleManifestObject;

				var uriAssetVersion = serverURL + "assetVersion.json";
				Plugin.Logger.LogMessage("assetVersion URL Path : " + uriAssetVersion);
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

				var noneDownloadList = new List<string>();
				var needPatchFileList = new List<AssetFileManifest>();
				var bundlerVersion = AssetPlatformManifestObject.version;
				CrashReporter.SetBundleMetaData(bundlerVersion);

				var labelUpdater = new FrameLimit(0.05f);
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
					while(en.MoveNext()) {
						if (labelUpdater.Valid())
							yield return en.Current;
					}
				}

				yield return resourceManager.StartCoroutine(CheckPatchCoroutine());
				resourceManager.XSetFieldValue("networkBroken", false);

				var Wait0_5 = new WaitForSeconds(0.5f);

				var maxCount = 0;
				if (needPatchFileList.Count > 0) {
					var totalsize = AssetBundles.Utility.GetPatchFileSizeTotal(needPatchFileList);

					resourceManager.isConfirmDownload = false;
					resourceManager.XSetFieldValue<long>("totalsize", totalsize);
					resourceManager.needDownloadDelegate(false, needPatchFileList.Count, totalsize, null);

					while (!resourceManager.isConfirmDownload)
						yield return Wait0_5;

					maxCount = SingleTon<DataManager>.Instance.BundleMaxDownloadCount;
					Plugin.Logger.LogMessage($"<color=cyan>BundleMaxCount : {maxCount}</color>");
					if (maxCount < 1) maxCount = 1;

					var runningCount = 0;
					var downloadQueue = new Queue<AssetFileManifest>(needPatchFileList);
					while (downloadQueue.Count > 0 || runningCount > 0) {
						while (runningCount < maxCount && downloadQueue.Count > 0) {
							AssetFileManifest manifestItem = downloadQueue.Dequeue();
							string bundleName = manifestItem.fileName;
							string savePath = serverURL + bundleName;
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
											Plugin.Logger.LogMessage("다운로드 완료: " + bundleName);
									})
								])
							);
						}
						yield return null;
					}
				}

				var _coDownLoad = resourceManager.XGetFieldValue<List<UnityWebRequest>>("_coDownLoad");
				foreach (UnityWebRequest unityWebRequest in _coDownLoad)
					unityWebRequest.Dispose();

				_coDownLoad.Clear();
				var _coNoneDownloadList = resourceManager.XGetFieldValue<List<UnityWebRequest>>("_coNoneDownloadList");
				if (!resourceManager.IsCoDownloadPaused) {
					maxCount = -1;

					noneDownloadList = noneDownloadList.Where(x =>
					x.StartsWith("novelbgtexture_ui_") ||
					x.StartsWith("char_")
					)
						.Concat(["lastoneshader", "atlas", "sfx", "fxeffect", "bulleteffect"])
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

						var curCount = (int)(i * 100f / noneDownloadList.Count);
						if (curCount != maxCount) {
							var progress = i / (float)noneDownloadList.Count;
							ueLazyUpdateLoading.Invoke($"필수 에셋 요청중... ({i} / {noneDownloadList.Count})", progress);
							maxCount = curCount;
							yield return null;
						}
					}

					var _panel_puzzle = resourceManager.XGetFieldValue<Panel_Puzzle>("_panel_puzzle");
					_panel_puzzle.GetProgressLb().text = Localization.Get("10");

					var isLoadingComplete = true;
					maxCount = -1;
					for (var i = 0; i < _coNoneDownloadList.Count; ++i) {
						request = _coNoneDownloadList[i];

						if (request.error != null) {
							resourceManager.needDownloadDelegate(true, 0, 0L, null);
							isLoadingComplete = false;
							Plugin.Logger.LogError("Request Error!");
							break;
						}

						if (!request.isDone) {
							while (request.error == null && !request.isDone)
								yield return null;

							if (request.error != null) {
								resourceManager.needDownloadDelegate(true, 0, 0L, null);
								isLoadingComplete = false;
								Plugin.Logger.LogError("Request Time Out");
								break;
							}
						}

						var loadedAssetBundle = new LoadedAssetBundle(DownloadHandlerAssetBundle.GetContent(request));
						if (!AssetBundleManager.LoadedAssetBundles.ContainsKey(loadedAssetBundle.m_AssetBundle.name))
							AssetBundleManager.LoadedAssetBundles.Add(loadedAssetBundle.m_AssetBundle.name, loadedAssetBundle);

						var curCount = (int)(i * 100f / _coNoneDownloadList.Count);
						if (curCount != maxCount) {
							var progress = i / (float)noneDownloadList.Count;
							ueLazyUpdateLoading.Invoke($"필수 에셋 로드중... ({i} / {noneDownloadList.Count})", progress);
							maxCount = curCount;
							yield return null;
						}
						request = null;
					}

					foreach (var coNoneDownload in _coNoneDownloadList)
						coNoneDownload.Dispose();

					_coNoneDownloadList.Clear();
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
				resourceManager.XSetFieldValue<NeedDownloadDelegate>("needDownloadDelegate", null);
				resourceManager.onUpdateAssetLoading.Invoke("Receiving...", -1f);
				resourceManager.XSetFieldValue<AssetPlatformManifest>("AssetPlatformManifestObject", null);
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

			LoadedAssetBundle Internal_Get(string key) {
				var i = __instance.XGetMethod<string, int>("FindEntry").Invoke(key);
				if (i >= 0) { // Dictionary raw indexer getter implement
					var entries = AccessTools.Field(__instance.GetType(), "_entries").GetValue(__instance);
					var get = AccessTools.Method(entries.GetType(), "GetValue", [typeof(int)]);
					var entry = get.Invoke(entries, [i]);
					return AccessTools.Field(entry.GetType(), "value").GetValue< LoadedAssetBundle>(entry);
				}
				return null;
			}

			if (__instance.ContainsKey(key)) {
				// Plugin.Logger.LogWarning($"[LazyLoad] Cached {key}, return it");
				__result = Internal_Get(key);
				return false;
			}

			if (AssetBundleManager.AssetBundleManifestObject == null) {
				__result = null;
				return false;
			}

			Plugin.Logger.LogWarning($"[LazyLoad] Requesting {key}");

			var url = SingleTon<DataManager>.Instance.BundleAddress + key;
			var hash = AssetBundleManager.AssetBundleManifestObject.GetAssetBundleHash(key);
			if (!Caching.IsVersionCached(url, hash)) {
				Plugin.Logger.LogWarning($"[LazyLoad] {key} not version cached!!!");
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
					Plugin.Logger.LogError($"캐시된 애셋 번들 동기 로드 실패: {uwr.error}");
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

			LoadedAssetBundle ret = null;
			Patch_AssetBundleManager_DictGetter(__instance, ref ret, key);
			value = ret;
			__result = value != null;
			return false;
		}
	}
}

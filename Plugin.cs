using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;

using LitJson;

using Symphony.Features;
using Symphony.UI;
using Symphony.UI.Panels;

using System;
using System.Collections;
using System.Reflection;

using UnityEngine;
using UnityEngine.Networking;

namespace Symphony {
	[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
	public class Plugin : BaseUnityPlugin {
		internal static new ManualLogSource Logger;
		internal static readonly Version Ver = Assembly.GetExecutingAssembly().GetName().Version;

		internal static readonly string VersionTag = $"v{Ver.Major}.{Ver.Minor}.{Ver.Revision}";

		internal static IntPtr hWnd => Helper.GetMainWindowHandle();

		private UIManager uiManager;
		private ConfigPanel configPanel;

		private class GithubReleaseInfo {
			public string tag_name { get; set; }
		}

		public void Awake() {
			// Plugin startup logic
			Logger = base.Logger;
			Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

			try {
				Enum.GetValues(typeof(ACTOR_CLASS)); // to test game assembly
			}
			catch {
				Logger.LogError("Failed to find ACTOR_CLASS, seems not installed on LastOrigin or binary changed!");
				return;
			}
			finally {
				StartCoroutine(this.CheckUpdate());
			}

			StartCoroutine(this.InitUI());

			this.gameObject.AddComponent<SimpleTweaks>();
			this.gameObject.AddComponent<WindowedResize>();
			this.gameObject.AddComponent<BattleHotkey>();
		}

		private IEnumerator InitUI() {
			yield return new WaitForEndOfFrame();

			this.uiManager = this.gameObject.AddComponent<UIManager>();
			this.configPanel = this.uiManager.AddPanel(new ConfigPanel());
			this.configPanel.enabled = false;
		}

		public void Update() {
			if (Input.GetKeyDown(KeyCode.F12))
				this.configPanel.enabled = !this.configPanel.enabled;
		}

		private IEnumerator CheckUpdate() {
			var req = UnityWebRequest.Get("https://api.github.com/repos/WolfgangKurz/Symphony/releases/latest");
			yield return req.SendWebRequest();

			if (req.result == UnityWebRequest.Result.ProtocolError || req.result == UnityWebRequest.Result.ConnectionError) {
				Logger.LogError($"[Symphony] Cannot fetch update data: {req.error}");
				yield break;
			}

			try {
				var json = req.downloadHandler.text;
				var tag = JsonMapper.ToObject<GithubReleaseInfo>(json).tag_name;
				if (tag != Plugin.VersionTag) {
					SceneBase.Instance.ShowMessage(
						$"Symphony 플러그인에 업데이트가 있습니다.\n" +
						$"새 버전: {tag}\n" +
						$"현재 버전: {Plugin.VersionTag}\n\n" +
						"Github 페이지로 이동하시겠습니까?",
						"Symphony",
						"이동하기", "닫기", "",
						GlobalDefines.MessageType.YESNO, () => {
							Application.OpenURL("https://github.com/WolfgangKurz/Symphony/releases");
						}
					);
				}
			}
			catch (Exception e) {
				Logger.LogError($"[Symphony] Cannot fetch update data: {e.ToString()}");
				yield break;
			}

			yield break;
		}
	}
}
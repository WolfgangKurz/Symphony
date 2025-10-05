#pragma warning disable BepInEx002 // Classes with BepInPlugin attribute must inherit from BaseUnityPlugin
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;

using LitJson;

using Symphony.UI;
using Symphony.UI.Panels;

using System;
using System.Collections;
using System.Linq;
using System.Reflection;

using UnityEngine;
using UnityEngine.Networking;

namespace Symphony {
	[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
	public class Plugin : BaseUnityPlugin {
		internal static new ManualLogSource Logger;
		internal static readonly Version Ver = Assembly.GetExecutingAssembly().GetName().Version;

		internal static readonly string VersionTag = $"v{Ver.Major}.{Ver.Minor}.{Ver.Build}";

		internal static string GameDir { get; private set; }

		internal static IntPtr hWnd => Helper.GetMainWindowHandle();

		private ConfigPanel configPanel;

		public void Awake() {
			// Plugin startup logic
			Logger = base.Logger;
			Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

			// ~~~\LastOrigin_Data\Managed\UnityEngine.CoreModule.dll
			GameDir = System.IO.Path.GetFullPath(@"./../../../", Assembly.GetCallingAssembly().Location);

			try {
				Enum.GetValues(typeof(ACTOR_CLASS)); // to test game assembly
			} catch {
				Logger.LogError("Failed to find ACTOR_CLASS, seems not installed on LastOrigin or binary changed!");
				return;
			}

			Conf.Migrate();

			StartCoroutine(this.InitUI());

			// AssetLoader
			Symphony.Features.AssetLoader.Load();

			var features = Assembly.GetExecutingAssembly().GetTypes()
				.Where(x => x.GetCustomAttributes(typeof(FeatureAttribute), false).Length > 0);
			foreach(var feat in features) 
				this.gameObject.AddComponent(feat);
		}

		private IEnumerator InitUI() {
			yield return new WaitForEndOfFrame();

			UIManager.Instance = this.gameObject.AddComponent<UIManager>();
			this.configPanel = UIManager.Instance.AddPanel(new ConfigPanel(this));
			this.configPanel.enabled = false;

			StartCoroutine(this.CheckUpdate());
			StartCoroutine(this.CheckReleaseNote());
		}

		public void Update() {
			if (Input.GetKeyDown(KeyCode.F1))
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
				var release = JsonMapper.ToObject<GithubReleaseInfo>(json);
				var tag = release.tag_name;
				if (tag != Plugin.VersionTag && release.assets.Length > 0)
					UIManager.Instance.AddPanel(new UpdateAvailablePanel(this, tag, release.assets));
			} catch (Exception e) {
				Logger.LogError($"[Symphony] Cannot fetch update data: {e.ToString()}");
				yield break;
			}
		}
		private IEnumerator CheckReleaseNote() {
			var req = UnityWebRequest.Get("https://api.github.com/repos/WolfgangKurz/Symphony/releases");
			yield return req.SendWebRequest();

			if (req.result == UnityWebRequest.Result.ProtocolError || req.result == UnityWebRequest.Result.ConnectionError) {
				Plugin.Logger.LogError($"[Symphony] Cannot fetch release data: {req.error}");
				yield break;
			}

			var lastVer = Conf.LastVersionTag.Value;
			if (lastVer.StartsWith("v")) lastVer = lastVer.Substring(1);

			try {
				var json = req.downloadHandler.text;
				var releases = JsonMapper.ToObject<GithubReleaseInfo[]>(json)
					.Where(x => {
						var diff = Helper.IsLesserVersion(lastVer, x.tag_name.Substring(1));
						Logger.LogDebug($"{lastVer}\t{x.tag_name.Substring(1)}\t{diff}");
						return diff;
					})
					.ToArray();

				if (releases.Length > 0)
					UIManager.Instance.AddPanel(new ReleaseNotePanel(this, releases));
			} catch (Exception e) {
				Plugin.Logger.LogError($"[Symphony] Cannot fetch release data: {e.ToString()}");
			}

			Conf.LastVersionTag.Value = Plugin.VersionTag.Substring(1);
		}
	}
}

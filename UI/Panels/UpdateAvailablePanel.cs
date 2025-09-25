using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

using UnityEngine;
using UnityEngine.Networking;

namespace Symphony.UI.Panels {
	internal class UpdateAvailablePanel : UIPanelBase {
		private string newVersion;
		private GithubReleaseInfo.GithubReleaseAsset[] downloadAssets;

		private bool installing = false;
		private bool installFailed = false;

		public override Rect rc { get; set; } = new Rect(0, 0, 0, 0);

		public UpdateAvailablePanel(MonoBehaviour instance, string newVersion, GithubReleaseInfo.GithubReleaseAsset[] assets) : base(instance) {
			this.newVersion = newVersion;
			this.downloadAssets = assets;
		}

		public override void Update() { }
		public override void OnGUI() {
			var sz = 0f;
			var h = 16f * 3f + 4f + 20f;

			sz = Mathf.Max(sz, GUIX.Heading("Symphony 플러그인에 업데이트가 있습니다").x);
			sz = Mathf.Max(sz, 60 + GUIX.Label(Plugin.VersionTag).x);
			sz = Mathf.Max(sz, 60 + GUIX.Label(this.newVersion).x);

			this.rc = new Rect(Screen.width / 2f - sz / 2f, Screen.height - h - 4f - 4f, sz, h);
			GUIX.Fill(rc.Expand(8, 4), GUIX.Colors.WindowBG);

			GUIX.Group(rc, () => {
				GUIX.Heading(new Rect(0, 0, sz, 16), "Symphony 플러그인에 업데이트가 있습니다");

				GUIX.Label(new Rect(0, 16, 100, 16), "현재 버전:");
				GUIX.Label(new Rect(60, 16, 100, 16), Plugin.VersionTag, Color.yellow);

				GUIX.Label(new Rect(0, 32, 100, 16), "새 버전:");
				GUIX.Label(new Rect(60, 32, 100, 16), this.newVersion, Color.green);

				if (!this.installing) {
					if (GUIX.Button(new Rect(sz - 60 - 4 - 60 - 4 - 60, 52, 60, 20), "Github")) {
						Application.OpenURL($"https://github.com/WolfgangKurz/Symphony/releases/{this.newVersion}");
						this.enabled = false;
					}
					if (GUIX.Button(new Rect(sz - 60 - 4 - 60, 52, 60, 20), "설치")) {
						instance.StartCoroutine(this.Install());
						this.installing = true;
					}
					if (GUIX.Button(
						new Rect(sz - 60, 52, 60, 20),
						"닫기",
						Color.HSVToRGB(0f, 0.6f, 0.6f),
						Color.HSVToRGB(0f, 0.7f, 0.7f),
						Color.HSVToRGB(0f, 0.8f, 0.8f)
					)) {
						this.enabled = false;
					}
				} else if(!this.installFailed) {
					GUIX.Button(
						new Rect(sz - 60, 52, 60, 20),
						"설치중",
						Color.HSVToRGB(0.3f, 0.3f, 0.3f),
						Color.HSVToRGB(0.3f, 0.3f, 0.3f),
						Color.HSVToRGB(0.3f, 0.3f, 0.3f)
					);
				} else {
					GUIX.Label(
						new Rect(sz - 60 - 4 - 60 - 4 - 160, 52, 160, 20),
						"자동 설치에 실패했습니다.",
						Color.red
					);
					if (GUIX.Button(new Rect(sz - 60 - 4 - 60, 52, 60, 20), "Github")) {
						Application.OpenURL($"https://github.com/WolfgangKurz/Symphony/releases/{this.newVersion}");
						this.enabled = false;
					}
					if (GUIX.Button(
						new Rect(sz - 60, 52, 60, 20),
						"닫기",
						Color.HSVToRGB(0f, 0.6f, 0.6f),
						Color.HSVToRGB(0f, 0.7f, 0.7f),
						Color.HSVToRGB(0f, 0.8f, 0.8f)
					)) {
						this.enabled = false;
					}
				}
			});
		}

		private IEnumerator Install() {
			var outputDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			foreach (var asset in this.downloadAssets) {
				var req = UnityWebRequest.Get(asset.browser_download_url);
				yield return req.SendWebRequest();

				if (req.result == UnityWebRequest.Result.ProtocolError || req.result == UnityWebRequest.Result.ConnectionError) {
					Plugin.Logger.LogError($"[Symphony] Cannot fetch update data: {req.error}");
					this.installFailed = true;
					yield break;
				}

				try {
					File.WriteAllBytes(
						Path.Combine(outputDir, asset.name),
						req.downloadHandler.data
					);
				}
				catch (Exception e) {
					Plugin.Logger.LogError($"[Symphony] Cannot fetch update data: {e.ToString()}");
					yield break;
				}
			}

			this.installing = false;
			this.enabled = false;

			// Restart app after update
			var args = Environment.GetCommandLineArgs();
			var pargs = string.Join(" ", args.Skip(1).Select(x => $"\"{x}\"")); // .Skip(1)

			var psi = new ProcessStartInfo {
				FileName = Path.GetFileName(args[0]),
				Arguments = pargs,
				WorkingDirectory = Path.GetDirectoryName(args[0]),
				UseShellExecute = false,
				RedirectStandardError = false,
				RedirectStandardInput = false,
				RedirectStandardOutput = false,
			};
			var keys = psi.Environment.Keys.ToArray();
			foreach (var k in keys) {
				if (k.StartsWith("DOORSTOP"))
					psi.EnvironmentVariables.Remove(k);
			}
			Process.Start(psi);

			Application.Quit();
		}
	}
}

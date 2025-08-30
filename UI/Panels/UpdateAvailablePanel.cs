using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine;

using static UnityEngine.RemoteConfigSettingsHelper;

namespace Symphony.UI.Panels {
	internal class UpdateAvailablePanel : UIPanelBase {
		private string newVersion;

		public override Rect rc { get; set; } = new Rect(0, 0, 0, 0);

		public UpdateAvailablePanel(string newVersion) {
			this.newVersion = newVersion;
		}

		public override void Update() { }
		public override void OnGUI() {
			var sz = 0f;
			var h = 16f * 3f + 4f + 20f;

			sz = Mathf.Max(sz, GUIX.Heading("Symphony 플러그인에 업데이트가 있습니다").x);
			sz = Mathf.Max(sz, 60 + GUIX.Label(Plugin.VersionTag).x);
			sz = Mathf.Max(sz, 60 + GUIX.Label(this.newVersion).x);

			var rc = new Rect(Screen.width / 2f - sz / 2f, Screen.height - h - 4f - 4f, sz, h);
			GUIX.Fill(rc.Expand(8, 4), GUIX.Colors.WindowBG);

			GUIX.Group(rc, () => {
				GUIX.Heading(new Rect(0, 0, sz, 16), "Symphony 플러그인에 업데이트가 있습니다");

				GUIX.Label(new Rect(0, 16, 100, 16), "현재 버전:");
				GUIX.Label(new Rect(60, 16, 100, 16), Plugin.VersionTag, Color.yellow);

				GUIX.Label(new Rect(0, 32, 100, 16), "새 버전:");
				GUIX.Label(new Rect(60, 32, 100, 16), this.newVersion, Color.green);

				if(GUIX.Button(new Rect(sz - 40 - 4 - 140, 52, 140, 20), "Github 페이지로 이동")) {
					Application.OpenURL($"https://github.com/WolfgangKurz/Symphony/releases/{this.newVersion}");
					this.enabled = false;
				}
				if(GUIX.Button(
					new Rect(sz - 40, 52, 40, 20),
					"닫기",
					Color.HSVToRGB(0f, 0.6f, 0.6f),
					Color.HSVToRGB(0f, 0.7f, 0.7f),
					Color.HSVToRGB(0f, 0.8f, 0.8f)
				)) {
					this.enabled = false;
				}
			});
		}
	}
}

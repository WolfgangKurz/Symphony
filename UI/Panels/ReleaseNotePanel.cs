using LitJson;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.Networking;

using static Symphony.UI.GUIX;

namespace Symphony.UI.Panels {
	internal class ReleaseNotePanel : UIPanelBase {
		public override Rect rc { get; set; } = new Rect(0f, 0f, 0f, 0f);

		private Rect panelViewport = new Rect(0, 0, 0, 0);
		private Vector2 panelScroll = Vector2.zero;

		private (string tag, Dictionary<string, string> bodies)[] textReleaseNote = [];
		private string[] langs = [];
		private string currentLang = "";

		public ReleaseNotePanel(MonoBehaviour instance) : base(instance) {
			instance.StartCoroutine(LoadReleaseNote());
		}
		public ReleaseNotePanel(MonoBehaviour instance, GithubReleaseInfo[] releases) : base(instance) {
			this.textReleaseNote = releases.Select(x => (x.tag_name, SplitLangs(x.body.Trim()))).ToArray();
			this.UpdateLangs();
		}

		private Dictionary<string, string> SplitLangs(string body) {
			var ret = new Dictionary<string, string>();
			var parts = body.Split("------------").Select(x => x.Trim());
			foreach (var part in parts) {
				if (part.StartsWith("> ")) {
					var lang = part.Substring(2, part.IndexOf("\n") - 3);
					ret.Add(lang, part.Substring(part.IndexOf("\n") + 1));
				}
				else if (ret.ContainsKey("EN"))
					ret["EN"] += "\n" + part;
				else
					ret.Add("EN", part);
			}
			return ret;
		}

		private IEnumerator LoadReleaseNote() {
			var req = UnityWebRequest.Get("https://api.github.com/repos/WolfgangKurz/Symphony/releases");
			yield return req.SendWebRequest();

			if (req.result == UnityWebRequest.Result.ProtocolError || req.result == UnityWebRequest.Result.ConnectionError) {
				Plugin.Logger.LogError($"[Symphony::ReleaseNotePanel] Cannot fetch release data: {req.error}");
				yield break;
			}

			try {
				var json = req.downloadHandler.text;
				var releases = JsonMapper.ToObject<GithubReleaseInfo[]>(json);

				var lst = new List<(string, Dictionary<string, string>)>();
				for (var i = 0; i < releases.Length; i++) {
					var release = releases[i];

					lst.Add((release.tag_name, SplitLangs(release.body.Trim())));
				}
				this.textReleaseNote = lst.ToArray();
			} catch (Exception e) {
				Plugin.Logger.LogError($"[Symphony::ReleaseNotePanel] Cannot fetch release data: {e.ToString()}");
				yield break;
			}

			this.UpdateLangs();
		}

		private void UpdateLangs() {
			this.langs = this.textReleaseNote.SelectMany(x => x.bodies.Keys).Distinct().ToArray();
			this.currentLang = this.langs.Contains("KR") ? "KR" : "";
		}

		public override void Start() {
			var config = UIManager.Instance.GetPanel<ConfigPanel>();
			if (config != null) config.locked = true;
		}
		public override void OnDestroy() {
			var config = UIManager.Instance.GetPanel<ConfigPanel>();
			if (config != null) config.locked = false;
		}

		public override void Update() {
			var w = Math.Min(Screen.width - 50, 480);
			var h = Screen.height - 50;
			this.rc = new Rect(Screen.width / 2 - w / 2, Screen.height / 2 - h / 2, w, h);
		}
		public override void OnGUI() {
			GUIX.ModalWindow(0, rc, this.PanelContent, "Symphony | Release Note", true);
		}
		private void PanelContent(int id) {
			this.panelViewport.width = rc.width;

			var offset = 0f;
			var panelRect = new Rect(0, 28, rc.width, rc.height - 18 - 28 - 28);
			this.panelScroll = GUIX.ScrollView(panelRect, this.panelScroll, this.panelViewport, false, false, () => {
				GUIX.Group(new Rect(4, 4, rc.width - 8, this.panelViewport.height - 8), () => {
					foreach(var line in this.textReleaseNote) {
						GUIX.Heading(new Rect(0, offset, rc.width - 20 - 8, 20), line.tag, Color.yellow);
						offset += GUIX.Heading(line.tag).y + 4;

						var body = line.bodies.ContainsKey(this.currentLang)
							? line.bodies[this.currentLang]
							: line.bodies.ContainsKey("EN")
								? line.bodies["EN"]
								: line.bodies.FirstOrDefault().Value ?? "";
						var sz = GUIX.Label(body, rc.width - 20 - 8 - 5, wrap: true);
						GUIX.Label(new Rect(5, offset, rc.width - 20 - 8 - 5, sz.y), body, wrap: true);
						offset += sz.y + 4;

						GUIX.HLine(new Rect(0, offset, rc.width - 20 - 8, 0));
						offset += 1 + 4;
					}
				});
			});
			this.panelViewport.height = offset;

			var x = 0f;
			foreach (var lang in this.langs) {
				var w = GUIX.Label(lang).x + 8;

				if (GUIX.Button(new Rect(4 + x, 4, w, 20), lang, this.currentLang == lang ? GUIX.Colors.ButtonHover : null))
					this.currentLang = lang;

				x += w + 4;
			}

			if (GUIX.Button(new Rect(4, rc.height - 18 - 24, rc.width - 8, 20), "닫기")) {
				UIManager.Instance.RemovePanel(this);
			}
		}
	}
}

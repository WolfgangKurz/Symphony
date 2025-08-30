using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using UnityEngine;

namespace Symphony.UI {
	internal class UIManager : MonoBehaviour {
		private GameObject BaseObject;

		private List<UIPanelBase> panels = new();

		private bool displayWelcome = false;

		public void Awake() {
			this.BaseObject = new GameObject("Symphony UIManager");

			StartCoroutine(this.WelcomeMessage());
		}

		public void Update() {
			this.panels.ForEach(p => p.Update());
		}
		public void OnGUI() {
			this.panels.ForEach(p => {
				if (p.enabled)
					p.OnGUI();
			});

			if (this.displayWelcome) {
				var description = "F12 키를 눌러 플러그인 설정을 할 수 있습니다";
				var cw = GUIX.Label(description).x + 4 + 4;

				var w = Screen.width;
				GUIX.Fill(new Rect(w / 2 - cw / 2, 5, cw, 8 + 32), GUIX.Colors.WindowBG);
				GUIX.Group(new Rect(w / 2 - cw / 2 + 4, 5 + 4, cw - 8, 32), () => {
					GUIX.Heading(new Rect(0, 0, 80, 16), "Symphony", Color.yellow);
					GUIX.Label(new Rect(80, 0, cw - 8 - 80, 16), Plugin.VersionTag);
					GUIX.Label(new Rect(0, 16, cw - 8, 16), description);
				});
			}
		}

		private IEnumerator WelcomeMessage() {
			this.displayWelcome = true;
			yield return new WaitForSeconds(5.0f);
			this.displayWelcome = false;
		}

		public T AddPanel<T>(T panel) where T: UIPanelBase {
			this.panels.Add(panel);
			return panel;
		}
	}
}

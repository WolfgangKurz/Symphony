using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.UIElements;

namespace Symphony.UI {
	internal class UIManager : MonoBehaviour {
		public static UIManager Instance { get; internal set; }

		private List<UIPanelBase> panels = new();
		private List<(UIPanelBase obj, Type type)> reservedToRemove = new();

		private bool displayWelcome = false;

		public void Awake() {
			StartCoroutine(this.WelcomeMessage());
			UIManager_Patch.Patch();
		}

		public void Update() {
			this.panels.ForEach(p => p.Update());

			if (this.reservedToRemove.Count > 0) {
				foreach (var rm in this.reservedToRemove) {
					if (rm.obj != null)
						this.RemovePanel(rm.obj);
					else if (rm.type != null)
						this.RemovePanel(rm.type);
				}
				this.reservedToRemove.Clear();
			}

			var bPassThrough = true;
			var pt = Input.mousePosition;
			pt.y = Screen.height - pt.y; // Left-Top start space
			foreach (var panel in panels) {
				if (panel.enabled && panel.rc.Contains(pt)) {
					bPassThrough = false;
					break;
				}
			}
			foreach (var cam in UICamera.list) {
				cam.useTouch = bPassThrough;
				cam.useMouse = bPassThrough;
			}
		}
		public void OnGUI() {
			this.panels.ForEach(p => {
				if (p.enabled)
					p.OnGUI();
			});

			if (this.displayWelcome) {
				var description = "F1 키를 눌러 플러그인 설정을 할 수 있습니다";
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

		public T GetPanel<T>() where T : UIPanelBase {
			return this.panels.Find(x => x.GetType() == typeof(T)) as T;
		}
		public T AddPanel<T>(T panel) where T : UIPanelBase {
			panel.Start();
			this.panels.Add(panel);
			return panel;
		}

		public void RemovePanel<T>(T panel) where T : UIPanelBase {
			panel.OnDestroy();
			this.panels.Remove(panel);
		}
		public void RemovePanel(Type T) {
			var panels = this.panels
				.Where(x => x.GetType() == T)
				.ToArray();
			foreach (var panel in panels) {
				panel.OnDestroy();
				this.panels.Remove(panel);
			}
		}
		public void RemovePanel<T>() where T : UIPanelBase => this.RemovePanel(typeof(T));

		public void ReserveRemovePanel<T>(T panel) where T : UIPanelBase => this.reservedToRemove.Add((panel, null));
		public void ReserveRemovePanel(Type T) => this.reservedToRemove.Add((null, T));
		public void ReserveRemovePanel<T>() where T : UIPanelBase => this.reservedToRemove.Add((null, typeof(T)));
	}
}

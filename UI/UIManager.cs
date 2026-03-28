using Symphony.Features;

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
		private bool assetLoaderStatisticsDisplay = false;

		private readonly Color Color_AssetLoaderStatistics = new(0.06f, 0.06f, 0.06f, 0.54f);
		private readonly Color Color_separator = new Color(0.94f, 0.94f, 0.94f, 0.54f);


		public void Awake() {
			StartCoroutine(this.WelcomeMessage());
			StartCoroutine(this.DisplayAssetLoaded());
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

			if (this.assetLoaderStatisticsDisplay) {
				var total = AssetLoader.AssetStatistics.Found.ToString();
				var loaded = AssetLoader.AssetStatistics.Loaded.ToString();
				var error = AssetLoader.AssetStatistics.Error.ToString();

				var w_total = GUIX.Label(total).x;
				var w_loaded = GUIX.Label(loaded).x;
				var w_error = GUIX.Label(error).x;

				var cw = Mathf.Max(
					GUIX.Heading("AssetLoader").x,
					w_total, w_loaded, w_error
				) + 4 + 4;

				var h = Screen.height;
				GUIX.Fill(new Rect(5, h - 5 - (8 + 32) - 8, cw, 32 + 8), Color_AssetLoaderStatistics);
				GUIX.Group(new Rect(5 + 4, h - 5 - (8 + 32) - 4, cw, 32), () => {
					GUIX.Heading(new Rect(0, 0, 80, 16), "AssetLoader", Color.yellow);

					var x = 0f;
					var sep = GUIX.Label(" / ").x;

					GUIX.Label(new Rect(x, 16, w_total, 16), total, Color.white);
					x += w_total;

					GUIX.Label(new Rect(x, 16, sep, 16), " / ", Color_separator);
					x += sep;

					GUIX.Label(new Rect(x, 16, w_loaded, 16), loaded, Color.green);
					x += w_loaded;

					GUIX.Label(new Rect(x, 16, sep, 16), " / ", Color_separator);
					x += sep;

					GUIX.Label(new Rect(x, 16, w_error, 16), error, Color.red);
				});
			}
		}

		private IEnumerator WelcomeMessage() {
			this.displayWelcome = true;
			yield return new WaitForSeconds(5.0f);
			this.displayWelcome = false;
		}

		private IEnumerator DisplayAssetLoaded() {
			this.assetLoaderStatisticsDisplay = true;
			yield return new WaitForSeconds(5.0f);
			this.assetLoaderStatisticsDisplay = false;
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

using GlobalDefines;

using HarmonyLib;

using Symphony.Features.KeyMapping;
using Symphony.UI;
using Symphony.UI.Panels;

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace Symphony.Features {
	internal class Experimental : MonoBehaviour {
		public void Start() {
			var harmony = new Harmony("Symphony.Experimental");

			KeyMappingConf.Load();
		}

		private const float KEY_MAP_CIRCLE = 13f;
		private static Queue<int> KeyMapping_SimulatingTouchQueue = new();
		IEnumerator KeyMapping_SimulateTouch(float rX, float rY) {
			var uid = UnityEngine.Random.RandomRangeInt(0, int.MaxValue);
			KeyMapping_SimulatingTouchQueue.Enqueue(uid);

			yield return new WaitUntil(() => KeyMapping_SimulatingTouchQueue.Peek() == uid); // wait available turn

			var pt = new Vector2(rX, rY) * new Vector2(Screen.width, Screen.height);

			UICamera.GetInputTouchCount = () => 1;
			UICamera.GetInputTouch = (i) => {
				var t = new UICamera.Touch();
				t.phase = TouchPhase.Began;
				t.fingerId = 0;
				t.position = pt;
				t.tapCount = 1;
				return t;
			};

			{
				var go = Instantiate(PrefabLoader.GetPrefab("UI_click_FX"));
				go.transform.parent = this.transform;
				go.transform.position = UICamera.mainCamera.ScreenToWorldPoint(pt);
				if (
					SingleTon<GameManager>.Instance.CurrentSceneType == eSceneType.LOBBY ||
					SingleTon<GameManager>.Instance.CurrentSceneType == eSceneType.CHARACTERDETAILS
				)
					go.transform.SetChildLayerBookDetail(LayerMask.NameToLayer("UI2"));
			}

			yield return null; // ensure next frame

			UICamera.GetInputTouch = (i) => {
				var t = new UICamera.Touch();
				t.phase = TouchPhase.Ended;
				t.fingerId = 0;
				t.position = pt;
				t.tapCount = 1;
				return t;
			};

			yield return null; // ensure next frame

			UICamera.GetInputTouch = null;
			UICamera.GetInputTouchCount = null;

			KeyMapping_SimulatingTouchQueue.Dequeue();
		}

		public void Update() {
			try {
				foreach (var map in KeyMappingConf.KeyMaps) {
					if (Helper.KeyCodeParse(map.Key, out var kc) && Input.GetKeyDown(kc))
						StartCoroutine(KeyMapping_SimulateTouch(map.X, map.Y));
				}
			} catch (Exception e) {
				Plugin.Logger.LogError(e);
			}
		}

		public void OnGUI() {
			if (UIManager.Instance?.GetPanel<KeyMapPanel>() == null) {
				var KeyMap_Alpha = Conf.Experimental.KeyMapping_Opacity.Value;
				if (KeyMap_Alpha > 0f) {
					for (var i = 0; i < KeyMappingConf.KeyMaps.Length; i++) {
						var map = KeyMappingConf.KeyMaps[i];
						var rcBase = new Rect(map.X * Screen.width, (1f - map.Y) * Screen.height, 0, 0);
						var rcCircle = rcBase.Expand(KEY_MAP_CIRCLE);

						var dup = KeyMappingConf.KeyMaps.Any((x, y) => y != i && x.Key == map.Key);

						GUIX.Circle(rcCircle, dup ? new Color(0.94f, 0.42f, 0.42f, KeyMap_Alpha) : new Color(0.2f, 0.65f, 0.94f, KeyMap_Alpha));
						GUIX.Label(
							rcCircle, map.Key,
							new Color(1f, 1f, 1f, KeyMap_Alpha), fontSize: 12, fontStyle: FontStyle.Bold,
							alignment: TextAnchor.MiddleCenter
						);
					}
				}
			}
		}
	}
}

using LOEventSystem;
using LOEventSystem.Msg;

using Symphony.Features;
using Symphony.Features.KeyMapping;

using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace Symphony.UI.Panels {
	internal class KeyMapPanel : UIPanelBase {
		public override Rect rc { get; set; } = new Rect(0f, 0f, 0f, 0f);

		private int InEdit = -1;
		private int InMoving = -1;
		private KeyMappingData EditingKey = null;
		private Vector2 beforeKeyPos = Vector2.zero;
		private Vector3 lastMousePos = Vector3.zero;

		private const float KEY_MAP_INNER_WIDTH = 44f; // BIG ENOUGH
		private const float KEY_MAP_INNER_HEIGHT = 21.213f / 2f; // INNER = CIRCLE * sqrt(2), half
		private const float KEY_MAP_CIRCLE = 15f;
		private const float KEY_MAP_CIRCLE_POWER2 = KEY_MAP_CIRCLE * KEY_MAP_CIRCLE;

		private string textDescription = string.Join("\n", [ // Use leading/trailing spaces to align to center
			"빈 공간에 좌클릭 : 새 키 생성　　",
			"　 　　키 좌클릭 : 키 편집 　　　",
			"　 　　키 드래그 : 키 위치 이동　",
			"　 　　키 우클릭 : 키 삭제 　　　"
		]);

		private string activeKey => Conf.Experimental.KeyMapping_Active.Value;
		private KeyMappingData[] CurrentKeyMap => KeyMappingConf.KeyMaps[activeKey];

		public KeyMapPanel(MonoBehaviour instance) : base(instance) { }

		public override void Start() {
			var config = UIManager.Instance.GetPanel<ConfigPanel>();
			if (config != null) config.locked = true;

			if (!KeyMappingConf.KeyMaps.ContainsKey(activeKey)) {
				UIManager.Instance.ReserveRemovePanel<KeyMapPanel>();
				return;
			}
		}
		public override void OnDestroy() {
			var config = UIManager.Instance.GetPanel<ConfigPanel>();
			if (config != null) config.locked = false;
		}

		public override void Update() {
			this.rc = new Rect(0, 0, Screen.width, Screen.height);
		}
		public override void OnGUI() {
			var e = Event.current;

			var rcFill = new Rect(0, 0, Screen.width, Screen.height);
			GUIX.Fill(rcFill, new Color(0f, 0f, 0f, 0.53f));

			if (GUIX.Button(
				new Rect(Screen.width / 2 - 60, 4, 120, 20),
				"키 맵 편집 종료"
			)) {
				UIManager.Instance.ReserveRemovePanel<KeyMapPanel>();
				return;
			}

			var pt = Input.mousePosition;
			pt.y = Screen.height - pt.y;
			var sz = GUIX.Label(textDescription);
			var rcDesc = new Rect(Screen.width / 2 - sz.x / 2, 28, sz.x, sz.y);

			var a_desc = 1f;
			if (rcDesc.Expand(20).Contains(pt)) a_desc = 0.333f;
			GUIX.Label(
				rcDesc,
				textDescription,
				new Color(1f, 1f, 1f, a_desc),
				alignment: TextAnchor.UpperCenter
			);

			if (e.type == EventType.MouseDown) {
				var pt_btnCheck = Input.mousePosition;
				pt_btnCheck.y = Screen.height - pt_btnCheck.y;
				if (new Rect(Screen.width / 2 - 60, 4, 120, 20).Contains(pt_btnCheck)) {
					// NOTE: Nothing to do
				}
				else if (Input.GetMouseButtonDown(0)) {
					if (this.InEdit >= 0) {
						var map = this.InEdit >= CurrentKeyMap.Length
								? this.EditingKey
								: CurrentKeyMap[this.InEdit];
						var rcBase = new Rect(map.X * Screen.width, map.Y * Screen.height, 0, 0);
						var d = Mathf.Pow(Input.mousePosition.x - rcBase.x, 2f) +
								Mathf.Pow(Input.mousePosition.y - rcBase.y, 2f);

						if (d >= KEY_MAP_CIRCLE_POWER2) { // not in editing circle
							if (this.InEdit >= CurrentKeyMap.Length) // New item
								KeyMappingConf.Save(
									activeKey,
									new List<KeyMappingData>(CurrentKeyMap) { this.EditingKey }.ToArray()
								);
							else
								KeyMappingConf.Save(
									activeKey,
									CurrentKeyMap
								);

							this.InEdit = -1;
							this.InMoving = -1;
							this.EditingKey = null;
						}
					}
					else {
						var i = Array.FindIndex(CurrentKeyMap, map => {
							var rcBase = new Rect(map.X * Screen.width, map.Y * Screen.height, 0, 0);
							var d = Mathf.Pow(Input.mousePosition.x - rcBase.x, 2f) +
									Mathf.Pow(Input.mousePosition.y - rcBase.y, 2f);
							if (d < KEY_MAP_CIRCLE_POWER2)  // r^2
								return true;
							return false;
						});
						if (i >= 0) {
							var map = CurrentKeyMap[i];

							this.InMoving = i;
							this.beforeKeyPos = new Vector2(map.X, map.Y);
							this.lastMousePos = Input.mousePosition;
						}
						else {
							this.InEdit = CurrentKeyMap.Length;
							this.EditingKey = new KeyMappingData() {
								Key = "",
								X = Input.mousePosition.x / Screen.width,
								Y = Input.mousePosition.y / Screen.height
							};
						}
					}
				}
				else if (Input.GetMouseButtonDown(1) && this.InMoving == -1) {
					var i = Array.FindIndex(CurrentKeyMap, map => {
						var rcBase = new Rect(map.X * Screen.width, map.Y * Screen.height, 0, 0);
						var d = Mathf.Pow(Input.mousePosition.x - rcBase.x, 2f) +
								Mathf.Pow(Input.mousePosition.y - rcBase.y, 2f);
						if (d < KEY_MAP_CIRCLE_POWER2)  // r^2
							return true;
						return false;
					});
					if (i >= 0) {
						KeyMappingConf.Save(
							activeKey,
							CurrentKeyMap.Where((_, x) => x != i).ToArray()
						);

						if (this.InEdit >= 0)
							this.InEdit = -1;
					}
				}
			}
			else if (e.type == EventType.MouseDrag && this.InMoving >= 0) {
				var delta = Input.mousePosition - this.lastMousePos;
				this.lastMousePos = Input.mousePosition;

				CurrentKeyMap[this.InMoving].X += delta.x / Screen.width;
				CurrentKeyMap[this.InMoving].Y += delta.y / Screen.height;
			}
			else if (e.type == EventType.DragExited && this.InMoving >= 0) {
				KeyMappingConf.Save(
					activeKey,
					CurrentKeyMap
				);
				this.InMoving = -1;
			}
			else if (e.type == EventType.MouseUp && this.InMoving >= 0) {
				var map = CurrentKeyMap[this.InMoving];
				if (this.beforeKeyPos == new Vector2(map.X, map.Y)) {
					this.InEdit = this.InMoving;
					this.EditingKey = CurrentKeyMap[this.InEdit];
				}
				this.InMoving = -1;

				KeyMappingConf.Save(
					activeKey,
					CurrentKeyMap
				);
			}

			for (var i = 0; i < CurrentKeyMap.Length; i++) {
				if (this.InEdit == i) continue;

				var map = CurrentKeyMap[i];
				var rcBase = new Rect(map.X * Screen.width, (1f - map.Y) * Screen.height, 0, 0);
				var rcCircle = rcBase.Expand(KEY_MAP_CIRCLE);
				var rcBinder = rcBase.Expand(KEY_MAP_INNER_WIDTH, KEY_MAP_INNER_HEIGHT);

				var dup = CurrentKeyMap.Any((x, y) => y != i && x.Key == map.Key);

				var a = this.InEdit >= 0 ? 0.33f : 1f;

				GUIX.Circle(rcCircle, dup ? new Color(0.94f, 0.42f, 0.42f, a) : new Color(0.2f, 0.65f, 0.94f, a));
				GUIX.Label(
					rcBinder, map.Key,
					new Color(1f, 1f, 1f, a), fontSize: 13, fontStyle: FontStyle.Bold,
					alignment: TextAnchor.MiddleCenter
				);

				var d = Mathf.Pow(Input.mousePosition.x - rcBase.x, 2f) +
						Mathf.Pow(Input.mousePosition.y - (Screen.height - rcBase.y), 2f);
				if (d < KEY_MAP_CIRCLE_POWER2) // r^2
					Helper.ChangeCursor(Helper.CursorType.SizeAll);
			}
			if (this.InEdit >= 0) {
				var map = this.EditingKey;
				var rcBase = new Rect(map.X * Screen.width, (1f - map.Y) * Screen.height, 0, 0);
				var rcCircle = rcBase.Expand(KEY_MAP_CIRCLE);
				var rcBinder = rcBase.Expand(KEY_MAP_INNER_WIDTH, KEY_MAP_INNER_HEIGHT);

				GUIX.Circle(rcCircle, new Color(0.2f, 0.65f, 0.94f));
				GUIX.Fill(rcBinder, GUIX.Colors.FrameBG);
				GUIX.Label(rcBinder, map.Key, fontSize: 14, fontStyle: FontStyle.Bold, alignment: TextAnchor.MiddleCenter);
				if (e.isKey && e.type == EventType.KeyDown) {
					if (e.keyCode == KeyCode.None) {
						var keys = EnumX.GetValues<KeyCode>();
						foreach (var k in keys) {
							if (Input.GetKeyDown(k) && !Helper.IsReservedKey(k)) {
								this.EditingKey.Key = k.ToString();
								break;
							}
						}
					}
					else if (!Helper.IsReservedKey(e.keyCode))
						this.EditingKey.Key = e.keyCode.ToString();
				}
			}
		}
	}
}

using Symphony.UI;

using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine;

namespace Symphony.Features.SimpleUIHelper {
	internal class AdvancedSearchComponent : MonoBehaviour {
		private Vector2 scrollPos = Vector2.zero;
		private Rect scrollRect = new Rect();

		bool isEditing = false;

		public void OnGUI() {
			const float baseRatio = 16f / 9f;
			const float rightWidthRatio = 1f - 0.1635416666f; // 314 / 1920
			const float rightTopRatio = 0.2833333333f; // 306 / 1080
			const float rightBottomRatio = 1f - 0.1851851851f; // 200 / 1080

			var sw = Screen.width;
			var sh = Screen.height;
			var sr = (float)sw / sh; // screen ratio

			var xMin = 0f;
			var yMin = 0f;
			var yMax = 0f;

			if (sr < baseRatio) { // width smaller
				xMin = sw * rightWidthRatio;

				var th = sw / baseRatio;
				var by = sh / 2f - th / 2f;
				yMin = by + th * rightTopRatio;
				yMax = by + th * rightBottomRatio;
			} else { // height smaller
				var tw = sh * baseRatio;

				xMin = sw - (1f - rightWidthRatio) * tw;
				yMin = sh * rightTopRatio;
				yMax = sh * rightBottomRatio;
			}

			this.scrollPos = GUIX.ScrollView(
				Rect.MinMaxRect(xMin, yMin, sw, yMax),
				this.scrollPos,
				this.scrollRect,
				false, false,
				() => {
					var gw = sw - xMin - 10 - 18;

					float offset = 5;
					if (GUIX.Button(
						new Rect(5, 5, gw, 40),
						"필터 편집"
					)) {
						this.isEditing = true;
						// TODO: Open Advanced Search Filter Editor
					}

					offset += 45;
					void Label(ref float offset, string text) {
						var sz = GUIX.Label(text, gw, wrap: true);
						GUIX.Label(
							new Rect( 5,  offset, gw, 20),
							text,
							alignment: TextAnchor.UpperLeft,
							wrap: true
						);
						offset += sz.y + 4;
					}
					void Sep(ref float offset) {
						GUIX.HLine(new Rect(5, offset, gw, 1));
						offset += 4;
					}

					// TODO: Just draw count of filters
					// Label(ref offset, "* 유형 = 경장형");
					// Sep(ref offset);
					// Label(ref offset, "* 역할 = 지원기");
					// Sep(ref offset);
					// Label(ref offset, "* 버프 보유");
					// Label(ref offset, "   - 아군 대상");
					// Label(ref offset, "   - 매 라운드");
					// Label(ref offset, "   - 피해 무효화");

					this.scrollRect.width = gw + 10;
					this.scrollRect.height = offset;
				}
			);
		}
	}
}

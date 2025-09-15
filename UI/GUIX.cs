using System;
using System.Collections.Generic;

using UnityEngine;

namespace Symphony.UI {
	internal static class GUIX {
		public class Colors {
			public static readonly Color Empty = new(0f, 0f, 0f, 0f);

			public static readonly Color WindowBG = new(0.06f, 0.06f, 0.06f, 0.94f);
			public static readonly Color Border = new(0.43f, 0.43f, 0.5f, 0.5f);

			public static readonly Color FrameBG = new(0.16f, 0.29f, 0.48f, 0.54f);
			public static readonly Color FrameBGHover = new(0.26f, 0.59f, 0.98f, 0.4f);
			public static readonly Color FrameBGActive = new(0.26f, 0.59f, 0.98f, 0.67f);

			public static readonly Color TitleBG = new(0.16f, 0.29f, 0.48f);

			public static readonly Color Checkmark = new(0.26f, 0.59f, 0.98f);

			public static readonly Color Button = new(0.26f, 0.59f, 0.98f, 0.4f);
			public static readonly Color ButtonHover = new(0.26f, 0.59f, 0.98f);
			public static readonly Color ButtonActive = new(0.06f, 0.53f, 0.98f);

			public static readonly Color SliderThumb = new(0.24f, 0.52f, 0.88f);
			public static readonly Color SliderThumbActive = new(0.26f, 0.59f, 0.98f);

			public static readonly Color ScrollBG = new(0.02f, 0.02f, 0.02f, 0.53f);
			public static readonly Color ScrollThumb = new(0.31f, 0.31f, 0.31f);
			public static readonly Color ScrollThumbHover = new(0.41f, 0.41f, 0.41f);
			public static readonly Color ScrollThumbActive = new(0.51f, 0.51f, 0.51f);
		}

		private static class SliderState {
			public static int hotControlID = 0;
			public static float dragStartValue;
			public static float dragStartMousePosition;
		}
		private const float SLIDER_THUMB_SIZE = 14f;
		private const float SLIDER_THUMB_PADDING = 2f;

		private static class ScrollbarState {
			public static int hotControlID = 0;
			public static float dragStartScrollPosition;
			public static float dragStartMousePosition;
		}

		private static Texture2D tex_Transparent = new Texture2D(1, 1);
		private static GUIStyle style_Empty = new GUIStyle();

		private static RenderTexture tex_CheckMark;
		private static RenderTexture tex_Circle;

		static GUIX() {
			tex_Transparent.SetPixel(0, 0, Color.clear);
			tex_Transparent.Apply();

			#region CheckMark
			{
				tex_CheckMark = new RenderTexture(30, 30, 0);
				tex_CheckMark.antiAliasing = 1;

				var pad = 30f / 6.0f;
				var sz = 30f - pad * 2f;
				var thickness = Mathf.Max(sz / 5.0f, 1f);
				sz -= thickness * 0.5f;
				var pos = new Vector2(pad + thickness * 0.25f, pad + thickness * 0.25f);

				var third = sz / 3f;
				var bx = pos.x + third;
				var by = pos.y + sz - third * 0.5f;
				var diag = Mathf.Sqrt(2f * Mathf.Pow(thickness * 0.5f, 2f));

				Graphics.SetRenderTarget(tex_CheckMark);

				var shader = Shader.Find("Hidden/Internal-Colored");
				var mat = new Material(shader);
				mat.hideFlags = HideFlags.HideAndDontSave;
				mat.SetPass(0);

				var vert = new Vector3[] {
					// new Vector3(bx - third, by - third),
					new Vector3(bx - third - diag, by - third),
					new Vector3(bx - third, by - third - diag),

					// new Vector3(bx, by),
					new Vector3(bx, by - diag),

					// new Vector3(bx + third * 2f, by - third * 2f),
					new Vector3(bx + third * 2f, by - third * 2f - diag),
					new Vector3(bx + third * 2f + diag, by - third * 2f),

					//
					new Vector3(bx, by + diag),
				};

				GL.Clear(true, true, Colors.Checkmark.AlphaMultiplied(0f));

				GL.PushMatrix();
				GL.LoadPixelMatrix(0, 30, 30, 0);

				GL.Begin(GL.TRIANGLES);
				GL.Color(Colors.Checkmark);

				GL.Vertex(vert[0]); GL.Vertex(vert[1]); GL.Vertex(vert[2]);
				GL.Vertex(vert[2]); GL.Vertex(vert[3]); GL.Vertex(vert[4]);
				GL.Vertex(vert[2]); GL.Vertex(vert[4]); GL.Vertex(vert[5]);
				GL.Vertex(vert[0]); GL.Vertex(vert[2]); GL.Vertex(vert[5]);

				GL.End();

				GL.PopMatrix();

				GameObject.Destroy(mat);

				Graphics.SetRenderTarget(null);
			}
			#endregion
			#region Circle
			{
				var size = 30;
				var half = (float)size / 2f;
				tex_Circle = new RenderTexture(size, size, 0);
				tex_Circle.antiAliasing = 4;

				Graphics.SetRenderTarget(tex_Circle);

				var shader = Shader.Find("Hidden/Internal-Colored");
				var mat = new Material(shader);
				mat.hideFlags = HideFlags.HideAndDontSave;
				mat.SetPass(0);

				var vert = new List<Vector3> {
					new Vector3(half, half)
				};
				for (float i = 0; i < 360f; i += 10f) {
					vert.Add(new Vector3(
						half + Mathf.Cos(Mathf.Deg2Rad * i) * half,
						half + Mathf.Sin(Mathf.Deg2Rad * i) * half
					));
				}

				GL.Clear(true, true, Color.white.AlphaMultiplied(0f));

				GL.PushMatrix();
				GL.LoadPixelMatrix(0, size, size, 0);

				GL.Begin(GL.TRIANGLES);
				GL.Color(Color.white);

				for (int i = 1; i < vert.Count; i++) {
					GL.Vertex(vert[0]);
					GL.Vertex(vert[i]);
					GL.Vertex(vert[i + 1 >= vert.Count ? 1 : i + 1]);
				}

				GL.End();

				GL.PopMatrix();

				GameObject.Destroy(mat);

				Graphics.SetRenderTarget(null);
			}
			#endregion
		}

		public static void DrawCheckmark(Rect rc) {
			var rc_check = rc.Width(rc.height);
			GUI.DrawTexture(rc_check, tex_CheckMark);
		}
		public static void DrawBorder(Rect rc) {
			rc = rc.Shrink(-1);
			GUIX.Fill(rc.Shrink(0, 1, rc.width - 1, 1), Colors.Border); // Left
			GUIX.Fill(rc.Shrink(rc.width - 1, 1, 0, 1), Colors.Border); // Right
			GUIX.Fill(rc.Shrink(0, 0, 0, rc.height - 1), Colors.Border); // Top
			GUIX.Fill(rc.Shrink(0, rc.height - 1, 0, 0), Colors.Border); // Bottom
		}

		public static void DrawGrid33(Rect rc, int idx) {
			var cw = rc.width / 8;
			var ch = rc.height / 8;

			var i = (idx % 3) + 3 * (2 - idx / 3);
			for (var x = 0; x < 3; x++) {
				for (var y = 0; y < 3; y++) {
					var cell = new Rect(rc.xMin + x * cw * 3, rc.yMin + y * ch * 3, cw * 2, ch * 2);

					if (i == x + y * 3)
						GUIX.Fill(cell, Color.white);
					else
						GUIX.DrawBorder(cell.Shrink(1));
				}
			}
		}

		public static void Fill(Rect rc, Color color) {
			var c = GUI.color;

			GUI.color = color;
			GUI.DrawTexture(rc, Texture2D.whiteTexture);

			GUI.color = c;
		}
		public static void Circle(Rect rc, Color color) {
			var c = GUI.color;
			var rc_circle = rc.Width(rc.height);

			GUI.color = color;
			GUI.DrawTexture(rc_circle, tex_Circle);

			GUI.color = c;
		}


		public static void Group(Rect rc, Action perform) {
			GUI.BeginGroup(rc);
			perform.Invoke();
			GUI.EndGroup();
		}
		public static Rect Window(int id, Rect clientRect, GUI.WindowFunction func, string text, bool resizable = false) {
			return GUI.Window(
				id,
				clientRect.Shrink(-1), // for border rendering
				(id) => {
					var rc = clientRect.ZeroOffset().Move(1, 1);
					GUIX.WindowFrame(rc, text);
					GUI.DragWindow(new Rect(0, 0, rc.width, 24));

					rc = rc.Shrink(0, 18, 0, 0).Shrink(1);
					GUIX.Group(rc, () => func(id));
				},
				text,
				GUIStyle.none
			)
				.Shrink(1)
				.LockInScreen();
		}
		public static Rect ModalWindow(int id, Rect clientRect, GUI.WindowFunction func, string text, bool resizable = false) {
			return GUI.ModalWindow(
				id,
				clientRect.Shrink(-1), // for border rendering
				(id) => {
					var rc = clientRect.ZeroOffset().Move(1, 1);
					GUIX.WindowFrame(rc, text);
					GUI.DragWindow(new Rect(0, 0, rc.width, 24));

					rc = rc.Shrink(0, 18, 0, 0).Shrink(1);
					GUIX.Group(rc, () => func(id));
				},
				text,
				GUIStyle.none
			)
				.Shrink(1)
				.LockInScreen();
		}
		public static void WindowFrame(Rect clientRect, string text) {
			var rc = clientRect;
			GUIX.DrawBorder(rc);
			GUIX.Fill(rc, Colors.WindowBG);

			rc = rc.Height(18);
			GUIX.Fill(rc, Colors.TitleBG);

			var style = new GUIStyle {
				alignment = TextAnchor.MiddleLeft,
				fontSize = 12,
			};
			style.normal.textColor = Color.white;
			GUI.Label(rc.Shrink(4, 2), text, style);
		}

		public static Vector2 ScrollView(
			Rect position, Vector2 scrollPosition, Rect viewRect,
			bool alwaysShowHorizontal, bool alwaysShowVertical,
			Action action
		) {
			var drawHorz = alwaysShowHorizontal || viewRect.width > position.width;
			var drawVert = alwaysShowVertical || viewRect.height > position.height;

			var rc = position.Shrink(0, 0, drawVert ? 14 : 0, drawHorz ? 14 : 0);
			GUI.BeginScrollView(
				rc, scrollPosition, viewRect,
				alwaysShowHorizontal, alwaysShowVertical,
				GUIStyle.none, GUIStyle.none
			);

			action?.Invoke();

			// wheel scrolling
			var e = Event.current;
			if (e.type == EventType.ScrollWheel) {
				var ret = new Vector2(
					Mathf.Clamp(scrollPosition.x + e.delta.x * 3f, 0f, viewRect.width - position.width),
					Mathf.Clamp(scrollPosition.y + e.delta.y * 3f, 0f, viewRect.height - position.height)
				);
				e.Use();
				return ret;
			}

			GUI.EndScrollView();

			// draw scrollbars if needed
			if (drawHorz) {
				return new(
					GUIX.ScrollbarHorizontal(
						Rect.MinMaxRect(
							position.xMin,
							position.yMax - 15, // include border
							position.xMax - (drawVert ? 15 : 0),
							position.yMax - 1
						),
						scrollPosition.x,
						position.width,
						viewRect.width
					),
					scrollPosition.y
				);
			}
			if (drawVert) {
				return new(
					scrollPosition.x,
					GUIX.ScrollbarVertical(
						Rect.MinMaxRect(
							position.xMax - 15, // include border
							position.yMin,
							position.xMax - 1,
							position.yMax - (drawHorz ? 15 : 0)
						),
						scrollPosition.y,
						position.height,
						viewRect.height
					)
				);
			}

			return scrollPosition;
		}

		public static float ScrollbarHorizontal(
			Rect rc,
			float scrollPosition, float width, float contentWidth,
			Color? normal = null, Color? hover = null, Color? active = null
		) {
			var controlID = GUIUtility.GetControlID(FocusType.Passive);
			var e = Event.current;

			var rcThumb = Rect.MinMaxRect(
				rc.xMin + scrollPosition / contentWidth * rc.width,
				rc.yMin,
				rc.xMin + (scrollPosition + width) / contentWidth * rc.width,
				rc.yMax
			).Shrink(2);

			switch (e.GetTypeForControl(controlID)) {
				case EventType.MouseDown:
					if (e.button == 0 && rcThumb.Contains(e.mousePosition)) {
						GUIUtility.hotControl = controlID;
						ScrollbarState.hotControlID = controlID;
						ScrollbarState.dragStartMousePosition = e.mousePosition.x;
						ScrollbarState.dragStartScrollPosition = scrollPosition;
						e.Use();
					}
					break;
				case EventType.MouseDrag:
					if (GUIUtility.hotControl == controlID) {
						var mouseDelta = e.mousePosition.x - ScrollbarState.dragStartMousePosition;
						var scrollDelta = (mouseDelta / rc.width) * contentWidth;

						scrollPosition = Mathf.Clamp(
							ScrollbarState.dragStartScrollPosition + scrollDelta,
							0,
							contentWidth - width
						);
						e.Use();
					}
					break;
				case EventType.MouseUp:
					if (GUIUtility.hotControl == controlID) {
						GUIUtility.hotControl = 0;
						e.Use();
					}
					break;

				case EventType.Repaint:
					GUIX.Fill(rc, Colors.ScrollBG);

					if (rcThumb.width <= 0 || rcThumb.height <= 0) break;

					var f_hover = false;
					var f_active = false;
					if (GUIUtility.hotControl == controlID)
						f_active = true;
					else if (rc.Contains(e.mousePosition))
						f_hover = true;

					var color = !f_hover && !f_active
						? normal ?? Colors.ScrollThumb
						: f_active
							? active ?? Colors.ScrollThumbActive
							: hover ?? Colors.ScrollThumbHover;

					if (rcThumb.width < rcThumb.height) { // smaller then circle
						var c = GUI.color; // just draw ellipse
						GUI.color = color;
						GUI.DrawTexture(rcThumb, tex_Circle);
						GUI.color = c;
					}
					else {
						GUI.BeginClip(rcThumb.Width(rcThumb.height / 2)); // Left side of thumb
						GUIX.Circle(new Rect(0, 0, rcThumb.height, rcThumb.height), color);
						GUI.EndClip();

						GUIX.Fill(rcThumb.Shrink(rcThumb.height / 2f, 0f), color); // Draw center

						// Right side of thumb
						GUI.BeginClip(Rect.MinMaxRect(rcThumb.xMax - rcThumb.height / 2, rcThumb.yMin, rc.xMax, rcThumb.yMax));
						GUIX.Circle(new Rect(-rcThumb.height / 2, 0, rcThumb.height, rcThumb.height), color);
						GUI.EndClip();
					}
					break;
			}

			return scrollPosition;
		}
		public static float ScrollbarVertical(
			Rect rc,
			float scrollPosition, float height, float contentHeight,
			Color? normal = null, Color? hover = null, Color? active = null
		) {
			var controlID = GUIUtility.GetControlID(FocusType.Passive);
			var e = Event.current;

			var rcThumb = Rect.MinMaxRect(
				rc.xMin,
				rc.yMin + scrollPosition / contentHeight * rc.height,
				rc.xMax,
				rc.yMin + (scrollPosition + height) / contentHeight * rc.height
			).Shrink(2);

			switch (e.GetTypeForControl(controlID)) {
				case EventType.MouseDown:
					if (e.button == 0 && rcThumb.Contains(e.mousePosition)) {
						GUIUtility.hotControl = controlID;
						ScrollbarState.hotControlID = controlID;
						ScrollbarState.dragStartMousePosition = e.mousePosition.y;
						ScrollbarState.dragStartScrollPosition = scrollPosition;
						e.Use();
					}
					break;
				case EventType.MouseDrag:
					if (GUIUtility.hotControl == controlID) {
						var mouseDelta = e.mousePosition.y - ScrollbarState.dragStartMousePosition;
						var scrollDelta = (mouseDelta / rc.height) * contentHeight;

						scrollPosition = Mathf.Clamp(
							ScrollbarState.dragStartScrollPosition + scrollDelta,
							0,
							contentHeight - height
						);
						e.Use();
					}
					break;
				case EventType.MouseUp:
					if (GUIUtility.hotControl == controlID) {
						GUIUtility.hotControl = 0;
						e.Use();
					}
					break;

				case EventType.Repaint:
					GUIX.Fill(rc, Colors.ScrollBG);

					if (rcThumb.width <= 0 || rcThumb.height <= 0) break;

					var f_hover = false;
					var f_active = false;
					if (GUIUtility.hotControl == controlID)
						f_active = true;
					else if (rc.Contains(e.mousePosition))
						f_hover = true;

					var color = !f_hover && !f_active
						? normal ?? Colors.ScrollThumb
						: f_active
							? active ?? Colors.ScrollThumbActive
							: hover ?? Colors.ScrollThumbHover;

					if (rcThumb.height < rcThumb.width) { // smaller then circle
						var c = GUI.color; // just draw ellipse
						GUI.color = color;
						GUI.DrawTexture(rcThumb, tex_Circle);
						GUI.color = c;
					}
					else {
						GUI.BeginClip(rcThumb.Height(rcThumb.width / 2)); // Top side of thumb
						GUIX.Circle(new Rect(0, 0, rcThumb.width, rcThumb.width), color);
						GUI.EndClip();

						GUIX.Fill(rcThumb.Shrink(0, rcThumb.width / 2f), color); // Draw center

						// Bottom side of thumb
						GUI.BeginClip(Rect.MinMaxRect(rcThumb.xMin, rcThumb.yMax - rcThumb.width / 2, rc.xMax, rcThumb.yMax));
						GUIX.Circle(new Rect(0, -rcThumb.width / 2, rcThumb.width, rcThumb.width), color);
						GUI.EndClip();
					}
					break;
			}

			return scrollPosition;
		}

		public static bool Button(Rect rc, string text, Color? normal = null, Color? hover = null, Color? active = null) {
			var ret = GUI.Button(rc, "", GUIStyle.none);

			var f_hover = false;
			var f_active = false;
			if (rc.Contains(Event.current.mousePosition))
				f_hover = true;
			if (f_hover && Input.GetMouseButton(0))
				f_active = true;

			var color = !f_hover && !f_active
				? normal ?? Colors.Button
				: f_hover && !f_active
					? hover ?? Colors.ButtonHover
					: active ?? Colors.ButtonActive;
			GUIX.Fill(rc, color);

			if (!string.IsNullOrWhiteSpace(text)) {
				var style = new GUIStyle {
					alignment = TextAnchor.MiddleCenter,
					fontSize = 13,
				};
				style.normal.textColor = Color.white;
				GUI.Label(rc, text, style);
			}

			return ret;
		}
		public static bool Toggle(Rect rc, bool value, string text) {
			var ret = GUI.Toggle(rc, value, "", GUIStyle.none);

			var hover = false;
			var active = false;
			if (rc.Contains(Event.current.mousePosition))
				hover = true;
			if (hover && Input.GetMouseButton(0))
				active = true;

			var color = !hover && !active
				? Colors.FrameBG
				: hover && !active
					? Colors.FrameBGHover
					: Colors.FrameBGActive;
			GUIX.Fill(rc.Width(rc.height), color);

			if (value) {
				GUIX.DrawCheckmark(rc);
			}

			if (!string.IsNullOrWhiteSpace(text)) {
				var style = new GUIStyle {
					alignment = TextAnchor.MiddleLeft,
					fontSize = 13,
				};
				style.normal.textColor = Color.white;
				GUI.Label(rc.Shrink(rc.height + 5, 0, 0, 0), text, style);
			}

			return ret;
		}
		public static bool Radio(Rect rc, bool isChecked, string text) {
			var ret = GUI.Button(rc, "", GUIStyle.none);

			var hover = false;
			var active = false;
			if (rc.Contains(Event.current.mousePosition))
				hover = true;
			if (hover && Input.GetMouseButton(0))
				active = true;

			var color = !hover && !active
				? Colors.FrameBG
				: hover && !active
					? Colors.FrameBGHover
					: Colors.FrameBGActive;
			GUIX.Circle(rc.Width(rc.height).Shrink(2), color);

			if (isChecked) {
				GUIX.Circle(rc.Shrink(5), Colors.Checkmark);
			}

			if (!string.IsNullOrWhiteSpace(text)) {
				var style = new GUIStyle {
					alignment = TextAnchor.MiddleLeft,
					fontSize = 13,
				};
				style.normal.textColor = Color.white;
				GUI.Label(rc.Shrink(rc.height + 5, 0, 0, 0), text, style);
			}

			return ret;
		}

		public static string TextField(Rect rc, string text, TextAnchor alignment = TextAnchor.MiddleLeft) {
			var style = new GUIStyle(GUIStyle.none) { alignment = alignment };
			style.normal.textColor = Color.white;
			style.hover.textColor = Color.white;
			style.active.textColor = Color.white;
			style.fontSize = 12;
			style.padding = new RectOffset(4, 4, 4, 4);
			GUIX.Fill(rc, Colors.FrameBG);

			var box = rc; // .Clip(GUIClip.topmostRect);
			if (box.Contains(Event.current.mousePosition))
				Helper.ChangeCursor(Helper.CursorType.IBeam);
			return GUI.TextField(rc, text, style);
		}

		public static float HorizontalSlider(
			Rect rc, float value, float leftValue, float rightValue,
			Func<float, string> template = null,
			Color? normal = null, Color? hover = null, Color? active = null,
			Color? thumb_normal = null, Color? thumb_active = null
		) {
			var controlID = GUIUtility.GetControlID(FocusType.Passive);
			var e = Event.current;

			var trackWidth = rc.width - SLIDER_THUMB_SIZE - SLIDER_THUMB_PADDING * 2;
			var valueRatio = rightValue == leftValue ? 0
				: rightValue - leftValue > 0
					? (value - leftValue) / (rightValue - leftValue)
					: (value - rightValue) / (leftValue - rightValue);
			var thumbX = rc.xMin + valueRatio * trackWidth;
			var rcThumb = Rect.MinMaxRect(thumbX, rc.yMin, thumbX + SLIDER_THUMB_SIZE + SLIDER_THUMB_PADDING * 2, rc.yMax);
			// SLIDER_THUMB_PADDING will be removed when draw

			switch (e.GetTypeForControl(controlID)) {
				case EventType.MouseDown:
					if (e.button == 0 && rc.Contains(e.mousePosition)) {
						GUIUtility.hotControl = controlID;

						var clickPosInTrack = e.mousePosition.x - rc.x - (SLIDER_THUMB_SIZE / 2f + SLIDER_THUMB_PADDING);
						var clickRatio = trackWidth > 0 ? clickPosInTrack / trackWidth : 0;
						var val = leftValue + clickRatio * (rightValue - leftValue);
						value = Mathf.Clamp(val, Mathf.Min(leftValue, rightValue), Mathf.Max(leftValue, rightValue));

						SliderState.hotControlID = controlID;
						SliderState.dragStartMousePosition = e.mousePosition.x;
						SliderState.dragStartValue = value;
						e.Use();
					}
					break;
				case EventType.MouseDrag:
					if (GUIUtility.hotControl == controlID) {
						var mouseDelta = e.mousePosition.x - SliderState.dragStartMousePosition;
						var valueDelta = trackWidth > 0 ? (mouseDelta / trackWidth) * (rightValue - leftValue) : 0;
						var val = SliderState.dragStartValue + valueDelta;
						value = Mathf.Clamp(val, Mathf.Min(leftValue, rightValue), Mathf.Max(leftValue, rightValue));
						e.Use();
					}
					break;
				case EventType.MouseUp:
					if (GUIUtility.hotControl == controlID) {
						GUIUtility.hotControl = 0;
						e.Use();
					}
					break;

				case EventType.Repaint:
					var f_hover = false;
					var f_active = false;
					if (GUIUtility.hotControl == controlID)
						f_active = true;
					else if (rc.Contains(e.mousePosition))
						f_hover = true;

					var color = !f_hover && !f_active
						? normal ?? Colors.FrameBG
						: f_hover && !f_active
							? hover ?? Colors.FrameBGHover
							: active ?? Colors.FrameBGActive;
					GUIX.Fill(rc, color);

					var th_color = !f_active
						? normal ?? Colors.SliderThumb
						: active ?? Colors.SliderThumbActive;
					GUIX.Fill(rcThumb.Shrink(SLIDER_THUMB_PADDING), th_color);

					var label = (template ?? (v => v.ToString()))?.Invoke(value) ?? "";
					GUIX.Label(rc, label, Color.white, TextAnchor.MiddleCenter);
					break;
			}

			return value;
		}
		public static float VerticalSlider(
			Rect rc, float value, float topValue, float bottomValue,
			Func<float, string> template = null,
			Color? normal = null, Color? hover = null, Color? active = null,
			Color? thumb_normal = null, Color? thumb_active = null
		) {
			var controlID = GUIUtility.GetControlID(FocusType.Passive);
			var e = Event.current;

			var trackHeight = rc.height - SLIDER_THUMB_SIZE - SLIDER_THUMB_PADDING * 2;
			var valueRatio = topValue == bottomValue ? 0
				: bottomValue - topValue > 0
					? (value - topValue) / (bottomValue - topValue)
					: (value - bottomValue) / (topValue - bottomValue);
			var thumbY = rc.yMin + valueRatio * trackHeight;
			var rcThumb = Rect.MinMaxRect(rc.xMin, thumbY, rc.xMax, thumbY + SLIDER_THUMB_SIZE + SLIDER_THUMB_PADDING * 2);
			// SLIDER_THUMB_PADDING will be removed when draw

			switch (e.GetTypeForControl(controlID)) {
				case EventType.MouseDown:
					if (e.button == 0 && rc.Contains(e.mousePosition)) {
						GUIUtility.hotControl = controlID;

						var clickPosInTrack = e.mousePosition.y - rc.y - (SLIDER_THUMB_SIZE / 2f + SLIDER_THUMB_PADDING);
						var clickRatio = trackHeight > 0 ? clickPosInTrack / trackHeight : 0;
						var val = topValue + clickRatio * (bottomValue - topValue);
						value = Mathf.Clamp(val, Mathf.Min(topValue, bottomValue), Mathf.Max(topValue, bottomValue));

						SliderState.hotControlID = controlID;
						SliderState.dragStartMousePosition = e.mousePosition.x;
						SliderState.dragStartValue = value;
						e.Use();
					}
					break;
				case EventType.MouseDrag:
					if (GUIUtility.hotControl == controlID) {
						var mouseDelta = e.mousePosition.y - SliderState.dragStartMousePosition;
						var valueDelta = trackHeight > 0 ? (mouseDelta / trackHeight) * (bottomValue - topValue) : 0;
						var val = SliderState.dragStartValue + valueDelta;
						value = Mathf.Clamp(val, Mathf.Min(topValue, bottomValue), Mathf.Max(topValue, bottomValue));
						e.Use();
					}
					break;
				case EventType.MouseUp:
					if (GUIUtility.hotControl == controlID) {
						GUIUtility.hotControl = 0;
						e.Use();
					}
					break;

				case EventType.Repaint:
					var f_hover = false;
					var f_active = false;
					if (GUIUtility.hotControl == controlID)
						f_active = true;
					else if (rc.Contains(e.mousePosition))
						f_hover = true;

					var color = !f_hover && !f_active
						? normal ?? Colors.FrameBG
						: f_hover && !f_active
							? hover ?? Colors.FrameBGHover
							: active ?? Colors.FrameBGActive;
					GUIX.Fill(rc, color);

					var th_color = !f_active
						? normal ?? Colors.SliderThumb
						: active ?? Colors.SliderThumbActive;
					GUIX.Fill(rcThumb.Shrink(SLIDER_THUMB_PADDING), th_color);

					var label = (template ?? (v => v.ToString()))?.Invoke(value) ?? "";
					GUIX.Label(rc, label, Color.white, TextAnchor.MiddleCenter);
					break;
			}

			return value;
		}

		public static void KeyBinder(string id, Rect rc, string key, Action<KeyCode> onChange) {
			var ec = Event.current;
			GUI.SetNextControlName(id);
			if (GUI.GetNameOfFocusedControl() == id) {
				if (ec.isKey && ec.type == EventType.KeyDown) {
					if (ec.keyCode == KeyCode.None) {
						var keys = EnumX.GetValues<KeyCode>();
						foreach (var k in keys) {
							if (Input.GetKeyDown(k) && !Helper.IsReservedKey(k)) {
								onChange.Invoke(k);
								break;
							}
						}
					}
					else if (!Helper.IsReservedKey(ec.keyCode))
						onChange.Invoke(ec.keyCode);
				}
			}
			GUIX.TextField(rc, key, TextAnchor.MiddleCenter);
		}
		public static void KeyBinder(string id, Rect rc, KeyCode key, Action<KeyCode> onChange) {
			var ec = Event.current;
			GUI.SetNextControlName(id);
			if (GUI.GetNameOfFocusedControl() == id) {
				if (ec.isKey && ec.type == EventType.KeyDown) {
					if (ec.keyCode == KeyCode.None) {
						var keys = EnumX.GetValues<KeyCode>();
						foreach (var k in keys) {
							if (Input.GetKeyDown(k) && !Helper.IsReservedKey(k)) {
								onChange.Invoke(k);
								break;
							}
						}
					}
					else if (!Helper.IsReservedKey(ec.keyCode))
						onChange.Invoke(ec.keyCode);
				}
			}
			GUIX.TextField(rc, key.ToString(), TextAnchor.MiddleCenter);
		}

		public static Vector2 Heading(Rect rc, string text, Color? color = null, TextAnchor alignment = TextAnchor.MiddleLeft) {
			var content = new GUIContent(text);
			var style = new GUIStyle {
				alignment = alignment,
				fontSize = 13,
				fontStyle = FontStyle.Bold,
			};
			style.normal.textColor = color ?? Color.white;
			GUI.Label(rc, content, style);
			return style.CalcSize(content);
		}
		public static Vector2 Heading(string text) {
			var content = new GUIContent(text);
			var style = new GUIStyle {
				fontSize = 13,
				fontStyle = FontStyle.Bold,
			};
			return style.CalcSize(content);
		}
		public static Vector2 Label(Rect rc, string text, Color? color = null, TextAnchor alignment = TextAnchor.MiddleLeft) {
			var content = new GUIContent(text);
			var style = new GUIStyle {
				alignment = alignment,
				fontSize = 12,
			};
			style.normal.textColor = color ?? Color.white;
			GUI.Label(rc, content, style);
			return style.CalcSize(content);
		}
		public static Vector2 Label(string text) {
			var content = new GUIContent(text);
			var style = new GUIStyle { fontSize = 12 };
			return style.CalcSize(content);
		}

		public static void HLine(Rect rc, Color? color = null) {
			GUIX.Fill(rc.Height(1), color ?? Colors.Border);
		}
		public static void VLine(Rect rc, Color? color = null) {
			GUIX.Fill(rc.Width(1), color ?? Colors.Border);
		}

		public static void DrawAtlasSprite(Rect rc, UIAtlas atlas, string spriteName) => GUIX.DrawSpriteData(rc, atlas, atlas.GetSprite(spriteName));
		public static void DrawSpriteData(Rect rc, UIAtlas atlas, UISpriteData sprite) {
			if (atlas == null || sprite == null) return;

			var tex = atlas.texture;

			// src corods (0.0 ~ 1.0)
			var coords = Rect.MinMaxRect(
				(float)sprite.x / tex.width,
				1.0f - (float)(sprite.y + sprite.height) / tex.height,
				(float)(sprite.x + sprite.width) / tex.width,
				1.0f - (float)sprite.y / tex.height
			);

			var padWidth = sprite.width + sprite.paddingLeft + sprite.paddingRight;
			var padHeight = sprite.height + sprite.paddingTop + sprite.paddingBottom;
			var ratioX = rc.width / padWidth;
			var ratioY = rc.height / padHeight;

			var outX = sprite.paddingLeft * ratioX;
			var outY = sprite.paddingTop * ratioY;
			var rcDest = new Rect(
				rc.x + outX, rc.y + outY,
				sprite.width * ratioX,
				sprite.height * ratioY
			);
			GUI.DrawTextureWithTexCoords(rc, tex, coords);
		}
	}
}

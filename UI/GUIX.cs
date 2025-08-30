using Symphony.Features;

using System;

using UnityEngine;

namespace Symphony.UI {
	internal static class GUIX {
		public class Colors {
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
		}


		private static RenderTexture tex_CheckMark;
		static GUIX() {
			#region CheckMark
			{
				tex_CheckMark = new RenderTexture(30, 30, 0);

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
	}
}

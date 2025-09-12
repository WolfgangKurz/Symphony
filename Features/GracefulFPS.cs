using Symphony.UI;

using System.Collections;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Symphony.Features {
	internal class GracefulFPS : MonoBehaviour {
		private FrameLimit DisplayFPSLimit = new(0.2f);

		private GUIStyle FPSStyle = new() {
			alignment = TextAnchor.MiddleCenter,
			normal = new() {
				textColor = Color.white,
			},
			fontSize = 13,
			fontStyle = FontStyle.Bold,
		};
		private string lastFPS = "0";

		private static FrameLimit FramerateLimit = new(1f);
		private static bool Ready = false;

		private static int OrigFramerate = -1;
		private static int OrigVSyncCount = 0;


		public void Start() {
			StartCoroutine(this.Init());
		}

		private IEnumerator Init() {
			yield return new WaitForSecondsRealtime(1f); // wait 1secs to get game's vanilla value

			OrigFramerate = Application.targetFrameRate;
			OrigVSyncCount = QualitySettings.vSyncCount;
			Ready = true;

			ApplyFPS();
		}

		public void Update() {
			if (Conf.GracefulFPS.DisplayFPS.Value && DisplayFPSLimit.Valid()) {
				lastFPS = (1.0f / Time.deltaTime).ToString("0.0");

				Helper.SetWindowTitle(Plugin.hWnd, "LastOrigin_VFUNKR - " + lastFPS + " FPS");
			}
		}

		public static void ApplyFPS() {
			if (!Ready) return;

			if (SceneManager.GetActiveScene().name == "Scene_StageBattle") {
				if (Conf.GracefulFPS.LimitBattleFPS.Value == "Fixed" && Conf.GracefulFPS.MaxBattleFPS.Value > 0) {
					Plugin.Logger.LogInfo($"[Symphony::SimpleTweak] Set framerate limit to {Conf.GracefulFPS.MaxBattleFPS.Value} (Battle)");
					Application.targetFrameRate = Conf.GracefulFPS.MaxBattleFPS.Value;
					QualitySettings.vSyncCount = 0;
					return;
				}
				else if (Conf.GracefulFPS.LimitBattleFPS.Value == "VSync") {
					Plugin.Logger.LogInfo($"[Symphony::SimpleTweak] Set framerate limit to VSync (Battle)");
					QualitySettings.vSyncCount = 1;
					return;
				}
			}

			if (Conf.GracefulFPS.LimitFPS.Value == "Fixed" && Conf.GracefulFPS.MaxFPS.Value > 0) {
				Plugin.Logger.LogInfo($"[Symphony::SimpleTweak] Set framerate limit to {Conf.GracefulFPS.MaxFPS.Value}");
				Application.targetFrameRate = Conf.GracefulFPS.MaxFPS.Value;
				QualitySettings.vSyncCount = 0;
				return;
			}
			else if (Conf.GracefulFPS.LimitFPS.Value == "VSync") {
				Plugin.Logger.LogInfo($"[Symphony::SimpleTweak] Set framerate limit to VSync");
				QualitySettings.vSyncCount = 1;
				return;
			}

			Plugin.Logger.LogInfo($"[Symphony::SimpleTweak] Set framerate limit to vanilla");
			Application.targetFrameRate = OrigFramerate;
			QualitySettings.vSyncCount = OrigVSyncCount;
		}

		public void OnGUI() { // Draw current FPS
			if (Conf.GracefulFPS.DisplayFPS.Value) {
				GUIX.Fill(new Rect(5, 5, 50, 20), GUIX.Colors.WindowBG);
				GUI.Label(new Rect(5, 5, 50, 20), lastFPS, FPSStyle);
			}
		}
	}
}

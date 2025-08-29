using BepInEx;
using BepInEx.Configuration;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using UnityEngine;

namespace Symphony {
	internal class MaximumFrame : MonoBehaviour {
		private int originalFramerate = -1;
		private int originalVSyncCount = 0;

		private readonly ConfigFile config = new ConfigFile(Path.Combine(Paths.ConfigPath, "Symphony.MaximumFrame.cfg"), true);
		private ConfigEntry<int> maximumFrame;

		public void Awake() {
			this.maximumFrame = this.config.Bind("MaximumFrame", "maximumFrame", -1, "Sets maximum frame limit, -1 means uses vanilla framerate");
		}

		public void Update() {
			this.Check_Framerate();
		}

		private float lastTime = 0f;
		private void Check_Framerate() {
			var cur = Time.realtimeSinceStartup;
			if (cur - this.lastTime < 5.0f) return;
			this.lastTime = cur;

			if (this.originalFramerate == -1) {
				this.originalFramerate = Application.targetFrameRate;
				this.originalVSyncCount = QualitySettings.vSyncCount;
			}

			this.config.Reload();
			if (maximumFrame.Value != -1 && maximumFrame.Value < 1)
				maximumFrame.Value = 1;

			if (maximumFrame.Value != -1) { // framerate has set
				if (Application.targetFrameRate != maximumFrame.Value || QualitySettings.vSyncCount != 0) { // should update
					Application.targetFrameRate = maximumFrame.Value;
					QualitySettings.vSyncCount = 0;
					Plugin.Logger.LogInfo(
						$"[Symphony::MaximumFrame] Set framerate limit to {maximumFrame.Value}" +
						(this.originalVSyncCount > 0 ? ", VSync also disabled" : "")
					);
				}
			}
			else { // framerate has not set (use vanilla)
				if (Application.targetFrameRate != this.originalFramerate || QualitySettings.vSyncCount != this.originalVSyncCount) {
					Application.targetFrameRate = this.originalFramerate;
					QualitySettings.vSyncCount = this.originalVSyncCount;
					Plugin.Logger.LogInfo($"[Symphony::MaximumFrame] Set framerate limit to vanilla");
				}
			}
		}
	}
}

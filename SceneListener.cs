using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine.SceneManagement;

namespace Symphony {
	internal class SceneListener {
		public static SceneListener Instance { get; } = new();
		private static object lockChange = new object();
		private static object lockEnter = new object();
		private static object lockExit = new object();

		public delegate void SceneChanged(string previous, string @new);
		public delegate void SceneEnter();
		public delegate void SceneExit();

		private List<SceneChanged> listenersChange = new();
		private Dictionary<string, List<SceneEnter>> listenersEnter = new();
		private Dictionary<string, List<SceneExit>> listenersExit = new();
		private SceneListener() {
			SceneManager.activeSceneChanged += (prev, @new) => {
				var namePrev = prev.name ?? "";
				var nameNew = @new.name ?? "";
				Plugin.Logger.LogDebug($"[Symphony::SceneListener] Scene change detected, was '{namePrev}', to '{nameNew}'");

				lock (lockExit) {
					if (this.listenersExit.ContainsKey(namePrev)) {
						var listeners = this.listenersExit[namePrev];
						foreach (var fn in listeners)
							fn();
					}
				}

				lock (lockEnter) {
					if (this.listenersEnter.ContainsKey(nameNew)) {
						var listeners = this.listenersEnter[nameNew];
						foreach (var fn in listeners)
							fn();
					}
				}

				lock(lockChange) {
					var listeners = this.listenersChange;
					foreach (var fn in listeners)
						fn(namePrev, nameNew);
				}
			};
		}

		public void On(SceneChanged action) {
			lock (lockChange) {
				this.listenersChange.Add(action);
			}
		}
		public void Off(SceneChanged action) {
			lock (lockChange) {
				this.listenersChange.Remove(action);
			}
		}


		public void OnEnter(string name, SceneEnter action) {
			lock (lockEnter) {
				if (!this.listenersEnter.ContainsKey(name))
					this.listenersEnter.Add(name, new());

				this.listenersEnter[name].Add(action);
			}
		}
		public void OffEnter(string name, SceneEnter action) {
			lock (lockEnter) {
				if (!this.listenersEnter.ContainsKey(name)) return;
				this.listenersEnter[name].Remove(action);
			}
		}

		public void OnExit(string name, SceneExit action) {
			lock (lockExit) {
				if (!this.listenersExit.ContainsKey(name))
					this.listenersExit.Add(name, new());

				this.listenersExit[name].Add(action);
			}
		}
		public void OffExit(string name, SceneExit action) {
			lock (lockExit) {
				if (!this.listenersExit.ContainsKey(name)) return;
				this.listenersExit[name].Remove(action);
			}
		}
	}
}

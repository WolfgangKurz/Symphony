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

		private Dictionary<string, List<Action<string>>> listenersChange = new();
		private Dictionary<string, List<Action>> listenersEnter = new();
		private Dictionary<string, List<Action>> listenersExit = new();
		private SceneListener() {
			SceneManager.activeSceneChanged += (prev, _new) => {
				var namePrev = prev.name ?? "";
				var nameNew = _new.name ?? "";
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
			};
		}

		public void On(string name, Action<string> action) {
			lock (lockChange) {
				if (!this.listenersChange.ContainsKey(name))
					this.listenersChange.Add(name, new());

				this.listenersChange[name].Add(action);
			}
		}
		public void Off(string name, Action<string> action) {
			lock (lockChange) {
				if (!this.listenersChange.ContainsKey(name)) return;
				this.listenersChange[name].Remove(action);
			}
		}


		public void OnEnter(string name, Action action) {
			lock (lockEnter) {
				if (!this.listenersEnter.ContainsKey(name))
					this.listenersEnter.Add(name, new());

				this.listenersEnter[name].Add(action);
			}
		}
		public void OffEnter(string name, Action action) {
			lock (lockEnter) {
				if (!this.listenersEnter.ContainsKey(name)) return;
				this.listenersEnter[name].Remove(action);
			}
		}

		public void OnExit(string name, Action action) {
			lock (lockExit) {
				if (!this.listenersExit.ContainsKey(name))
					this.listenersExit.Add(name, new());

				this.listenersExit[name].Add(action);
			}
		}
		public void OffExit(string name, Action action) {
			lock (lockExit) {
				if (!this.listenersExit.ContainsKey(name)) return;
				this.listenersExit[name].Remove(action);
			}
		}
	}
}

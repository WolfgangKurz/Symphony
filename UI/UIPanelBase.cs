using UnityEngine;

namespace Symphony.UI {
	internal abstract class UIPanelBase {
		public abstract Rect rc { get; set; }
		public bool enabled;
		protected MonoBehaviour instance { get; }

		public UIPanelBase(MonoBehaviour instance) {
			this.instance = instance;
			this.enabled = true;
		}

		public abstract void Update();
		public abstract void OnGUI();
	}
}

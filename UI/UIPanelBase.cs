using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine;

namespace Symphony.UI {
	internal abstract class UIPanelBase {
		public abstract Rect rc { get; set; }
		public bool enabled;

		public UIPanelBase() {
			this.enabled = true;
		}

		public abstract void Update();
		public abstract void OnGUI();
	}
}

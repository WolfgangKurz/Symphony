using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine;

namespace Symphony {
	/// <summary>
	/// Simple class to limit processing based on time
	/// </summary>
	internal class FrameLimit {
		private float t = 0f;
		private float period = 0f;

		public FrameLimit(float period) {
			this.period = period;
		}

		public bool Valid() {
			var cur = Time.realtimeSinceStartup;
			if (cur - this.t < this.period) return false;

			this.t = cur;
			return true;
		}
		public void Reset() {
			this.t = 0f;
		}
	}
}

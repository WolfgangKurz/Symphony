using System.Collections.Generic;

using UnityEngine;

namespace Symphony {
	internal class TempStorageComponent : MonoBehaviour {
		public Dictionary<string, object> data { get; } = new();
	}
}

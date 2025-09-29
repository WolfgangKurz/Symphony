using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Symphony.AdvancedSearchGUI.Model {
	internal static class LevelHelper {
		public static int[] GetValues() => new int[120].Select((_, i) => i + 1).ToArray(); // 1 ~ 120
	}
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace Symphony.AdvancedSearchGUI.Model {
	public enum ConditionCompare_Numeric : byte {
		Equal,
		NotEqual,
		Less,
		LessEqual,
		Bigger,
		BiggerEqual,
		FromTo, // A ... B (includes A and B)
	}
	internal static class ConditionCompareHelper_Numeric {
		private static readonly Dictionary<ConditionCompare_Numeric, string> table = new Dictionary<ConditionCompare_Numeric, string>() {
			{ ConditionCompare_Numeric.Equal,       "＝" },
			{ ConditionCompare_Numeric.NotEqual,    "≠" },
			{ ConditionCompare_Numeric.Less,        "＜" },
			{ ConditionCompare_Numeric.LessEqual,   "≤" },
			{ ConditionCompare_Numeric.Bigger,      "＞" },
			{ ConditionCompare_Numeric.BiggerEqual, "≥" },
			{ ConditionCompare_Numeric.FromTo,      "∼" },
		};

		public static string Convert(ConditionCompare_Numeric compare) {
			if (table.ContainsKey(compare))
				return table[compare];
			return compare.ToString();
		}
		public static ConditionCompare_Numeric Convert(string name) {
			if (table.ContainsValue(name))
				return table.FirstOrDefault(x => x.Value == name).Key;
			return ConditionCompare_Numeric.Equal;
		}

		public static string[] GetValues()
			=> Enum.GetValues(typeof(ConditionCompare_Numeric))
				.Cast<ConditionCompare_Numeric>()
				.Select(Convert)
				.ToArray();
	}

	public enum ConditionCompare_Equal {
		Equal,
		NotEqual,
	}
	internal static class ConditionCompareHelper_Equal{
		private static readonly Dictionary<ConditionCompare_Equal, string> table = new Dictionary<ConditionCompare_Equal, string>() {
			{ ConditionCompare_Equal.Equal,       "＝" },
			{ ConditionCompare_Equal.NotEqual,    "≠" },
		};

		public static string Convert(ConditionCompare_Equal compare) {
			if (table.ContainsKey(compare))
				return table[compare];
			return compare.ToString();
		}
		public static ConditionCompare_Equal Convert(string name) {
			if (table.ContainsValue(name))
				return table.FirstOrDefault(x => x.Value == name).Key;
			return ConditionCompare_Equal.Equal;
		}

		public static string[] GetValues()
			=> Enum.GetValues(typeof(ConditionCompare_Equal))
				.Cast<ConditionCompare_Equal>()
				.Select(Convert)
				.ToArray();
	}

	public enum ConditionCompare_Active {
		Any = 0,
		Active1 = 1,
		Active2 = 2,
	}
	internal static class ConditionCompareHelper_Active {
		private static readonly Dictionary<ConditionCompare_Active, string> table = new Dictionary<ConditionCompare_Active, string>() {
			{ ConditionCompare_Active.Any,     "아무 액티브" },
			{ ConditionCompare_Active.Active1, "액티브 1" },
			{ ConditionCompare_Active.Active2, "액티브 2" },
		};

		public static string Convert(ConditionCompare_Active compare) {
			if (table.ContainsKey(compare))
				return table[compare];
			return compare.ToString();
		}
		public static ConditionCompare_Active Convert(string name) {
			if (table.ContainsValue(name))
				return table.FirstOrDefault(x => x.Value == name).Key;
			return ConditionCompare_Active.Any;
		}

		public static ConditionCompare_Active[] GetValues()
			=> Enum.GetValues(typeof(ConditionCompare_Active))
				.Cast<ConditionCompare_Active>()
				.ToArray();
	}

	public enum ConditionCompare_Target {
		Any = 0,
		Team = 1,
		Enemy = 2,
	}
	internal static class ConditionCompareHelper_Target {
		private static readonly Dictionary<ConditionCompare_Target, string> table = new Dictionary<ConditionCompare_Target, string>() {
			{ ConditionCompare_Target.Any,   "아무 대상" },
			{ ConditionCompare_Target.Team,  "아군 대상" },
			{ ConditionCompare_Target.Enemy, "적군 대상" },
		};

		public static string Convert(ConditionCompare_Target compare) {
			if (table.ContainsKey(compare))
				return table[compare];
			return compare.ToString();
		}
		public static ConditionCompare_Target Convert(string name) {
			if (table.ContainsValue(name))
				return table.FirstOrDefault(x => x.Value == name).Key;
			return ConditionCompare_Target.Any;
		}

		public static ConditionCompare_Target[] GetValues()
			=> Enum.GetValues(typeof(ConditionCompare_Target))
				.Cast<ConditionCompare_Target>()
				.ToArray();
	}
}

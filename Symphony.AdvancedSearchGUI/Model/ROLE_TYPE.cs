using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Symphony.AdvancedSearchGUI.Model {
	internal enum ROLE_TYPE : int {
		TANKER,
		NUKER,
		SUPPORTER,
	}
	internal static class RoleTypeHelper {
		private static readonly Dictionary<ROLE_TYPE, string> table = new Dictionary<ROLE_TYPE, string>() {
			{ ROLE_TYPE.NUKER,     "공격기" },
			{ ROLE_TYPE.TANKER,    "보호기" },
			{ ROLE_TYPE.SUPPORTER, "지원기" },
		};

		public static string Convert(ROLE_TYPE value) {
			if (table.ContainsKey(value))
				return table[value];
			return value.ToString();
		}
		public static ROLE_TYPE Convert(string name) {
			if (table.ContainsValue(name))
				return table.FirstOrDefault(x => x.Value == name).Key;
			return ROLE_TYPE.NUKER;
		}

		public static ROLE_TYPE[] GetValues()
			=> Enum.GetValues(typeof(ROLE_TYPE))
				.Cast<ROLE_TYPE>()
				.ToArray();
	}
	internal class RoleTypeConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			if (value is ROLE_TYPE role)
				return RoleTypeHelper.Convert(role);
			return value.ToString();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			if (value is string name)
				return RoleTypeHelper.Convert(name);
			return ROLE_TYPE.NUKER;
		}
	}
}

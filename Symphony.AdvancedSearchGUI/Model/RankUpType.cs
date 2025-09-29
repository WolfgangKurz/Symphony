using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Symphony.AdvancedSearchGUI.Model {
	internal enum RankUpType : int {
		Initial, // 기본 등급
		Maximum, // 최대 등급
		B,
		A,
		S,
		SS,
	}
	internal static class RankUpTypeHelper {
		private static readonly Dictionary<RankUpType, string> table = new Dictionary<RankUpType, string>() {
			{ RankUpType.Initial, "기본 등급" },
			{ RankUpType.Maximum, "최대 등급" },
			{ RankUpType.B,       "B" },
			{ RankUpType.A,       "A" },
			{ RankUpType.S,       "S" },
			{ RankUpType.SS,      "SS" },
		};

		public static string Convert(RankUpType value) {
			if (table.ContainsKey(value))
				return table[value];
			return value.ToString();
		}
		public static RankUpType Convert(string name) {
			if (table.ContainsValue(name))
				return table.FirstOrDefault(x => x.Value == name).Key;
			return RankUpType.Initial;
		}

		public static RankUpType[] GetValues()
			=> Enum.GetValues(typeof(RankUpType))
				.Cast<RankUpType>()
				.ToArray();
	}
	internal class RankUpTypeConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			if (value is RankUpType RankUpType)
				return RankUpTypeHelper.Convert(RankUpType);
			return value.ToString();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			if (value is string name)
				return RankUpTypeHelper.Convert(name);
			return RankUpType.Initial;
		}
	}
}

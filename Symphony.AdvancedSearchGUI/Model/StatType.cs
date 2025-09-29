using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Symphony.AdvancedSearchGUI.Model {
	internal enum StatType : int {
		ATK,       // 공격력
		DEF,       // 방어력
		HP,        // 체력
		ACC,       // 적중률
		EVA,       // 회피율
		CRI,       // 치명타
		SPD,       // 행동력
		Res_Fire,  // 화염 저항
		Res_Frost, // 냉기 저항
		Res_Elec,  // 전기 저항
	}
	internal static class StatTypeHelper {
		private static readonly Dictionary<StatType, string> table = new Dictionary<StatType, string>() {
			{ StatType.ATK,       "공격력" },
			{ StatType.DEF,       "방어력" },
			{ StatType.HP,        "체력" },
			{ StatType.ACC,       "적중률" },
			{ StatType.EVA,       "회피율" },
			{ StatType.CRI,       "치명타" },
			{ StatType.SPD,       "행동력" },
			{ StatType.Res_Fire,  "화염 저항" },
			{ StatType.Res_Frost, "냉기 저항" },
			{ StatType.Res_Elec,  "전기 저항" },
		};

		public static string Convert(StatType value) {
			if (table.ContainsKey(value))
				return table[value];
			return value.ToString();
		}
		public static StatType Convert(string name) {
			if (table.ContainsValue(name))
				return table.FirstOrDefault(x => x.Value == name).Key;
			return StatType.ATK;
		}

		public static StatType[] GetValues()
			=> Enum.GetValues(typeof(StatType))
				.Cast<StatType>()
				.ToArray();
	}
	internal class StatTypeConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			if (value is StatType StatType)
				return StatTypeHelper.Convert(StatType);
			return value.ToString();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			if (value is string name)
				return StatTypeHelper.Convert(name);
			return StatType.ATK;
		}
	}
}

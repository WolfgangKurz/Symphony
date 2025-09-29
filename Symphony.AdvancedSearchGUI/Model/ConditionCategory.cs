using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Symphony.AdvancedSearchGUI.Model {
	internal enum ConditionCategory : ushort {
		Invalid,
		Rarity,
		Class,
		Role,
		Body,
		Stat,
		Active_Target,
		Active_NoGuard,
		Active_Grid,
		Enum,
		Buff,
		BuffName,
	}

	internal static class ConditionCategoryHelper {
		private static readonly Dictionary<ConditionCategory, string> table = new Dictionary<ConditionCategory, string>() {
			{ ConditionCategory.Rarity,         "등급" },
			{ ConditionCategory.Class,          "유형" },
			{ ConditionCategory.Role,           "역할" },
			{ ConditionCategory.Body,           "신체" },
			{ ConditionCategory.Stat,           "스탯" },
			{ ConditionCategory.Active_Target,  "액티브 대상" },
			{ ConditionCategory.Active_NoGuard, "보호 무시" },
			{ ConditionCategory.Active_Grid,    "그리드 지정" },
			{ ConditionCategory.Enum,           "공격 속성" },
			{ ConditionCategory.Buff,           "버프 보유" },
			{ ConditionCategory.BuffName,       "버프 보유 (이름)" },
		};

		public static string Convert(ConditionCategory category) {
			if (table.ContainsKey(category))
				return table[category];
			return category.ToString();
		}
		public static ConditionCategory Convert(string name) {
			if (table.ContainsValue(name))
				return table.FirstOrDefault(x => x.Value == name).Key;
			return ConditionCategory.Invalid;
		}

		public static ConditionCategory[] GetValues(bool forNew) {
			var enumerable = Enum.GetValues(typeof(ConditionCategory))
				.Cast<ConditionCategory>();

			if (forNew) return enumerable.ToArray();
			return enumerable
				.Where(x => x != ConditionCategory.Invalid)
				.ToArray();
		}
	}
	internal class ConditionCategoryConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			if (value is ConditionCategory category) 
				return ConditionCategoryHelper.Convert(category);
			return value.ToString();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			if (value is string name)
				return ConditionCategoryHelper.Convert(name);
			return ConditionCategory.Invalid;
		}
	}
}

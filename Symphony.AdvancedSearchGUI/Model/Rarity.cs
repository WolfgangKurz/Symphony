using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Symphony.AdvancedSearchGUI.Model {
	internal enum Rarity : int {
		B = 2,
		A = 3,
		S = 4,
		SS = 5,
	}
	internal static class RarityHelper {
		private static readonly Dictionary<Rarity, string> table = new Dictionary<Rarity, string>() {
			{ Rarity.B,  "B" },
			{ Rarity.A,  "A" },
			{ Rarity.S,  "S" },
			{ Rarity.SS, "SS" },
		};

		public static string Convert(Rarity value) {
			if (table.ContainsKey(value))
				return table[value];
			return value.ToString();
		}
		public static Rarity Convert(string name) {
			if (table.ContainsValue(name))
				return table.FirstOrDefault(x => x.Value == name).Key;
			return Rarity.SS;
		}

		public static Rarity[] GetValues()
			=> Enum.GetValues(typeof(Rarity))
				.Cast<Rarity>()
				.ToArray();
	}
	internal class RarityConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			if (value is Rarity rarity)
				return RarityHelper.Convert(rarity);
			return value.ToString();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			if (value is string name)
				return RarityHelper.Convert(name);
			return Rarity.SS;
		}
	}
}

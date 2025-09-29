using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Symphony.AdvancedSearchGUI.Model {
	internal enum ACTOR_CLASS : int {
		TROOPER,
		ARMORED,
		MOBILITY,
	}
	internal static class ActorClassHelper {
		private static readonly Dictionary<ACTOR_CLASS, string> table = new Dictionary<ACTOR_CLASS, string>() {
			{ ACTOR_CLASS.TROOPER,  "경장형" },
			{ ACTOR_CLASS.MOBILITY, "기동형" },
			{ ACTOR_CLASS.ARMORED,  "중장형" },
		};

		public static string Convert(ACTOR_CLASS value) {
			if (table.ContainsKey(value))
				return table[value];
			return value.ToString();
		}
		public static ACTOR_CLASS Convert(string name) {
			if (table.ContainsValue(name))
				return table.FirstOrDefault(x => x.Value == name).Key;
			return ACTOR_CLASS.TROOPER;
		}

		public static ACTOR_CLASS[] GetValues()
			=> Enum.GetValues(typeof(ACTOR_CLASS))
				.Cast<ACTOR_CLASS>()
				.ToArray();
	}
	internal class ActorClassConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			if (value is ACTOR_CLASS _class)
				return ActorClassHelper.Convert(_class);
			return value.ToString();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			if (value is string name)
				return ActorClassHelper.Convert(name);
			return ACTOR_CLASS.TROOPER;
		}
	}
}

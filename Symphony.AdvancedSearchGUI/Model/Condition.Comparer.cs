using CommunityToolkit.Mvvm.ComponentModel;

using System;

namespace Symphony.AdvancedSearchGUI.Model.Interface {
	public interface IConditionComparer {
		Type ValueType { get; }

		bool Compare(object Value);
	}
	public abstract class ConditionComparer<TCompareType, TValue> : ObservableObject, IConditionComparer where TCompareType : Enum {
		protected TCompareType _compareType;
		public TCompareType CompareType {
			get => this._compareType;
			set => SetProperty(ref this._compareType, value);
		}
		public abstract string CompareTypeAccessor { get; set; }

		public Type ValueType => typeof(TValue);

		public abstract bool Compare(TValue Value);

		public bool Compare(object Value) {
			if (Value is TValue tValue)
				return this.Compare(tValue);

			throw new ArgumentException("Invalid Condition value type");
		}
	}
}

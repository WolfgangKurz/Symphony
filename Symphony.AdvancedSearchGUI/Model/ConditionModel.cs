using CommunityToolkit.Mvvm.ComponentModel;

using Symphony.AdvancedSearchGUI.Model.Interface;

namespace Symphony.AdvancedSearchGUI.Model {
	#region Enums
	public enum ConditionConnectorType {
		OR,
		AND,
	}

	#endregion

	internal class ConditionModel : ObservableObject {
		private ConditionConnectorType _connector = ConditionConnectorType.OR;
		public ConditionConnectorType Connector {
			get => this._connector;
			set => SetProperty(ref this._connector, value);
		}

		private ConditionCategory _category = ConditionCategory.Invalid;
		public ConditionCategory Category {
			get => this._category;
			set => SetProperty(ref this._category, value);
		}

		private IConditionComparer _comparer = null;
		public IConditionComparer Comparer {
			get => this._comparer;
			set => SetProperty(ref this._comparer, value);
		}
	}

	#region Condition Implements
	internal class ConditionComparer_Rarity : ConditionComparer<ConditionCompare_Numeric, Rarity> {
		public override string CompareTypeAccessor {
			get => ConditionCompareHelper_Numeric.Convert(this.CompareType);
			set => this.CompareType = ConditionCompareHelper_Numeric.Convert(value);
		}

		private Rarity _Value1 = Rarity.SS, _Value2 = Rarity.B; // SS, B
		public Rarity Value1 {
			get => this._Value1;
			set => SetProperty(ref this._Value1, value);
		}
		public Rarity Value2 {
			get => this._Value2;
			set => SetProperty(ref this._Value2, value);
		}

		public override bool Compare(Rarity Value) {
			switch (this.CompareType) {
				case ConditionCompare_Numeric.Equal:
					return Value == this.Value1;
				case ConditionCompare_Numeric.NotEqual:
					return Value != this.Value1;
				case ConditionCompare_Numeric.Less:
					return Value < this.Value1;
				case ConditionCompare_Numeric.LessEqual:
					return Value <= this.Value1;
				case ConditionCompare_Numeric.Bigger:
					return Value > this.Value1;
				case ConditionCompare_Numeric.BiggerEqual:
					return Value >= this.Value1;
				case ConditionCompare_Numeric.FromTo:
					return Value >= this.Value2 && Value <= this.Value1;
			}
			return false;
		}
	}
	internal class ConditionComparer_Class : ConditionComparer<ConditionCompare_Equal, ACTOR_CLASS> {
		public override string CompareTypeAccessor {
			get => ConditionCompareHelper_Equal.Convert(this.CompareType);
			set => this.CompareType = ConditionCompareHelper_Equal.Convert(value);
		}

		private ACTOR_CLASS _value = ACTOR_CLASS.TROOPER;
		public ACTOR_CLASS Value {
			get => this._value;
			set => SetProperty(ref this._value, value);
		}

		public override bool Compare(ACTOR_CLASS Value) {
			return this.CompareType == ConditionCompare_Equal.Equal
				? this.Value == Value
				: this.Value != Value;
		}
	}
	internal class ConditionComparer_Role : ConditionComparer<ConditionCompare_Equal, ROLE_TYPE> {
		public override string CompareTypeAccessor {
			get => ConditionCompareHelper_Equal.Convert(this.CompareType);
			set => this.CompareType = ConditionCompareHelper_Equal.Convert(value);
		}

		private ROLE_TYPE _value = ROLE_TYPE.NUKER;
		public ROLE_TYPE Value {
			get => this._value;
			set => SetProperty(ref this._value, value);
		}

		public override bool Compare(ROLE_TYPE Value) {
			return this.CompareType == ConditionCompare_Equal.Equal
				? this.Value == Value
				: this.Value != Value;
		}
	}
	internal class ConditionComparer_Body : ConditionComparer<ConditionCompare_Equal, ACTOR_BODY_TYPE> {
		public override string CompareTypeAccessor {
			get => ConditionCompareHelper_Equal.Convert(this.CompareType);
			set => this.CompareType = ConditionCompareHelper_Equal.Convert(value);
		}

		private ACTOR_BODY_TYPE _value = ACTOR_BODY_TYPE.ANDROID;
		public ACTOR_BODY_TYPE Value {
			get => this._value;
			set => SetProperty(ref this._value, value);
		}

		public override bool Compare(ACTOR_BODY_TYPE Value) {
			return this.CompareType == ConditionCompare_Equal.Equal
				? this.Value == Value
				: this.Value != Value;
		}
	}
	internal class ConditionComparer_Stat: ConditionComparer<ConditionCompare_Numeric, UNIT_STAT> {
		public override string CompareTypeAccessor {
			get => ConditionCompareHelper_Numeric.Convert(this.CompareType);
			set => this.CompareType = ConditionCompareHelper_Numeric.Convert(value);
		}

		private StatType _statType;
		public StatType StatType {
			get => this._statType;
			set => SetProperty(ref this._statType, value);
		}

		private RankUpType _rankUpType;
		public RankUpType RankUpType {
			get => this._rankUpType;
			set => SetProperty(ref this._rankUpType, value);
		}

		private float _Value1 = 0f, _Value2 = 0f;
		public float Value1 {
			get => this._Value1;
			set => SetProperty(ref this._Value1, value);
		}
		public float Value2 {
			get => this._Value2;
			set => SetProperty(ref this._Value2, value);
		}

		public override bool Compare(UNIT_STAT target) {
			float Value = float.MinValue;
			switch (this._statType ){
				case StatType.ATK:
					Value = target.ATK;
					break;
					case StatType.DEF:
					Value = target.DEF;
					break;
				case StatType.HP:
					Value = target.HP;
					break;
				case StatType.ACC:
					Value = target.ACC;
					break;
				case StatType.EVA:
					Value = target.EVA;
					break;
				case StatType.CRI:
					Value = target.CRI;
					break;
				case StatType.SPD:
					Value = target.SPD;
					break;
				case StatType.Res_Fire:
					Value = target.Res_Fire;
					break;
				case StatType.Res_Frost:
					Value = target.Res_Frost;
					break;
				case StatType.Res_Elec:
					Value = target.Res_Elec;
					break;
			}

			switch (this.CompareType) {
				case ConditionCompare_Numeric.Equal:
					return Value == this.Value1;
				case ConditionCompare_Numeric.NotEqual:
					return Value != this.Value1;
				case ConditionCompare_Numeric.Less:
					return Value < this.Value1;
				case ConditionCompare_Numeric.LessEqual:
					return Value <= this.Value1;
				case ConditionCompare_Numeric.Bigger:
					return Value > this.Value1;
				case ConditionCompare_Numeric.BiggerEqual:
					return Value >= this.Value1;
				case ConditionCompare_Numeric.FromTo:
					return Value >= this.Value2 && Value <= this.Value1;
			}
			return false;
		}
	}
	internal class ConditionComparer_Active_Target : ConditionComparer<ConditionCompare_Equal, TARGET_TYPE /* Skill.GetTargetType() */> {
		public override string CompareTypeAccessor {
			get => ConditionCompareHelper_Equal.Convert(this.CompareType);
			set => this.CompareType = ConditionCompareHelper_Equal.Convert(value);
		}

		private ConditionCompare_Active _active = ConditionCompare_Active.Any;
		private ConditionCompare_Target _target = ConditionCompare_Target.Any;
		public ConditionCompare_Active Active  {
			get => this._active;
			set => SetProperty(ref this._active, value);
		}
		public ConditionCompare_Target Target {
			get => this._target;
			set => SetProperty(ref this._target, value);
		}

		public override bool Compare(TARGET_TYPE Value) {
			if (this.CompareType == ConditionCompare_Equal.Equal) {
				if (this.Target == ConditionCompare_Target.Any) return true;
				return Value.IsForTeam() == (this.Target == ConditionCompare_Target.Team);
			}
			else {
				if (this.Target == ConditionCompare_Target.Any) return false;
				return Value.IsForTeam() != (this.Target == ConditionCompare_Target.Team);
			}
		}
	}
	internal class ConditionComparer_Active_NoGuard : ConditionComparer<ConditionCompare_Equal, int /* Skill.GuardPierce */> {
		public override string CompareTypeAccessor {
			get => ConditionCompareHelper_Equal.Convert(this.CompareType);
			set => this.CompareType = ConditionCompareHelper_Equal.Convert(value);
		}

		private ConditionCompare_Active _active = ConditionCompare_Active.Any;
		public ConditionCompare_Active Active {
			get => this._active;
			set => SetProperty(ref this._active, value);
		}

		public override bool Compare(int Value) {
			return this.CompareType == ConditionCompare_Equal.Equal
				? Value != 0
				: Value == 0;
		}
	}
	internal class ConditionComparer_Active_Grid : ConditionComparer<ConditionCompare_Equal, TARGET_TYPE /* Skill.GetTargetType() */> {
		public override string CompareTypeAccessor {
			get => ConditionCompareHelper_Equal.Convert(this.CompareType);
			set => this.CompareType = ConditionCompareHelper_Equal.Convert(value);
		}

		private ConditionCompare_Active _active = ConditionCompare_Active.Any;
		public ConditionCompare_Active Active {
			get => this._active;
			set => SetProperty(ref this._active, value);
		}

		public override bool Compare(TARGET_TYPE Value) {
			return this.CompareType == ConditionCompare_Equal.Equal
				? Value.IsForGrid()
				: !Value.IsForGrid();
		}
	}
	#endregion
}

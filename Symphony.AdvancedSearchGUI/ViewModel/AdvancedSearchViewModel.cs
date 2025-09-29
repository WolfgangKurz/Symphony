using CommunityToolkit.Mvvm.ComponentModel;

using Symphony.AdvancedSearchGUI.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Symphony.AdvancedSearchGUI.ViewModel {
	internal class AdvancedSearchViewModel : ObservableObject {
		public List<ConditionModel> Conditions { get; } = new List<ConditionModel>();
		public List<ConditionConnectorType> Connectors { get; } = new List<ConditionConnectorType>();

		public AdvancedSearchViewModel() {
			this.Conditions.Add(new ConditionModel() {
				Category = ConditionCategory.Rarity,
				Connector = ConditionConnectorType.AND,
				Comparer = new ConditionComparer_Rarity(),
			});
			this.Conditions.Add(new ConditionModel() {
				Category = ConditionCategory.Class,
				Connector = ConditionConnectorType.OR,
				Comparer = new ConditionComparer_Class(),
			});
			this.Conditions.Add(new ConditionModel() {
				Category = ConditionCategory.Role,
				Connector = ConditionConnectorType.OR,
				Comparer = new ConditionComparer_Role(),
			});
			this.Conditions.Add(new ConditionModel() {
				Category = ConditionCategory.Body,
				Connector = ConditionConnectorType.AND,
				Comparer = new ConditionComparer_Body(),
			});
			this.Conditions.Add(new ConditionModel() {
				Category = ConditionCategory.Stat,
				Connector = ConditionConnectorType.AND,
				Comparer = new ConditionComparer_Stat(),
			});
		}
	}
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Symphony {
	[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
	internal class FeatureAttribute : Attribute {
		readonly string featureName;
		public string PositionalString => this.featureName;

		public FeatureAttribute(string FeatureName) {
			this.featureName = FeatureName;
		}
	}
}

using HarmonyLib;

using Symphony.data;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using UnityEngine;

namespace Symphony.Features {
	internal class FancySkillDesc {
		public static void Install() {
			var harmony = new Harmony("Symphony.FancySkillDesc");
			harmony.Patch(
				AccessTools.Method(typeof(DescriptHelper), nameof(DescriptHelper.GetSkillDesc), [
					typeof(Table_Skill), typeof(Table_SkillLevel), typeof(double[,]), typeof(string), typeof(ClientPcInfo)
				]),
				prefix: new HarmonyMethod(typeof(FancySkillDesc), nameof(FancySkillDesc.GetSkillDesc))
			);
		}

		// #xxx{ or $[!@]xxx
		private static Regex TemplateRegex1 = new(@"#([0-9]+)\{", RegexOptions.Compiled);
		private static Regex TemplateRegex2 = new(@"(\$[!@]?)([0-9]+)\B", RegexOptions.Compiled);
		private static bool GetSkillDesc(
			ref string __result,
			Table_Skill skill,
			Table_SkillLevel skillLevel,
			double[,] attrValue,
			string fullLinkKey = null,
			ClientPcInfo myInfo = null
		) {
			if (!FancySkillDescData.table.ContainsKey(skill.Key)) {
				Plugin.Logger.LogMessage($"Skill {skill.Key} is not ready to be fancy");
				return true;
			}

			var str = skill.SkillDescription.Localize();
			if (str != FancySkillDescData.table[skill.Key].src) {
				Plugin.Logger.LogMessage($"Skill {skill.Key} description is not targeted");
				return true;
			}

			Plugin.Logger.LogMessage($"Skill {skill.Key}, let's fancy!");
			try {
				str = FancySkillDescData.table[skill.Key].to;

				float atkValue;
				{
					var lb = (double)Common.GetLinkBonusValue(myInfo, CORE_LINK_BONUS.STAGE_SKILLRATIO_UP);
					var flb = Common.GetFullLinkBonusValue(fullLinkKey, CORE_LINK_BONUS.STAGE_SKILLRATIO_UP);
					var fb = myInfo.GetFavorBonusValue(CORE_LINK_BONUS.STAGE_SKILLRATIO_UP);
					atkValue = Mathf.Floor(
						Common.FloatParseNotCulture(DataManager.GetAttrPcAttack(attrValue)) *
						(Common.FloatParseNotCulture(skillLevel.SkillAttackRate) + (float)(lb + flb + fb))
					);
				}

				var b_lb = (int)Common.GetLinkBonusValue(myInfo, CORE_LINK_BONUS.STAGE_BUFFLEVEL_UP);
				var b_flb = (int)Common.GetFullLinkBonusValue(fullLinkKey, CORE_LINK_BONUS.STAGE_BUFFLEVEL_UP);
				var b_fb = (int)myInfo.GetFavorBonusValue(CORE_LINK_BONUS.STAGE_BUFFLEVEL_UP);
				int bonusLv = (b_lb + b_flb + b_fb);
				int buffLevel = skillLevel.SkillLevel - 1 + bonusLv;

				var buffs = new List<(float baseValue, float levelValue, int count, float rate, NUM_OUTPUTTYPE numType)>();
				foreach (var buffIdx in skillLevel.BuffEffectIndex) {
					var be = SingleTon<DataManager>.Instance.GetTableBuffEffect(buffIdx);
					var bl = be._dic_BuffDesc;
					for (var i = 0; i < 5; i++) {
						var bk = (i + 1).ToString();
						if (!bl.ContainsKey(bk)) continue;

						var b = bl[bk];
						if (b.BuffIcon == "" && b.BuffEffectType_Desc == "" && b.BuffEffectValue == "0" && b.BuffEffectType == (BUFFEFFECT_TYPE)0)
							continue;

						float rate = 0f;
						switch (i) {
							case 0: rate = float.Parse(be.BuffEffectRate1); break;
							case 1: rate = float.Parse(be.BuffEffectRate2); break;
							case 2: rate = float.Parse(be.BuffEffectRate3); break;
							case 3: rate = float.Parse(be.BuffEffectRate4); break;
							case 4: rate = float.Parse(be.BuffEffectRate5); break;
						}

						float.TryParse(b.BuffEffectValue, out var vb);
						float.TryParse(b.BuffEffectLevelValue, out var vl);
						buffs.Add((vb, vl, b.BuffEffectLeftCount, rate, b.DescNumberType));
					}
				}

				var ret = str.Replace("{0}", Common.COLOR_VALUE + atkValue.ToString() + Common.COLOR_END); // Damage value (yellow)
				while (true) {
					var m = TemplateRegex1.Match(ret);
					if (m.Success) { // #x
						var idx = int.Parse(m.Groups[1].Value);
						switch (idx) {
							case 1: // Condition (orange)
								ret = TemplateRegex1.Replace(ret, Common.COLOR_EXPANDLEVEL_MAX, 1);
								break;
							case 2: // Buff name (skyblue)
								ret = TemplateRegex1.Replace(ret, "[c][42deff]", 1);
								break;
							case 3: // Buff value (cyan)
								ret = TemplateRegex1.Replace(ret, "[c][65f0f0]", 1);
								break;
							case 4: // Note ()
								ret = TemplateRegex1.Replace(ret, Common.COLOR_BLUEGRAY, 1);
								break;
							default:
								ret = TemplateRegex1.Replace(ret, Common.COLOR_WHITE, 1);
								break;
						}
					}
					else {
						m = TemplateRegex2.Match(ret);
						if (m.Success) {
							var type = m.Groups[1].Value;
							var idx = int.Parse(m.Groups[2].Value);

							string buffValue;
							var suffix = buffs[idx].numType == NUM_OUTPUTTYPE.INTEGER ? "" : "%";
							var multiplier = buffs[idx].numType == NUM_OUTPUTTYPE.INTEGER ? 1f : 100f;

							if (type.Length == 1) { // Buff value (yellow)
								buffValue = (
									(buffs[idx].baseValue + buffs[idx].levelValue * buffLevel) *
									multiplier
								).ToString() + suffix;
								ret = TemplateRegex2.Replace(ret, Common.COLOR_VALUE + buffValue + Common.COLOR_END, 1);
							}
							else if (type[1] == '!') { // Buff count (cyan)
								buffValue = buffs[idx].count.ToString();
								ret = TemplateRegex2.Replace(ret, Common.COLOR_BLUE + buffValue + Common.COLOR_END, 1);
							}
							else if (type[1] == '!') { // Buff rate (green)
								buffValue = buffs[idx].count.ToString();
								ret = TemplateRegex2.Replace(ret, Common.COLOR_GREEN + buffValue + Common.COLOR_END, 1);
							}
						}
						else
							break;
					}
				}

				ret = ret.Replace("}", Common.COLOR_END);
				__result = ret;
				return false;
			}catch (Exception e) {
				Plugin.Logger.LogError(e.ToString());
				return true;
			}
		}
	}
}

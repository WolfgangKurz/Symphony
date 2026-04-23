using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using UnityEngine;

	namespace Symphony.Features {
	internal class SimpleUI_FancySkillDesc {
		private static readonly Regex UnitKeyRegex = new Regex(@"^Skill_(.+)_(?:N|CH)_(?:[0-9]+)$", RegexOptions.Compiled);
		private static readonly Regex SkillKeyRegex = new Regex(@"^.+_(N|CH)_([0-9]+)$", RegexOptions.Compiled);

		private static readonly Regex SectionRegex = new(@"\$\$([A-Za-z0-9\-_]+)\$?~(.+?)~\$\$(\1)\$?", RegexOptions.Compiled | RegexOptions.Singleline);
		private static readonly Regex CommentSectionRegex = new(@"^\$\$([A-Za-z0-9\-_]+)(?::([?0-9,\-@]+))?\$?$", RegexOptions.Compiled);
		private static readonly Regex ParamAttrRegex = new(@"param=""\$([0-9]+)(?::([PpNn]))?""", RegexOptions.Compiled);
		private static readonly Regex TagRegex = new(@"<(?<close>/)?(?<name>[A-Za-z0-9_-]+)(?<attrs>[^<>]*?)(?<self>/)?>", RegexOptions.Compiled);
		private static readonly Regex SymbolTokenRegex = new(@"\[\$(?<kind>elem|buff):(?<key>[^\]]+)\]", RegexOptions.Compiled);
		private static readonly string[] BooleanAttributes = [ "r", "rr", "inv", "signless", "floor", "loc" ];

		private static int SectionCounter = 0;
		private static string LastRenderCacheKey = "";
		private static readonly Dictionary<string, RenderCacheEntry> RenderCache = [];
		private static readonly Dictionary<string, float> SymbolAspectCache = [];
		private static INGUIAtlas BuffIconAtlas;

		private static readonly HashSet<string> BuffIconSet = [
			"ACCURACY_DOWN", "ACCURACY_UP", "Action_Number_Change_Down", "Action_Number_Change_Up", "AP_DOWN", "AP_SHIFT", "AP_UP",
			"ARMORED_DMG_DOWN", "ARMORED_DMG_UP", "ATK_DOWN", "ATK_UP", "BARRIER", "BARRIER_PIERCE", "BUFFEFFECTRATE_CHANGE",
			"CHANGE_CHAR", "CHANGE_GRID", "charge", "COLLAPSE", "COUNTER", "CRITICAL_DOWN", "CRITICAL_UP", "Current_Hp_Piercedown",
			"DAMAGE_ABSORB", "DAMAGE_REDUCE", "DamageAmp_Me", "DamageAmp_Opp", "DEBUFF_PERDOWN", "DEBUFF_RATEUP", "DEF_All",
			"DEF_Char", "DEF_DOWN", "DEF_Line", "DEF_PIERCE_UP", "DEF_RESSURRECT", "DEF_Side", "DEF_UP", "Disallow", "EVADE_DOWN",
			"EVADE_UP", "EXP_UP", "FireATK_UP", "FireDMG_DOT", "FIRERES_DOWN", "FIRERES_UP", "FireRes_Value_Fix", "FireRes_Value_Min",
			"FireRes_Value_Reverse", "Guardpierce_Apply", "Guardpierce_No_Apply", "HP_DOWN", "HP_UP", "IceATK_UP", "IceDMG_DOT",
			"ICERES_DOWN", "ICERES_UP", "IceRes_Value_Fix", "IceRes_Value_Min", "IceRes_Value_Reverse", "IMMUNITY_DEBUFF",
			"INVINCIBLE", "LightningATK_UP", "LightningDMG_DOT", "LIGHTNINGRES_DOWN", "LIGHTNINGRES_UP", "LightningRes_Value_Fix",
			"LightningRes_Value_Min", "LightningRes_Value_Reverse", "MARKING", "MOBILITY_DMG_DOWN", "MOBILITY_DMG_UP", "PhyATK_UP",
			"phyDMG_DOT", "PROVOKE", "Pull", "Push", "RANGE_DOWN", "RANGE_UP", "REMOVE_BUFF", "Remove_Buff_Resist", "REMOVE_DEBUFF",
			"SCOUTING", "SEAL_SKILL", "SKILL_DOWN", "SKILL_UP", "SNARE", "Speed_DOWN", "Speed_UP", "STUN", "SUMMON_INSTENV",
			"SUPPORT_ATTACK", "TOGETHER_ATTACK", "TROOPER_DMG_DOWN", "TROOPER_DMG_UP", "VULNERABLE", "White",
		];

		private sealed class CommentData {
			public string Title { get; set; }
			public string Body { get; set; }
			public string RawBody { get; set; }
		}

		private sealed class SymbolSpriteEntry {
			public int StartIndex { get; set; }
			public int PlaceholderLength { get; set; }
			public float Width { get; set; }
			public float Height { get; set; }
			public INGUIAtlas Atlas { get; set; }
			public string SpriteName { get; set; }
			public float X { get; set; }
			public float Y { get; set; }
		}

		private sealed class PlaceholderBuildResult {
			public string Text { get; set; } = "";
			public int DotStartOffset { get; set; }
			public int VisibleLength { get; set; }
			public float PrintedWidth { get; set; }
			public float TargetWidth { get; set; }
		}

		private sealed class RenderCacheEntry {
			public string Text { get; set; } = "";
			public Dictionary<string, CommentData> Actions { get; set; } = [];
		}

		private sealed class RenderContext {
			public Table_SkillLevel SkillLevel;
			public double[,] AttrValue;
			public List<(float BaseValue, float LevelValue, int Count, float Rate)> Buffs { get; set; }
			public int BonusLevel { get; set; }
			public Dictionary<string, CommentData> Comments { get; set; }
		}

		#region SkillDesc Rendering
		internal static bool GetSkillDesc(
			ref string __result,
			Table_Skill skill,
			Table_SkillLevel skillLevel,
			double[,] attrValue,
			string fullLinkKey = null,
			ClientPcInfo myInfo = null
		) {
			if (!Conf.SimpleUI.Use_FancySkillDesc.Value) return true;

			var cacheKey = BuildRenderCacheKey(skill?.Key, skillLevel?.SkillLevel ?? 0);
			if (TryGetRenderedSkillDesc(cacheKey, out var cachedEntry) && cachedEntry != null) {
				__result = cachedEntry.Text ?? "";
				LastRenderCacheKey = cacheKey;
				return false;
			}

			if (!TryGetUnitData(skill.Key, out var data)) {
				Plugin.Logger.LogWarning($"[Symphony::SimpleUI::FancySkillDesc] Failed to render {skill.Key}: Cannot parse unit's key");
				return true;
			}

			if (!TryGetDescriptionKey(skill.Key, data, out var desc)) {
				Plugin.Logger.LogWarning($"[Symphony::SimpleUI::FancySkillDesc] Failed to render {skill.Key}: Not found in database");
				return true;
			}

			try {
				var context = BuildContext(skillLevel, attrValue, fullLinkKey, myInfo);
				__result = RenderDescription(desc, context);
				CacheRenderedSkillDesc(cacheKey, __result, context);
				LastRenderCacheKey = cacheKey;
				return false;
			} catch (Exception e) {
				Plugin.Logger.LogError($"[Symphony::SimpleUI::FancySkillDesc] Failed to render {skill.Key}: {e}");
				return true;
			}
		}

		internal static void Patch_SkillDesc(Panel_CharacterDetails __instance) {
			var lbl = __instance.XGetFieldValue<UILabel>("_lblSkillDesc");
			if (lbl == null) return;

			var binder = lbl.GetComponent<FancySkillDescBinding>();
			if (binder == null)
				binder = lbl.gameObject.AddComponent<FancySkillDescBinding>();
		}

		private static int GetSkillDamage(Table_SkillLevel skillLevel, double[,] attrValue, float bonusValue) {
			var f = Common.FloatParseNotCulture(DataManager.GetAttrPcAttack(attrValue)) *
				(Common.FloatParseNotCulture(skillLevel.SkillAttackRate) + bonusValue);
			return (int)Mathf.Floor(f);
		}

		private static bool TryGetUnitData(string skillKey, out Dictionary<string, string> data) {
			data = null;

			var match = UnitKeyRegex.Match(skillKey);
			if (!match.Success) return false;

			var unitKey = match.Groups[1].Value;
			return Data.FancySkillDesc.Data.TryGetValue(unitKey, out data);
		}

		private static bool TryGetDescriptionKey(string skillKey, Dictionary<string, string> data, out string desc) {
			desc = null;

			var match = SkillKeyRegex.Match(skillKey);
			if (!match.Success) return false;

			var family = match.Groups[1].Value;
			var index = match.Groups[2].Value;
			var dataKey = family == "CH" ? $"F{index}" : index;
			return data.TryGetValue(dataKey, out desc);
		}

		private static RenderContext BuildContext(Table_SkillLevel skillLevel, double[,] attrValue, string fullLinkKey, ClientPcInfo myInfo) {
			// Build buff list
			var buffs = new List<(float BaseValue, float LevelValue, int Count, float Rate)>();
			foreach (var buffIdx in skillLevel.BuffEffectIndex) {
				var be = SingleTon<DataManager>.Instance.GetTableBuffEffect(buffIdx);
				var descs = be._dic_BuffDesc;
				for (var i = 0; i < 5; i++) {
					var key = (i + 1).ToString();
					if (!descs.ContainsKey(key)) continue;

					var desc = descs[key];
					if ( // Except invalid buffs
						desc.BuffIcon == "" &&
						desc.BuffEffectType_Desc == "" &&
						desc.BuffEffectValue == "0" &&
						desc.BuffEffectType == (BUFFEFFECT_TYPE)0
					) continue;

					float rate = i switch {
						0 => ParseFloat(be.BuffEffectRate1),
						1 => ParseFloat(be.BuffEffectRate2),
						2 => ParseFloat(be.BuffEffectRate3),
						3 => ParseFloat(be.BuffEffectRate4),
						4 => ParseFloat(be.BuffEffectRate5),
						_ => 0f,
					};

					buffs.Add((
						ParseFloat(desc.BuffEffectValue),
						ParseFloat(desc.BuffEffectLevelValue),
						desc.BuffEffectLeftCount,
						rate
					));
				}
			}

			var linkBonus = (int)Common.GetLinkBonusValue(myInfo, CORE_LINK_BONUS.STAGE_BUFFLEVEL_UP);
			var fullLinkBonus = (int)Common.GetFullLinkBonusValue(fullLinkKey, CORE_LINK_BONUS.STAGE_BUFFLEVEL_UP);
			var favorBonus = (int?)myInfo?.GetFavorBonusValue(CORE_LINK_BONUS.STAGE_BUFFLEVEL_UP) ?? 0;
			return new RenderContext {
				AttrValue = attrValue,
				Buffs = buffs,
				BonusLevel = skillLevel.SkillLevel - 1 + linkBonus + fullLinkBonus + favorBonus,
				Comments = [],
				SkillLevel = skillLevel,
			};
		}

		private static string RenderDescription(string desc, RenderContext context) {
			var sectionBodies = new Dictionary<string, string>();
			foreach (Match match in SectionRegex.Matches(desc))
				sectionBodies[match.Groups[1].Value] = match.Groups[2].Value.Trim('\r', '\n');

			var content = SectionRegex.Replace(desc, "");
			var root = ParseFragment(content);
			return RenderNodes(root.Nodes(), context, sectionBodies).Trim();
		}

		private static string RenderNodes(IEnumerable<XNode> nodes, RenderContext context, Dictionary<string, string> sectionBodies) {
			var sb = new StringBuilder();
			foreach (var node in nodes) {
				switch (node) {
					case XText text:
						sb.Append(text.Value);
						break;
					case XElement element:
						sb.Append(RenderElement(element, context, sectionBodies));
						break;
				}
			}
			return sb.ToString();
		}

		private static string RenderElement(XElement element, RenderContext context, Dictionary<string, string> sectionBodies) {
			var inner = RenderNodes(element.Nodes(), context, sectionBodies);
			switch (element.Name.LocalName) {
				case "sec":
					return WrapColor(GetSectionColor(element.Attribute("typ")?.Value), inner);
				case "dmg":
					if (!element.HasElements && string.IsNullOrWhiteSpace(inner)) {
						var damage = GetSkillDamage(context.SkillLevel, context.AttrValue, context.BonusLevel - (context.SkillLevel.SkillLevel - 1));
						var dmg = damage.ToString("#,###");

						var elems = element.Attribute("elem")?.Value.Split(",") ?? [];
						var tokens = string.Join("", elems.Select(RenderElemToken));
						var elemDisps = string.Join("・", elems.Select(GetElemDisplayName));

						var adaptive = elems.Length > 1 ? "적응형 " : "";

						return WrapColor("fce391", $"{tokens} {dmg} {elemDisps} {adaptive}피해");
					}
					return WrapColor("fce391", inner);
				case "val":
					return RenderValue(element, context);
				case "chance":
					return WrapColor("198754", RenderChance(element, context));
				case "elem":
					return RenderElemToken(element.Attribute("type")?.Value);
				case "buff":
					return RenderBuffToken(element.Attribute("typ")?.Value, inner);
				case "cmt":
					return RenderComment(element, context, sectionBodies);
				case "box":
					return RenderBox(element, inner);
				case "char":
					return RenderChar(element.Attribute("uid")?.Value);
				case "equip":
					return RenderEquip(element.Attribute("uid")?.Value);
				case "strike":
					return $"[s]{inner}[/s]";
				case "strong":
					return $"[b]{inner}[/b]";
				case "sub":
					return inner;
				case "i":
				case "s":
					return inner;
				default:
					return inner;
			}
		}

		#region RenderXXX
		private static string RenderComment(XElement element, RenderContext context, Dictionary<string, string> sectionBodies) {
			var title = element.Attribute("t")?.Value ?? innerText(element);
			var rawBody = innerText(element).Trim();
			var renderedBody = rawBody;

			var sectionMatch = CommentSectionRegex.Match(rawBody);
			if (sectionMatch.Success) {
				var sectionKey = sectionMatch.Groups[1].Value;
				if (sectionBodies.TryGetValue(sectionKey, out var body)) {
					var expanded = ConvertSectionParams(body, sectionMatch.Groups[2].Value);
					var root = ParseFragment(expanded);
					renderedBody = RenderNodes(root.Nodes(), context, sectionBodies).Trim();
					rawBody = body;
				}
			}

			var actionId = $"action_{System.Threading.Interlocked.Increment(ref SectionCounter)}";
			context.Comments[actionId] = new CommentData {
				Title = title,
				Body = renderedBody,
				RawBody = rawBody,
			};

			return $"[url={actionId}][u]{title}[/u][/url]";
		}
		private static string RenderBox(XElement element, string inner) {
			var title = element.Attribute("title")?.Value ?? element.Attribute("t")?.Value ?? "";
			var lines = inner.Trim('\r', '\n');
			return $"\n{WrapColor("adb5bd", $"# {title}")}\n{lines}\n";
		}
		private static string RenderChar(string uid) {
			var name = SingleTon<DataManager>.Instance.GetTablePC(uid)?.Name?.Localize() ?? uid;
			return WrapColor("deebf7", $"[u]{name}[/u]");
		}
		private static string RenderEquip(string uid) {
			var name = SingleTon<DataManager>.Instance.GetTableItemEquip(uid)?.ItemName?.Localize() ?? uid;
			return WrapColor("deebf7", $"[u]{name}[/u]");
		}
		private static string RenderValue(XElement element, RenderContext context) {
			float baseValue;
			float perValue;

			var idxAttr = element.Attribute("idx")?.Value;
			if (!string.IsNullOrEmpty(idxAttr) && int.TryParse(idxAttr, out var idx) && idx >= 0 && idx < context.Buffs.Count) {
				var buff = context.Buffs[idx];
				baseValue = buff.BaseValue;
				perValue = buff.LevelValue;
			} else {
				baseValue = ParseFloat(element.Attribute("base")?.Value);
				perValue = ParseFloat(element.Attribute("per")?.Value);
			}

			var value = baseValue + perValue * context.BonusLevel;
			var ratio = element.Attribute("r") != null;
			var ratio2 = element.Attribute("rr") != null;
			if (ratio2) value *= 10000f;
			else if (ratio) value *= 100f;

			var displaySign = value >= 0f;
			if (element.Attribute("inv") != null)
				displaySign = !displaySign;

			var absValue = Math.Abs(value);
			if (element.Attribute("floor") != null)
				absValue = (float)Math.Floor(absValue);

			var forcePn = element.Attribute("forcePN")?.Value?.ToLowerInvariant();
			var signText = forcePn switch {
				"p" => "+",
				"n" => "-",
				_ => absValue == 0f ? "" : (displaySign ? "+" : "-"),
			};

			var signless = element.Attribute("signless") != null;
			var color = (forcePn == "n" || (!string.IsNullOrEmpty(signText) && signText == "-")) ? "dc3545" : "0d6efd";
			var number = absValue.ToString("0.####", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
			if (string.IsNullOrEmpty(number)) number = "0";

			return WrapColor(color, $"{(signless ? "" : signText)}{number}{(ratio || ratio2 ? "%" : "")}");
		}
		private static string RenderChance(XElement element, RenderContext context) {
			var idxAttr = element.Attribute("idx")?.Value;
			var chance = 0f;
			if (!string.IsNullOrEmpty(idxAttr) && int.TryParse(idxAttr, out var idx) && idx >= 0 && idx < context.Buffs.Count)
				chance = context.Buffs[idx].Rate * 100f;

			var text = chance.ToString("0.####", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
			if (string.IsNullOrEmpty(text)) text = "0";
			return $"{text}%";
		}
		#endregion

		private static XElement ParseFragment(string content) {
			return XElement.Parse($"<root>{NormalizeXmlLikeMarkup(content)}</root>", LoadOptions.PreserveWhitespace);
		}
		private static string NormalizeXmlLikeMarkup(string content) {
			if (string.IsNullOrEmpty(content)) return content;

			return TagRegex.Replace(content, match => {
				if (match.Groups["close"].Success) return match.Value;

				var attrs = match.Groups["attrs"].Value;
				foreach (var attr in BooleanAttributes)
					attrs = Regex.Replace(attrs, $@"(?<=\s){attr}(?=(\s|/|$))", $@"{attr}=""true""");

				var selfClose = match.Groups["self"].Success ? "/" : "";
				return $"<{match.Groups["name"].Value}{attrs}{selfClose}>";
			});
		}

		private static string ConvertSectionParams(string body, string parameterText) {
			var parameters = ParseSectionParameters(parameterText);
			return ParamAttrRegex.Replace(body, match => {
				var rawIndex = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) - 1;
				var forcePn = match.Groups[2].Success ? match.Groups[2].Value.ToLowerInvariant() : "";

				if (rawIndex < 0 || rawIndex >= parameters.Count)
					return forcePn == "" ? @"base=""0"" per=""0""" : $@"base=""0"" per=""0"" forcePN=""{forcePn}""";

				var parameter = parameters[rawIndex];
				if (!parameter.HasValue)
					return forcePn == "" ? "" : $@"forcePN=""{forcePn}""";

				return forcePn == ""
					? $@"idx=""{parameter.Value}"""
					: $@"idx=""{parameter.Value}"" forcePN=""{forcePn}""";
			});
		}

		private static List<int?> ParseSectionParameters(string parameterText) {
			if (string.IsNullOrWhiteSpace(parameterText)) return [];

			return parameterText
				.Split(',')
				.Select(part => {
					part = part.Trim();
					if (part == "?") return (int?)null;
					return int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
						? value
						: null;
				})
				.ToList();
		}

		private static string RenderElemToken(string elem) {
			if (string.IsNullOrWhiteSpace(elem)) return "[$elem:physics]";
			var tokens = elem.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).Select(x => $"[$elem:{x}]");
			return string.Join("", tokens);
		}
		private static string RenderBuffToken(string buffIcon, string inner) {
			if (string.IsNullOrWhiteSpace(buffIcon) || !BuffIconSet.Contains(buffIcon))
				return "";

			return $"[$buff:{buffIcon}]{inner}";
		}

		private static string ReplaceSymbolTokensWithPlaceholders(UILabel label, string text, List<SymbolSpriteEntry> entries = null) {
			if (string.IsNullOrEmpty(text)) return text;
			if (label == null) return text;

			label.UpdateNGUIText();

			var sb = new StringBuilder(text.Length);
			var lastIndex = 0;
			var outputIndex = 0;
			var x = 0f;
			foreach (Match match in SymbolTokenRegex.Matches(text)) {
				if (match.Index > lastIndex) {
					var segment = text.Substring(lastIndex, match.Index - lastIndex);
					sb.Append(segment);
					x = CalculateWrappedTextX(segment, x);
					outputIndex += segment.Length;
				}

				var placeholder = BuildSymbolPlaceholder(
					label,
					match.Groups["kind"].Value,
					match.Groups["key"].Value,
					entries
				);
				if (placeholder.Text.Length > 0) {
					if (x > 0f && x + placeholder.TargetWidth > label.width) {
						sb.Append('\n');
						outputIndex += 1;
						x = 0f;
					}
				}

				if (entries != null && placeholder.VisibleLength > 0) {
					var entry = entries[^1];
					entry.StartIndex = outputIndex + placeholder.DotStartOffset;
				}

				sb.Append(placeholder.Text);
				x += placeholder.TargetWidth;
				outputIndex += placeholder.Text.Length;
				lastIndex = match.Index + match.Length;
			}

			if (lastIndex < text.Length) {
				var tail = text.Substring(lastIndex);
				sb.Append(tail);
			}

			return sb.ToString();
		}

		private static PlaceholderBuildResult BuildSymbolPlaceholder(UILabel label, string kind, string key, List<SymbolSpriteEntry> entries) {
			if (!TryGetSymbolSpriteInfo(kind, key, out var atlas, out var spriteName, out var aspect))
				return new PlaceholderBuildResult();

			var symbolHeight = GetPrintedLineHeight(label);
			var targetWidth = Mathf.Max(1f, symbolHeight * aspect);
			var placeholder = BuildTransparentDotPlaceholder(targetWidth);

			entries?.Add(new SymbolSpriteEntry {
				StartIndex = -1,
				PlaceholderLength = placeholder.VisibleLength,
				Width = targetWidth,
				Height = symbolHeight,
				Atlas = atlas,
				SpriteName = spriteName,
			});
			return placeholder;
		}

		private static PlaceholderBuildResult BuildTransparentDotPlaceholder(float targetWidth) {
			const string prefix = "[c][00000000]";
			const string suffix = "[-][/c]";
			var dots = new StringBuilder();
			var printedWidth = 0f;

			while (printedWidth < targetWidth || dots.Length == 0) {
				dots.Append('.');
				printedWidth = GetPrintedWidth(dots.ToString());
			}

			return new PlaceholderBuildResult {
				Text = $"{prefix}{dots}{suffix}",
				DotStartOffset = prefix.Length,
				VisibleLength = dots.Length,
				PrintedWidth = printedWidth,
				TargetWidth = targetWidth,
			};
		}

		private static float CalculateWrappedTextX(string segment, float currentX) {
			if (string.IsNullOrEmpty(segment))
				return currentX;

			var seg = segment.Split('\n').Last();

			var targetWidth = NGUIText.regionWidth;
			var textLength = seg.Length;
			var x = segment.Contains('\n', StringComparison.Ordinal) ? 0f : currentX;

			var prevChar = 0;
			var subscriptMode = 0;
			var isBold = false;
			var isItalic = false;
			var isUnderline = false;
			var isStrike = false;
			var ignoreColor = false;

			var emptyList = new BetterList<Color>();

			for (var i = 0; i < textLength; i++) {
				var c = seg[i];
				if (c < 32) {
					prevChar = c;
					continue;
				}

				if (
					NGUIText.encoding &&
					NGUIText.ParseSymbol(
						seg, ref i, emptyList, NGUIText.premultiply,
						ref subscriptMode, ref isBold, ref isItalic, ref isUnderline, ref isStrike, ref ignoreColor
					)
				) {
					i--;
					continue;
				}

				var glyphScale = subscriptMode == 0 ? NGUIText.fontScale : NGUIText.fontScale * 0.6f;

				var symbol = NGUIText.useSymbols ? NGUIText.GetSymbol(seg, i, textLength) : null;
				if (symbol != null) {
					var symbolSize = symbol.advance * glyphScale;
					var next = x + symbolSize;

					if (next > targetWidth) x = 0f;
					x += symbolSize + NGUIText.finalSpacingX;
					i += symbol.length - 1;
					prevChar = 0;
				}
				else {
					var glyph = NGUIText.GetGlyph(c, prevChar, glyphScale);
					if (glyph == null) continue;

					prevChar = c;

					var glyphSize = glyph.advance + NGUIText.finalSpacingX;
					var next = x + glyphSize;

					if (next > targetWidth) x = 0f;
					x += glyphSize;
					if (subscriptMode != 0) x = Mathf.Round(x);
				}
			}
			return x;
		}

		private static float GetPrintedWidth(string text) {
			if (string.IsNullOrEmpty(text)) return 0f;
			return NGUIText.CalculatePrintedSize(text).x;
		}

		private static float GetPrintedLineHeight(UILabel label) {
			if (label == null) return 1f;
			var printed = NGUIText.CalculatePrintedSize(".");
			return Mathf.Max(printed.y, 1f);
		}

		private static bool TryGetSymbolSpriteInfo(string kind, string key, out INGUIAtlas atlas, out string spriteName, out float aspect) {
			atlas = null;
			spriteName = null;
			var cacheKey = $"{kind}:{key}";
			if (SymbolAspectCache.TryGetValue(cacheKey, out aspect)) {
				switch (kind) {
					case "elem":
						atlas = GetBuffIconAtlas();
						spriteName = GetElemSpriteName(key);
						break;
					case "buff" when BuffIconSet.Contains(key):
						atlas = GetBuffIconAtlas();
						spriteName = $"BuffIcon_{key}";
						break;
				}
				return aspect > 0f && atlas != null && !string.IsNullOrEmpty(spriteName);
			}

			aspect = 0f;
			var sprite = default(UISpriteData);
			switch (kind) {
				case "elem":
					atlas = GetBuffIconAtlas();
					spriteName = GetElemSpriteName(key);
					sprite = atlas?.GetSprite(spriteName);
					break;
				case "buff" when BuffIconSet.Contains(key):
					atlas = GetBuffIconAtlas();
					spriteName = $"BuffIcon_{key}";
					sprite = atlas?.GetSprite(spriteName);
					break;
			}
			if (sprite == null || sprite.width <= 0 || sprite.height <= 0) {
				SymbolAspectCache[cacheKey] = 0f;
				atlas = null;
				spriteName = null;
				return false;
			}

			aspect = (float)sprite.width / sprite.height;
			SymbolAspectCache[cacheKey] = aspect;
			return true;
		}

		private static string GetElemDisplayName(string key) => key switch {
			"physics" => "물리",
			"fire" => "화염",
			"ice" => "냉기",
			"lightning" => "전기",
			_ => "???",
		};
		private static string GetElemSpriteName(string key) => key switch {
			"fire" => "UI_Battle_Atktype_fire",
			"ice" => "UI_Battle_Atktype_ice",
			"lightning" => "UI_Battle_Atktype_electric",
			_ => "UI_Battle_Atktype_normal",
		};

		private static INGUIAtlas GetBuffIconAtlas() => BuffIconAtlas ??= SingleTon<ResourceManager>.Instance.LoadAtlas("BuffIconAtlas");
		private static string GetSectionColor(string typ) => typ switch {
			null => "fff2cc",
			"dmg" => "fce391",
			"note" => "98d718",
			"buff" => "a6d0f7",
			"important" => "42deff",
			"attr" => "ffc107",
			"cond" => "d63384",
			"chance" => "198754",
			"ref" => "6c757d",
			_ => "fff2cc",
		};

		private static string WrapColor(string color, string content) {
			if (string.IsNullOrEmpty(content)) return "";
			return $"[c][{color}]{content}{Common.COLOR_END}";
		}

		private static string BuildRenderCacheKey(string skillKey, int skillLevel) => $"{skillKey ?? ""}::{skillLevel}";

		private static void CacheRenderedSkillDesc(string cacheKey, string renderedText, RenderContext context) {
			if (string.IsNullOrEmpty(cacheKey)) return;

			RenderCache[cacheKey] = new RenderCacheEntry {
				Text = renderedText ?? "",
				Actions = new Dictionary<string, CommentData>(context.Comments),
			};
		}

		private static bool TryGetRenderedSkillDesc(string cacheKey, out RenderCacheEntry entry) {
			if (!string.IsNullOrEmpty(cacheKey) && RenderCache.TryGetValue(cacheKey, out entry)) {
				return true;
			}

			entry = null;
			return false;
		}

		private static string GetLastRenderCacheKey() => LastRenderCacheKey;

		private static float ParseFloat(string value) {
			if (string.IsNullOrWhiteSpace(value)) return 0f;
			if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) return 0f;
			return parsed;
		}

		private static string innerText(XElement element) => string.Concat(element.Nodes().Select(node => node switch {
			XText text => text.Value,
			XElement child => child.ToString(SaveOptions.DisableFormatting),
			_ => "",
		}));
		#endregion

		#region UILabel Patch
		private sealed class FancySkillDescBinding : MonoBehaviour {
			private UILabel Label;
			private string LastSourceText = "";
			private string LastCacheKey = "";
			private string LastText = "";
			private int LastSymbolCount = 0;
			private Dictionary<string, CommentData> Actions = [];
			private readonly List<SymbolSpriteEntry> SymbolEntries = [];
			private readonly List<GameObject> SymbolObjects = [];
			private Transform SymbolRoot;
			private GameObject PopupRoot;
			private UILabel PopupLabel;
			private int OriginalFontSize = -1;

			public void Start() {
				if(!TryGetComponent<UILabel>(out Label))
					return;

				ApplyFontSizeAdjustment();
				RefreshIfNeeded(force: true);
			}

			public void LateUpdate() {
				RefreshIfNeeded();
			}

			public void OnClick() {
				if (Label == null) return;

				var action = Label.GetUrlAtPosition(UICamera.lastWorldPosition);
				if (string.IsNullOrEmpty(action)) return;
				if (!Actions.TryGetValue(action, out var data)) return;

				TogglePopup(data);
			}

			public void OnDestroy() {
				ClearSymbols();
				if (PopupRoot != null)
					Destroy(PopupRoot);

				Actions.Clear();
				LastSourceText = "";
				LastCacheKey = "";
				LastText = "";
				LastSymbolCount = 0;
			}

			private void RefreshIfNeeded(bool force = false) {
				if (Label == null) return;
				ApplyFontSizeAdjustment();

				var currentCacheKey = GetLastRenderCacheKey();
				var sourceText = Label.text ?? "";
				if (TryGetRenderedSkillDesc(currentCacheKey, out var currentEntry) && currentEntry != null)
					sourceText = currentEntry.Text ?? sourceText;

				if (
					!force &&
					LastSourceText == sourceText &&
					LastCacheKey == currentCacheKey &&
					string.Equals(Label.text ?? "", LastText ?? "", StringComparison.Ordinal) &&
					IsSymbolStateCurrent()
				)
					return;

				SymbolEntries.Clear();
				var currentText = ReplaceSymbolTokensWithPlaceholders(Label, sourceText, SymbolEntries);
				var symbolsChanged = LastSymbolCount != SymbolEntries.Count;
				var textChanged = !string.Equals(currentText, Label.text ?? "", StringComparison.Ordinal);

				if (!force && !textChanged && !symbolsChanged && IsSymbolStateCurrent()) {
					LastSourceText = sourceText;
					LastCacheKey = currentCacheKey;
					LastText = currentText;
					Actions = currentEntry?.Actions ?? [];
					return;
				}

				if (!string.Equals(currentText, Label.text ?? "", StringComparison.Ordinal))
					Label.text = currentText;

				LastSourceText = sourceText;
				LastCacheKey = currentCacheKey;
				LastText = currentText;
				LastSymbolCount = SymbolEntries.Count;
				Actions = [];
				HidePopup();

				if (currentEntry == null)
				{
					SymbolEntries.Clear();
					ClearSymbols();
					return;
				}

				Actions = currentEntry.Actions ?? [];
				ClearSymbols();
				LayoutSymbols();
				BuildSymbolSprites();
			}

			private bool IsSymbolStateCurrent() {
				if (LastSymbolCount <= 0)
					return SymbolRoot == null && SymbolObjects.Count == 0;

				return SymbolRoot != null && SymbolObjects.Count == LastSymbolCount;
			}

			private void TogglePopup(CommentData data) {
				if (PopupRoot != null && PopupRoot.activeSelf && PopupLabel != null && PopupLabel.text == data.Body) {
					HidePopup();
					return;
				}

				EnsurePopup();
				PopupLabel.text = data.Body ?? data.RawBody ?? "";
				PopupRoot.SetActive(true);
			}

			private void EnsurePopup() {
				if (PopupRoot != null && PopupLabel != null) return;

				PopupRoot = new GameObject("FancySkillDescPopup");
				PopupRoot.layer = Label.gameObject.layer;
				PopupRoot.transform.SetParent(Label.transform.parent, false);
				PopupRoot.transform.localPosition = Label.transform.localPosition + new Vector3(0f, -Mathf.Max(Label.height, 40f) - 12f, 0f);

				PopupLabel = PopupRoot.AddComponent<UILabel>();
				PopupLabel.bitmapFont = Label.bitmapFont;
				PopupLabel.trueTypeFont = Label.trueTypeFont;
				PopupLabel.fontSize = Label.fontSize;
				PopupLabel.width = Mathf.Max(Label.width, 480);
				PopupLabel.overflowMethod = UILabel.Overflow.ResizeHeight;
				PopupLabel.supportEncoding = true;
				PopupLabel.symbolStyle = Label.symbolStyle;
				PopupLabel.pivot = UIWidget.Pivot.TopLeft;
				PopupLabel.color = Color.white;
			}

			private void HidePopup() {
				if (PopupRoot != null)
					PopupRoot.SetActive(false);
			}

			private void LayoutSymbols() {
				if (Label == null || SymbolEntries.Count == 0)
					return;

				Label.UpdateNGUIText();
				var verts = new List<Vector3>();
				var indices = new List<int>();
				NGUIText.PrintApproximateCharacterPositions(LastText, verts, indices);

				foreach (var entry in SymbolEntries) {
					if (entry == null)
						continue;

					if (!TryGetSymbolPosition(entry, verts, indices, out var x, out var y))
						continue;

					entry.X = x;
					entry.Y = y;
				}
			}

			private void BuildSymbolSprites() {
				if (Label == null || SymbolEntries.Count == 0)
					return;

				var root = EnsureSymbolRoot();
				if (root == null)
					return;

					foreach (var entry in SymbolEntries) {
						if (entry == null || entry.Atlas == null || string.IsNullOrEmpty(entry.SpriteName))
							continue;

					var spriteData = entry.Atlas.GetSprite(entry.SpriteName);
					if (spriteData == null)
						continue;

					var go = new GameObject($"FancySkillDescSymbol_{entry.SpriteName}");
					go.layer = Label.gameObject.layer;
					go.transform.SetParent(root, false);

					var sprite = go.AddComponent<UISprite>();
					sprite.atlas = entry.Atlas;
					sprite.spriteName = entry.SpriteName;
					sprite.pivot = UIWidget.Pivot.TopLeft;
						sprite.width = Mathf.Max(1, Mathf.RoundToInt(entry.Width));
						sprite.height = Mathf.Max(1, Mathf.RoundToInt(entry.Height));
						sprite.depth = Label.depth + 1;

						var localX = entry.X;
						var localY = entry.Y;
						sprite.cachedTransform.localPosition = new Vector3(localX, localY, 0f);
						SymbolObjects.Add(go);
					}
			}

			private static bool TryGetSymbolPosition(SymbolSpriteEntry entry, List<Vector3> verts, List<int> indices, out float x, out float y) {
				x = 0f;
				y = 0f;

				if (entry == null || verts == null || indices == null || verts.Count == 0 || indices.Count == 0)
					return false;

				var start = entry.StartIndex;
				var end = start + Math.Max(entry.PlaceholderLength - 1, 0);
				var minX = float.MaxValue;
				var maxY = float.MinValue;
				var found = false;

				for (var i = 0; i < verts.Count && i < indices.Count; i++) {
					var index = indices[i];
					if (index < start || index > end)
						continue;

					var pos = verts[i];
					minX = Mathf.Min(minX, pos.x);
					maxY = Mathf.Max(maxY, pos.y);
					found = true;
				}

				if (!found)
					return false;

				x = minX;
				y = maxY;
				return true;
			}

			private Transform EnsureSymbolRoot() {
				if (SymbolRoot != null)
					return SymbolRoot;

				var root = new GameObject("FancySkillDescSymbols");
				root.layer = Label.gameObject.layer;
				root.transform.SetParent(Label.cachedTransform, false);
				root.transform.localPosition = Vector3.zero;
				root.transform.localScale = Vector3.one;
				SymbolRoot = root.transform;
				return SymbolRoot;
			}

			private void ClearSymbols() {
				foreach (var go in SymbolObjects) {
					if (go != null)
						Destroy(go);
				}
				SymbolObjects.Clear();

				if (SymbolRoot != null && SymbolRoot.childCount == 0) {
					Destroy(SymbolRoot.gameObject);
					SymbolRoot = null;
				}
			}

			private void ApplyFontSizeAdjustment() {
				if (Label == null)
					return;

				if (OriginalFontSize <= 0)
					OriginalFontSize = Label.fontSize;

				var adjustedFontSize = Mathf.Max(1, OriginalFontSize - 2);
				if (Label.fontSize != adjustedFontSize)
					Label.fontSize = adjustedFontSize;
			}

		}
		#endregion
	}
}

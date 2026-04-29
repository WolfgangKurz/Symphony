using LO_ClientNetwork;

using Sgml;

using Symphony.Data;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

using UnityEngine;

using ElemDict = System.Collections.Generic.Dictionary<string, System.Xml.Linq.XElement>;
using ElemList = System.Collections.Generic.List<System.Xml.Linq.XElement>;

namespace Symphony.Features {
	internal class SimpleUI_FancySkillDesc {
		private static readonly Regex UnitKeyRegex = new Regex(@"^Skill_(.+)_(?:N|CH)_(?:[0-9]+)$", RegexOptions.Compiled);
		private static readonly Regex SkillKeyRegex = new Regex(@"^.+_(N|CH)_([0-9]+)$", RegexOptions.Compiled);

		private static int? cachedFontSize = null;

		internal static void Patch_SkillDesc(Panel_CharacterDetails __instance) {
			if (!Conf.SimpleUI.Use_FancySkillDesc.Value) return;

			var skillIndex = __instance.XGetFieldValue<int>("_skillIndex");
			var skillInfo = __instance.XGetFieldValue<SkillInfo>("_SelectSkillInfo");

			var skillKey = skillInfo.SkillKeyString;

			var lbl = __instance.XGetFieldValue<UILabel>("_lblSkillDesc");
			if (lbl == null) {
				Plugin.Logger.LogWarning($"[Symphony::Experimental::FancySkillDesc] Failed to render {skillKey}: SkillDesc label not found");
				return;
			}

			if (!TryGetUnitData(skillKey, out var unitKey, out var data)) {
				Plugin.Logger.LogWarning($"[Symphony::Experimental::FancySkillDesc] Failed to render {skillKey}: Cannot parse unit's key");
				return;
			}

			if (!TryGetDescriptionKey(skillKey, data, out var desc)) {
				Plugin.Logger.LogWarning($"[Symphony::Experimental::FancySkillDesc] Failed to render {skillKey}: Not found in database");
				return;
			}

			var comp = lbl.GetComponent<SkillDesc>();
			if (comp == null)
				comp = lbl.gameObject.AddComponent<SkillDesc>();

			comp.targetPC = SingleTon<DataManager>.Instance.GetTablePC($"Char_{unitKey}_N");
			comp.targetClientPC = __instance.XGetFieldValue<ClientPcInfo>("_SelectPCInfo");
			comp.isCH = __instance.XGetFieldValue<ChangeCharType>("_changeCharType") != ChangeCharType.Normal;
			comp.skillInfo = skillInfo;
			comp.text = desc; // SkillDesc component will patch automatically

			if (cachedFontSize == null)
				cachedFontSize = lbl.fontSize;

			lbl.fontSize = (int)(cachedFontSize * 0.95f);
		}

		private static bool TryGetUnitData(string skillKey, out string unitKey, out Dictionary<string, string> data) {
			unitKey = null;
			data = null;

			var match = UnitKeyRegex.Match(skillKey);
			if (!match.Success) return false;

			unitKey = match.Groups[1].Value;
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


		public class SkillDesc : MonoBehaviour {
			public Texture2D boxTexture = new Texture2D(2, 2);

			private UILabel sourceLabel = null;
			private GameObject viewObject = null;
			private TooltipInstance tooltipInstance = new();

			private ElemDict dictSections = new();
			private ElemList dictComments = new();
			private ElemDict inheritedSections = null;

			private bool subscribed = false;
			private bool renderQueued = false;
			private bool sourceSnapshotValid = false;
			private int sourceSnapshotWidth = -1;
			private int sourceSnapshotHeight = -1;
			private int screenSnapshotWidth = -1;
			private int screenSnapshotHeight = -1;

			private void Start() {
				ImageConversion.LoadImage(this.boxTexture, Resource.SkillSubbox);
			}
			private void OnEnable() {
				Setup();
				InvalidateSourceSnapshot();
				QueueRender();
			}
			private void OnDisable() {
				Unset();
				CleanupViews();
			}

			private void LateUpdate() {
				if (!this.renderQueued || this.sourceLabel == null) return;
				this.renderQueued = false;

				if (!HasSourceChanged(this.sourceLabel)) return;
				if (!TryBuildRoot(this.text ?? "", out var root)) {
					CaptureSourceSnapshot(this.sourceLabel);
					return;
				}

				RenderTemplate(root, this.sourceLabel);
			}

			#region Game Data
			private Table_PC _targetPC = null;
			public Table_PC targetPC {
				get { return this._targetPC; }
				set {
					if (this._targetPC == value) return;

					this._targetPC = value;

					this.QueueRender();
				}
			}

			private bool _isCH = false;
			public bool isCH {
				get { return this._isCH; }
				set {
					if (this._isCH == value) return;

					this._isCH = value;

					this.QueueRender();
				}
			}

			private ClientPcInfo _targetClientPC = null;
			public ClientPcInfo targetClientPC {
				get { return this._targetClientPC; }
				set {
					if (this._targetClientPC == value) return;

					this._targetClientPC = value;

					this.QueueRender();
				}
			}

			private SkillInfo _skillInfo = null;
			public SkillInfo skillInfo {
				get { return this._skillInfo; }
				set {
					if (this._skillInfo == value) return;

					this._skillInfo = value;

					if (value != null) {
						try {
							var skillKey = value.SkillKeyString;
							this.tableSkillLevel = SingleTon<DataManager>.Instance.GetTableSkillLevel(skillKey, value.SkillLevel);
						} catch { // Invalid data
							this.tableSkillLevel = null;
							this._skillInfo = null;
						}
					}

					this.QueueRender();
				}
			}
			private Table_SkillLevel tableSkillLevel = null;

			private string _text = null;
			public string text {
				get { return this._text; }
				set {
					if (this._text == value) return;
					this._text = value;
					this.QueueRender();
				}
			}

			private string GetUnitName(string key)
				=> (SingleTon<DataManager>.Instance.GetTableCharCollection(key)?.Char_Name ??
				SingleTon<DataManager>.Instance.GetTableCharCollection($"Char_{key}_N")?.Char_Name ?? 
				key).Localize();

			private string GetEquipName(string key)
				=> (SingleTon<DataManager>.Instance.GetTableItemEquip(key)?.ItemName ??
				SingleTon<DataManager>.Instance.GetTableItemEquip($"{key}_T4")?.ItemName ??
				key).Localize().Replace(" EX", "");

			private int GetSkillLevel() => this.skillInfo?.SkillLevel ?? 0;
			public float GetSkillValue(int slot, int idx) {
				if (this.skillInfo == null || this.targetPC == null) return 0f;
				if (slot == 0 || slot > 10) return 0f; // Invalid slot id

				Table_SkillLevel sk = null;
				if (slot < 0)
					sk = this.tableSkillLevel;

				else {
					var grp = SingleTon<DataManager>.Instance.GetTableSkillGroup(
						this.isCH
							? this.targetPC.SkillGroupIndex_CH
							: this.targetPC.SkillGroupIndex
					);
					if(grp == null) return 0f;

					sk = SingleTon<DataManager>.Instance.GetTableSkillLevel(slot switch {
						1 => grp.SkillIndex1,
						2 => grp.SkillIndex2,
						3 => grp.SkillIndex3,
						4 => grp.SkillIndex4,
						5 => grp.SkillIndex5,
						6 => grp.SkillIndex6,
						7 => grp.SkillIndex7,
						8 => grp.SkillIndex8,
						9 => grp.SkillIndex9,
						10 => grp.SkillIndex10,
						_ => null
					}, this.skillInfo.SkillLevel);
				}

				if (sk == null) return 0f;

				var current = 0;
				foreach (var buffKey in sk.BuffEffectIndex.Where(x => !string.IsNullOrEmpty(x))) {
					var buff = SingleTon<DataManager>.Instance.GetTableBuffEffect(buffKey);
					if (buff == null) {
						if (current == idx) return 0f; // unknown value
						current++;

						continue;
					}

					if (buff._dic_BuffDesc == null) continue;

					for (var i = 1; i <= 5; i++) {
						if (!buff._dic_BuffDesc.TryGetValue(i.ToString(), out var b))
							continue;

						if (string.IsNullOrEmpty(b.BuffIcon) &&
							string.IsNullOrEmpty(b.BuffEffectType_Desc) &&
							b.BuffEffectValue == "0"
						)
							continue;

						if (
							!float.TryParse(b.BuffEffectValue, out var value) ||
							!float.TryParse(b.BuffEffectLevelValue, out var levelValue)
						) {
							current++;
							continue;
						}

						if (current == idx)
							return value + (this.skillInfo.SkillLevel - 1) * levelValue;

						current++;
					}
				}
				return 0f;
			}
			public int GetSkillDamage() {
				if (this.skillInfo == null || this.targetPC == null || this.targetClientPC == null || this.tableSkillLevel == null)
					return 0;

				var pc = this.targetClientPC;

				var attrValue = pc.attrValue;
				var fullLinkKey = pc.CoreLinkBonus_KeyString;

				var linkBonus = Common.GetLinkBonusValue(pc, CORE_LINK_BONUS.STAGE_SKILLRATIO_UP);
				var fullLinkBonus = Common.GetFullLinkBonusValue(fullLinkKey, CORE_LINK_BONUS.STAGE_SKILLRATIO_UP);
				var favorBonus = pc.GetFavorBonusValue(CORE_LINK_BONUS.STAGE_SKILLRATIO_UP);
				var bonus = linkBonus + fullLinkBonus + favorBonus;

				return (int)Mathf.Floor(
					Common.FloatParseNotCulture(DataManager.GetAttrPcAttack(attrValue)) *
					(Common.FloatParseNotCulture(tableSkillLevel.SkillAttackRate) + bonus)
				);
			}

			private static INGUIAtlas LoadSymbolAtlas() => SingleTon<ResourceManager>.Instance.LoadAtlas("BuffIconAtlas").GetComponent<UIAtlas>();
			#endregion

			#region Setup & Unset
			private void Setup() {
				// already set up
				if (this.sourceLabel != null) return;

				if (!this.TryGetComponent<UILabel>(out var label)) {
					Debug.LogWarning("SkillDesc should be attached to UILabel gameobject.");
					return;
				}

				// fires when need to update
				if (!this.subscribed) {
					label.onPostFill += OnSourcePostFill;
					label.SetDirty();
					this.subscribed = true;
				}

				this.sourceLabel = label;
			}
			private void Unset() {
				if (this.sourceLabel != null && subscribed)
					this.sourceLabel.onPostFill -= OnSourcePostFill;

				this.subscribed = false;
				this.sourceLabel = null;
			}

			private void OnSourcePostFill(UIWidget widget, int bufferOffset, List<Vector3> verts, List<Vector2> uvs, List<Color> cols) {
				if (HasSourceChanged(widget as UILabel ?? this.sourceLabel))
					QueueRender();

				// make original label skip drawing
				verts.Clear();
				uvs.Clear();
				cols.Clear();
			}
			#endregion

			#region Template
			private void QueueRender() {
				if (!this.isActiveAndEnabled) return;
				this.renderQueued = true;
			}

			private void InvalidateSourceSnapshot() {
				this.sourceSnapshotValid = false;
				this.sourceSnapshotWidth = -1;
				this.sourceSnapshotHeight = -1;
				this.screenSnapshotWidth = -1;
				this.screenSnapshotHeight = -1;
			}

			private bool HasSourceChanged(UILabel label) {
				if (label == null) return false;
				if (!this.sourceSnapshotValid) return true;

				return this.sourceSnapshotWidth != label.width ||
					   this.sourceSnapshotHeight != label.height ||
					   this.screenSnapshotWidth != Screen.width ||
					   this.screenSnapshotHeight != Screen.height;
			}

			private void CaptureSourceSnapshot(UILabel label) {
				if (label == null) {
					InvalidateSourceSnapshot();
					return;
				}

				this.sourceSnapshotValid = true;
				this.sourceSnapshotWidth = label.width;
				this.sourceSnapshotHeight = label.height;
				this.screenSnapshotWidth = Screen.width;
				this.screenSnapshotHeight = Screen.height;
			}

			private bool TryBuildRoot(string source, out XElement root) {
				root = null;
				if (string.IsNullOrWhiteSpace(source)) // Nothing to draw
				{
					CleanupViews();
					return false;
				}

				var src = PreprocessBox(TranspileSections(source.Replace("\t", ""))).Trim();
				Plugin.Logger.LogInfo(src);
				root = XElement.Load(new SgmlReader {
					DocType = "HTML",
					InputStream = new StringReader($"<_>{src}</_>"),
					IgnoreDtd = true, // prevent insert <html> and <body> tag
					WhitespaceHandling = System.Xml.WhitespaceHandling.All, // preserve all whitespace only nodes
					TextWhitespace = TextWhitespaceHandling.None, // preserve leading/trailing whitespaces in text nodes
				});

				return true;
			}

			private LayoutData RenderTemplate(XElement root, UILabel sourceLabel) {
				// Remove previous views
				this.CleanupViews();

				this.dictSections = this.inheritedSections != null
					? new ElemDict(this.inheritedSections)
					: new ElemDict();
				this.dictComments = new();

				this.viewObject = CreateChildObject("SkillDescView", sourceLabel.transform);
				var layout = CreateRenderLayers(
					sourceLabel,
					viewObject.transform,
					root.Nodes(),
					sourceLabel.width,
					sourceLabel.height,
					-1f,
					dictSections,
					dictComments
				);
				CaptureSourceSnapshot(sourceLabel);
				return layout;
			}

			private void CleanupViews() {
				this.CleanupTooltip();

				if (this.viewObject != null) {
					Destroy(this.viewObject);
					this.viewObject = null;
				}
			}
			#endregion

			#region Tooltip
			private struct TooltipInstance {
				public GameObject tooltip;
				public GameObject dim;
				public GameObject background;
			}

			private void CleanupTooltip() {
				if (this.tooltipInstance.tooltip != null)
					Destroy(this.tooltipInstance.tooltip);

				this.tooltipInstance.tooltip = null;

				// will be destroyed with tooltip
				this.tooltipInstance.dim = null;
				this.tooltipInstance.background = null;
			}

			private void CloseTooltip() {
				// no tooltip generated
				if (this.tooltipInstance.tooltip == null) return;

				var tooltip = this.tooltipInstance.tooltip;

				// disable colliders
				foreach (var col in tooltip.GetComponentsInChildren<Collider>(true)) col.enabled = false;
				foreach (var col in tooltip.GetComponentsInChildren<Collider2D>(true)) col.enabled = false;

				var dim = this.tooltipInstance.dim?.GetComponent<UITexture>();
				var bg = this.tooltipInstance.background?.GetComponent<UITexture>();

				var f1 = dim != null ? FadeWidget(dim, 0f, UITweener.Method.EaseIn) : null;
				var f2 = bg != null ? FadeWidget(bg, 0f, UITweener.Method.EaseIn) : null;
				var f = f1 ?? f2;

				if (f != null)
					f?.SetOnFinished(() => this.CleanupTooltip());
				else
					this.CleanupTooltip();
			}

			private void ShowTooltip(IEnumerable<XNode> nodes, Vector3 worldPosition) {
				this.CleanupTooltip();

				if (this.sourceLabel == null || nodes == null) return;

				var label = this.sourceLabel;

				var tooltipRoot = GetTooltipRootTransform(label);
				var tooltip = CreateChildObject("SkillDescTooltip", tooltipRoot);
				this.tooltipInstance = new() {
					tooltip = tooltip
				};

				var padding = Mathf.Max(12f, label.fontSize);
				var width = Mathf.Clamp(label.width - padding * 2f, 160f, 420f);
				var contentWidth = Mathf.Max(2, Mathf.RoundToInt(width - padding * 2f));
				var screenRect = this.GetScreenRectInLocal(tooltipRoot);

				#region Dim
				var dim = CreateTooltipTexture("Dim", tooltip.transform, Texture2D.whiteTexture, new Color(0f, 0f, 0f, 0f), label.depth + 90);
				dim.SetRect(
					Mathf.Round(screenRect.xMin),
					Mathf.Round(screenRect.yMin),
					Mathf.Max(1f, Mathf.Round(screenRect.width)),
					Mathf.Max(1f, Mathf.Round(screenRect.height))
				);

				var dismiss = dim.gameObject.AddComponent<TooltipDismissHandler>();
				dismiss.owner = this;
				NGUITools.AddWidgetCollider(dim.gameObject);
				this.tooltipInstance.dim = dim.gameObject;
				#endregion

				#region Popup
				var popup = CreateChildObject("Popup", tooltip.transform);

				var local = tooltipRoot.InverseTransformPoint(worldPosition);
				local.x = ClampLoose(local.x + padding, screenRect.xMin + padding, screenRect.xMax - width - padding);
				local.y -= padding;
				local.z = 0f;
				popup.transform.localPosition = local;

				var bg = CreateTooltipTexture("Background", popup.transform, boxTexture != null ? boxTexture : Texture2D.whiteTexture, new Color(0.035f, 0.035f, 0.04f, 0f), label.depth - 4);
				bg.type = UIBasicSprite.Type.Sliced;
				bg.border = new Vector4(8f, 8f, 8f, 8f);
				this.tooltipInstance.background = bg.gameObject;

				var contentLabel = CreateTooltipLabel(
					"Content Label",
					popup.transform,
					label,
					contentWidth,
					Mathf.Max(8, Mathf.RoundToInt(label.fontSize * 0.9f)),
					new Vector3(padding, -padding, 0f),
					SerializeNodes(nodes)
				);

				var contentDesc = contentLabel.gameObject.AddComponent<SkillDesc>();
				contentDesc.boxTexture = this.boxTexture;
				contentDesc.inheritedSections = this.dictSections.Count > 0
					? new ElemDict(this.dictSections)
					: null;

				LayoutData layout = null;
				if (contentDesc.TryBuildRoot(contentLabel.text ?? "", out var contentRoot)) {
					layout = contentDesc.RenderTemplate(contentRoot, contentLabel);
					contentDesc.renderQueued = false;
				}

				// resize height to content size
				var contentHeight = layout?.contentHeight ?? contentLabel.fontSize;
				var height = Mathf.Max(label.fontSize + padding * 2f, contentHeight + padding * 2f);
				bg.SetRect(0f, -Mathf.Round(height), Mathf.Round(width), Mathf.Round(height));

				// fade in
				FadeWidget(dim, 0.48f, UITweener.Method.EaseOut);
				FadeWidget(bg, 0.88f, UITweener.Method.EaseOut);

				local.y = ClampLoose(local.y, screenRect.yMin + height + padding, screenRect.yMax - padding);
				popup.transform.localPosition = local;
				#endregion

				// Adjust depth
				foreach (var widget in popup.GetComponentsInChildren<UIWidget>(true))
					widget.depth += 100;
				var minPopupDepth = popup.GetComponentsInChildren<UIWidget>(true)
					.Where(w => w != bg)
					.Min(w => w.depth);

				bg.depth = minPopupDepth - 1;
				dim.depth = bg.depth - 1;
			}

			private static Transform GetTooltipRootTransform(UILabel label) {
				if (label == null) return null;

				var root = label.root;
				if (root == null)
					root = NGUITools.FindInParents<UIRoot>(label.transform);
				if (root == null)
					root = UIRoot.list.FirstOrDefault(r => r != null && r.gameObject.layer == label.gameObject.layer);

				return root != null ? root.transform : label.transform;
			}

			private static float ClampLoose(float value, float min, float max) => min <= max ? Mathf.Clamp(value, min, max) : max;

			private static TweenAlpha FadeWidget(UIWidget widget, float alpha, UITweener.Method method) {
				if (widget == null) return null;
				var tween = TweenAlpha.Begin(widget.gameObject, 0.14f /* duration */, alpha);
				tween.method = method;
				return tween;
			}

			private static UITexture CreateTooltipTexture(string name, Transform parent, Texture texture, Color color, int depth) {
				var tex = CreateChildObject(name, parent).AddComponent<UITexture>();
				tex.mainTexture = texture;
				tex.pivot = UIWidget.Pivot.TopLeft;
				tex.color = color;
				tex.depth = depth;
				return tex;
			}

			private static UILabel CreateTooltipLabel(
				string name,
				Transform parent,
				UILabel template,
				int width,
				int fontSize,
				Vector3 localPosition,
				string text
			) {
				var label = CreateChildObject(name, parent).AddComponent<UILabel>();
				label.pivot = UIWidget.Pivot.TopLeft;
				label.width = Mathf.Max(2, width);
				label.height = Mathf.Max(2, template.height);
				label.depth = template.depth;
				label.color = Color.white;
				label.fontSize = fontSize;
				label.fontStyle = template.fontStyle;
				label.spacingX = template.spacingX;
				label.overflowMethod = UILabel.Overflow.ClampContent;
				label.keepCrispWhenShrunk = template.keepCrispWhenShrunk;
				label.supportEncoding = false;

				if (template.trueTypeFont != null)
					label.trueTypeFont = template.trueTypeFont;
				else
					label.bitmapFont = template.bitmapFont;

				label.text = text ?? "";
				label.transform.localPosition = localPosition;
				return label;
			}

			private static string SerializeNodes(IEnumerable<XNode> nodes) {
				if (nodes == null) return "";

				var sb = new StringBuilder();
				using (var writer = XmlWriter.Create(new StringWriter(sb), new XmlWriterSettings {
					OmitXmlDeclaration = true,
					ConformanceLevel = ConformanceLevel.Fragment
				})) {
					foreach (var node in nodes)
						node.WriteTo(writer);
				}

				return sb.ToString();
			}

			private void ShowCommentTooltip(int index, Vector3 worldPosition) {
				if (this.sourceLabel == null) return;
				if (index < 0 || index >= this.dictComments.Count) return;

				var comment = this.dictComments[index];
				if (comment == null) return;
				this.ShowTooltip(comment.Nodes(), worldPosition);
			}

			private Rect GetScreenRectInLocal(Transform localRoot) {
				var fallback = GetSourceLabelRectInLocal(localRoot);
				if (localRoot == null) return fallback;

				var cam = NGUITools.FindCameraForLayer(localRoot.gameObject.layer);
				if (cam == null) cam = UICamera.currentCamera;
				if (cam == null) return fallback;

				var z = cam.WorldToScreenPoint(localRoot.position).z;
				var corners = new[]
				{
			localRoot.InverseTransformPoint(cam.ScreenToWorldPoint(new Vector3(0f, 0f, z))),
			localRoot.InverseTransformPoint(cam.ScreenToWorldPoint(new Vector3(0f, Screen.height, z))),
			localRoot.InverseTransformPoint(cam.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, z))),
			localRoot.InverseTransformPoint(cam.ScreenToWorldPoint(new Vector3(Screen.width, 0f, z))),
		};

				var xMin = corners.Min(p => p.x);
				var xMax = corners.Max(p => p.x);
				var yMin = corners.Min(p => p.y);
				var yMax = corners.Max(p => p.y);
				if (xMax <= xMin || yMax <= yMin) return fallback;
				return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
			}

			private Rect GetSourceLabelRectInLocal(Transform localRoot) {
				if (this.sourceLabel == null) return Rect.MinMaxRect(0f, 0f, 1f, 1f);

				var relativeTo = localRoot != null ? localRoot : this.sourceLabel.transform;
				var bounds = this.sourceLabel.CalculateBounds(relativeTo);
				if (bounds.size.x > 0f && bounds.size.y > 0f)
					return Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);

				return Rect.MinMaxRect(0f, -this.sourceLabel.height, this.sourceLabel.width, 0f);
			}
			#endregion

			#region Event Handling
			private sealed class TooltipDismissHandler : MonoBehaviour {
				public SkillDesc owner;

				private void OnClick() => owner?.CloseTooltip();
			}
			#endregion

			#region Layer
			private LayoutData CreateRenderLayers(
				UILabel template,
				Transform parent,
				IEnumerable<XNode> nodes,
				int width,
				int height,
				float defaultFontSize,
				ElemDict sections,
				ElemList comments,
				Color? initialColor = null,
				bool shadowText = false
			) {
				var symbolAtlas = LoadSymbolAtlas();
				var origin = GetTopLeftOffset(template, width, height);
				var layout = BuildLayout(
					template,
					nodes.ToList(),
					width,
					height,
					defaultFontSize,
					sections,
					comments,
					initialColor ?? template.color,
					shadowText,
					symbolAtlas,
					origin
				);
				var layerHeight = Mathf.CeilToInt(layout.contentHeight);
				var widgetHeight = IsTopPivot(template.pivot) ? layerHeight : height;

				var boxLayer = CreateChildObject("Box Layer", parent).AddComponent<BoxLayerWidget>();
				SetupLayer(boxLayer, width, widgetHeight, template.depth - 20, template.pivot);
				boxLayer.mesh = BuildBoxMesh(boxTexture != null ? boxTexture : Texture2D.whiteTexture, layout);
				boxLayer.texture = boxTexture != null ? boxTexture : Texture2D.whiteTexture;

				var textLayer = CreateChildObject("Text Layer", parent).AddComponent<TextLayerWidget>();
				SetupLayer(textLayer, width, widgetHeight, template.depth, template.pivot);
				textLayer.mesh = BuildTextMesh(template, width, layerHeight, layout);
				textLayer.textMaterial = GetFontMaterial(template);
				textLayer.comments = layout.comments;

				var symbolLayer = CreateChildObject("Symbol Layer", parent).AddComponent<SymbolLayerWidget>();
				SetupLayer(symbolLayer, width, widgetHeight, template.depth + 1, template.pivot);
				symbolLayer.mesh = BuildSymbolMesh(symbolAtlas, layout);
				symbolLayer.symbolMaterial = symbolAtlas != null ? symbolAtlas.spriteMaterial : null;

				textLayer.hitCheck = textLayer.IsCommentHit;
				var handler = textLayer.gameObject.AddComponent<CommentHitHandler>();
				handler.owner = this;
				handler.textLayer = textLayer;
				NGUITools.AddWidgetCollider(textLayer.gameObject);

				// Adjust layer position
				boxLayer.transform.localPosition += new Vector3(0, 5f);
				symbolLayer.transform.localPosition += new Vector3(0, 5f);

				boxLayer.MarkAsChanged();
				textLayer.MarkAsChanged();
				symbolLayer.MarkAsChanged();
				return layout;
			}

			private static void SetupLayer(UIWidget widget, int width, int height, int depth, UIWidget.Pivot pivot) {
				widget.pivot = pivot;
				widget.width = Mathf.Max(2, width);
				widget.height = Mathf.Max(2, height);
				widget.depth = depth;
				widget.color = Color.white;
				// NGUI may move the transform while preserving the pivot position.
				widget.transform.localPosition = Vector3.zero;
			}

			private static bool IsTopPivot(UIWidget.Pivot pivot)
				=> pivot == UIWidget.Pivot.TopLeft || pivot == UIWidget.Pivot.Top || pivot == UIWidget.Pivot.TopRight;

			private static Vector2 GetTopLeftOffset(UIWidget widget, int width, int height) {
				if (widget == null) return Vector2.zero;

				var pivot = widget.pivotOffset;
				return new Vector2(-pivot.x * width, (1f - pivot.y) * height);
			}

			private LayoutData BuildLayout(
				UILabel template,
				IEnumerable<XNode> nodes,
				int width,
				int height,
				float defaultFontSize,
				ElemDict sections,
				ElemList comments,
				Color initialColor,
				bool shadowText,
				INGUIAtlas symbolAtlas,
				Vector2 origin
			) {
				var layout = new LayoutData();
				var renderer = new LayerRenderer(
					this,
					template,
					layout,
					Mathf.Max(2, width),
					Mathf.Max(2, height),
					defaultFontSize,
					sections,
					comments,
					initialColor,
					shadowText,
					symbolAtlas,
					origin.x,
					origin.y
				);
				renderer.RenderNodes(nodes ?? Enumerable.Empty<XNode>());
				renderer.Trim();
				layout.contentHeight = renderer.ContentHeight;
				return layout;
			}

			private static MeshData BuildTextMesh(UILabel template, int width, int height, LayoutData layout) {
				var mesh = new MeshData();
				if (template == null || layout == null) return mesh;

				for (var i = 0; i < layout.texts.Count; i++) {
					var run = layout.texts[i];
					if (string.IsNullOrEmpty(run.text)) continue;

					if (run.shadow)
						AddText(mesh, template, width, height, run.text, run.position + new Vector3(1f, -1f, 0f), run.fontSize, run.scale, new Color(0f, 0f, 0f, run.color.a * 0.65f));
					AddText(mesh, template, width, height, run.text, run.position, run.fontSize, run.scale, run.color);
				}

				return mesh;
			}

			private static void AddText(MeshData mesh, UILabel template, int width, int height, string text, Vector3 offset, int fontSize, float scale, Color color) {
				var start = mesh.verts.Count;
				scale = scale > 0f ? scale : 1f;
				ConfigureText(template, fontSize, Mathf.Max(2, width), Mathf.Max(2, height), color);
				NGUIText.Print(text, mesh.verts, mesh.uvs, mesh.cols);

				for (var i = start; i < mesh.verts.Count; i++)
					mesh.verts[i] = new Vector3(mesh.verts[i].x * scale, mesh.verts[i].y * scale, mesh.verts[i].z) + offset;

				NGUIText.bitmapFont = null;
				NGUIText.dynamicFont = null;
			}

			private static MeshData BuildBoxMesh(Texture texture, LayoutData layout) {
				var mesh = new MeshData();
				if (layout == null) return mesh;

				var tex = texture != null ? texture : Texture2D.whiteTexture;
				if (tex == null) return mesh;

				for (var i = 0; i < layout.boxes.Count; i++) {
					var box = layout.boxes[i];
					if (box.line) {
						mesh.pixelLines.Add(new PixelLineDraw {
							rect = box.rect,
							color = box.color
						});
						continue;
					}

					AddSlicedTexture(mesh.verts, mesh.uvs, mesh.cols, tex, box);
				}

				return mesh;
			}

			private static MeshData BuildSymbolMesh(INGUIAtlas atlas, LayoutData layout) {
				var mesh = new MeshData();
				if (layout == null || atlas == null) return mesh;

				var mat = atlas.spriteMaterial;
				var tex = mat != null ? mat.mainTexture : null;
				if (tex == null) return mesh;

				for (var i = 0; i < layout.symbols.Count; i++) {
					var sym = layout.symbols[i];
					var sp = atlas.GetSprite(sym.spriteName);
					if (sp == null) continue;

					var uv = NGUIMath.ConvertToTexCoords(new Rect(sp.x, sp.y, sp.width, sp.height), tex.width, tex.height);
					AddTexturedQuad(mesh.verts, mesh.uvs, mesh.cols, sym.rect.xMin, sym.rect.yMin, sym.rect.xMax, sym.rect.yMax, uv.xMin, uv.yMin, uv.xMax, uv.yMax, sym.color);
				}

				return mesh;
			}

			private static Material GetFontMaterial(UILabel label) {
				if (label == null) return null;
				if (label.trueTypeFont != null) return label.trueTypeFont.material;

				var font = label.bitmapFont;
				font = font != null ? font.finalFont : null;
				return font != null ? font.material : null;
			}

			private static void ConfigureText(UILabel template, int fontSize, int width, int height, Color color) {
				NGUIText.fontSize = fontSize;
				NGUIText.fontStyle = template.fontStyle;
				NGUIText.rectWidth = Mathf.Max(2, width);
				NGUIText.rectHeight = Mathf.Max(2, height);
				NGUIText.regionWidth = Mathf.Max(2, width);
				NGUIText.regionHeight = Mathf.Max(2, height);
				NGUIText.gradient = false;
				NGUIText.encoding = false;
				NGUIText.premultiply = false;
				NGUIText.symbolStyle = NGUIText.SymbolStyle.None;
				NGUIText.maxLines = 0;
				NGUIText.spacingX = template.spacingX;
				NGUIText.spacingY = GetLineSpacing(fontSize);
				NGUIText.tint = color;
				NGUIText.alignment = NGUIText.Alignment.Left;
				NGUIText.pixelDensity = 1f;

				var font = template.bitmapFont;
				font = font != null ? font.finalFont : null;
				var ttf = template.trueTypeFont;
				NGUIText.fontScale = ttf != null
					? 1f
					: font != null && font.defaultSize > 0
						? (float)fontSize / font.defaultSize
						: 1f;
				NGUIText.bitmapFont = font;
				NGUIText.dynamicFont = ttf;
				NGUIText.Update();
			}

			private static int GetLineSpacing(float fontSize)
				=> Mathf.RoundToInt(fontSize / 2.5f);

			private sealed class MeshData {
				public readonly List<Vector3> verts = new();
				public readonly List<Vector2> uvs = new();
				public readonly List<Color> cols = new();
				public readonly List<PixelLineDraw> pixelLines = new();

				public void AddTo(List<Vector3> targetVerts, List<Vector2> targetUvs, List<Color> targetCols) {
					targetVerts.AddRange(verts);
					targetUvs.AddRange(uvs);
					targetCols.AddRange(cols);
				}
			}

			private sealed class LayoutData {
				public readonly List<TextDraw> texts = new();
				public readonly List<BoxDraw> boxes = new();
				public readonly List<SymbolDraw> symbols = new();
				public readonly List<CommentHit> comments = new();
				public float contentHeight;
			}

			private struct TextDraw {
				public string text;
				public Vector3 position;
				public int fontSize;
				public float scale;
				public Color color;
				public bool shadow;
			}

			private struct BoxDraw {
				public Rect rect;
				public Color color;
				public float titleHeight;
				public bool titleOnlyFill;
				public bool line;
			}

			private struct PixelLineDraw {
				public Rect rect;
				public Color color;
			}

			private struct SymbolDraw {
				public string spriteName;
				public Rect rect;
				public Color color;
			}

			private struct CommentHit {
				public int index;
				public Rect rect;
			}

			private sealed class CommentHitHandler : MonoBehaviour {
				public SkillDesc owner;
				public TextLayerWidget textLayer;

				private void OnClick() {
					if (owner == null || textLayer == null) return;
					if (textLayer.TryGetCommentAtWorld(UICamera.lastWorldPosition, out var index))
						owner.ShowCommentTooltip(index, UICamera.lastWorldPosition);
				}
			}

			private sealed class LayerRenderer {
				private readonly SkillDesc owner;
				private readonly UILabel template;
				private readonly LayoutData layout;
				private readonly int maxWidth;
				private readonly int maxHeight;
				private readonly float defaultFontSize;
				private readonly float lineGap;
				private readonly ElemDict sections;
				private readonly ElemList comments;
				private readonly Color currentColor;
				private readonly bool shadowText;
				private readonly INGUIAtlas symbolAtlas;
				private readonly float originX;
				private readonly float originY;
				private readonly float textScale;

				private float x;
				private float y;
				private float lineHeight;
				private float lineStart;
				private bool trimmed;
				private readonly StringBuilder pendingText = new();
				private readonly List<float> lineStarts = new();
				private readonly List<float> lineWidths = new();
				private readonly List<float> lineTops = new();

				public float ContentHeight { get; private set; }
				private int LineCount => lineWidths.Count;
				private float LastLineWidth => lineWidths.Count > 0 ? lineWidths[lineWidths.Count - 1] : x;
				private float EffectiveFontSize => defaultFontSize * textScale;

				public LayerRenderer(
					SkillDesc owner,
					UILabel template,
					LayoutData layout,
					int width,
					int height,
					float defaultFontSize = -1f,
					ElemDict sections = null,
					ElemList comments = null,
					Color? initialColor = null,
					bool shadowText = false,
					INGUIAtlas symbolAtlas = null,
					float originX = 0f,
					float originY = 0f,
					float textScale = 1f
				) {
					this.owner = owner;
					this.template = template;
					this.layout = layout;
					this.maxWidth = Mathf.Max(2, width);
					this.maxHeight = Mathf.Max(2, height);
					this.defaultFontSize = defaultFontSize > 0f ? defaultFontSize : template.fontSize;
					this.textScale = Mathf.Max(0.01f, textScale);
					this.lineGap = Mathf.Max(2f, GetLineSpacing(EffectiveFontSize) * 0.25f);
					this.lineHeight = EffectiveFontSize;
					this.sections = sections ?? new ElemDict();
					this.comments = comments ?? new ElemList();
					this.currentColor = initialColor ?? Color.white;
					this.shadowText = shadowText;
					this.originX = originX;
					this.originY = originY;
					this.symbolAtlas = symbolAtlas;
				}

				public void RenderNodes(IEnumerable<XNode> nodes) {
					foreach (var node in nodes)
						RenderNode(node);
				}

				public void Trim() {
					if (trimmed) return;

					FlushText();
					CaptureLine();
					ContentHeight = Mathf.Max(ContentHeight, -y + lineHeight);
					trimmed = true;
				}

				private void RenderNode(XNode node) {
					switch (node) {
						case XText text:
							AddText(text.Value);
							break;
						case XElement el:
							RenderElement(el);
							break;
					}
				}

				private void RenderElement(XElement el) {
					switch (el.Name.LocalName) {
						case "buff": {
								var typ = el.Attribute("typ")?.Value;
								AddSymbol(string.IsNullOrEmpty(typ) ? null : "BuffIcon_" + typ);
							}
							return;
						case "elem": {
								var typ = el.Attribute("type")?.Value;
								AddSymbol(string.IsNullOrEmpty(typ) ? null : GetElementSpriteName(typ));
							}
							return;
						case "val":
							AddValue(el);
							return;
						case "char": {
								var key = el.Attribute("uid")?.Value;
								if (key == null) return;
								var name = owner.GetUnitName(key);
								FlushText();
								AddInlineText(name ?? key, HexToColorLocal("6e7bf7"), -1);
								return;
							}
						case "equip": {
								var key = el.Attribute("uid")?.Value;
								if (key == null) return;
								var name = owner.GetEquipName(key);
								FlushText();
								AddInlineText(name ?? key, HexToColorLocal("6e7bf7"), -1);
								return;
							}
						case "cmt":
							AddComment(el);
							return;
						case "sec":
							FlushText();
							AddRange(el);
							return;
						case "dmg":
							FlushText();
							AddRange(el, nested => nested.AddDamage(el, owner.GetSkillDamage()));
							return;
						case "box":
							FlushText();
							AddBox(el);
							return;
						case "define": {
								var name = el.Attribute("name")?.Value;
								if (!string.IsNullOrEmpty(name))
									sections[name] = el;
								return;
							}
						case "import": {
								var name = el.Attribute("name")?.Value;
								if (!string.IsNullOrEmpty(name) && sections.TryGetValue(name, out var section))
									RenderNodes(section.Nodes());
								return;
							}
					}

					RenderNodes(el.Nodes());
				}

				private void AddValue(XElement el) {
					FlushText();

					var level = owner.GetSkillLevel();
					float value;

					var pBase = el.Attribute("base")?.Value;
					var pPer = el.Attribute("per")?.Value;
					var pIdx = el.Attribute("idx")?.Value;
					if (float.TryParse(pBase, out var fBase) && float.TryParse(pPer, out var fPer))
						value = fBase + fPer * level;
					else if (int.TryParse(pIdx, out var iIdx)) {
						var pSlot = el.Attribute("slot")?.Value;
						value = int.TryParse(pSlot, out var iSlot)
							? owner.GetSkillValue(iSlot, iIdx)
							: owner.GetSkillValue(-1, iIdx);
					}
					else
						value = 0f;

					var bR = el.Attribute("r")?.Value != null;
					var bInv = el.Attribute("inv")?.Value != null;
					var bSignless = el.Attribute("signless")?.Value != null;

					if (bInv) value = -value;
					if (bR) value *= 100f;

					var sign = bSignless || value == 0f ? "" : value > 0f ? "+" : "";
					var postfix = bR ? "%" : "";
					var color = value == 0f
						? "232323"
						: (bSignless ? -1f : 1f) * value > 0f
							? "0d6efd"
							: "dc3545";
					AddInlineText($"{sign}{value:#,##0.##}{postfix}", HexToColorLocal(color), -1);
				}

				private void AddComment(XElement el) {
					FlushText();

					var title = GetCommentTitle(el);
					var index = comments.Count;
					comments.Add(el);
					AddInlineText(title, CurrentColor, index, true);
				}

				private void AddDamage(XElement el, int damage) {
					var elems = (el.Attribute("elem")?.Value ?? "")
						.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
						.Select(v => v.Trim())
						.Where(v => !string.IsNullOrEmpty(v))
						.ToArray();

					foreach (var elem in elems)
						AddSymbol(GetElementSpriteName(elem));

					var elemDisps = string.Join("·", elems.Select(x => x switch
					{
						"physics" => "물리",
						"fire" => "화염",
						"ice" => "냉기",
						"lightning" => "전기",
						_ => "???",
					}));
					var adaptive = elems.Length > 1 ? "적응형 " : "";
					var text = elems.Length > 0
						? $" {damage:#,##0} {elemDisps} {adaptive}피해"
						: $" {damage:#,##0} 피해";
					AddInlineText(text, Color.black, -1);
				}

				private void AddRange(XElement el, Action<LayerRenderer> renderOverride = null) {
					var (inner, foreground) = GetRangeColors(el);
					var parentFontSize = Mathf.Max(1f, EffectiveFontSize);
					var contentFontSize = Mathf.Max(8f, parentFontSize * 0.8f);
					var rangeTextScale = contentFontSize / parentFontSize;
					var paddingX = parentFontSize * 0.32f;
					var paddingY = parentFontSize * 0.18f;
					var rangeLineHeight = parentFontSize + paddingY * 2f;

					var startX = x;
					var startY = y;
					if (startX > 0f) {
						var previewVerticalInset = (GetLineAdvance() - rangeLineHeight) * 0.5f;
						var previewContentYOffset = Mathf.Max(0f, (rangeLineHeight - contentFontSize) * 0.5f - contentFontSize * 0.16f);
						var preview = RenderRangeContent(
							new LayoutData(),
							new ElemDict(sections),
							new ElemList(comments),
							startX,
							startY,
							previewVerticalInset,
							previewContentYOffset);

						if (preview.WouldStartOrContinueOnAnotherLine())
							NewLine(false);
					}

					startX = x;
					startY = y;
					var verticalInset = (GetLineAdvance() - rangeLineHeight) * 0.5f;
					var contentYOffset = Mathf.Max(0f, (rangeLineHeight - contentFontSize) * 0.5f - contentFontSize * 0.16f);
					var rangeBoxIndex = layout.boxes.Count;
					var nested = RenderRangeContent(layout, sections, comments, startX, startY, verticalInset, contentYOffset);

					var firstLine = 0;
					while (firstLine < nested.LineCount && nested.IsLineEmpty(firstLine))
						firstLine++;

					if (firstLine >= nested.LineCount)
						return;

					var lastLine = nested.LineCount - 1;
					while (lastLine > firstLine && nested.IsLineEmpty(lastLine))
						lastLine--;

					if (firstLine == lastLine) {
						var lineX = nested.lineStarts[firstLine];
						var lineW = Mathf.Max(0f, nested.lineWidths[firstLine] - lineX);
						var lineTop = startY + nested.lineTops[firstLine];
						var contentW = Mathf.Min(maxWidth - lineX, Mathf.Max(lineW + paddingX * 2f, parentFontSize));
						var boxH = rangeLineHeight;
						var boxTop = lineTop - verticalInset;
						InsertBoxRect(ref rangeBoxIndex, Rect.MinMaxRect(originX + lineX, originY + boxTop - boxH, originX + lineX + contentW, originY + boxTop), inner, 0f, false);

						y = lineTop;
						x = lineX + contentW + 2f;
						lineHeight = Mathf.Max(lineHeight, boxH);
						ContentHeight = Mathf.Max(ContentHeight, -y);
						return;
					}

					for (var i = firstLine; i <= lastLine; i++) {
						if (nested.IsLineEmpty(i)) continue;

						var isLast = i == lastLine;
						var lineX = nested.lineStarts[i];
						var lineW = Mathf.Max(0f, nested.lineWidths[i] - lineX);
						var lineTop = startY + nested.lineTops[i];
						var boxTop = lineTop - verticalInset;
						var segmentW = isLast
							? Mathf.Min(maxWidth - lineX, Mathf.Max(lineW + paddingX * 2f, parentFontSize))
							: maxWidth - lineX;
						InsertBoxRect(ref rangeBoxIndex, Rect.MinMaxRect(originX + lineX, originY + boxTop - rangeLineHeight, originX + lineX + segmentW, originY + boxTop), inner, 0f, false);
					}

					y = startY + nested.lineTops[lastLine];
					var lastLineX = nested.lineStarts[lastLine];
					var lastLineW = Mathf.Max(0f, nested.lineWidths[lastLine] - lastLineX);
					x = lastLineX + Mathf.Min(maxWidth - lastLineX, Mathf.Max(lastLineW + paddingX * 2f, parentFontSize)) + 2f;
					lineHeight = Mathf.Max(lineHeight, rangeLineHeight);
					ContentHeight = Mathf.Max(ContentHeight, -y);

					LayerRenderer RenderRangeContent(
						LayoutData targetLayout,
						ElemDict targetSections,
						ElemList targetComments,
						float renderStartX,
						float renderStartY,
						float renderVerticalInset,
						float renderContentYOffset) {
						var renderer = new LayerRenderer(
							owner,
							template,
							targetLayout,
							Mathf.RoundToInt(maxWidth - paddingX * 2f),
							maxHeight,
							defaultFontSize,
							targetSections,
							targetComments,
							HexToColorLocal(foreground),
							true,
							symbolAtlas,
							originX + paddingX,
							originY + renderStartY - renderVerticalInset - renderContentYOffset,
							textScale * rangeTextScale
						) {
							x = renderStartX,
							lineStart = renderStartX
						};

						if (renderOverride != null) renderOverride(renderer);
						else renderer.RenderNodes(el.Nodes());
						renderer.Trim();
						return renderer;
					}
				}

				private void AddBox(XElement el) {
					if (x > 0f) NewLine(false);

					var title = el.Attribute("t")?.Value ?? el.Attribute("title")?.Value ?? "Box";
					var padX = Mathf.Max(8f, template.fontSize * 0.45f);
					var padY = Mathf.Max(6f, template.fontSize * 0.35f);
					var titleSize = Mathf.Max(8, Mathf.RoundToInt(template.fontSize * 0.72f));
					var titleExtraPadding = Mathf.Max(4f, padY * 0.6f);
					var titleBarHeight = Mathf.Max(titleSize + 4f, titleSize + padY * 0.7f) + titleExtraPadding;
					var contentSize = Mathf.Max(8, Mathf.RoundToInt(template.fontSize * 0.9f));
					var startY = y;

					var titleSizePx = MeasureText(title, titleSize);
					var titleOffsetY = Mathf.Max(0f, (titleBarHeight - Mathf.Max(1f, titleSizePx.y)) * 0.5f + 1f);
					layout.texts.Add(new TextDraw {
						text = title,
						position = new Vector3(originX + Mathf.Max(2f, padX * 0.45f), originY + startY - titleOffsetY, 0f),
						fontSize = titleSize,
						scale = 1f,
						color = Color.white,
						shadow = true
					});

					var nested = new LayerRenderer(
						owner,
						template,
						layout,
						Mathf.RoundToInt(maxWidth - padX * 2f),
						maxHeight,
						contentSize,
						sections,
						comments,
						CurrentColor,
						shadowText,
						symbolAtlas,
						originX + padX,
						originY + startY - titleBarHeight - padY,
						textScale
					) {
						x = 0f,
						y = 0f,
						lineHeight = contentSize * textScale
					};
					nested.RenderNodes(el.Nodes());
					nested.Trim();

					var boxHeight = titleBarHeight + padY + nested.ContentHeight + padY;
					AddBoxRect(Rect.MinMaxRect(originX, originY + startY - boxHeight, originX + maxWidth, originY + startY), new Color(0.8392157f, 0.854902f, 0.8705882f, 0.1490196f), titleBarHeight, true);

					y -= boxHeight + lineGap * 2f;
					x = 0f;
					lineHeight = EffectiveFontSize;
					ContentHeight = Mathf.Max(ContentHeight, -y);
				}

				private void AddText(string text) {
					if (string.IsNullOrEmpty(text)) return;

					var start = 0;
					for (var i = 0; i < text.Length; i++) {
						if (text[i] != '\n') continue;
						if (i > start)
							AppendText(text.Substring(start, i - start));
						FlushText();
						NewLine();
						start = i + 1;
					}

					if (start < text.Length)
						AppendText(text.Substring(start));
				}

				private void AppendText(string text) {
					if (!string.IsNullOrEmpty(text))
						pendingText.Append(text);
				}

				private void FlushText() {
					if (pendingText.Length <= 0) return;

					var text = pendingText.ToString();
					pendingText.Length = 0;
					AddInlineText(text, CurrentColor, -1);
				}

				private void AddInlineText(string text, Color color, int commentIndex, bool underline = false) {
					if (string.IsNullOrEmpty(text)) return;

					text = text.Replace("\r", "");
					var line = new StringBuilder();
					var lineLimit = Mathf.Max(2f, maxWidth - x);

					foreach (var part in EnumerateWrapUnits(text)) {
						if (part == "\n") {
							EmitTextRun(line.ToString(), color, commentIndex, underline);
							line.Length = 0;
							NewLine(false);
							lineLimit = maxWidth;
							continue;
						}

						var candidate = line.ToString() + part;
						if (!string.IsNullOrWhiteSpace(part) && MeasureText(candidate, Mathf.RoundToInt(defaultFontSize), textScale).x > lineLimit) {
							if (line.Length > 0) {
								EmitTextRun(line.ToString().TrimEnd(), color, commentIndex, underline);
								line.Length = 0;
								NewLine(false);
								lineLimit = maxWidth;
							}
							else if (x > 0f) {
								NewLine(false);
								lineLimit = maxWidth;
							}

							if (string.IsNullOrWhiteSpace(part)) continue;
						}

						line.Append(part);
					}

					if (line.Length > 0)
						EmitTextRun(line.ToString(), color, commentIndex, underline);
				}

				private void EmitTextRun(string text, Color color, int commentIndex, bool underline = false) {
					if (string.IsNullOrEmpty(text)) return;

					var fontSize = Mathf.RoundToInt(defaultFontSize);
					var size = MeasureText(text, fontSize, textScale);
					layout.texts.Add(new TextDraw {
						text = text,
						position = new Vector3(originX + x, originY + y, 0f),
						fontSize = fontSize,
						scale = textScale,
						color = color,
						shadow = shadowText
					});

					if (commentIndex >= 0) {
						layout.comments.Add(new CommentHit {
							index = commentIndex,
							rect = Rect.MinMaxRect(originX + x, originY + y - size.y, originX + x + size.x, originY + y)
						});
					}

					if (underline) {
						var underlineTop = y - Mathf.Max(1f, size.y) + Mathf.Max(1f, EffectiveFontSize * 0.12f);
						layout.boxes.Add(new BoxDraw {
							rect = Rect.MinMaxRect(originX + x, originY + underlineTop - 1f, originX + x + size.x, originY + underlineTop),
							color = color,
							line = true
						});
					}

					x += size.x;
					lineHeight = Mathf.Max(lineHeight, size.y);
				}

				private void AddSymbol(string spriteName) {
					FlushText();

					var sp = !string.IsNullOrEmpty(spriteName) && symbolAtlas != null ? symbolAtlas.GetSprite(spriteName) : null;
					if (sp == null) {
						ReserveSymbolSpace();
						return;
					}

					var size = Mathf.Max(8f, EffectiveFontSize);
					var width = sp.height > 0 ? size * sp.width / sp.height : size;
					var advance = width + Mathf.Max(2f, EffectiveFontSize * 0.15f);

					if (x > 0f && x + advance > maxWidth)
						NewLine(false);

					var topAdjust = Mathf.Max(0f, (GetLineAdvance() - size) * 0.5f);
					layout.symbols.Add(new SymbolDraw {
						spriteName = spriteName,
						rect = Rect.MinMaxRect(originX + x, originY + y - topAdjust - size, originX + x + width, originY + y - topAdjust),
						color = Color.white
					});

					x += advance;
					lineHeight = Mathf.Max(lineHeight, size);
				}

				private void ReserveSymbolSpace() {
					var size = Mathf.Max(8f, EffectiveFontSize);
					var advance = size + Mathf.Max(2f, EffectiveFontSize * 0.15f);

					if (x > 0f && x + advance > maxWidth)
						NewLine(false);

					x += advance;
					lineHeight = Mathf.Max(lineHeight, size);
				}

				private void AddBoxRect(Rect rect, Color color, float titleHeight, bool titleOnlyFill) {
					layout.boxes.Add(new BoxDraw {
						rect = rect,
						color = color,
						titleHeight = titleHeight,
						titleOnlyFill = titleOnlyFill
					});
				}

				private void InsertBoxRect(ref int index, Rect rect, Color color, float titleHeight, bool titleOnlyFill) {
					layout.boxes.Insert(Mathf.Clamp(index, 0, layout.boxes.Count), new BoxDraw {
						rect = rect,
						color = color,
						titleHeight = titleHeight,
						titleOnlyFill = titleOnlyFill
					});
					index++;
				}

				private void NewLine(bool addHr = true) {
					CaptureLine();
					var advance = GetLineAdvance();
					if (addHr) {
						var margin = Mathf.Max(4f, lineGap * 1.6f);
						var lineBottom = y - advance;
						var hrTop = lineBottom - margin;
						layout.boxes.Add(new BoxDraw {
							rect = Rect.MinMaxRect(originX, originY + hrTop - 1f, originX + maxWidth, originY + hrTop),
							color = new Color(1f, 1f, 1f, 0.25f),
							line = true
						});
						y = hrTop - 1f - margin;
					}
					else
						y -= advance + lineGap;

					x = 0f;
					lineStart = 0f;
					ContentHeight = Mathf.Max(ContentHeight, -y);
					lineHeight = EffectiveFontSize;
				}

				private float GetLineAdvance()
					=> Mathf.Max(lineHeight, EffectiveFontSize + GetLineSpacing(EffectiveFontSize));

				private void CaptureLine() {
					if (trimmed) return;
					lineStarts.Add(lineStart);
					lineWidths.Add(x);
					lineTops.Add(y);
				}

				private bool IsLineEmpty(int index)
					=> index < 0 || index >= lineWidths.Count || lineWidths[index] <= lineStarts[index] + 0.01f;

				private bool WouldStartOrContinueOnAnotherLine() {
					var firstLine = 0;
					while (firstLine < LineCount && IsLineEmpty(firstLine))
						firstLine++;

					if (firstLine >= LineCount) return false;
					if (firstLine > 0) return true;

					var lastLine = LineCount - 1;
					while (lastLine > firstLine && IsLineEmpty(lastLine))
						lastLine--;

					return lastLine > firstLine;
				}

				private Vector2 MeasureText(string text, int fontSize, float scale = 1f) {
					if (string.IsNullOrEmpty(text)) return Vector2.zero;

					SkillDesc.ConfigureText(template, fontSize, 100000, maxHeight, CurrentColor);
					var size = NGUIText.CalculatePrintedSize(text);
					NGUIText.bitmapFont = null;
					NGUIText.dynamicFont = null;
					return size * Mathf.Max(0.01f, scale);
				}

				private Color CurrentColor => currentColor;

				private static IEnumerable<string> EnumerateWrapUnits(string text) {
					for (var i = 0; i < text.Length; i++) {
						if (text[i] == '\n') {
							yield return "\n";
							continue;
						}

						if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) {
							yield return text.Substring(i, 2);
							i++;
							continue;
						}

						yield return text[i].ToString();
					}
				}

				private static string GetElementSpriteName(string type) {
					return "UI_Battle_Atktype_" + (type switch {
						"physics" => "normal",
						"lightning" => "electric",
						_ => type,
					});
				}

				private static string GetCommentTitle(XElement el) {
					var title = el.Attribute("t")?.Value ?? el.Attribute("title")?.Value;
					if (!string.IsNullOrEmpty(title)) return title;

					var text = (el.Value ?? "").Trim();
					return string.IsNullOrEmpty(text) ? "Comment" : text;
				}

				private (Color inner, string foreground) GetRangeColors(XElement el) {
					var tuple = el.Name.LocalName == "dmg"
						? ("fce391", "000000")
						: el.Attribute("typ")?.Value switch {
							"dmg" => ("fce391", "000000"),
							"note" => ("e2f0d9", "000000"),
							"buff" => ("f8f9fa", "000000"),
							"important" => ("0d6efd", "ffffff"),
							"attr" => ("ffc107", "000000"),
							"cond" => ("712529", "ffffff"),
							"chance" => ("198754", "ffffff"),
							"ref" => ("21252926", "000000"),
							_ => ("fff2cc", "000000"),
						};

					return (HexToColorLocal(tuple.Item1), tuple.Item2);
				}

				private static Color HexToColorLocal(string color) {
					var value = Convert.ToUInt32(color, 16);
					if (color.Length == 6)
						return new Color(
							((value >> 16) & 0xFF) / 255f,
							((value >> 8) & 0xFF) / 255f,
							(value & 0xFF) / 255f,
							1f);

					return new Color(
						((value >> 24) & 0xFF) / 255f,
						((value >> 16) & 0xFF) / 255f,
						((value >> 8) & 0xFF) / 255f,
						(value & 0xFF) / 255f);
				}
			}

			private static GameObject CreateChildObject(string name, Transform parent) => CreateChildObject(name, parent, Vector3.zero);
			private static GameObject CreateChildObject(string name, Transform parent, Vector3 localPosition) {
				var go = new GameObject(name);
				var tr = go.transform;
				if (parent != null) {
					tr.SetParent(parent, false);
					go.layer = parent.gameObject.layer;
				}
				tr.localPosition = localPosition;
				tr.localScale = Vector3.one;
				return go;
			}

			private sealed class TextLayerWidget : UIWidget {
				public MeshData mesh;
				public List<CommentHit> comments;
				public Material textMaterial;

				public override Material material => textMaterial;
				public override Texture mainTexture {
					get {
						var mat = material;
						return mat != null ? mat.mainTexture : null;
					}
				}

				public bool IsCommentHit(Vector3 worldPos) => TryGetCommentAtWorld(worldPos, out _);

				public bool TryGetCommentAtWorld(Vector3 worldPos, out int index) {
					index = -1;
					if (comments == null) return false;

					var local = transform.InverseTransformPoint(worldPos);
					for (var i = 0; i < comments.Count; i++) {
						var hit = comments[i];
						if (!hit.rect.Contains(new Vector2(local.x, local.y))) continue;

						index = hit.index;
						return true;
					}

					return false;
				}

				public override void OnFill(List<Vector3> verts, List<Vector2> uvs, List<Color> cols) {
					var offset = verts.Count;
					mesh?.AddTo(verts, uvs, cols);

					if (onPostFill != null)
						onPostFill(this, offset, verts, uvs, cols);
				}
			}

			private sealed class BoxLayerWidget : UIWidget {
				public MeshData mesh;
				public Texture texture;

				private Shader boxShader;

				public override Texture mainTexture => texture != null ? texture : Texture2D.whiteTexture;
				public override Shader shader => boxShader != null ? boxShader : (boxShader = Shader.Find("Unlit/Transparent Colored"));

				public override void OnFill(List<Vector3> verts, List<Vector2> uvs, List<Color> cols) {
					mesh?.AddTo(verts, uvs, cols);
					if (mesh == null) return;

					for (var i = 0; i < mesh.pixelLines.Count; i++)
						AddPixelLine(verts, uvs, cols, mesh.pixelLines[i]);
				}

				private void AddPixelLine(List<Vector3> verts, List<Vector2> uvs, List<Color> cols, PixelLineDraw line) {
					var left = Mathf.Round(line.rect.xMin);
					var top = Mathf.Round(line.rect.yMax);
					var width = Mathf.Max(1f, Mathf.Round(line.rect.width));
					var x0 = left;
					var x1 = left + width;
					var y0 = top - 1f;
					var y1 = top;

					var camera = panel != null ? NGUITools.FindCameraForLayer(gameObject.layer) : null;
					if (camera != null) {
						var t = cachedTransform;
						var ptLeft = new Vector3(line.rect.xMin, line.rect.yMax, 0f);
						var ptRight = new Vector3(line.rect.xMax, line.rect.yMax, 0f);
						ptLeft = camera.WorldToScreenPoint(t.TransformPoint(ptLeft));
						ptRight = camera.WorldToScreenPoint(t.TransformPoint(ptRight));
						var depth = ptLeft.z;
						var snappedLeft = Mathf.Round(ptLeft.x);
						var snappedRight = Mathf.Round(ptRight.x);
						var screenX0 = Mathf.Min(snappedLeft, snappedRight);
						var screenX1 = Mathf.Max(snappedLeft, snappedRight);
						if (screenX1 - screenX0 < 1f) screenX1 = screenX0 + 1f;
						var screenY1 = Mathf.Round(ptLeft.y);
						var screenY0 = screenY1 - 1f;
						var localMin = t.InverseTransformPoint(camera.ScreenToWorldPoint(new Vector3(screenX0, screenY0, depth)));
						var localMax = t.InverseTransformPoint(camera.ScreenToWorldPoint(new Vector3(screenX1, screenY1, depth)));

						x0 = Mathf.Min(localMin.x, localMax.x);
						x1 = Mathf.Max(localMin.x, localMax.x);
						y0 = Mathf.Min(localMin.y, localMax.y);
						y1 = Mathf.Max(localMin.y, localMax.y);
					}

					var c = line.color;
					c.a *= finalAlpha;
					AddTexturedQuad(verts, uvs, cols, x0, y0, x1, y1, 0.5f, 0.5f, 0.5f, 0.5f, c);
				}
			}

			private sealed class SymbolLayerWidget : UIWidget {
				public MeshData mesh;
				public Material symbolMaterial;

				public override Material material => symbolMaterial;
				public override Texture mainTexture {
					get {
						var mat = material;
						return mat != null ? mat.mainTexture : null;
					}
				}

				public override void OnFill(List<Vector3> verts, List<Vector2> uvs, List<Color> cols) {
					mesh?.AddTo(verts, uvs, cols);
				}
			}

			private static void AddSlicedTexture(List<Vector3> verts, List<Vector2> uvs, List<Color> cols, Texture tex, BoxDraw box) {
				var rect = box.rect;
				if (rect.width <= 0f || rect.height <= 0f) return;

				if (tex.width <= 0 || tex.height <= 0) return;

				var sourceBorder = new Vector4(
					Mathf.Min(8f, tex.width * 0.5f),
					Mathf.Min(8f, tex.height * 0.5f),
					Mathf.Min(8f, tex.width * 0.5f),
					Mathf.Min(8f, tex.height * 0.5f)
				);
				var drawBorder = new Vector4(4f, 4f, 4f, 4f);
				drawBorder.x = Mathf.Min(drawBorder.x, rect.width * 0.5f);
				drawBorder.z = Mathf.Min(drawBorder.z, rect.width * 0.5f);
				drawBorder.y = Mathf.Min(drawBorder.y, rect.height * 0.5f);
				drawBorder.w = Mathf.Min(drawBorder.w, rect.height * 0.5f);

				var invW = 1f / tex.width;
				var invH = 1f / tex.height;
				var x = new[] { rect.xMin, rect.xMin + drawBorder.x, rect.xMax - drawBorder.z, rect.xMax };
				var y = new[] { rect.yMin, rect.yMin + drawBorder.y, rect.yMax - drawBorder.w, rect.yMax };
				var u = new[] { 0f, sourceBorder.x * invW, 1f - sourceBorder.z * invW, 1f };
				var v = new[] { 0f, sourceBorder.y * invH, 1f - sourceBorder.w * invH, 1f };

				var c = box.color;
				var titleBottom = rect.yMax - Mathf.Clamp(box.titleHeight, 0f, rect.height);

				for (var ix = 0; ix < 3; ix++) {
					for (var iy = 0; iy < 3; iy++) {
						if (box.titleOnlyFill && ix == 1 && iy == 1) {
							var fillBottom = Mathf.Max(titleBottom, y[iy]);
							var fillTop = y[iy + 1];
							if (fillBottom >= fillTop) continue;

							var uvBottom = Mathf.Lerp(v[iy], v[iy + 1], Mathf.InverseLerp(y[iy], y[iy + 1], fillBottom));
							AddTexturedQuad(verts, uvs, cols, x[ix], fillBottom, x[ix + 1], fillTop, u[ix], uvBottom, u[ix + 1], v[iy + 1], c);
							continue;
						}

						AddTexturedQuad(verts, uvs, cols, x[ix], y[iy], x[ix + 1], y[iy + 1], u[ix], v[iy], u[ix + 1], v[iy + 1], c);
					}
				}
			}

			private static void AddTexturedQuad(
				List<Vector3> verts, List<Vector2> uvs, List<Color> cols,
				float x0, float y0, float x1, float y1,
				float u0, float v0, float u1, float v1, Color color
			) {
				verts.Add(new Vector3(x0, y0, 0f));
				verts.Add(new Vector3(x0, y1, 0f));
				verts.Add(new Vector3(x1, y1, 0f));
				verts.Add(new Vector3(x1, y0, 0f));

				uvs.Add(new Vector2(u0, v0));
				uvs.Add(new Vector2(u0, v1));
				uvs.Add(new Vector2(u1, v1));
				uvs.Add(new Vector2(u1, v0));

				cols.Add(color);
				cols.Add(color);
				cols.Add(color);
				cols.Add(color);
			}
			#endregion

			#region Preprocess
			private string TranspileSections(string input) {
				var rgDefine = new Regex(@"\$\$([A-Za-z0-9\-_]+)~\n(.+?)\n~\$\$(\1)$", RegexOptions.Compiled | RegexOptions.Singleline);
				var rgCall = new Regex(@"\$\$([A-Za-z0-9\-_]+):([^$]+)\$", RegexOptions.Compiled);

				var sections = new StringBuilder();

				// Convert section defines
				var sb = new StringBuilder();
				var cursor = 0;
				while (cursor < input.Length) {
					var m = rgDefine.Match(input, cursor);
					if (!m.Success) break;

					if (m.Index != cursor)
						sb.Append(input.Substring(cursor, m.Index - cursor));

					var name = m.Groups[1].Value;
					var body = m.Groups[2].Value.Trim();
					sections.Append($"<define name=\"{name}\">{body}</define>");

					cursor = m.Index + m.Length;
				}
				if (cursor < input.Length)
					sb.Append(input.Substring(cursor));

				// Convert section calls
				input = sb.ToString();
				sb.Clear();
				cursor = 0;
				while (cursor < input.Length) {
					var m = rgCall.Match(input, cursor);
					if (!m.Success) break;

					if (m.Index != cursor)
						sb.Append(input.Substring(cursor, m.Index - cursor));

					var name = m.Groups[1].Value;
					var @params = m.Groups[2].Value.Split(',');
					sb.Append($"<import name=\"{name}\"");
					for (var pi = 0; pi < @params.Length; pi++)
						sb.Append($" p{pi}=\"{@params[pi]}\"");
					sb.Append($"/>");

					cursor = m.Index + m.Length;
				}
				if (cursor < input.Length)
					sb.Append(input.Substring(cursor));

				return sections.ToString() + sb.ToString();
			}
			private string PreprocessBox(string input) {
				var regexBox = new Regex(@"\n?(<box[^>]*>)\n?(.*?)\n?(</box>)\n?", RegexOptions.Compiled | RegexOptions.Singleline);
				var regexLine = new Regex(@"(.+)", RegexOptions.Compiled | RegexOptions.Multiline);

				return regexBox.Replace(input, m => m.Groups[1].Value + regexLine.Replace(m.Groups[2].Value, "  $1") + m.Groups[3].Value);
			}
			#endregion
		}
	}
}

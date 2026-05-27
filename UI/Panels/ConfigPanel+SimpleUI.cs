using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace Symphony.UI.Panels {
	internal partial class ConfigPanel {
		private enum ConfigPanel_SimpleUI_SubpageType : int {
			None = 0,
			Battle, // 전투 관련
			ListItemDisplay, // 목록 항목 표시
			ListSearch, // 목록 검색
			ListSorting, // 목록 정렬
			CharacterDetail, // 전투원 상세 정보
			Workbench, // 공방
			Composite, // 복합
			Benefit, // 베네핏 효과
		}
		private ConfigPanel_SimpleUI_SubpageType Conf_SimpleUI_Subpage = ConfigPanel_SimpleUI_SubpageType.None;

		private bool BenefitExpandNormal = false;
		private bool BenefitExpandFinal = false;

		private HashSet<string> BenefitNormalList_Cached = null;
		private HashSet<string> BenefitFinalList_Cached = null;

		private static Table_CharCollection[] UnitKeys = null;
		private static Dictionary<int, string> UnitNameTable = null;
		private static Throttle UnitKeysThrottle = new(FetchUnitKey, TimeSpan.TicksPerSecond);

		private static void FetchUnitKey() {
			var man = SingleTon<DataManager>.Instance;
			if ((man.GetAllPc()?.Count ?? 0) == 0) return;
			// Should player's character list not empty,
			// that means game loaded fully

			var pc = SingleTon<DataManager>.Instance.GetTableCharCollection();
			ConfigPanel.UnitKeys = pc.Values
				.OrderBy(x => x.Char_Number)
				.ToArray();

			ConfigPanel.UnitNameTable = pc.Values
				.ToDictionary(
					v => v.Char_Number,
					v => v.Char_Name.Localize()
				);
		}

		private void Conf_SimpleUI(ref float offset) {
			if (ConfigPanel.UnitKeys == null)
				ConfigPanel.UnitKeysThrottle.Run();

			if(this.BenefitNormalList_Cached == null) {
				this.BenefitNormalList_Cached = new(Conf.SimpleUI.List_BenefitUnits_Normal.Value.Split(",", StringSplitOptions.RemoveEmptyEntries));
				this.BenefitFinalList_Cached = new(Conf.SimpleUI.List_BenefitUnits_Final.Value.Split(",", StringSplitOptions.RemoveEmptyEntries));
			}

			void Subpage_Battle(ref float offset) {
				DrawToggle(ref offset, "마지막 방문 전투 지역 버튼 추가", Conf.SimpleUI.Use_LastBattleMap);
				DrawToggle(ref offset, "마지막 자율 전투 지역 버튼 추가", Conf.SimpleUI.Use_LastOfflineBattle);
				offset += 10f;
				DrawToggle(ref offset, "자율 전투 확인 대신 맵으로", Conf.SimpleUI.Use_OfflineBattle_Bypass);
				offset += 10f;
				DrawToggle(ref offset, "전투 적 미리보기", Conf.SimpleUI.Use_MapEnemyPreview);
			}
			void Subpage_ListItemDisplay(ref float offset) {
				DrawToggle(ref offset, "전투원 소모 자원 표기 기본 끄기", Conf.SimpleUI.Default_CharacterCost_Off);
				offset += 10f;
				DrawToggle(ref offset, "더블 클릭으로 전투원 상세 보기", Conf.SimpleUI.DblClick_CharWarehouse);
				offset += 10f;
				DrawToggle(ref offset, "더 작은 전투원 목록 항목", Conf.SimpleUI.Small_CharWarehouse);
				DrawToggle(ref offset, "더 작은 전투원 선택 항목", Conf.SimpleUI.Small_CharSelection);
				DrawToggle(ref offset, "더 작은 전투원 도감 항목", Conf.SimpleUI.Small_CharScrapbook);
				DrawToggle(ref offset, "더 작은 장비 목록 항목", Conf.SimpleUI.Small_ItemWarehouse);
				DrawToggle(ref offset, "더 작은 장비 선택 항목", Conf.SimpleUI.Small_ItemSelection);
				DrawToggle(ref offset, "더 작은 임시 창고 항목", Conf.SimpleUI.Small_TempInventory);
				DrawToggle(ref offset, "더 작은 소모품 목록 항목", Conf.SimpleUI.Small_Consumables);
			}
			void Subpage_ListSearch(ref float offset) {
				DrawToggle(ref offset, "전투원 목록에서 Enter로 검색", Conf.SimpleUI.EnterToSearch_CharWarehouse);
				DrawToggle(ref offset, "전투원 선택에서 Enter로 검색", Conf.SimpleUI.EnterToSearch_CharSelection);
				DrawToggle(ref offset, "장비 목록에서 Enter로 검색", Conf.SimpleUI.EnterToSearch_ItemWarehouse);
				DrawToggle(ref offset, "장비 선택에서 Enter로 검색", Conf.SimpleUI.EnterToSearch_ItemSelection);
			}
			void Subpage_ListSorting(ref float offset) {
				DrawToggle(ref offset, "소모품 목록 정렬", Conf.SimpleUI.Sort_Consumables);
				DrawToggle(ref offset, "장비 목록 정렬 (전용 장비 우선)", Conf.SimpleUI.Sort_Equips_ExclusiveFirst);
				offset += 10f;
				DrawToggle(ref offset, "정렬 기준 추가", Conf.SimpleUI.Use_SortBy_Extra);
			}
			void Subpage_CharacterDetail(ref float offset) {
				DrawToggle(ref offset, "이전/다음 전투원 버튼 추가", Conf.SimpleUI.Use_CharacterDetail_NextPrev);

				DrawToggle(ref offset, "즐겨찾기 추가", Conf.SimpleUI.Use_Character_Favorite);

				DrawToggle(ref offset, "더 나은 전투원 강화", Conf.SimpleUI.Use_BetterPCEnchant);
			}
			void Subpage_Workbench(ref float offset) {
				DrawToggle(ref offset, "전투원 제조 결과 미리보기", Conf.SimpleUI.Use_CharacterMakingPreview);
				DrawToggle(ref offset, "장비 제조 결과 미리보기", Conf.SimpleUI.Use_EquipMakingPreview);
				offset += 10f;
				DrawToggle(ref offset, "분해에 모든 전투원 선택 추가", Conf.SimpleUI.Use_Disassemble_SelectAll_Character);
				DrawToggle(ref offset, "분해에 모든 장비 선택 추가", Conf.SimpleUI.Use_Disassemble_SelectAll_Equip);
			}
			void Subpage_Composite(ref float offset) {
				DrawToggle(ref offset, "도감은 멋져야 한다", Conf.SimpleUI.Use_ScrapbookMustBeFancy);
				DrawLabel(ref offset, """
전투원 도감에서 스킨 배경을 표시하며,
자세히 보기 화면에 진입할 때 회전하지 않도록 변경하고,
배경 및 장식품 감추기/보이기 버튼을 추가합니다.
""", Color_description, 12);
					

				offset += 10f;

				DrawToggle(ref offset, "교환소: 손도 깔끔", Conf.SimpleUI.Use_Exchange_NoMessyHand);
				DrawLabel(ref offset, """
교환소의 '품절된 상품 숨기기'를 체크상태로 변경하고,
현재 보고있는 상품에 관련된 소모품만 목록에 표시합니다.
한 번에 20개만 구매할 수 있는 상품을 자동으로
반복 구매할 수 있습니다.
""", Color_description, 12);
					

				offset += 10f;

				DrawToggle(ref offset, "더 좋은 시설 인벤토리", Conf.SimpleUI.Use_BetterFacilityInventory);
				DrawLabel(ref offset, "기지의 시설 목록이 레벨 및 이름순으로 정렬되고, 보유중인 시설은 레벨이 표시됩니다.", Color_description, 12);

				offset += 10f;

				DrawToggle(ref offset, "기지 네비게이션 돌려내", Conf.SimpleUI.Use_GiveMeBackLivingStationNavigation);
				DrawLabel(ref offset, "기지의 상단에 네비게이션 버튼을 다시 표시합니다.", Color_description, 12);
			}
			void Subpage_Benefit(ref float offset) {
				DrawLineButton(ref offset, "일반 베네핏 효과 대상 " + (this.BenefitExpandNormal ? "▲" : "▼"), () => {
					this.BenefitExpandNormal = !this.BenefitExpandNormal;
				});
				if(this.BenefitExpandNormal) {
					if (UnitKeys != null) {
						foreach (var kv in UnitKeys) {
							bool b = this.BenefitNormalList_Cached.Contains(kv.Key);
							DrawToggle(ref offset, ConfigPanel.UnitNameTable[kv.Char_Number], ref b, () => {
								if (b)
									this.BenefitNormalList_Cached.Add(kv.Key);
								else
									this.BenefitNormalList_Cached.Remove(kv.Key);

								Conf.SimpleUI.List_BenefitUnits_Normal.Value = string.Join(",", this.BenefitNormalList_Cached);
							}, 10);
						}
					}
				}

				offset += 10f;
				DrawSeparator(ref offset);
				offset += 10f;

				DrawLineButton(ref offset, "최종 베네핏 효과 대상 " + (this.BenefitExpandFinal ? "▲" : "▼"), () => {
					this.BenefitExpandFinal = !this.BenefitExpandFinal;
				});
				if (this.BenefitExpandFinal) {
					if (UnitKeys != null) {
						foreach (var kv in UnitKeys) {
							bool b = this.BenefitFinalList_Cached.Contains(kv.Key);
							DrawToggle(ref offset, ConfigPanel.UnitNameTable[kv.Char_Number], ref b, () => {
								if (b)
									this.BenefitFinalList_Cached.Add(kv.Key);
								else
									this.BenefitFinalList_Cached.Remove(kv.Key);

								Conf.SimpleUI.List_BenefitUnits_Final.Value = string.Join(",", this.BenefitFinalList_Cached);
							}, 10);
						}
					}
				}
			}

			var headingRect = new Rect(60, offset, WIDTH_FILL - 60, 20);
			if (this.Conf_SimpleUI_Subpage != ConfigPanel_SimpleUI_SubpageType.None) {
				DrawLineButton(ref offset, "< 뒤로", () => {
					this.Conf_SimpleUI_Subpage = ConfigPanel_SimpleUI_SubpageType.None;
				}, 0, WIDTH_FILL - 50);
				DrawSeparator(ref offset);
			}

			switch (this.Conf_SimpleUI_Subpage) {
				case ConfigPanel_SimpleUI_SubpageType.None:
					DrawLineButton(ref offset, "전투 관련 개선", () => {
						this.Conf_SimpleUI_Subpage = ConfigPanel_SimpleUI_SubpageType.Battle;
					});
					DrawLineButton(ref offset, "목록 항목 표시 개선", () => {
						this.Conf_SimpleUI_Subpage = ConfigPanel_SimpleUI_SubpageType.ListItemDisplay;
					});
					DrawLineButton(ref offset, "목록 검색 개선", () => {
						this.Conf_SimpleUI_Subpage = ConfigPanel_SimpleUI_SubpageType.ListSearch;
					});
					DrawLineButton(ref offset, "목록 정렬 개선", () => {
						this.Conf_SimpleUI_Subpage = ConfigPanel_SimpleUI_SubpageType.ListSorting;
					});
					DrawLineButton(ref offset, "전투원 상세 정보", () => {
						this.Conf_SimpleUI_Subpage = ConfigPanel_SimpleUI_SubpageType.CharacterDetail;
					});
					DrawLineButton(ref offset, "공방 개선", () => {
						this.Conf_SimpleUI_Subpage = ConfigPanel_SimpleUI_SubpageType.Workbench;
					});
					DrawLineButton(ref offset, "복합 개선", () => {
						this.Conf_SimpleUI_Subpage = ConfigPanel_SimpleUI_SubpageType.Composite;
					});
					DrawLineButton(ref offset, "베네핏 효과", () => {
						this.Conf_SimpleUI_Subpage = ConfigPanel_SimpleUI_SubpageType.Benefit;
					});

					DrawSeparator(ref offset);

					DrawToggle(ref offset, "편성에 전체 해제 추가", Conf.SimpleUI.Use_Squad_Clear);

					DrawSeparator(ref offset);

					DrawToggle(ref offset, "스크롤/패닝 가속, 줌 반전하기", Conf.SimpleUI.Use_AccelerateScrollDelta);

					DrawSeparator(ref offset);

					DrawToggle(ref offset, "스토리 뷰어 텍스트 표시 문제 수정", Conf.SimpleUI.Use_NovelDialog_LabelFix);

					DrawSeparator(ref offset);

					DrawToggle(ref offset, "소모품 설명 스크롤화", Conf.SimpleUI.Use_ScrollableConsumableDescription);
					break;
				case ConfigPanel_SimpleUI_SubpageType.Battle:
					GUIX.Heading(headingRect, "전투 관련 개선", alignment: TextAnchor.MiddleCenter);
					Subpage_Battle(ref offset);
					break;
				case ConfigPanel_SimpleUI_SubpageType.ListItemDisplay:
					GUIX.Heading(headingRect, "목록 항목 표시 개선", alignment: TextAnchor.MiddleCenter);
					Subpage_ListItemDisplay(ref offset);
					break;
				case ConfigPanel_SimpleUI_SubpageType.ListSearch:
					GUIX.Heading(headingRect, "목록 검색 개선", alignment: TextAnchor.MiddleCenter);
					Subpage_ListSearch(ref offset);
					break;
				case ConfigPanel_SimpleUI_SubpageType.ListSorting:
					GUIX.Heading(headingRect, "목록 정렬 개선", alignment: TextAnchor.MiddleCenter);
					Subpage_ListSorting(ref offset);
					break;
				case ConfigPanel_SimpleUI_SubpageType.CharacterDetail:
					GUIX.Heading(headingRect, "전투원 상세 정보 개선", alignment: TextAnchor.MiddleCenter);
					Subpage_CharacterDetail(ref offset);
					break;
				case ConfigPanel_SimpleUI_SubpageType.Workbench:
					GUIX.Heading(headingRect, "공방 개선", alignment: TextAnchor.MiddleCenter);
					Subpage_Workbench(ref offset);
					break;
				case ConfigPanel_SimpleUI_SubpageType.Composite:
					GUIX.Heading(headingRect, "복합 개선", alignment: TextAnchor.MiddleCenter);
					Subpage_Composite(ref offset);
					break;
				case ConfigPanel_SimpleUI_SubpageType.Benefit:
					GUIX.Heading(headingRect, "베네핏 효과", alignment: TextAnchor.MiddleCenter);
					Subpage_Benefit(ref offset);
					break;
			}

		}
	}
}

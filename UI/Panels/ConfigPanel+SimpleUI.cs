using UnityEngine;

namespace Symphony.UI.Panels {
	internal partial class ConfigPanel {
		private enum ConfigPanel_SimpleUI_SubpageType : int {
			None = 0,
			Battle, // 전투 관련
			ListItemDisplay, // 목록 항목 표시
			ListSearch, // 목록 검색
			ListSorting, // 목록 정렬
			Workbench, // 공방
			Composite, // 복합
		}
		private ConfigPanel_SimpleUI_SubpageType Conf_SimpleUI_Subpage = ConfigPanel_SimpleUI_SubpageType.None;

		private void Conf_SimpleUI(ref float offset) {
			void Subpage_Battle(ref float offset) {
				DrawToggle(ref offset, "자율 전투 확인 대신 맵으로", Conf.SimpleUI.Use_OfflineBattle_Bypass);
				offset += 4f;
				DrawToggle(ref offset, "전투 적 미리보기", Conf.SimpleUI.Use_MapEnemyPreview);
			}
			void Subpage_ListItemDisplay(ref float offset) {
				DrawToggle(ref offset, "전투원 소모 자원 표기 기본 끄기", Conf.SimpleUI.Default_CharacterCost_Off);
				offset += 4f;
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
				offset += 4f;
				DrawToggle(ref offset, "전투원 이름 정렬 추가", Conf.SimpleUI.Use_SortByName);
				DrawToggle(ref offset, "전투원 소속 부대 정렬 추가", Conf.SimpleUI.Use_SortByGroup);
				DrawToggle(ref offset, "전투원 링크 수 정렬 추가", Conf.SimpleUI.Use_SortByLinks);
			}
			void Subpage_Workbench(ref float offset) {
				DrawToggle(ref offset, "전투원 제조 결과 미리보기", Conf.SimpleUI.Use_CharacterMakingPreview);
				DrawToggle(ref offset, "장비 제조 결과 미리보기", Conf.SimpleUI.Use_EquipMakingPreview);
				offset += 4f;
				DrawToggle(ref offset, "분해에 모든 전투원 선택 추가", Conf.SimpleUI.Use_Disassemble_SelectAll_Character);
				DrawToggle(ref offset, "분해에 모든 장비 선택 추가", Conf.SimpleUI.Use_Disassemble_SelectAll_Equip);
			}
			void Subpage_Composite(ref float offset) {
				DrawToggle(ref offset, "도감은 멋져야 한다", Conf.SimpleUI.Use_ScrapbookMustBeFancy);
				offset += 4f;
				DrawToggle(ref offset, "교환소: 손도 깔끔", Conf.SimpleUI.Use_Exchange_NoMessyHand);
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
					DrawLineButton(ref offset, "공방 개선", () => {
						this.Conf_SimpleUI_Subpage = ConfigPanel_SimpleUI_SubpageType.Workbench;
					});
					DrawLineButton(ref offset, "복합 개선", () => {
						this.Conf_SimpleUI_Subpage = ConfigPanel_SimpleUI_SubpageType.Composite;
					});

					DrawSeparator(ref offset);

					DrawToggle(ref offset, "편성에 전체 해제 추가", Conf.SimpleUI.Use_Squad_Clear);

					DrawSeparator(ref offset);

					DrawToggle(ref offset, "스크롤/패닝 가속, 줌 반전하기", Conf.SimpleUI.Use_AccelerateScrollDelta);
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
				case ConfigPanel_SimpleUI_SubpageType.Workbench:
					GUIX.Heading(headingRect, "공방 개선", alignment: TextAnchor.MiddleCenter);
					Subpage_Workbench(ref offset);
					break;
				case ConfigPanel_SimpleUI_SubpageType.Composite:
					GUIX.Heading(headingRect, "복합 개선", alignment: TextAnchor.MiddleCenter);
					Subpage_Composite(ref offset);
					break;
			}

		}
	}
}

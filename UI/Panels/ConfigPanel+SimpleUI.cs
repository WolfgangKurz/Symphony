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
		}
		private ConfigPanel_SimpleUI_SubpageType Conf_SimpleUI_Subpage = ConfigPanel_SimpleUI_SubpageType.None;

		private void Conf_SimpleUI(ref float offset) {
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
				offset += 10f;
				DrawToggle(ref offset, "정렬 기준 추가", Conf.SimpleUI.Use_SortBy_Extra);
			}
			void Subpage_CharacterDetail(ref float offset) {
				DrawToggle(ref offset, "이전/다음 전투원 버튼 추가", Conf.SimpleUI.Use_CharacterDetail_NextPrev);
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
				DrawLabel(ref offset, "전투원 도감에서 스킨 배경을 표시하며,\n자세히 보기 화면에 진입할 때 회전하지 않도록 변경하고,\n배경 및 장식품 감추기/보이기 버튼을 추가합니다.", Color_description, 20);

				offset += 10f;

				DrawToggle(ref offset, "교환소: 손도 깔끔", Conf.SimpleUI.Use_Exchange_NoMessyHand);
				DrawLabel(ref offset, "교환소의 '품절된 상품 숨기기'를 체크상태로 변경하고,\n현재 보고있는 상품에 관련된 소모품만 목록에 표시합니다.", Color_description, 20);

				offset += 10f;

				DrawToggle(ref offset, "더 좋은 시설 인벤토리", Conf.SimpleUI.Use_BetterFacilityInventory);
				DrawLabel(ref offset, "기지의 시설 목록이 레벨 및 이름순으로 정렬되고, 보유중인 시설은 레벨이 표시됩니다.", Color_description, 20);
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

					DrawSeparator(ref offset);

					DrawToggle(ref offset, "편성에 전체 해제 추가", Conf.SimpleUI.Use_Squad_Clear);

					DrawSeparator(ref offset);

					DrawToggle(ref offset, "스크롤/패닝 가속, 줌 반전하기", Conf.SimpleUI.Use_AccelerateScrollDelta);

					DrawSeparator(ref offset);

					DrawToggle(ref offset, "스토리 뷰어 텍스트 표시 문제 수정", Conf.SimpleUI.Use_NovelDialog_LabelFix);
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
			}

		}
	}
}

using GlobalDefines;

using HarmonyLib;

using LOEventSystem;
using LOEventSystem.Msg;

using System.Collections.Generic;
using System.Reflection;

using UnityEngine;

namespace Symphony.Features {
	internal class LastBattle : MonoBehaviour {
		private static NGUIAtlas atlas;

		public void Start() {
			var harmony = new Harmony("Symphony.LastBattle");
			harmony.Patch(
				AccessTools.Method(typeof(Panel_GameModeMenu), "Start"),
				postfix: new HarmonyMethod(typeof(LastBattle), nameof(LastBattle.Panel_GameModeMenu_Start))
			);
			harmony.Patch(
				AccessTools.Method(typeof(SceneStageBattle), "Start"),
				postfix: new HarmonyMethod(typeof(LastBattle), nameof(LastBattle.Update_MapStage))
			);
		}

		private static void Panel_GameModeMenu_Start(Panel_GameModeMenu __instance) {
			if (!Conf.LastBattle.Use_LastBattleMap.Value) return;

			var map = SingleTon<DataManager>.Instance.GetTableChapterStage(Conf.LastBattle.LastBattleMapKey.Value);
			var chapter = map != null
				? SingleTon<DataManager>.Instance.GetTableMapChapter(map?.ChapterIndex)
				: null;

			if(!string.IsNullOrEmpty( chapter?.Event_Category)) {
				var evChapter = SingleTon<DataManager>.Instance.GetTableEventChapter(chapter.Key);
				if (evChapter.Event_OpenType == 0) { // Closed event
					Plugin.Logger.LogWarning("[Symphony::LastBattle] Last visited map was event and closed, reset to none");
					Conf.LastBattle.LastBattleMapKey.Value = "";
					map = null;
					chapter = null;
				}
			}

			var goMain = (GameObject)__instance.GetType()
				.GetField("_goMain", BindingFlags.Instance | BindingFlags.NonPublic)
				.GetValue(__instance);

			#region Make custom atlas
			if (atlas == null) {
				atlas = ScriptableObject.CreateInstance<NGUIAtlas>();
				Plugin.Logger.LogWarning(atlas);

				var src_sprite = goMain.GetComponentInChildren<UISprite>();
				var src_atlas = src_sprite.atlas;
				var src_mat = (Material)src_atlas.GetType()
					.GetField("material", BindingFlags.Instance | BindingFlags.NonPublic)
					.GetValue(src_atlas);

				var tex = new Texture2D(1, 1, TextureFormat.ARGB32, false, true);
				tex.LoadImage(Resource.LastBattleAtlas);

				var mat = new Material(src_mat);
				mat.name = "LastBattle_Atlas";
				mat.mainTexture = tex;

				var t = atlas.GetType();
				t.GetField("material", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(atlas, mat);
				t.GetField("materialBright", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(atlas, mat);
				t.GetField("materialCustom", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(atlas, mat);
				t.GetField("materialGray", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(atlas, mat);

				t.GetField("mSpriteIndices", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(atlas, new Dictionary<string, int> {
					{ "UI_SelectWorldBtn_MainStory_Small", 0 }
				});
				t.GetField("mSprites", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(atlas, new List<UISpriteData> {
					new UISpriteData {
						name = "UI_SelectWorldBtn_MainStory_Small",
						x = 0,
						y = 0,
						width = 644,
						height = 280,
						paddingLeft = 0,
						paddingTop = 3,
						paddingRight = 28,
						paddingBottom = 24,
						borderLeft = 0,
						borderTop = 0,
						borderRight = 0,
						borderBottom = 0,
					}
				});
			}
			#endregion

			{ // goBtn adjust
				var bgsp = goMain.transform.Find("BgSp");
				bgsp.localPosition = new Vector3(bgsp.localPosition.x, 15f, bgsp.localPosition.z);

				var box = goMain.GetComponent<BoxCollider>();
				box.center = new Vector3(-20.96f, -113.26f, 0f);
				box.size = new Vector3(646.504f, 247.321f, 0f);

				var sp = bgsp.GetComponent<UISprite>();
				sp.atlas = atlas;
				sp.spriteName = "UI_SelectWorldBtn_MainStory_Small";
				sp.height = 280;

				var lb_Title = goMain.transform.Find("ChapterTitleLb");
				lb_Title.transform.localPosition -= new Vector3(0f, 70f, 0f);

				var lb_Num = goMain.transform.Find("ChapterNumLb");
				lb_Num.transform.localPosition -= new Vector3(0f, 70f, 0f);

				var lb_Text = goMain.transform.Find("TextSetPositionUiSprite");
				lb_Text.transform.localPosition -= new Vector3(0f, 70f, 0f);
			}

			var btn = GameObject.Instantiate(goMain);
			btn.name = "LastBattle";
			btn.transform.SetParent(goMain.transform.parent);
			btn.transform.localScale = goMain.transform.localScale;
			btn.transform.localPosition = goMain.transform.localPosition;

			{ // btn adjust
				var bgsp = btn.transform.Find("BgSp");
				bgsp.localPosition = new Vector3(bgsp.localPosition.x, 270f, bgsp.localPosition.z);

				var box = btn.GetComponent<BoxCollider>();
				box.center = new Vector3(-20.96f, 141.74f, 0f);
				box.size = new Vector3(646.504f, 247.321f, 0f);

				var sp = bgsp.GetComponent<UISprite>();
				sp.atlas = atlas;
				sp.spriteName = "UI_SelectWorldBtn_MainStory_Small";
				sp.height = 280;

				var chapterName = !string.IsNullOrEmpty(chapter?.Event_Category)
					? $"EventName_{chapter.Event_Category}".Localize()
					: chapter?.ChapterString ?? "";

				var lb_Title = btn.transform.Find("ChapterTitleLb");
				lb_Title.transform.localPosition += new Vector3(0f, 260f, 0f);
				lb_Title.GetComponent<UILabel>().text = map?.StageName?.Localize() ?? "";

				var lb_Num = btn.transform.Find("ChapterNumLb");
				lb_Num.transform.localPosition += new Vector3(0f, 260f, 0f);
				lb_Num.GetComponent<UILabel>().text = map != null
					? $"{chapterName}. {map.StageIdxString}"
					: "";

				var lb_Text = btn.transform.Find("TextSetPositionUiSprite");
				lb_Text.transform.localPosition += new Vector3(0f, 260f, 0f);

				lb_Text.Find("!").gameObject.SetActive(false);
				var lb_Text_Title = lb_Text.Find("TitleLb");
				lb_Text_Title.GetComponent<UILocalize>().enabled = false;
				lb_Text_Title.GetComponent<UILabel>().text = "마지막 전투 지역";

				var uiBtn = btn.GetComponent<UIButton>();
				uiBtn.onClick.Clear();
				uiBtn.onClick.Add(new(() => {
					if (map == null) return;

					SingleTon<GameManager>.Instance.MapInit();
					if (string.IsNullOrEmpty(chapter?.Event_Category)) {
						SingleTon<GameManager>.Instance.MapStage = map;
						SingleTon<GameManager>.Instance.GameMode = GAME_MODE.STORY;
					}
					else {
						SingleTon<GameManager>.Instance.MapEventChapter = SingleTon<DataManager>.Instance.GetTableEventChapter(chapter.Key);
						SingleTon<GameManager>.Instance.GameMode = GAME_MODE.EVENT;
					}
					Handler.Broadcast(new SceneChange(Const.Scene_World)); // __instance.ShowScene(Const.Scene_World);
				}));
			}
		}

		private static void Update_MapStage() {
			var map = SingleTon<GameManager>.Instance.MapStage;
			if (map == null) {
				Plugin.Logger.LogDebug("[Symphony::LastBattle] GameManager.MapStage has reset");
				Conf.LastBattle.LastBattleMapKey.Value = "";
				return;
			}

			if (Conf.LastBattle.Use_LastBattleMap.Value)
				Plugin.Logger.LogInfo("[Symphony::LastBattle] Last battle stage is " + map.Key);

			// Last visited battle map always be logged
			Conf.LastBattle.LastBattleMapKey.Value = map.Key;
		}
	}
}

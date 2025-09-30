using GlobalDefines;

using HarmonyLib;

using Symphony.Features.KeyMapping;
using Symphony.UI;
using Symphony.UI.Panels;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityEngine;

namespace Symphony.Features {
	[Feature("Experimental")]
	internal class Experimental : MonoBehaviour {
		public void Start() {
			var harmony = new Harmony("Symphony.Experimental");

			harmony.Patch(
				AccessTools.Method(typeof(Creature), nameof(Creature.DisappearBuffEffectParticleAll)),
				prefix: new HarmonyMethod(typeof(Experimental), nameof(Experimental.Patch_Creature_DisappearBuffEffectParticleAll))
			);
			harmony.Patch(
				AccessTools.Method(typeof(CreatureState_Evade), "MoveBackEvade"),
				prefix: new HarmonyMethod(typeof(Experimental), nameof(Experimental.Patch_CreatureState_Evade_MoveBackEvade))
			);

			KeyMappingConf.Load();
		}

		#region Key Mapping
		private const float KEY_MAP_CIRCLE = 13f;
		private static Queue<int> KeyMapping_SimulatingTouchQueue = new();
		IEnumerator KeyMapping_SimulateTouch(float rX, float rY) {
			var uid = UnityEngine.Random.RandomRangeInt(0, int.MaxValue);
			KeyMapping_SimulatingTouchQueue.Enqueue(uid);

			yield return new WaitUntil(() => KeyMapping_SimulatingTouchQueue.Peek() == uid); // wait available turn

			var pt = new Vector2(rX, rY) * new Vector2(Screen.width, Screen.height);

			UICamera.GetInputTouchCount = () => 1;
			UICamera.GetInputTouch = (i) => {
				var t = new UICamera.Touch();
				t.phase = TouchPhase.Began;
				t.fingerId = 0;
				t.position = pt;
				t.tapCount = 1;
				return t;
			};

			{
				var go = Instantiate(PrefabLoader.GetPrefab("UI_click_FX"));
				go.transform.parent = this.transform;
				go.transform.position = UICamera.mainCamera.ScreenToWorldPoint(pt);
				if (
					SingleTon<GameManager>.Instance.CurrentSceneType == eSceneType.LOBBY ||
					SingleTon<GameManager>.Instance.CurrentSceneType == eSceneType.CHARACTERDETAILS
				)
					go.transform.SetChildLayerBookDetail(LayerMask.NameToLayer("UI2"));
			}

			yield return null; // ensure next frame

			UICamera.GetInputTouch = (i) => {
				var t = new UICamera.Touch();
				t.phase = TouchPhase.Ended;
				t.fingerId = 0;
				t.position = pt;
				t.tapCount = 1;
				return t;
			};

			yield return null; // ensure next frame

			UICamera.GetInputTouch = null;
			UICamera.GetInputTouchCount = null;

			KeyMapping_SimulatingTouchQueue.Dequeue();
		}
		#endregion

		public void Update() {
			#region KeyMapping
			if (Conf.Experimental.Use_KeyMapping.Value) {
				try {
					var act = Conf.Experimental.KeyMapping_Active.Value;
					if (KeyMappingConf.KeyMaps.ContainsKey(act)) {
						var maps = KeyMappingConf.KeyMaps[act];
						foreach (var map in maps) {
							if (Helper.KeyCodeParse(map.Key, out var kc) && Input.GetKeyDown(kc))
								StartCoroutine(KeyMapping_SimulateTouch(map.X, map.Y));
						}
					}
				} catch (Exception e) {
					Plugin.Logger.LogError(e);
				}
			}
			#endregion
		}

		public void OnGUI() {
			#region KeyMapping
			if (UIManager.Instance?.GetPanel<KeyMapPanel>() == null && Conf.Experimental.Use_KeyMapping.Value) {
				var KeyMap_Alpha = Conf.Experimental.KeyMapping_Opacity.Value;
				if (KeyMap_Alpha > 0f) {
					var act = Conf.Experimental.KeyMapping_Active.Value;
					if (KeyMappingConf.KeyMaps.ContainsKey(act)) {
						var maps = KeyMappingConf.KeyMaps[act];
						for (var i = 0; i < maps.Length; i++) {
							var map = maps[i];
							var rcBase = new Rect(map.X * Screen.width, (1f - map.Y) * Screen.height, 0, 0);
							var rcCircle = rcBase.Expand(KEY_MAP_CIRCLE);

							var dup = maps.Any((x, y) => y != i && x.Key == map.Key);

							GUIX.Circle(rcCircle, dup ? new Color(0.94f, 0.42f, 0.42f, KeyMap_Alpha) : new Color(0.2f, 0.65f, 0.94f, KeyMap_Alpha));
							GUIX.Label(
								rcCircle, map.Key,
								new Color(1f, 1f, 1f, KeyMap_Alpha), fontSize: 12, fontStyle: FontStyle.Bold,
								alignment: TextAnchor.MiddleCenter
							);
						}
					}
				}
			}
			#endregion
		}

		#region Freezing fixers
		#region DisappearBuffEffectParticleAll "Collection was mutated while being enumerated" exception fix
		private static bool Patch_Creature_DisappearBuffEffectParticleAll(Creature __instance) {
			if (!Conf.Experimental.Fix_BattleFreezing.Value) return true;

			// Make copy to prevent collection changed exception
			var lst = __instance.XGetFieldValue<List<ulong>>("_ListDisappearDelayParticle").ToArray();
			foreach (ulong item in lst)
				__instance.DestroyAttachBuffEffectParticle(item);

			Plugin.Logger.LogDebug("[Symphony::Experimental] Freezing Patched (DisappearBuffEffectParticleAll)");
			return false;
		}
		#endregion

		#region AnimationClip Event missing fix
		private static void Patch_XXXstep_Animation(Creature creature) {
			if (creature == null) return;

			if (creature is Character) {
				var _animator = creature.Animator;

				eAndroidAnimationType[] animTypes = [
					eAndroidAnimationType.backstep,
					eAndroidAnimationType.frontstep
				];
				foreach (var animType in animTypes) {
					var clip = _animator.GetClip(animType);
					if (clip == null) return;
					if (!clip.events.Any(x => x.functionName == "EventJumpMoveStart")) {
						Plugin.Logger.LogInfo($"[Symphony::Experimental] EventJumpMoveStart missing patched, for {animType.ToString()} animation");

						var ev = new AnimationEvent();
						ev.functionName = "EventJumpMoveStart";
						ev.time = 0;
						clip.AddEvent(ev);
					}
				}
			}
		}
		private static bool Patch_CreatureState_Evade_MoveBackEvade(CreatureState_Evade __instance, ref IEnumerator __result) {
			IEnumerator Fn() {
				var _creature = __instance.XGetFieldValue<Creature>("creature");


				var f = _creature.CreatureType == eCreatureType.CHARACTER ? -2.3561945f : -0.7853982f;
				var x = 5f * Mathf.Cos(f);
				var y = -5f * Mathf.Sin(f);

				var linePCStart = (
					_creature.CreatureType != eCreatureType.MONSTER
						? MonoSingleton<StageMapData>.Instance.GetCharacterLineData(_creature.LineSpot)
						: MonoSingleton<StageMapData>.Instance.GetMonsterLineData(_creature.LineSpot)
				).transform.position;
				var linePCEnd = linePCStart + new Vector3(x, y, 0.0f);

				__instance.XSetFieldValue<Vector3>("originalPos", linePCStart);

				_creature.Animator.PlayAnimation(eAndroidAnimationType.backstep);
				Experimental.Patch_XXXstep_Animation(_creature); // Fix, fill missing jump start event for AnimationClip

				var field = __instance.GetType()
					.GetField("JumpMoveTime", BindingFlags.NonPublic | BindingFlags.Instance);

				while (true) {
					var mt = field.GetValue<float>(__instance);

					if (mt != 0.0f) break;
					yield return null;
				}

				var lineTime = 0.0f;
				while (true) {
					lineTime += Time.deltaTime;
					_creature.transform.position = Vector3.Lerp(linePCStart, linePCEnd, lineTime / field.GetValue<float>(__instance));

					if (lineTime <= 1.0f)
						yield return null;
					else
						break;
				}

				_creature.Animator.PlayAnimation(_creature.CurIdle);
				Experimental.Patch_XXXstep_Animation(_creature); // Fix, fill missing jump start event for AnimationClip

				__instance.transform.position = linePCEnd;
				yield return new WaitForEndOfFrame();
			}
			__result = Fn();

			return false;
		}
		#endregion
		#endregion
	}
}

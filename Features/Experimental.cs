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
				AccessTools.Method(typeof(Creature), nameof(Creature.PlayAnimation)),
				postfix: new HarmonyMethod(typeof(Experimental), nameof(Experimental.Patch_Creature_PlayAnimation))
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
		private static void Patch_Nabi_NS1_Skill2(ref AnimationEvent[] events) {
			if (!events.Any(x => x.functionName == "EventHit" && x.stringParameter == "vfx_Nabi_ns1_skill1_hit")) return;
			Plugin.Logger.LogDebug("[Symphony::Experimental] Fix Freezing for Nabi NS1 Skill2");

			var audio = events.FirstOrDefault(x => x.objectReferenceParameter != null).objectReferenceParameter as AudioClip;

			#region Patch
			events = [ // Copy of non-skin
				new() {
					time = 0.016666667f,
					functionName = "EventDynamicBone",
					stringParameter = "Bone_Hair",
				},
				new() {
					time = 0.033333333f,
					functionName = "EventDynamicBone",
					stringParameter = "Bone_Breast",
				},
				new() {
					time = 0.050000000f,
					functionName = "EventDynamicBone",
					stringParameter = "Bone_Skirt",
				},
				new() {
					time = 0.166666667f,
					functionName = "EventCameraControlSelfMultiParam",
					stringParameter = "0.2,25,0,40",
				},
				new() {
					time = 0.5f,
					functionName = "EventSoundPlaySFXPrefab",
					objectReferenceParameter = null,
				},
				new() {
					time = 1.0f,
					functionName = "EventSoundPlaySFXPrefab",
					objectReferenceParameter=audio,
				},
				new() {
					time = 1.25f,
					functionName = "EventSoundPlaySFXPrefab",
					objectReferenceParameter = audio,
				},
				new() {
					time = 3.0f,
					functionName = "EventEvadeCheck",
				},
				new() {
					time = 3.333333333f,
					functionName = "EventSoundPlaySFXPrefab",
					objectReferenceParameter = null,
				},
				new() {
					time = 3.55f,
					functionName = "EventCameraShake",
					stringParameter = "0.15",
					floatParameter = 0.2f,
				},
				new() {
					time = 3.55f,
					functionName = "EventHit",
					stringParameter = "",
				},
				new() {
					time = 3.75f,
					functionName = "EventSoundPlaySFXPrefab",
					objectReferenceParameter = null,
				},
				new() {
					time = 3.75f,
					functionName = "EventHit",
					stringParameter = "noHit_m",
				},
				new() {
					time = 4.0f,
					functionName = "EventHit",
					stringParameter = "noHit_m",
				},
				new() {
					time = 4.0f,
					functionName = "EventCameraShake",
					stringParameter = "0.2",
					floatParameter = 0.2f,
				},
				new() {
					time = 4.25f,
					functionName = "EventHit",
					stringParameter = "noHit_m",
				},
				new() {
					time = 4.333333333f,
					functionName = "EventSoundPlaySFXPrefab",
					objectReferenceParameter = null,
				},
				new() {
					time = 4.333333333f,
					functionName = "EventThrowProjectileFire1",
					stringParameter = "eff_nabi_ns1_skill2_bullet", // Change name
				},
				new() {
					time = 4.333333333f,
					functionName = "EventCameraShake",
					stringParameter = "0.7",
					floatParameter = 1.0f,
				},
				new() {
					time = 4.5f,
					functionName = "EventHit",
					stringParameter = "noHit_m",
				},
				new() {
					time = 4.5f,
					functionName = "EventHit",
					stringParameter = "",
				},
				new() {
					time = 4.75f,
					functionName = "EventHit",
					stringParameter = "noHit_m",
				},
				new() {
					time = 4.75f,
					functionName = "EventSoundPlaySFXPrefab",
					objectReferenceParameter = null,
				},
				new() {
					time = 5.0f,
					functionName = "EventCameraShake",
					stringParameter = "0.7",
					floatParameter = 1.0f,
				},
				new() {
					time = 5.0f,
					functionName = "EventHit",
					stringParameter = "noHit_m",
				}
			];
			#endregion
		}
		private static void Patch_Creature_PlayAnimation(Creature __instance, eAndroidAnimationType aniType) {
			var creature = __instance;
			Plugin.Logger.LogInfo($"[Symphony::Experimental] Creature_PlayAnimation : {__instance?.name}, {aniType}");

			eAndroidAnimationType[] animTypes_step = [
				eAndroidAnimationType.frontstep,
				eAndroidAnimationType.frontstep_protect,
				eAndroidAnimationType.backstep,
				eAndroidAnimationType.backstep_protect,
			];
			if (animTypes_step.Contains(aniType)) {
				// Fix, fill missing jump start event for AnimationClip
				Experimental.Patch_XXXstep_Animation(creature);
			}

			// Patch LC_Nabi NS1 Skill2
			if (aniType == eAndroidAnimationType.skill2 && (creature as Character)?.TablePC.Key == "Char_LC_Nabi_N") {
				var clip = __instance.Animator.GetClip(eAndroidAnimationType.skill2);
				var events = clip.events;
				Experimental.Patch_Nabi_NS1_Skill2(ref events);
				clip.events = events;
			}
		}
		#endregion
		#endregion
	}
}

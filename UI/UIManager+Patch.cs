using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

using UnityEngine;

namespace Symphony.UI {
	internal class UIManager_Patch {
		public static void Patch() {
			var harmony = new Harmony("Symphony.UIManager");
			harmony.Patch( // Hide touch indicator when on UI
				AccessTools.Method(typeof(GameManager), "Update"),
				transpiler: new HarmonyMethod(typeof(UIManager_Patch), nameof(UIManager_Patch.Patch_GameManager_Update))
			);
		}

		private static IEnumerable<CodeInstruction> Patch_GameManager_Update(MethodBase _, IEnumerable<CodeInstruction> instructions) {
			Plugin.Logger.LogInfo("[Symphony::UIManager] Start to patch GameManager.Update");

			var Input_GetMouseButtonDown = AccessTools.Method(typeof(Input), "GetMouseButtonDown");

			var matcher = new CodeMatcher(instructions);
			matcher.MatchForward(false,
				/* Input.GetMouseButtonDown(0) */
				new CodeMatch(OpCodes.Ldc_I4_0), // 0
				new CodeMatch(OpCodes.Call, Input_GetMouseButtonDown), // Input.GetMouseButtonDown(int)
				new CodeMatch(OpCodes.Brfalse) // == false -> goto OPERAND
			);

			if (matcher.IsInvalid) {
				Plugin.Logger.LogWarning("[Symphony::UIManager] Failed to patch GameManager.Update, target instructions not found");
				return instructions;
			}

			matcher.Advance(2);
			var jumpTo = matcher.Operand;
			matcher.Advance(1);

			matcher.InsertAndAdvance(
				new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UIManager_Patch), nameof(UIManager_Patch.IsClickEnabled))),
				new CodeInstruction(OpCodes.Brfalse, jumpTo)
			);

			return matcher.InstructionEnumeration();
		}

		private static bool IsClickEnabled() {
			foreach (var cam in UICamera.list) {
				if (cam.useTouch || cam.useMouse)
					return true;
			}
			return false;
		}
	}
}

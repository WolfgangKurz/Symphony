using GlobalDefines;

using LO_ClientNetwork;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

using UnityEngine;

using static Skill;

namespace Symphony.Features {
	internal class Notification : MonoBehaviour {
		private static string NotiToolPath => Path.Combine(
			Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
			"Symphony.Notification.exe"
		);

		private static void ScheduleNotification(DateTime displayTime, string Tag, string Title, string Message) {
			// Notification Tool not exists
			if (!File.Exists(NotiToolPath)) return;

			if (!Conf.Notification.Handle_Notification.Value) {
				CancelNotification(Tag); // Cancel previous scheduled notification
				return;
			}

			try {
				var pi = new ProcessStartInfo {
					FileName = NotiToolPath,
					Arguments = string.Join(" ", new string[] {
					"-Schedule",
					"-Tag:" + Tag,
					"-Title:" + Title,
					"-Message:" + Message,
					"-Time:" + displayTime.ToString(),
				}.Select(x => $"\"{x.Replace("\"", "\\\"")}\"")),
					UseShellExecute = true,
				};
				Process.Start(pi).WaitForExit();
			} catch (Exception e) {
				Plugin.Logger.LogWarning("[Symphony::Notification] Failed to call NotificationTool");
				Plugin.Logger.LogWarning(e.ToString());
			}
		}
		private static void CancelNotification(string Tag) {
			// Notification Tool not exists
			if (!File.Exists(NotiToolPath)) return;


			try {
				var pi = new ProcessStartInfo {
					FileName = NotiToolPath,
					Arguments = string.Join(" ", new string[] {
					"-Cancel",
					"-Tag:" + Tag,
				}.Select(x => $"\"{x.Replace("\"", "\\\"")}\"")),
					UseShellExecute = true,
				};
				Process.Start(pi).WaitForExit();
			} catch (Exception e) {
				Plugin.Logger.LogWarning("[Symphony::Notification] Failed to call NotificationTool");
				Plugin.Logger.LogWarning(e.ToString());
			}
		}

		public void Start() {
			// Extract Notification Tool
			try {
				File.WriteAllBytes(NotiToolPath, Resource.NotiBinary);
			} catch(Exception e) {
				Plugin.Logger.LogWarning("[Symphony::Notification] Failed to extract NotificationTool");
				Plugin.Logger.LogWarning(e.ToString());
			}

			EventManager.StartListening(this, 20U, new Action<WebResponseState>(this.HandlePakcetExplorationIngInfo));
			EventManager.StartListening(this, 19U, new Action<WebResponseState>(this.HandlePakcetExplorationEnter));
			EventManager.StartListening(this, 22U, new Action<WebResponseState>(this.HandlePakcetExplorationCancel));

			EventManager.StartListening(this, 36U, new Action<WebResponseState>(this.HandlePacketUnitRestoreSlotList));
			EventManager.StartListening(this, 38U, new Action<WebResponseState>(this.HandlePakcetUnitRestoreUseItem));
			EventManager.StartListening(this, 37U, new Action<WebResponseState>(this.HandlePakcetUnitRestoreAdd));

			EventManager.StartListening(this, 210U, new Action<WebResponseState>(this.HandlePacket_OfflineBattleInfo));
			EventManager.StartListening(this, 207U, new Action<WebResponseState>(this.HandlePacket_OfflineBattleEnter));
			EventManager.StartListening(this, 208U, new Action<WebResponseState>(this.HandlePacket_OfflineBattleEnd));

			EventManager.StartListening(this, 76U, new Action<WebResponseState>(this.HandlePakcetResearchBegin));
			EventManager.StartListening(this, 77U, new Action<WebResponseState>(this.HandlePakcetResearchCancel));
			EventManager.StartListening(this, 79U, new Action<WebResponseState>(this.HandlePakcetResearchUseItem));
		}
		public void OnDestroy() {
			EventManager.StopListening(this);
		}

		private void HandlePakcetExplorationIngInfo(WebResponseState obj) {
			var p = obj as W2C_EXPLORATION_INGINFO;
			if (p.result.ErrorCode != 0) return;

			if (!GameOption.NoticeAlram) return; // Toast not allowed
			if (!GameOption.NoticeNightAlram && (DateTime.Now.Hour >= 21 || DateTime.Now.Hour < 8)) return; // Night toast not allowed
			if (!GameOption.ExplorationAlram) return; // Exploration toast not allowed

			var list = p.result.ExplorationList;
			if (list == null) {
				var squads = SingleTon<DataManager>.Instance.GetAllSquadInfo();
				foreach (var sq in squads)
					CancelNotification($"Exploration_{sq.SquadIndex}");
				Plugin.Logger.LogDebug($"[Symphony::Notification] Cancel for Exploration");
				return;
			}

			foreach (var info in list) {
				var tag = $"Exploration_{info.SquadIndex}";

				var scheduleTime = DateTime.Now.AddSeconds(info.EndTime - info.EnterTime);
				var stageName = SingleTon<DataManager>.Instance.GetTableChapterStage(info.StageKeyString).StageIdxString ?? "Unknown";

				ScheduleNotification(
					scheduleTime,
					tag,
					$"탐색 완료 - {info.SquadIndex}번 스쿼드",
					$"{stageName} 지역 탐색이 완료되었습니다."
				);
				Plugin.Logger.LogDebug($"[Symphony::Notification] Schedule for {tag}, Scheduled Time : {scheduleTime.ToString()}");
			}
		}
		private void HandlePakcetExplorationEnter(WebResponseState obj) {
			var p = obj as W2C_EXPLORATION_ENTER;
			if (p.result.ErrorCode != 0) return;

			if (!GameOption.NoticeAlram) return; // Toast not allowed
			if (!GameOption.NoticeNightAlram && (DateTime.Now.Hour >= 21 || DateTime.Now.Hour < 8)) return; // Night toast not allowed
			if (!GameOption.ExplorationAlram) return; // Exploration toast not allowed

			var info = p.result.EnterInfo;
			var tag = $"Exploration_{info.SquadIndex}";

			var scheduleTime = DateTime.Now.AddSeconds(info.EndTime - info.EnterTime);
			var stageName = SingleTon<DataManager>.Instance.GetTableChapterStage(info.StageKeyString).StageIdxString ?? "Unknown";

			ScheduleNotification(
				scheduleTime,
				tag,
				$"탐색 완료 - {info.SquadIndex}번 스쿼드",
				$"{stageName} 지역 탐색이 완료되었습니다."
			);
			Plugin.Logger.LogDebug($"[Symphony::Notification] Schedule for {tag}, Scheduled Time : {scheduleTime.ToString()}");
		}
		private void HandlePakcetExplorationCancel(WebResponseState obj) {
			var p = obj as W2C_EXPLORATION_CANCEL;
			if (p.result.ErrorCode != 0) return;

			var tag = $"Exploration_{p.result.SquadIndex}";
			CancelNotification(tag);
			Plugin.Logger.LogDebug($"[Symphony::Notification] Cancel for {tag}");
		}

		private void HandlePacketUnitRestoreSlotList(WebResponseState obj) {
			if ((obj as W2C_PCRESTORE_SLOTLIST).result.ErrorCode != 0) return;

			if (!GameOption.NoticeAlram) return; // Toast not allowed
			if (!GameOption.NoticeNightAlram && (DateTime.Now.Hour >= 21 || DateTime.Now.Hour < 8)) return; // Night toast not allowed
			if (!GameOption.RestoreAlram) return; // PC Restore toast not allowed

			IEnumerator Fn() {
				yield return null;

				var slots = SingleTon<DataManager>.Instance.GEtPCRetoreSlotInfo();
				foreach (var kv in slots) {
					var tag = $"PCRecovery_{kv.Value.SlotNo}";

					if (kv.Value.PCID == 0) 
						CancelNotification(tag);
					else {
						var scheduleTime = DateTime.Now.AddSeconds(
							kv.Value.ExpireDate - SingleTon<DataManager>.Instance.GetDBTimestamp()
						);
						var chr = SingleTon<DataManager>.Instance.GetMyPCClient(kv.Value.PCID);

						ScheduleNotification(
							scheduleTime,
							tag,
							$"수복 완료 - {kv.Value.SlotNo + 1}번 슬롯",
							$"전투원 '{chr.GetPCName()}'의 수복이 완료되었습니다."
						);
						Plugin.Logger.LogDebug($"[Symphony::Notification] Schedule for {tag}, Scheduled Time : {scheduleTime.ToString()}");
					}
				}
			}
			StartCoroutine(Fn());
		}
		private void HandlePakcetUnitRestoreUseItem(WebResponseState obj) {
			var p = obj as W2C_PCRESTORE_USEITEM;
			if (p.result.ErrorCode != 0) return;

			var tag = $"PCRecovery_{p.result.SlotNo}";
			CancelNotification(tag);
			Plugin.Logger.LogDebug($"[Symphony::Notification] Cancel for {tag}");
		}
		private void HandlePakcetUnitRestoreAdd(WebResponseState obj) {
			var p = obj as W2C_PCRESTORE_ADD;
			if (p.result.ErrorCode != 0) return;

			if (!GameOption.NoticeAlram) return; // Toast not allowed
			if (!GameOption.NoticeNightAlram && (DateTime.Now.Hour >= 21 || DateTime.Now.Hour < 8)) return; // Night toast not allowed
			if (!GameOption.RestoreAlram) return; // PC Restore toast not allowed

			IEnumerator Fn() {
				yield return null;

				var slot = p.result.RegInfo;
					var tag = $"PCRecovery_{slot.SlotNo}";

				if (slot.PCID == 0)
					CancelNotification(tag);
				else {
					var scheduleTime = DateTime.Now.AddSeconds(
						slot.ExpireDate - SingleTon<DataManager>.Instance.GetDBTimestamp()
					);
					var chr = SingleTon<DataManager>.Instance.GetMyPCClient(slot.PCID);

					ScheduleNotification(
						scheduleTime,
						tag,
						$"수복 완료 - {slot.SlotNo + 1}번 슬롯",
						$"전투원 '{chr.GetPCName()}'의 수복이 완료되었습니다."
					);
					Plugin.Logger.LogDebug($"[Symphony::Notification] Schedule for {tag}, Scheduled Time : {scheduleTime.ToString()}");
				}
			}
			StartCoroutine(Fn());
		}

		private void HandlePacket_OfflineBattleInfo(WebResponseState obj) {
			var p = obj as W2C_AUTO_REPEAT_INFO;
			if (p.result.ErrorCode != 0) return;

			if (!GameOption.NoticeAlram) return; // Toast not allowed
			if (!GameOption.NoticeNightAlram && (DateTime.Now.Hour >= 21 || DateTime.Now.Hour < 8)) return; // Night toast not allowed
			if (!GameOption.OfflineBattleAlram) return; // OfflineBattle toast not allowed

			var info = p.result.AutoRepeatInfo;
			var tag = "OfflienBattle";

			if (info == null || info.EndUnixTime == 0) {
				CancelNotification(tag);
				Plugin.Logger.LogDebug($"[Symphony::Notification] Cancel for {tag}");
				return;
			}

			var scheduleTime = new DateTime((long)info.EndUnixTime * TimeSpan.TicksPerSecond);
			var stageName = SingleTon<DataManager>.Instance.GetTableChapterStage(info.StageKey)?.StageIdxString ?? "Unknown";

			ScheduleNotification(
				scheduleTime,
				tag,
				$"자율 전투 완료 - {info.SquadIndex}번 스쿼드",
				$"{stageName} 지역 자율 전투가 완료되었습니다."
			);
			Plugin.Logger.LogDebug($"[Symphony::Notification] Schedule for {tag}, Scheduled Time : {scheduleTime.ToString()}");
		}
		private void HandlePacket_OfflineBattleEnter(WebResponseState obj) {
			var p = obj as W2C_AUTO_REPEAT_START;
			if (p.result.ErrorCode != 0) return;

			if (!GameOption.NoticeAlram) return; // Toast not allowed
			if (!GameOption.NoticeNightAlram && (DateTime.Now.Hour >= 21 || DateTime.Now.Hour < 8)) return; // Night toast not allowed
			if (!GameOption.OfflineBattleAlram) return; // OfflineBattle toast not allowed

			var info = p.result.AutoRepeatInfo;
			var tag = "OfflienBattle";

			var scheduleTime = new DateTime((long)info.EndUnixTime * TimeSpan.TicksPerSecond);
			var stageName = SingleTon<DataManager>.Instance.GetTableChapterStage(info.StageKey).StageIdxString ?? "Unknown";

			ScheduleNotification(
				scheduleTime,
				tag,
				$"자율 전투 완료 - {info.SquadIndex}번 스쿼드",
				$"{stageName} 지역 자율 전투가 완료되었습니다."
			);
			Plugin.Logger.LogDebug($"[Symphony::Notification] Schedule for {tag}, Scheduled Time : {scheduleTime.ToString()}");
		}
		private void HandlePacket_OfflineBattleEnd(WebResponseState obj) {
			var p = obj as W2C_AUTO_REPEAT_END;
			if (p.result.ErrorCode != 0) return;

			var tag = "OfflineBattle";
			CancelNotification(tag);
			Plugin.Logger.LogDebug($"[Symphony::Notification] Cancel for {tag}");
		}

		private void HandlePakcetResearchBegin(WebResponseState obj) {
			var p = obj as W2C_RESEARCH_BEGIN;
			if (p.result.ErrorCode != 0) return;

			var info = p.result.NewInfo;
			var time = info.EndTime - info.StartTime;
			var tag = "Research";

			var scheduleTime = DateTime.Now.AddSeconds(time);

			var title = string.Empty;
			var tableResearch = SingleTon<DataManager>.Instance.GetTableResearch(info.ResearchKeyString);
			if (tableResearch != null)
				title = Localization.Format("1000043", (object)tableResearch.Research_Name.Localize());

			ScheduleNotification(
				scheduleTime,
				tag,
				$"연구 완료",
				string.IsNullOrEmpty(title)
					? "연구가 완료되었습니다"
					: $"'{title}' 연구가 완료되었습니다."
			);
			Plugin.Logger.LogDebug($"[Symphony::Notification] Schedule for {tag}, Scheduled Time : {scheduleTime.ToString()}");
		}
		private void HandlePakcetResearchCancel(WebResponseState obj) {
			var p = obj as W2C_RESEARCH_CANCEL;
			if (p.result.ErrorCode != 0) return;

			var tag = "Research";
			CancelNotification(tag);
			Plugin.Logger.LogDebug($"[Symphony::Notification] Cancel for {tag}");
		}
		private void HandlePakcetResearchUseItem(WebResponseState obj) {
			var p = obj as W2C_RESEARCH_USEITEM;
			if (p.result.ErrorCode != 0) return;

			var tag = "Research";
			CancelNotification(tag);
			Plugin.Logger.LogDebug($"[Symphony::Notification] Cancel for {tag}");
		}
	}
}

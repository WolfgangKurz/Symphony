using HarmonyLib;

using LO_ClientNetwork;

using LOEventSystem;
using LOEventSystem.Msg;

using Microsoft.Data.Sqlite;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using UnityEngine;

namespace Symphony.Features {
	[Feature("Statistics")]
	internal class Statistics : MonoBehaviour, Listener {
		private readonly struct ResourceSnapshot : IEquatable<ResourceSnapshot> {
			public readonly uint Metal;
			public readonly uint Nutrient;
			public readonly uint Power;
			public readonly uint MaxRes;

			public ResourceSnapshot(uint metal, uint nutrient, uint power, uint maxRes) {
				this.Metal = metal;
				this.Nutrient = nutrient;
				this.Power = power;
				this.MaxRes = maxRes;
			}

			public bool Equals(ResourceSnapshot other)
				=> this.Metal == other.Metal &&
				this.Nutrient == other.Nutrient &&
				this.Power == other.Power &&
				this.MaxRes == other.MaxRes;

			public override bool Equals(object obj) => obj is ResourceSnapshot other && this.Equals(other);

			public override int GetHashCode() => HashCode.Combine(this.Metal, this.Nutrient, this.Power, this.MaxRes);
		}

		public static string StatisticsDir => Path.Combine(Plugin.GameDir, "Statistics");
		public static string StatisticsViewerPath => Path.Combine(StatisticsDir, "viewer.html");
		private static string StatisticsDBPath => CurrentUID == 0
			? Path.Combine(StatisticsDir, "Statistics.sqlite")
			: Path.Combine(StatisticsDir, $"Statistics.{CurrentUID}.sqlite");

		private static readonly object dbLock = new();

		private static ulong CurrentUID = 0;
		private static bool registered = false;

		private static readonly Dictionary<string, int> LastItemCounts = new();
		private static readonly Dictionary<string, long> ItemReferenceIds = new();

		private static ResourceSnapshot? LastResources = null;

		public void Start() {
			var harmony = new Harmony("Symphony.Statistics");

			registered = true;
			try {
				if (!Directory.Exists(StatisticsDir))
					Directory.CreateDirectory(StatisticsDir);

				File.WriteAllBytes(StatisticsViewerPath, Resource.StatisticsViewerHtml);

				SQLitePCL.Batteries_V2.Init();
			} catch (Exception e) {
				Plugin.Logger.LogWarning($"[Symphony::Statistics] Failed to initialize statistics database: {e}");
				registered = false;
			}

			harmony.Patch(
				AccessTools.Method(typeof(DataManager), nameof(DataManager.InitAccount)),
				postfix: new HarmonyMethod(typeof(Statistics), nameof(Statistics.After_Logout))
			);

			harmony.Patch(
				AccessTools.Method(typeof(DataManager), "HandlePakcetLogin"),
				postfix: new HarmonyMethod(typeof(Statistics), nameof(Statistics.After_HandlePacketLogin))
			);

			harmony.Patch(
				AccessTools.Method(typeof(DataManager), nameof(DataManager.ItemUpdate), [typeof(UpdateItemInfo)]),
				postfix: new HarmonyMethod(typeof(Statistics), nameof(Statistics.After_ItemUpdate))
			);
			harmony.Patch(
				AccessTools.Method(typeof(DataManager), "HandlePakcetInvenItemList"),
				postfix: new HarmonyMethod(typeof(Statistics), nameof(Statistics.After_ItemUpdate))
			);

			if (registered)
				Handler.RegListner(this, eType.ResourceRefresh);
		}

		private static void InitializeDatabase() {
			lock (dbLock) {
				using var connection = OpenConnection();
				using var command = connection.CreateCommand();
				command.CommandText = @"
CREATE TABLE IF NOT EXISTS resources (
	time TEXT NOT NULL,
	metal INTEGER NOT NULL,
	nutrient INTEGER NOT NULL,
	power INTEGER NOT NULL,
	maxres INTEGER NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_resources_time ON resources(time);
CREATE INDEX IF NOT EXISTS ix_resources_time ON resources(time DESC);

CREATE TABLE IF NOT EXISTS item_refs (
	id INTEGER PRIMARY KEY AUTOINCREMENT,
	key TEXT NOT NULL UNIQUE,
	disp_name TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS items (
	time TEXT NOT NULL,
	item INTEGER NOT NULL,
	count INTEGER NOT NULL,
	FOREIGN KEY(item) REFERENCES item_refs(id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_items_item_time ON items(item, time);
CREATE INDEX IF NOT EXISTS ix_items_item_time ON items(item, time DESC);
";
				command.ExecuteNonQuery();
			}
		}

		private static SqliteConnection OpenConnection() {
			var connection = new SqliteConnection($"Data Source={StatisticsDBPath}");
			connection.Open();
			return connection;
		}

		private static string GetCurrentMinuteBucket() {
			var now = DateTimeOffset.UtcNow;
			return new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero).ToString("O");
		}

		private static ResourceSnapshot GetCurrentResourceSnapshot() {
			var metal = SingleTon<DataManager>.Instance.Metal;
			var nutrient = SingleTon<DataManager>.Instance.Nutrient;
			var power = SingleTon<DataManager>.Instance.Power;

			var maxRes = (uint)(
				SingleTon<DataManager>.Instance.GetTableGlobalValue(GLOBAL_VALUE_TYPE.RESOURCE_CHARGE_MAX_DEFAULT).Value +
				(
					(SingleTon<DataManager>.Instance.GetUserLevel() - 1) *
					SingleTon<DataManager>.Instance.GetTableGlobalValue(GLOBAL_VALUE_TYPE.RESOURCE_CHARGE_MAX_ACCLEVEL).Value
				)
			);

			return new ResourceSnapshot(metal, nutrient, power, maxRes);
		}

		private static Dictionary<string, int> GetCurrentItemCounts() {
			return SingleTon<DataManager>.Instance.GetAllItem()
				.Where(x => x.ItemType == 4)
				.GroupBy(x => x.ItemKeyString)
				.ToDictionary(x => x.Key, x => x.Sum(y => y.StackCount));
		}

		private static void LogResources() {
			if (!Conf.Statistics.Use_ResourceLogging.Value) return;
			if (!registered || CurrentUID == 0) return;

			var snapshot = GetCurrentResourceSnapshot();
			if (LastResources.HasValue && LastResources.Value.Equals(snapshot))
				return;

			LastResources = snapshot;
			var timestamp = GetCurrentMinuteBucket();

			Task.Run(() => {
				try {
					lock (dbLock) {
						using var connection = OpenConnection();
						using var command = connection.CreateCommand();
						command.CommandText = @"
INSERT INTO resources(time, metal, nutrient, power, maxres)
VALUES ($time, $metal, $nutrient, $power, $maxres)
ON CONFLICT(time) DO UPDATE SET
	metal = excluded.metal,
	nutrient = excluded.nutrient,
	power = excluded.power,
	maxres = excluded.maxres;";
						command.Parameters.AddWithValue("$time", timestamp);
						command.Parameters.AddWithValue("$metal", snapshot.Metal);
						command.Parameters.AddWithValue("$nutrient", snapshot.Nutrient);
						command.Parameters.AddWithValue("$power", snapshot.Power);
						command.Parameters.AddWithValue("$maxres", snapshot.MaxRes);
						command.ExecuteNonQuery();
					}
				} catch (Exception e) {
					Plugin.Logger.LogWarning($"[Symphony::Statistics] Failed to write resource log: {e}");
				}
			});
		}

		private static void LogItems() {
			if (!Conf.Statistics.Use_ResourceLogging.Value || !Conf.Statistics.Use_ItemsLogging.Value) return;
			if (!registered || CurrentUID == 0) return;

			var currentCounts = GetCurrentItemCounts();
			var changedCounts = new Dictionary<string, int>();

			foreach (var pair in currentCounts) {
				if (!LastItemCounts.TryGetValue(pair.Key, out var previousCount) || previousCount != pair.Value)
					changedCounts[pair.Key] = pair.Value;
			}

			foreach (var pair in LastItemCounts) {
				if (!currentCounts.ContainsKey(pair.Key))
					changedCounts[pair.Key] = 0;
			}

			if (changedCounts.Count == 0)
				return;

			foreach (var pair in changedCounts) {
				if (pair.Value > 0) LastItemCounts[pair.Key] = pair.Value;
				else LastItemCounts.Remove(pair.Key);
			}

			var timestamp = GetCurrentMinuteBucket();

			Task.Run(() => {
				try {
					lock (dbLock) {
						using var connection = OpenConnection();
						using var transaction = connection.BeginTransaction();

						foreach (var item in changedCounts) {
							var itemId = EnsureItemReference(connection, transaction, item.Key, GetItemDisplayName(item.Key));
							using var command = connection.CreateCommand();
							command.Transaction = transaction;
							command.CommandText = @"
INSERT INTO items(time, item, count)
VALUES ($time, $item, $count)
ON CONFLICT(item, time) DO UPDATE SET
	count = excluded.count;";
							command.Parameters.AddWithValue("$time", timestamp);
							command.Parameters.AddWithValue("$item", itemId);
							command.Parameters.AddWithValue("$count", item.Value);
							command.ExecuteNonQuery();
						}

						transaction.Commit();
					}
				} catch (Exception e) {
					Plugin.Logger.LogWarning($"[Symphony::Statistics] Failed to write item log: {e}");
				}
			});
		}

		private static string GetItemDisplayName(string key) {
			var table = SingleTon<DataManager>.Instance.GetTableItemConsumable(key);
			return table?.ItemName.Localize() ?? key;
		}

		private static long EnsureItemReference(SqliteConnection connection, SqliteTransaction transaction, string key, string displayName) {
			if (ItemReferenceIds.TryGetValue(key, out var cachedId))
				return cachedId;

			using (var selectCommand = connection.CreateCommand()) {
				selectCommand.Transaction = transaction;
				selectCommand.CommandText = "SELECT id FROM item_refs WHERE key = $key LIMIT 1;";
				selectCommand.Parameters.AddWithValue("$key", key);
				var existing = selectCommand.ExecuteScalar();
				if (existing is long existingId) {
					ItemReferenceIds[key] = existingId;
					return existingId;
				}
				if (existing is int existingId32) {
					ItemReferenceIds[key] = existingId32;
					return existingId32;
				}
			}

			using (var insertCommand = connection.CreateCommand()) {
				insertCommand.Transaction = transaction;
				insertCommand.CommandText = @"
INSERT INTO item_refs(key, disp_name)
VALUES ($key, $disp_name)
ON CONFLICT(key) DO NOTHING;";
				insertCommand.Parameters.AddWithValue("$key", key);
				insertCommand.Parameters.AddWithValue("$disp_name", displayName);
				insertCommand.ExecuteNonQuery();
			}

			using (var selectCommand = connection.CreateCommand()) {
				selectCommand.Transaction = transaction;
				selectCommand.CommandText = "SELECT id FROM item_refs WHERE key = $key LIMIT 1;";
				selectCommand.Parameters.AddWithValue("$key", key);
				var inserted = selectCommand.ExecuteScalar();
				var itemId = Convert.ToInt64(inserted);
				ItemReferenceIds[key] = itemId;
				return itemId;
			}
		}

		private static void LoadLastResources() {
			LastResources = null;
			LastItemCounts.Clear();
			ItemReferenceIds.Clear();

			if (!registered || CurrentUID == 0)
				return;

			try {
				lock (dbLock) {
					using var connection = OpenConnection();

					using (var resourceCommand = connection.CreateCommand()) {
						resourceCommand.CommandText = @"
SELECT metal, nutrient, power, maxres
FROM resources
ORDER BY rowid DESC
LIMIT 1;";
						using var reader = resourceCommand.ExecuteReader();
						if (reader.Read()) {
							LastResources = new ResourceSnapshot(
								checked((uint)reader.GetInt64(0)),
								checked((uint)reader.GetInt64(1)),
								checked((uint)reader.GetInt64(2)),
								checked((uint)reader.GetInt64(3))
							);
						}
					}

					using (var itemCommand = connection.CreateCommand()) {
						itemCommand.CommandText = @"
SELECT refs.id, refs.key, logs.count
FROM items AS logs
INNER JOIN item_refs AS refs ON refs.id = logs.item
INNER JOIN (
	SELECT item, MAX(rowid) AS max_rowid
	FROM items
	GROUP BY item
) AS latest ON latest.item = logs.item AND latest.max_rowid = logs.rowid
;";
						using var reader = itemCommand.ExecuteReader();
						while (reader.Read()) {
							var itemId = reader.GetInt64(0);
							var itemKey = reader.GetString(1);
							var count = reader.GetInt32(2);

							ItemReferenceIds[itemKey] = itemId;
							if (count > 0)
								LastItemCounts[itemKey] = count;
						}
					}
				}
			} catch (Exception e) {
				Plugin.Logger.LogWarning($"[Symphony::Statistics] Failed to load last statistics: {e}");
			}
		}

		public void OnEvent(Base msg) {
			if (msg.Type == eType.ResourceRefresh) {
				Statistics.LogResources();
			}
		}

		private static void After_ItemUpdate() {
			Statistics.LogItems();
		}

		private static void After_HandlePacketLogin() {
			var info = SingleTon<DataManager>.Instance.GetUserInfo();
			if (info != null) Statistics.CurrentUID = info.WID;

			Statistics.InitializeDatabase();
			Statistics.LoadLastResources();
		}

		private static void After_Logout() {
			Statistics.CurrentUID = 0;
			LastResources = null;
			LastItemCounts.Clear();
			ItemReferenceIds.Clear();
		}
	}
}

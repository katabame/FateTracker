using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace FateTracker.Windows
{
	internal class FateListWindow : Window, IDisposable
	{
		private readonly IFateTable fateTable;
		private readonly IObjectTable objectTable;
		private readonly IGameGui gameGui;
		private readonly IClientState clientState;
		private readonly IDataManager dataManager;
		private readonly IPluginLog log;

		private sealed record Row(uint Id, string Name, int Level, string State, int Progress, long Remaining, bool Bonus, float? Distance, Vector3 Position);

		public FateListWindow(IFateTable fateTable, IObjectTable objectTable, IGameGui gameGui, IClientState clientState, IDataManager dataManager, IPluginLog log) : base("FATE一覧###FateTracker_FateList")
		{
			this.fateTable = fateTable;
			this.objectTable = objectTable;
			this.gameGui = gameGui;
			this.clientState = clientState;
			this.dataManager = dataManager;
			this.log = log;

			Size = new Vector2(560, 260);
			SizeCondition = ImGuiCond.FirstUseEver;
		}

		public void Dispose()　{　}

		public static string StateText(string state) => state switch
		{
			"Preparing" => "準備中",
			"Running" => "進行中",
			"WaitingForEnd" => "終了待ち",
			"Ending" => "終了中",
			"Ended" => "終了済",
			"Failed" => "失敗",
			_ => state,
		};

		private static string FormatTime(long seconds)
		{
			if (seconds <= 0) return "-";
			var t = TimeSpan.FromSeconds(seconds);
			return $"{(int)t.TotalMinutes}:{t.Seconds:D2}";
		}

		private void SetFlag(Vector3 pos)
		{
			try
			{
				var mapId = clientState.MapId;
				var map = dataManager.GetExcelSheet<Map>().GetRowOrDefault(mapId);
				if (map == null) return;

				var m = map.Value;
				float Conv(float world, int offset) => 0.02f * offset + 2048f / m.SizeFactor + 0.02f * world + 1f;

				var x = Conv(pos.X, m.OffsetX);
				var y = Conv(pos.Z, m.OffsetY);

				gameGui.OpenMapWithMapLink(new MapLinkPayload(clientState.TerritoryType, mapId, x, y));
			}
			catch (Exception ex)
			{
				log.Error(ex, "マップフラグの設定に失敗しました。");
			}
		}

		public override void Draw()
		{
			using var child = ImRaii.Child("ChildWithAScrollbar", Vector2.Zero, true);

			if (child.Success)
			{
				var player = objectTable.LocalPlayer;
				var rows = new List<Row>();

				foreach (var fate in fateTable)
				{
					if (fate == null) continue;

					float? distance = null;
					if (player != null)
					{
						var d = fate.Position - player.Position;
						distance = MathF.Sqrt(d.X * d.X + d.Z * d.Z);
					}

					rows.Add(new Row(
						fate.FateId,
						fate.Name.TextValue,
						fate.Level,
						StateText(fate.State.ToString()),
						fate.Progress,
						fate.TimeRemaining,
						fate.HasBonus,
						distance,
						fate.Position
					));
				}

				ImGui.TextUnformatted($"出現中のFATE: {rows.Count}件");

				if (rows.Count == 0)
				{
					ImGui.TextDisabled("現在、出現中のFATEはありません。");
					return;
				}

				var sorted = rows.OrderBy(r => r.Distance ?? float.MaxValue).ToList();

				if (!ImGui.BeginTable("fatelist", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp)) return;

				ImGui.TableSetupColumn("FATE");
				ImGui.TableSetupColumn("状態");
				ImGui.TableSetupColumn("進行度");
				ImGui.TableSetupColumn("残り時間");
				ImGui.TableSetupColumn("距離");
				ImGui.TableHeadersRow();

				foreach (var r in sorted)
				{
					ImGui.TableNextRow();

					ImGui.TableNextColumn();
					if (ImGui.Selectable($"Lv{r.Level} {r.Name}{(r.Bonus ? " ★" : "")}##{r.Id}", false, ImGuiSelectableFlags.SpanAllColumns))
					{
						SetFlag(r.Position);
					}

					if (ImGui.IsItemHovered())
					{
						ImGui.SetTooltip("クリックでマップにフラグを立てる");
					}

					ImGui.TableNextColumn();
					ImGui.TextUnformatted(r.State);

					ImGui.TableNextColumn();
					ImGui.ProgressBar(Math.Clamp(r.Progress / 100f, 0f, 1f), new Vector2(-1, 0), $"{r.Progress}%");

					ImGui.TableNextColumn();
					ImGui.TextUnformatted(FormatTime(r.Remaining));

					ImGui.TableNextColumn();
					ImGui.TextUnformatted(r.Distance is { } dist ? $"{dist:F0}y" : "-");
				}

				ImGui.EndTable();
			}
		}
	}
}

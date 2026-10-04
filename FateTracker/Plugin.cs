using Dalamud.Game.ClientState.Fates;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FateTracker.Windows;
using System;
using System.Collections.Generic;

namespace FateTracker;

public sealed class Plugin : IDalamudPlugin
{
	[PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
	[PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
	[PluginService] internal static IPluginLog Log { get; private set; } = null!;
	[PluginService] internal static IFramework Framework { get; private set; } = null!;
	[PluginService] internal static IClientState ClientState { get; private set; } = null!;
	[PluginService] internal static IFateTable FateTable { get; private set; } = null!;
	[PluginService] internal static IToastGui ToastGui { get; private set; } = null!;
	[PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
	[PluginService] internal static IGameGui GameGui { get; private set; } = null!;
	[PluginService] internal static IDataManager DataManager { get; private set; } = null!;

	private const string CommandName = "/fatetracker";
	private const string ListCommandName = "/fatelist";

	public Configuration Configuration { get; init; }

	public readonly WindowSystem WindowSystem = new("FateTracker");
	private MainWindow MainWindow { get; init; }
	private FateListWindow ListWindow { get; init; }

	private readonly HashSet<uint> knownFates = [];
	private DateTime primeUntil = DateTime.MinValue;
	private DateTime nextCheck = DateTime.MinValue;

	public Plugin()
	{
		Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
		MainWindow = new MainWindow(this);
		WindowSystem.AddWindow(MainWindow);

		ListWindow = new FateListWindow(FateTable, ObjectTable, GameGui, ClientState, DataManager, Log);
		WindowSystem.AddWindow(ListWindow);

		CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
		{
			HelpMessage = "FATE出現通知の設定を開きます。"
		});

		CommandManager.AddHandler(ListCommandName, new CommandInfo(OnListCommand)
		{
			HelpMessage = "出現中のFATE一覧と進行度を表示します。"
		});

		PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
		PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
		Framework.Update += OnUpdate;
		ClientState.TerritoryChanged += OnTerritoryChanged;

		StartPriming();

		Log.Information($"{PluginInterface.Manifest.Name} loaded.");
	}

	public void Dispose()
	{
		PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
		PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
		Framework.Update -= OnUpdate;
		ClientState.TerritoryChanged -= OnTerritoryChanged;

		WindowSystem.RemoveAllWindows();
		((IDisposable)MainWindow).Dispose();

		CommandManager.RemoveHandler(CommandName);
		CommandManager.RemoveHandler(ListCommandName);
	}

	private void OnCommand(string command, string args)
	{
		MainWindow.Toggle();
	}

	private void OnListCommand(string command, string args)
	{
		ListWindow.Toggle();
	}

	public void ToggleMainUi() => MainWindow.Toggle();

	private void OnTerritoryChanged(uint territoryId) => StartPriming();

	private void StartPriming()
	{
		knownFates.Clear();
		primeUntil = DateTime.UtcNow.AddSeconds(5);
	}

	private void OnUpdate(IFramework framework)
	{
		var now = DateTime.UtcNow;
		if (now < nextCheck) return;
		nextCheck = now.AddSeconds(1);
		if (!ClientState.IsLoggedIn) return;

		var priming = now < primeUntil;
		var current = new HashSet<uint>();

		foreach (var fate in FateTable)
		{
			if (fate == null) continue;
			var id = (uint)fate.FateId;
			current.Add(id);

			if (!knownFates.Add(id)) continue;
			if (priming || !Configuration.Enabled) continue;
			if (Configuration.MinLevel > 0 && fate.Level < Configuration.MinLevel) continue;

			Notify(fate);
		}

		knownFates.IntersectWith(current);
	}

	private void Notify(IFate fate)
	{
		try
		{
			var message = Configuration.ShowLevel
				? $"FATE {fate.Name} Lv.{fate.Level} が出現しました！"
				: $"FATE {fate.Name} が出現しました！";

			if (Configuration.UseQuestStyle)
			{
				ToastGui.ShowQuest(message);
			}
			else
			{
				ToastGui.ShowNormal(message);
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Toast通知に失敗しました。");
		}
	}
}

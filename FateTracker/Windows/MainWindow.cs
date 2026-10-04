using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

namespace FateTracker.Windows;

public class MainWindow : Window, IDisposable
{
	private readonly Plugin plugin;

	public MainWindow(Plugin plugin) : base("FATE出現通知設定###FateTracker_MainWindow")
	{
		this.plugin = plugin;
		Size = new Vector2(320, 400);
		SizeCondition = ImGuiCond.FirstUseEver;
	}

	public void Dispose() { }

	public override void Draw()
	{
		using var child = ImRaii.Child("ChildWithAScrollbar", Vector2.Zero, true);

		if (child.Success)
		{
			var config = plugin.Configuration;

			var enabled = config.Enabled;
			if (ImGui.Checkbox("通知を有効にする", ref enabled))
			{
				config.Enabled = enabled;
				config.Save();
			}

			var showLevel = config.ShowLevel;
			if(ImGui.Checkbox("レベルを表示する", ref showLevel))
			{
				config.ShowLevel = showLevel;
				config.Save();
			}

			var useQuestStyle = config.UseQuestStyle;
			if (ImGui.Checkbox("クエスト風の表示にする", ref useQuestStyle))
			{
				config.UseQuestStyle = useQuestStyle;
				config.Save();
			}

			var minLevel = config.MinLevel;
			ImGui.SetNextItemWidth(100);
			if (ImGui.InputInt("通知するFATEの最小レベル (0で無効)", ref minLevel))
			{
				config.MinLevel = System.Math.Clamp(minLevel, 0, 100);
				config.Save();
			}
		}
	}
}

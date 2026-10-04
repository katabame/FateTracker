using Dalamud.Configuration;
using System;

namespace FateTracker;

[Serializable]
public class Configuration : IPluginConfiguration
{
	public int Version { get; set; } = 1;

	public bool Enabled { get; set; } = true;
	public bool ShowLevel { get; set; } = true;
	public bool UseQuestStyle { get; set; } = false;
	public int MinLevel { get; set; } = 0;

	public void Save()
	{
		Plugin.PluginInterface.SavePluginConfig(this);
	}
}

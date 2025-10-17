
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using SamplePlugin.Windows;
using System;
using System.Diagnostics;

namespace SamplePlugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    private const string CommandName = "/sct";
    private static readonly string CommandFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Kintelligence", "Plugin", "StrawberryCookieTools.txt");

    private readonly Stopwatch fileCheckStopwatch = new();

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("StrawberryCookieTools");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this, goatImagePath);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the main window."
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Framework.Update += OnFrameworkUpdate;
        fileCheckStopwatch.Start();

        Log.Information($"=== Initialized {PluginInterface.Manifest.Name} ===");
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (fileCheckStopwatch.ElapsedMilliseconds < 200)
        {
            return;
        }
        fileCheckStopwatch.Restart();

        try
        {
            if (!File.Exists(CommandFilePath))
            {
                var directory = Path.GetDirectoryName(CommandFilePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.Create(CommandFilePath).Close();
                return;
            }

            var command = File.ReadAllText(CommandFilePath).Trim();

            if (string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            Log.Information($"Executing command from file: {command}");
            CommandManager.ProcessCommand(command);
            
            File.WriteAllText(CommandFilePath, string.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing command file.");
        }
    }

    private void OnCommand(string command, string args)
    {
        MainWindow.Toggle();
    }
    
    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}



using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using SamplePlugin.Windows;
using System;
using System.Diagnostics;
using System.Text.Json;

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
    private ulong _loggedInContentId;

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

        ClientState.Login += OnLogin;
        ClientState.Logout += OnLogout;

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
        
        ClientState.Login -= OnLogin;
        ClientState.Logout -= OnLogout;
    }

    private void OnLogin()
    {
        try
        {
            var contentId = ClientState.LocalContentId;
            if (contentId == 0) return;

            _loggedInContentId = contentId;

            var pid = Process.GetCurrentProcess().Id;
            var directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Kintelligence", "Plugin", "StrawberryCookieTools");
            var filePath = Path.Combine(directoryPath, $"{contentId}.json"); // Change extension to .json

            Directory.CreateDirectory(directoryPath);

            var localPlayer = ClientState.LocalPlayer;
            var loginInfo = new CharacterLoginInfo
            {
                Pid = pid,
                ContentId = contentId,
                CharacterName = localPlayer?.Name.ToString(),
                WorldName = localPlayer?.HomeWorld.Value.Name.ToString(),
                ClassJobAbbreviation = localPlayer?.ClassJob.Value.Abbreviation.ToString(),
                Level = localPlayer?.Level ?? 0
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            var jsonString = JsonSerializer.Serialize(loginInfo, options);

            File.WriteAllText(filePath, jsonString);
            Log.Information($"Created JSON file for Content ID {contentId} at {filePath}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating JSON file.");
        }
    }

    private void OnLogout(int type, int code)
    {
        try
        {
            if (_loggedInContentId == 0) return;

            var directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Kintelligence", "Plugin", "StrawberryCookieTools");
            var filePath = Path.Combine(directoryPath, $"{_loggedInContentId}.json"); // Change extension to .json

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Log.Information($"Deleted JSON file for Content ID {_loggedInContentId} at {filePath}");
            }
            _loggedInContentId = 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error deleting JSON file.");
        }
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

public class CharacterLoginInfo
{
    public int Pid { get; set; }
    public ulong ContentId { get; set; }
    public string? CharacterName { get; set; }
    public string? WorldName { get; set; }
    public string? ClassJobAbbreviation { get; set; }
    public int Level { get; set; }
}


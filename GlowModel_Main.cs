using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Listeners;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Units;

public sealed class GlowMain : IModSharpModule, IEventListener
{
    public string DisplayName => "GlowModule";
    public string DisplayAuthor => "si";

    private readonly ILogger<GlowMain> _logger;
    private readonly ISharedSystem _sharedSystem;
    private readonly IEventManager _events;
    private readonly GlowManager _glowManager;

    public GlowMain(ISharedSystem sharedSystem,
        string? dllPath = null,
        string? sharpPath = null,
        Version? version = null,
        Microsoft.Extensions.Configuration.IConfiguration? coreConfiguration = null,
        bool hotReload = false)
    {
        ArgumentNullException.ThrowIfNull(dllPath);
        ArgumentNullException.ThrowIfNull(sharpPath);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(coreConfiguration);

        _sharedSystem = sharedSystem ?? throw new ArgumentNullException(nameof(sharedSystem));
        _logger = _sharedSystem.GetLoggerFactory().CreateLogger<GlowMain>();
        _events = _sharedSystem.GetEventManager();

        _glowManager = new GlowManager(sharedSystem, _logger);
    }

    public bool Init()
    {
        _logger.LogInformation("GlowMain Init()");
        _glowManager.RegisterCommands();
        return true;
    }

    public void PostInit()
    {
        _logger.LogInformation("GlowMain PostInit()");
        _events.InstallEventListener(this);
        _events.HookEvent("round_start");
        _events.HookEvent("player_disconnect");
    }

    public void Shutdown()
    {
        _logger.LogInformation("GlowMain Shutdown()");
        _events.RemoveEventListener(this);
        _glowManager.CleanupAll();
        _glowManager.UnregisterCommands();
    }

    public int ListenerVersion => 1;
    public int ListenerPriority => 0;

    public void FireGameEvent(IGameEvent ev)
    {
        _logger.LogDebug($"FireGameEvent: {ev.Name}");

        if (ev.Name == "round_start")
        {
            _glowManager.CleanupAll();
            _logger.LogInformation("新回合開始，已清理所有 Glow");
        }
        else if (ev.Name == "player_disconnect")
        {
            var slot = (PlayerSlot)ev.GetInt("userid");
            _glowManager.DisableGlowForSlot(slot);
            _logger.LogInformation("玩家斷線，已清理該玩家 Glow");
        }
    }
}



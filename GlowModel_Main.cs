using Microsoft.Extensions.Logging;
using PlayerManager_Shared.Abstractions;
using Sharp.Shared;
using Sharp.Shared.Listeners;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Units;

public sealed class GlowMain : IModSharpModule, IEventListener, IGameListener
{
    public string DisplayName => "PlayerManager_Shared";
    public string DisplayAuthor => "Widez";

    private readonly ILogger<GlowMain> _logger;
    private readonly ISharedSystem _sharedSystem;
    private readonly IEventManager _events;

    private GlowManager _glowManager;
    private IPlayerManager _playerManager;

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
    }

    public bool Init() => true;
    public void PostInit()
    {
        _events.InstallEventListener(this);
        _sharedSystem.GetModSharp().InstallGameListener(this);
        _events.HookEvent("player_disconnect");
    }

    public void OnAllModulesLoaded()
    {
        var wrapper = _sharedSystem.GetSharpModuleManager()
            .GetRequiredSharpModuleInterface<IPlayerManager>(IPlayerManager.Identity);

        _playerManager = wrapper.Instance
                         ?? throw new InvalidOperationException("PlayerManager_Shared 介面不可為 null");

        _logger.LogInformation("GlowMain 成功取得 PlayerManager");

        // 初始化 GlowManager，這裡一定有 playerManager
        _glowManager = new GlowManager(_sharedSystem, _logger, _playerManager);
        _glowManager.RegisterCommands();
    }

    public void OnLibraryDisconnect(string name)
    {
        if (name == IPlayerManager.Identity)
        {
            _logger.LogWarning("PlayerManager 已卸載，GlowMain 將退回 Slot 模式");
            _playerManager = null;
        }
    }

    public void Shutdown()
    {
        _events.RemoveEventListener(this);
        _sharedSystem.GetModSharp().RemoveGameListener(this);
        _glowManager?.CleanupAll();
        _glowManager?.UnregisterCommands();
    }

    public int ListenerVersion => IGameListener.ApiVersion;
    public int ListenerPriority => int.MaxValue;

    public void OnRoundRestart()
    {
        _logger.LogInformation("回合即將重啟，清理暫存狀態");
        _glowManager?.CleanupAll();
    }
    public void FireGameEvent(IGameEvent ev)
    {
        
        if (ev.Name == "player_disconnect")
        {
            int rawUserId = ev.GetInt("userid");
            var userId = new UserID((ushort)rawUserId);

            var client = _sharedSystem.GetClientManager().GetGameClient(userId);
            if (client == null || !client.IsValid)
            {
                _logger.LogInformation("Player disconnect：Unknown Client (UserID {UserId})，Clear Glow", rawUserId);
                return;
            }

            var player = _playerManager.GetPlayer(client);
            if (player == null)
            {
                _logger.LogInformation("Player disconnect：Unknown Player (UserID {UserId})，Clear Glow", rawUserId);
                return;
            }

            _glowManager?.DisableGlowForSlot(player.Client.Slot);
            _logger.LogInformation("Player disconnect：{Name} (Slot {Slot})，Clear Glow",
                player.Name, player.Client.Slot.AsPrimitive());
        }

    }
}




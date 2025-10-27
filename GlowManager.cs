using GlowPlugin;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using Sharp.Shared.Units;

public class GlowManager
{
    private readonly ISharedSystem _sharedSystem;
    private readonly ILogger _logger;
    private readonly GlowModules _glowModules;

    public GlowManager(ISharedSystem sharedSystem, ILogger logger)
    {
        _sharedSystem = sharedSystem;
        _logger = logger;
        _glowModules = new GlowModules(sharedSystem, logger);
    }

    public void RegisterCommands()
    {
        var cm = _sharedSystem.GetClientManager();
        cm.InstallCommandCallback("glow", OnGlowCommand);
        cm.InstallCommandCallback("disableglow", OnDisableGlowCommand);
        _logger.LogInformation("Glow commands registered");
    }

    public void UnregisterCommands()
    {
        var cm = _sharedSystem.GetClientManager();
        cm.RemoveCommandCallback("glow", OnGlowCommand);
        cm.RemoveCommandCallback("disableglow", OnDisableGlowCommand);
        _logger.LogInformation("Glow commands unregistered");
    }

    private ECommandAction OnGlowCommand(IGameClient client, StringCommand command)
    {
        if (!client.IsValid || client.IsFakeClient)
            return ECommandAction.Stopped;

        var pawn = _sharedSystem.GetEntityManager().FindPlayerPawnBySlot(client.Slot);
        if (pawn == null)
        {
            client.ConsolePrint("無法啟用 Glow：玩家沒有 Pawn");
            return ECommandAction.Stopped;
        }

        _glowModules.EnablePlayerGlow(pawn, client.Slot);
        client.ConsolePrint("玩家 Glow 已啟用！");
        return ECommandAction.Stopped;
    }

    private ECommandAction OnDisableGlowCommand(IGameClient client, StringCommand command)
    {
        if (!client.IsValid || client.IsFakeClient)
            return ECommandAction.Stopped;

        _glowModules.DisablePlayerGlow(client.Slot);
        client.ConsolePrint("玩家 Glow 已停用！");
        return ECommandAction.Stopped;
    }

    public void DisableGlowForSlot(PlayerSlot slot) => _glowModules.DisablePlayerGlow(slot);

    public void CleanupAll() => _glowModules.CleanupAll();
}




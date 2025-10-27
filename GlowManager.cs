using GlowPlugin;
using Microsoft.Extensions.Logging;
using PlayerManager_Shared.Abstractions;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using Sharp.Shared.Units;
using System.Linq;

public class GlowManager
{
    private readonly ISharedSystem _sharedSystem;
    private readonly ILogger _logger;
    private readonly GlowModules _glowModules;
    private readonly IPlayerManager _playerManager; // ✅ 強制依賴

    public GlowManager(ISharedSystem sharedSystem, ILogger logger, IPlayerManager playerManager)
    {
        _sharedSystem = sharedSystem;
        _logger = logger;
        _glowModules = new GlowModules(sharedSystem, logger);
        _playerManager = playerManager ?? throw new ArgumentNullException(nameof(playerManager));
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

        var target = FindTargetPlayer(command, client);
        if (target == null)
        {
            client.ConsolePrint("找不到指定的玩家");
            return ECommandAction.Stopped;
        }

        var pawn = _sharedSystem.GetEntityManager().FindPlayerPawnBySlot(target.Client.Slot);
        if (pawn == null)
        {
            client.ConsolePrint($"玩家 {target.Name} 沒有 Pawn，無法啟用 Glow");
            return ECommandAction.Stopped;
        }

        _glowModules.EnablePlayerGlow(pawn, target.Client.Slot);
        client.ConsolePrint($"已對玩家 {target.Name} 啟用 Glow！");
        return ECommandAction.Stopped;
    }

    private ECommandAction OnDisableGlowCommand(IGameClient client, StringCommand command)
    {
        if (!client.IsValid || client.IsFakeClient)
            return ECommandAction.Stopped;

        var target = FindTargetPlayer(command, client);
        if (target == null)
        {
            client.ConsolePrint("找不到指定的玩家");
            return ECommandAction.Stopped;
        }

        _glowModules.DisablePlayerGlow(target.Client.Slot);
        client.ConsolePrint($"已對玩家 {target.Name} 停用 Glow！");
        return ECommandAction.Stopped;
    }

    /// <summary>
    /// 根據指令參數尋找目標玩家
    /// </summary>
    private IGamePlayer? FindTargetPlayer(StringCommand command, IGameClient self)
    {
        // 預設對自己
        IGamePlayer? target = _playerManager.GetPlayer(self);

        if (command.ArgCount > 0)
        {
            var nameArg = command.GetArg(1);
            var players = _playerManager.GetPlayers(false);

            var match = players.FirstOrDefault(p =>
                p.IsValid() &&
                p.IsConnected &&
                !string.IsNullOrEmpty(p.Name) &&
                p.Name.Contains(nameArg, StringComparison.OrdinalIgnoreCase));

            if (match != null)
                target = match;
        }

        return target;
    }

    public void DisableGlowForSlot(PlayerSlot slot) => _glowModules.DisablePlayerGlow(slot);

    public void CleanupAll() => _glowModules.CleanupAll();
}






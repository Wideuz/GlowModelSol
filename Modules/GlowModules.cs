using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using Sharp.Shared.Units;
using System;
using System.Collections.Generic;

namespace GlowPlugin;

public class GlowModules
{
    private readonly ISharedSystem _sharedSystem;
    private readonly ILogger _logger;

    // slot -> [glow, relay]
    private readonly Dictionary<PlayerSlot, List<IBaseModelEntity>> _glowingEntities = new();

    public GlowModules(ISharedSystem sharedSystem, ILogger logger)
    {
        _sharedSystem = sharedSystem;
        _logger = logger;
    }

    /// <summary>
    /// 啟用玩家 Glow
    /// </summary>
    public void EnablePlayerGlow(IPlayerPawn pawn, PlayerSlot slot, int duration = 0)
    {
        try
        {
            if (_glowingEntities.ContainsKey(slot))
            {
                _logger.LogInformation("Glow already active for slot {slot}", slot.AsPrimitive());
                return;
            }

            var model = GetModelNameSafe(pawn) ?? "";
            var origin = ToEKVString(pawn.GetAbsOrigin());
            var angles = ToEKVString(pawn.GetAbsAngles());
            var entityMgr = _sharedSystem.GetEntityManager();
            var transmitMgr = _sharedSystem.GetTransmitManager();

            // Relay (不可見)
            var relay = entityMgr.SpawnEntitySync<IBaseModelEntity>(
                "prop_dynamic",
                new Dictionary<string, KeyValuesVariantValueItem>
                {
                    ["model"] = model,
                    ["origin"] = origin,
                    ["angles"] = angles,
                    ["spawnflags"] = 256,
                    ["rendermode"] = (int)RenderMode.None,
                });

            // Glow 模型
            var glow = entityMgr.SpawnEntitySync<IBaseModelEntity>(
                "prop_dynamic",
                new Dictionary<string, KeyValuesVariantValueItem>
                {
                    ["model"] = model,
                    ["origin"] = origin,
                    ["angles"] = angles,
                    ["spawnflags"] = 256,
                    ["renderamt"] = 1,
                    ["glowcolor"] = "255 0 0",
                    ["glowrange"] = 5000,
                    ["glowteam"] = -1,
                    ["glowstate"] = 3
                });

            if (relay == null || glow == null)
            {
                _logger.LogWarning("Glow entity spawn failed for slot {slot}", slot.AsPrimitive());
                return;
            }

            // 嘗試 FollowEntity
            try
            {
                relay.AcceptInput("FollowEntity", pawn, relay, "!activator");
                glow.AcceptInput("FollowEntity", relay, glow, "!activator");
                _logger.LogInformation("Glow follow established via FollowEntity");
            }
            catch
            {
                _logger.LogWarning("FollowEntity failed, fallback to SetParent only");
                relay.AcceptInput("SetParent", pawn);
                glow.AcceptInput("SetParent", relay);
            }

            _glowingEntities[slot] = new List<IBaseModelEntity> { glow, relay };

            // 使用 TransmitManager 隱藏自己
            var controller = entityMgr.FindPlayerControllerBySlot(slot);
            if (controller != null)
            {
                transmitMgr.AddEntityHooks(glow, true);
                transmitMgr.SetEntityState(glow.Index, controller.Index, false, -1);
            }

            var playerName = controller?.PlayerName ?? "Unknown";
            _logger.LogInformation(
                "PlayerGlow enabled for slot {slot} (player={player}, model={model})",
                slot.AsPrimitive(), playerName, model
            );

            // 如果有 duration → 到期自動清理
            if (duration > 0)
            {
                _sharedSystem.GetModSharp().PushTimer(() =>
                {
                    DisablePlayerGlow(slot);
                    return TimerAction.Stop;
                }, duration, GameTimerFlags.StopOnRoundEnd | GameTimerFlags.StopOnMapEnd);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EnablePlayerGlow failed for slot {slot}", slot.AsPrimitive());
        }
    }

    /// <summary>
    /// 停用玩家 Glow
    /// </summary>
    public void DisablePlayerGlow(PlayerSlot slot)
    {
        if (_glowingEntities.TryGetValue(slot, out var entities))
        {
            foreach (var ent in entities)
            {
                if (ent != null && ent.IsValidEntity)
                    ent.AcceptInput("Kill");
            }

            _glowingEntities.Remove(slot);
            _logger.LogInformation("PlayerGlow disabled for slot {slot}", slot.AsPrimitive());
        }
    }

    /// <summary>
    /// 清理所有 Glow
    /// </summary>
    public void CleanupAll()
    {
        foreach (var kv in _glowingEntities)
        {
            foreach (var ent in kv.Value)
            {
                if (ent != null && ent.IsValidEntity)
                    ent.AcceptInput("Kill");
            }
        }

        _glowingEntities.Clear();
        _logger.LogInformation("All glow entities cleaned up");
    }

    // 工具方法
    private static string? GetModelNameSafe(IPlayerPawn pawn)
    {
        var body = pawn.GetBodyComponent();
        var sceneNode = body?.GetSceneNode();
        var skeleton = sceneNode?.AsSkeletonInstance;
        var modelState = skeleton?.GetModelState();
        return modelState?.ModelName;
    }

    private static string ToEKVString(Vector v)
        => $"{v.X} {v.Y} {v.Z}";
}



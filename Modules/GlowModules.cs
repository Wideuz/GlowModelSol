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
    private readonly Dictionary<PlayerSlot, List<IBaseModelEntity>> _glowingEntities = new();

    public GlowModules(ISharedSystem sharedSystem, ILogger logger)
    {
        _sharedSystem = sharedSystem;
        _logger = logger;
    }

    // -------------------------
    // 玩家 Glow (Relay + GlowModel)
    // -------------------------
    public void EnablePlayerGlow(IPlayerPawn pawn, PlayerSlot slot)
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

            // Glow 模型 (可見 + 發光)
            var glow = entityMgr.SpawnEntitySync<IBaseModelEntity>(
                "prop_dynamic",
                new Dictionary<string, KeyValuesVariantValueItem>
                {
                    ["model"] = model,
                    ["origin"] = origin,
                    ["angles"] = angles,
                    ["spawnflags"] = 256,
                    ["renderamt"] = 1, // 確保模型有最小透明度
                });

            if (relay == null || glow == null)
            {
                _logger.LogWarning("Glow entity spawn failed");
                return;
            }

            // 綁定跟隨
            relay.AcceptInput("FollowEntity", pawn);
            glow.AcceptInput("FollowEntity", relay);

            // 設 GlowProperty
            var gp = glow.GetGlowProperty();
            gp.Glowing = true;
            gp.GlowColorOverride = new Color32(255, 0, 0, 255); // 紅色
            // 或者用 gp.GlowColor = new Vector(1.0f, 0.0f, 0.0f);
            gp.GlowRangeMin = 0;
            gp.GlowRangeMax = 5000;
            gp.GlowTeam = -1; // -1 = 所有人可見
            gp.GlowType = 3;  // Always on

            _glowingEntities[slot] = new List<IBaseModelEntity> { glow, relay };

            var controller = entityMgr.FindPlayerControllerBySlot(slot);
            var playerName = controller?.PlayerName ?? "Unknown";
            _logger.LogInformation(
                "PlayerGlow enabled for slot {slot} (player={player}, model={model})",
                slot.AsPrimitive(), playerName, model
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EnablePlayerGlow failed");
        }
    }

    public void DisablePlayerGlow(PlayerSlot slot)
    {
        if (_glowingEntities.TryGetValue(slot, out var entities))
        {
            foreach (var ent in entities)
            {
                if (ent != null && ent.IsValidEntity)
                {
                    ent.AcceptInput("Kill");
                }
            }
            _glowingEntities.Remove(slot);
            _logger.LogInformation("PlayerGlow disabled for slot {slot}", slot.AsPrimitive());
        }
    }

    // -------------------------
    // 物件 Glow (保留接口)
    // -------------------------
    public void EnableObjectGlow(IPlayerPawn pawn, PlayerSlot slot)
    {
        try
        {
            var model = GetModelNameSafe(pawn) ?? "";
            var origin = ToEKVString(pawn.GetAbsOrigin());
            var angles = ToEKVString(pawn.GetAbsAngles());

            var modelGlow = _sharedSystem.GetEntityManager().SpawnEntitySync<IBaseModelEntity>(
                "prop_dynamic",
                new Dictionary<string, KeyValuesVariantValueItem>
                {
                    ["model"] = model,
                    ["origin"] = origin,
                    ["angles"] = angles,
                    ["spawnflags"] = 256,
                    ["rendermode"] = (int)RenderMode.Normal,
                });

            if (modelGlow == null)
            {
                _logger.LogWarning("Glow object spawn failed");
                return;
            }

            var gp = modelGlow.GetGlowProperty();
            gp.Glowing = true;
            gp.GlowColorOverride = new Color32(0, 0, 255, 255); // 藍色
            gp.GlowRangeMin = 0;
            gp.GlowRangeMax = 5000;
            gp.GlowTeam = 0;
            gp.GlowType = 3;

            if (!_glowingEntities.ContainsKey(slot))
                _glowingEntities[slot] = new List<IBaseModelEntity>();

            _glowingEntities[slot].Add(modelGlow);

            _logger.LogInformation(
                "ObjectGlow enabled for slot {slot}, model={model}",
                slot.AsPrimitive(), model
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EnableObjectGlow failed");
        }
    }

    public void DisableObjectGlow(PlayerSlot slot) => DisablePlayerGlow(slot);

    // -------------------------
    // 全部清理
    // -------------------------
    public void CleanupAll()
    {
        foreach (var kv in _glowingEntities)
        {
            foreach (var ent in kv.Value)
            {
                if (ent != null && ent.IsValidEntity)
                {
                    ent.AcceptInput("Kill");
                }
            }
        }
        _glowingEntities.Clear();
        _logger.LogInformation("All glow entities cleaned up");
    }

    // -------------------------
    // 工具方法
    // -------------------------
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



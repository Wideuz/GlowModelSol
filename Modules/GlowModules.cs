using Microsoft.Extensions.Logging;
using PlayerManager_Shared; // ✅ 引入 GamePlayer
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using Sharp.Shared.Units;
using System;
using System.Collections.Generic;
using System.Globalization;

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

    public bool CreateGlow(IGameClient player, IPlayerPawn pawn, Color32 color, int maxDistance, IEnumerable<EntityIndex> whoCanSee)
    {
        var model = GetModelNameSafe(pawn);
        if (string.IsNullOrEmpty(model))
            return false;

        var entityMgr = _sharedSystem.GetEntityManager();
        var transmitMgr = _sharedSystem.GetTransmitManager();

        // Relay
        var relayKv = new Dictionary<string, KeyValuesVariantValueItem>
        {
            { "model", model },
            { "spawnflags", 256 },
            { "rendermode", 10 },
            { "disablereceiveshadows", true },
            { "disableshadows", true },
        };

        if (entityMgr.SpawnEntitySync<IBaseModelEntity>("prop_dynamic", relayKv) is not { } relay)
            return false;

        // Glow
        var glowKv = new Dictionary<string, KeyValuesVariantValueItem>
        {
            { "model", model },
            { "spawnflags", 256 },
            { "disablereceiveshadows", true },
            { "disableshadows", true },
            { "glowcolor", $"{color.R} {color.G} {color.B} {color.A}" },
            { "glowrangemin", 30 },
            { "glowrange", maxDistance },
            { "glowteam", -1 },
            { "glowstate", 3 },
            { "renderamt", 1 },
        };

        if (entityMgr.SpawnEntitySync<IBaseModelEntity>("prop_dynamic", glowKv) is not { } glow)
        {
            relay.AcceptInput("Kill");
            return false;
        }

        // 綁定跟隨
        relay.AcceptInput("FollowEntity", pawn, null, "!activator");
        glow.AcceptInput("FollowEntity", relay, null, "!activator");

        // 傳輸控制
        var slot = player.Slot;
        var controllerIndex = player.ControllerIndex;

        transmitMgr.AddEntityHooks(relay, false);
        transmitMgr.AddEntityHooks(glow, false);
        transmitMgr.SetEntityOwner(relay.Index, controllerIndex);
        transmitMgr.SetEntityOwner(glow.Index, controllerIndex);
        transmitMgr.SetEntityState(relay.Index, controllerIndex, false, -1);
        transmitMgr.SetEntityState(glow.Index, controllerIndex, false, -1);

        foreach (var index in whoCanSee)
        {
            transmitMgr.SetEntityState(relay.Index, index, true, -1);
            transmitMgr.SetEntityState(glow.Index, index, true, -1);
        }

        _glowingEntities[slot] = new List<IBaseModelEntity> { glow, relay };

        return true;
    }

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
    }

    private static string? GetModelNameSafe(IPlayerPawn pawn)
    {
        var body = pawn.GetBodyComponent();
        var sceneNode = body?.GetSceneNode();
        var skeleton = sceneNode?.AsSkeletonInstance;
        var modelState = skeleton?.GetModelState();
        return modelState?.ModelName;
    }
}



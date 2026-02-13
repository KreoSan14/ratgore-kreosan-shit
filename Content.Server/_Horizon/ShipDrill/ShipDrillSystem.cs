using Content.Server.Gatherable;
using Content.Server.Gatherable.Components;
using Content.Shared.Decals;
using Content.Shared.Maps;
using Content.Shared.Power;
using Content.Shared.Tiles;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;

namespace Content.Server._Horizon.ShipDrill;

public sealed class ShipDrillSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = null!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitionManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GatherableSystem _gatherable = default!;
    [Dependency] private readonly SharedDecalSystem _decal = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private readonly HashSet<EntityUid> _cache = new(32);

    private EntityQuery<GatherableComponent> _gatherQuery;

    // ID тайла, который разрешено ломать
    private const string TargetTileId = "FloorAsteroidSand";

    public override void Initialize()
    {
        SubscribeLocalEvent<ShipDrillComponent, PowerChangedEvent>(OnPowerChange);

        _gatherQuery = GetEntityQuery<GatherableComponent>();
    }

    private void OnPowerChange(EntityUid uid, ShipDrillComponent component, ref PowerChangedEvent args)
    {
        component.Powered = args.Powered;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ShipDrillComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var drill, out var xform))
        {
            if (!drill.Powered)
                continue;

            drill.Accumulator += frameTime;
            if (drill.Accumulator < drill.Cooldown)
                continue;

            drill.Accumulator = 0f;

            var coords = _transform.GetMapCoordinates(xform);
            var nGridUid = xform.GridUid;

            if (nGridUid is not {} gridUid)
                continue;

            // Добываем область 5x5 вокруг бура
            for (var x = -2; x <= 2; x++)
            {
                for (var y = -2; y <= 2; y++)
                {
                    // Пропускаем самую центральную клетку (где находится бур)
                    if (x == 0 && y == 0)
                        continue;
                        
                    TryMine(coords.Offset(x, y), gridUid, uid, drill.HitSound);
                }
            }
        }
    }

    private void TryMine(MapCoordinates coordinates, EntityUid drillGrid, EntityUid drill, SoundSpecifier? sound)
    {
        if (!_mapManager.TryFindGridAt(coordinates, out var gridUid, out var mapGrid))
            return;

        if (drillGrid == gridUid)
            return;

        _cache.Clear();
        _lookup.GetEntitiesInRange(coordinates.MapId, coordinates.Position, EntityLookupSystem.LookupEpsilon, _cache, LookupFlags.Static);
        var playSound = false;
        foreach (var ent in _cache)
        {
            if (_gatherQuery.TryComp(ent, out var gatherable))
            {
                _gatherable.Gather(ent, null, gatherable);
                playSound = true;
            }
        }

        if (_cache.Count <= 0) // ctrl+c ctrl+v с TileSystem::DeconstructTile, только не дропает плитку
        {
            var tileRef = _map.GetTileRef(gridUid, mapGrid, coordinates);
            var tileDef = (ContentTileDefinition) _tileDefinitionManager[tileRef.Tile.TypeId];

            if (tileDef.ID != TargetTileId)
                return; 

            var ev = new FloorTileAttemptEvent();
            RaiseLocalEvent(gridUid, ref ev);

            if (ev.Cancelled && tileDef.ID == "Plating")
                return;

            // Destroy any decals on the tile
            var decals = _decal.GetDecalsInRange(gridUid, coordinates.Position, 0.5f);
            foreach (var (id, _) in decals)
            {
                _decal.RemoveDecal(tileRef.GridUid, id);
            }

            _map.SetTile(gridUid, mapGrid, tileRef.GridIndices, Tile.Empty);
            playSound = true;
        }

        if (playSound && sound != null)
        {
            _audio.PlayPvs(sound, drill);
        }
    }
}
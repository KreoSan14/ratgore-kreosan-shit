using Content.Server.Explosion.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Projectiles;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

namespace Content.Server._Crescent.ProximityFuse;

public sealed class ProximityFuseSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ProximityFuseComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            comp.UpdateAccumulator += frameTime;
            if (comp.UpdateAccumulator < comp.UpdateInterval)
                continue;
            comp.UpdateAccumulator -= comp.UpdateInterval;

            if (!TryComp<ProjectileComponent>(uid, out var projectile) ||
                !TryComp<TransformComponent>(projectile.Shooter, out var shooterTransform))
                continue;

            if (comp.Safety > 0)
            {
                comp.Safety -= frameTime;
                // Safety only counts real time, not update interval – keep original behaviour
                if (comp.Safety > 0)
                    continue;
            }

            var projectileMapPos = _transform.ToMapCoordinates(xform.Coordinates);
            var shooterGrid = shooterTransform.GridUid;

            var targetsInRange = _lookup.GetEntitiesInRange<ProximityFuseTargetComponent>(projectileMapPos, comp.MaxRange);
            var seenTargets = new HashSet<EntityUid>();

            foreach (var targetUid in targetsInRange)
            {
                if (!TryComp<TransformComponent>(targetUid, out var targetXform))
                    continue;

                // Cache target map position (once per target)
                var targetMapPos = _transform.ToMapCoordinates(targetXform.Coordinates);
                var distance = Vector2.Distance(targetMapPos.Position, projectileMapPos.Position);

                // Skip targets on the shooter's grid (friendly fire protection)
                if (shooterGrid == targetXform.GridUid)
                    continue;

                seenTargets.Add(targetUid);

                // Update or add target tracking info
                if (comp.Targets.TryGetValue(targetUid, out var targetInfo))
                {
                    targetInfo.LastDistance = targetInfo.Distance;
                    targetInfo.Distance = distance;

                    // Detonate if moving away (increasing distance)
                    if (targetInfo.Distance > targetInfo.LastDistance)
                        Detonate(uid);
                }
                else
                {
                    comp.Targets[targetUid] = new Target
                    {
                        Distance = distance,
                        LastDistance = distance
                    };
                }
            }

            // (Using ToList to avoid modifying dictionary during enumeration)
            foreach (var trackedUid in comp.Targets.Keys.ToList())
            {
                if (!seenTargets.Contains(trackedUid))
                    comp.Targets.Remove(trackedUid);
            }
        }
    }

    /// <summary>
    /// Explodes the entity if it has an explosive component, otherwise deletes it.
    /// </summary>
    public void Detonate(EntityUid uid)
    {
        if (TryComp<ExplosiveComponent>(uid, out var explosiveComp))
            _entMan.System<ExplosionSystem>().TriggerExplosive(uid);
        else
            _entMan.DeleteEntity(uid);
    }
}
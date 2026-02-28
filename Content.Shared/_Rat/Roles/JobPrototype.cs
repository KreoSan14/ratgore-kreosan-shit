using Content.Shared._Rat.Ranks;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using System.Collections.Generic;

namespace Content.Shared.Roles;

public sealed partial class JobPrototype
{
    [DataField]
    public readonly Dictionary<ProtoId<RankPrototype>, HashSet<JobRequirement>?>? Ranks;
}
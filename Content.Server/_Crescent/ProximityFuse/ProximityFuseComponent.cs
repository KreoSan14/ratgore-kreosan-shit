[RegisterComponent]
public sealed partial class ProximityFuseComponent : Component
{
    [DataField]
    public float MaxRange = 10f;

    [DataField]
    public float Safety = 0.5f;

    /// <summary>
    /// Tracks targets that are currently inside the fuse range.
    /// Key = target entity, Value = distance tracking data.
    /// </summary>
    [DataField]
    public Dictionary<EntityUid, Target> Targets = new();

    /// <summary>
    /// Accumulator for rate‑limiting updates (0.1s interval = 10 Hz).
    /// </summary>
    [DataField]
    public float UpdateAccumulator = 0f;

    /// <summary>
    /// Minimum time between proximity checks.
    /// </summary>
    [DataField]
    public float UpdateInterval = 0.1f;
}

/// <summary>
/// Stores distance information for a single tracked target.
/// </summary>
public class Target
{
    public float Distance { get; set; }
    public float LastDistance { get; set; }
}
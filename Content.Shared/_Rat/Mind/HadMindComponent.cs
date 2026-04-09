namespace Content.Shared._Rat.Mind;

/// <summary>
///     Marks an entity that has had a mind at some point (was player-controlled).
///     This component is permanent and does not get removed even after the mind is gone.
/// </summary>
[RegisterComponent]
public sealed partial class HadMindComponent : Component
{
}

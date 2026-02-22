using System.ComponentModel.DataAnnotations;
using Content.Client.Gameplay;
using Content.Shared._Crescent.SpaceBiomes;
using Content.Shared.Audio;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Random;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Timer = Robust.Shared.Timing.Timer;
using Robust.Shared.Utility;
using Content.Client.CombatMode;
using Content.Shared.CombatMode;
using System.IO;
using Robust.Shared.Toolshed.Commands.Values;
using Content.Shared.Preferences;
using Content.Client.Lobby;
using System.Diagnostics;
using System.Threading;
using Robust.Shared.Timing;
using Content.Shared._Crescent.HullrotFaction;

namespace Content.Client.Audio;

/// <summary>
/// This handles playing ambient music over time, and combat music per faction.
/// </summary>
public sealed partial class ContentAudioSystem
{
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IStateManager _state = default!;
    [Dependency] private readonly RulesSystem _rules = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly CombatModeSystem _combatModeSystem = default!;
    [Dependency] private readonly IPrototypeManager _protMan = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IClientPreferencesManager _prefsManager = default!;

    // Options menu
    private static float _volumeSliderAmbient;
    private static float _volumeSliderCombat;
    private static bool _combatMusicToggle;

    // Music state
    private EntityUid? _ambientMusicStream;
    private AmbientMusicPrototype? _musicProto;
    private SpaceBiomePrototype? _lastBiome;
    private TimeSpan _timeUntilNextAmbientTrack = TimeSpan.FromSeconds(10);
    private Dictionary<string, AmbientMusicPrototype>? _musicTracks; // Changed to Dictionary for O(1) lookups
    private float _ambientMusicFadeInTime = 10f;
    private float _combatMusicFadeInTime = 2f;
    private TimeSpan _combatStartUpTime = TimeSpan.FromSeconds(3.0);
    private TimeSpan _combatWindDownTime = TimeSpan.FromSeconds(30.0);
    private bool _lastCombatState = false;
    private bool _validStationMusic = false;
    private string _lastStationMusic = "";
    private bool _isCombatMusicPlaying = false;

    // Cached prototypes for faster access
    private AmbientMusicPrototype? _cachedDefaultMusic;
    private AmbientMusicPrototype? _cachedCombatDefaultMusic;
    private SpaceBiomePrototype? _cachedDefaultBiome;

    private CancellationTokenSource _combatMusicCancelToken = new();
    private CancellationTokenSource _ambientMusicCancelToken = new();

    private ISawmill _sawmill = default!;

    private void InitializeAmbientMusic()
    {
        SubscribeNetworkEvent<SpaceBiomeSwapMessage>(OnBiomeChange);
        SubscribeNetworkEvent<NewVesselEnteredMessage>(OnVesselChange);
        SubscribeLocalEvent<ToggleCombatActionEvent>(OnCombatModeToggle);

        Subs.CVar(_configManager, CCVars.AmbientMusicVolume, AmbienceCVarChanged, true);
        Subs.CVar(_configManager, CCVars.CombatMusicVolume, CombatCVarChanged, true);
        Subs.CVar(_configManager, CCVars.CombatMusicEnabled, CombatToggleChanged, true);
        _sawmill = IoCManager.Resolve<ILogManager>().GetSawmill("audio.ambience");

        // Cache default prototypes
        _cachedDefaultMusic = _proto.Index<AmbientMusicPrototype>("default");
        _cachedDefaultBiome = _proto.Index<SpaceBiomePrototype>("default");
        _cachedCombatDefaultMusic = _proto.Index<AmbientMusicPrototype>("combatmodedefault");

        // Setup tracks to pull from. Runs once.
        RefreshMusicTracks();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnProtoReload);
        _state.OnStateChanged += OnStateChange;
        SubscribeNetworkEvent<RoundEndMessageEvent>(OnRoundEndMessage);
    }

    /// <summary>
    /// Helper method to reset a CancellationTokenSource
    /// </summary>
    private void ResetCancellationToken(ref CancellationTokenSource tokenSource)
    {
        tokenSource.Cancel();
        tokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Helper method to get or initialize the last biome
    /// </summary>
    private bool TryGetLastBiome(out SpaceBiomePrototype? biome)
    {
        biome = _lastBiome;
        
        if (biome != null)
            return true;

        if (_player.LocalSession?.AttachedEntity != null &&
            _entMan.TryGetComponent<SpaceBiomeTrackerComponent>(_player.LocalSession.AttachedEntity, out var comp) &&
            comp.Biome != null)
        {
            biome = _proto.Index<SpaceBiomePrototype>(comp.Biome);
            _lastBiome = biome;
            return true;
        }

        biome = _cachedDefaultBiome;
        _lastBiome = biome;
        return biome != null;
    }

    /// <summary>
    /// Consolidated music playback method
    /// </summary>
    private void PlayAmbientMusicById(string musicId, bool setupTimer = true, bool isCombatMusic = false)
    {
        // Try to get the music prototype
        if (!_musicTracks!.TryGetValue(musicId, out var musicProto))
        {
            // Fall back to default
            musicProto = isCombatMusic ? _cachedCombatDefaultMusic : _cachedDefaultMusic;
            
            if (musicProto == null)
            {
                _sawmill.Error($"Failed to find music prototype for {musicId} and no default available");
                return;
            }
        }

        _musicProto = musicProto;

        try
        {
            var soundcol = _proto.Index<SoundCollectionPrototype>(musicProto.ID);
            var path = _random.Pick(soundcol.PickFiles).ToString();
            
            var volume = musicProto.Sound.Params.Volume;
            var fadeTime = isCombatMusic ? _combatMusicFadeInTime : _ambientMusicFadeInTime;
            
            PlayMusicTrack(path, volume, fadeTime, isCombatMusic);

            if (setupTimer)
            {
                ResetCancellationToken(ref _ambientMusicCancelToken);
                Timer.Spawn(
                    _audio.GetAudioLength(path) + _timeUntilNextAmbientTrack,
                    () => ReplayAmbientMusic(),
                    _ambientMusicCancelToken.Token);
            }
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to play music {musicId}: {ex.Message}");
        }
    }

    private void ReplayAmbientMusic()
    {
        if (_musicProto == null)
        {
            PlayAmbientMusicById("default", true, false);
            _lastBiome = _cachedDefaultBiome;
            return;
        }

        PlayAmbientMusicById(_musicProto.ID, true, _isCombatMusicPlaying);
    }

    private void OnBiomeChange(SpaceBiomeSwapMessage ev)
    {
        var biome = _protMan.Index<SpaceBiomePrototype>(ev.Biome);
        _lastBiome = biome;

        if (_combatModeSystem.IsInCombatMode() || _validStationMusic)
            return;

        FadeOut(_ambientMusicStream);
        
        if (_musicTracks == null)
            return;

        PlayAmbientMusicById(biome.ID, true, false);
    }

    private void OnVesselChange(NewVesselEnteredMessage ev)
    {
        _sawmill.Debug($"went to ship {ev.Name}");

        if (string.IsNullOrEmpty(ev.AmbientMusicPrototype))
        {
            _validStationMusic = false;
            _sawmill.Debug("NO MUSIC FOUND FOR SHIP");
            return;
        }
        
        _validStationMusic = true;
        _lastStationMusic = ev.AmbientMusicPrototype;
        _sawmill.Debug($"MUSIC FOUND FOR SHIP! {ev.AmbientMusicPrototype}");

        if (_combatModeSystem.IsInCombatMode())
            return;

        FadeOut(_ambientMusicStream);
        
        if (_musicTracks == null)
            return;

        PlayAmbientMusicById(ev.AmbientMusicPrototype, true, false);
    }

    private void OnCombatModeToggle(ToggleCombatActionEvent ev)
    {
        if (!_combatMusicToggle || !_timing.IsFirstTimePredicted)
            return;

        ResetCancellationToken(ref _combatMusicCancelToken);

        var currentCombatState = _combatModeSystem.IsInCombatMode();
        _sawmill.Debug($"ToggleCombatActionEvent performer: {ev.Performer}");

        if (!TryComp<HullrotFactionComponent>(ev.Performer, out var factionComp))
        {
            _sawmill.Debug("NO HULLROT FACTION COMPONENT FOUND! YOU NEED TO ADD A FACTION COMPONENT TO THIS ROLE!");
            return;
        }

        var delay = currentCombatState ? _combatStartUpTime : _combatWindDownTime;
        Timer.Spawn(delay, () => SwitchCombatMusic(factionComp.Faction), _combatMusicCancelToken.Token);
    }

    private void SwitchCombatMusic(string faction)
    {
        ResetCancellationToken(ref _ambientMusicCancelToken);

        var currentCombatState = _combatModeSystem.IsInCombatMode();

        if (_lastCombatState == currentCombatState)
            return;

        _lastCombatState = currentCombatState;

        FadeOut(_ambientMusicStream);

        if (currentCombatState)
        {
            // Combat mode ON - play faction combat music
            var combatMusicId = $"combatmode{faction}";
            PlayAmbientMusicById(combatMusicId, false, true);
        }
        else
        {
            // Combat mode OFF - resume ambient music
            if (!TryGetLastBiome(out _))
                return;

            var musicId = _validStationMusic ? _lastStationMusic : _lastBiome!.ID;
            PlayAmbientMusicById(musicId, true, false);
        }
    }

    private void CombatToggleChanged(bool enabled)
    {
        _combatMusicToggle = enabled;

        if (enabled)
            return;

        // Combat music disabled - revert to ambient
        ResetCancellationToken(ref _combatMusicCancelToken);
        ResetCancellationToken(ref _ambientMusicCancelToken);

        var currentCombatState = _combatModeSystem.IsInCombatMode();

        if (_lastCombatState == currentCombatState)
            return;

        _lastCombatState = currentCombatState;

        FadeOut(_ambientMusicStream);

        if (!TryGetLastBiome(out _))
            return;

        var musicId = _validStationMusic ? _lastStationMusic : _lastBiome!.ID;
        PlayAmbientMusicById(musicId, true, false);
    }

    private void PlayMusicTrack(string path, float volume, float fadein, bool combatMode)
    {
        _isCombatMusicPlaying = combatMode;
        _sawmill.Debug($"NOW PLAYING: {path} | COMBAT MODE: {_isCombatMusicPlaying}");

        volume += combatMode ? _volumeSliderCombat : _volumeSliderAmbient;

        var strim = _audio.PlayGlobal(
            path,
            Filter.Local(),
            false,
            AudioParams.Default.WithVolume(volume));

        if (strim == null)
            return;

        _ambientMusicStream = strim.Value.Entity;

        if (fadein != 0)
            FadeIn(_ambientMusicStream, strim.Value.Component, fadein);
    }

    private void RefreshMusicTracks()
    {
        var tracks = new Dictionary<string, AmbientMusicPrototype>();
        
        foreach (var ambience in _proto.EnumeratePrototypes<AmbientMusicPrototype>())
        {
            _sawmill.Debug($"logged ambient sound {ambience.ID}");
            tracks[ambience.ID] = ambience;
        }

        if (tracks.Count == 0)
        {
            _sawmill.Debug("NO MUSIC FOUND, SOMETHING IS WRONG!");
        }

        _musicTracks = tracks;
    }

    private void AmbienceCVarChanged(float obj)
    {
        _volumeSliderAmbient = SharedAudioSystem.GainToVolume(obj);

        if (_ambientMusicStream != null && _musicProto != null && !_isCombatMusicPlaying)
        {
            _audio.SetVolume(_ambientMusicStream, _musicProto.Sound.Params.Volume + _volumeSliderAmbient);
        }
    }

    private void CombatCVarChanged(float obj)
    {
        _volumeSliderCombat = SharedAudioSystem.GainToVolume(obj);

        if (_ambientMusicStream != null && _musicProto != null && _isCombatMusicPlaying)
        {
            _audio.SetVolume(_ambientMusicStream, _musicProto.Sound.Params.Volume + _volumeSliderCombat);
        }
    }

    private void ShutdownAmbientMusic()
    {
        _state.OnStateChanged -= OnStateChange;
        _ambientMusicStream = _audio.Stop(_ambientMusicStream);
    }

    private void OnProtoReload(PrototypesReloadedEventArgs obj)
    {
        if (obj.WasModified<AmbientMusicPrototype>())
            RefreshMusicTracks();
    }

    private void OnStateChange(StateChangedEventArgs obj)
    {
        if (obj.NewState is not GameplayState)
            DisableAmbientMusic();
    }

    private void OnRoundEndMessage(RoundEndMessageEvent ev)
    {
        if (_ambientMusicStream == null)
        {
            _sawmill.Debug("AMBIENT MUSIC STREAM WAS NULL? FROM OnRoundEndMessage()");
            return;
        }
        _ambientMusicStream = _audio.Stop(_ambientMusicStream);
    }

    public void DisableAmbientMusic()
    {
        if (_ambientMusicStream == null)
        {
            _sawmill.Debug("AMBIENT MUSIC STREAM WAS NULL? FROM DisableAmbientMusic()");
            return;
        }
        FadeOut(_ambientMusicStream);
        _ambientMusicStream = null;
    }
}
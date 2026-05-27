using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPet.Core.Pets;

namespace HyperPet.App.ViewModels;

/// <summary>
/// One row in the Settings dialog's "State" tab — pairs a sprite animation
/// state with its current playback fps and the original (pet.json) value so
/// the per-row Reset and the dialog-wide Default action can restore it.
/// </summary>
public sealed class StateSpeedRowViewModel : INotifyPropertyChanged
{
    private int _fps;
    private PlayMode _playMode;

    public StateSpeedRowViewModel(
        string stateName,
        int originalFps,
        int currentFps,
        PlayMode originalPlayMode,
        PlayMode currentPlayMode)
    {
        StateName = stateName;
        DisplayName = HumanizeStateName(stateName);
        OriginalFps = originalFps;
        OriginalPlayMode = originalPlayMode;
        _fps = currentFps;
        _playMode = currentPlayMode;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raw state key from pet.json (e.g. "runRight").</summary>
    public string StateName { get; }

    /// <summary>Human-readable label (e.g. "Run Right").</summary>
    public string DisplayName { get; }

    /// <summary>Original fps from pet.json — target value for Reset.</summary>
    public int OriginalFps { get; }

    /// <summary>Original play mode from pet.json — target value for Reset.</summary>
    public PlayMode OriginalPlayMode { get; }

    /// <summary>
    /// Current fps shown in the textbox. Clamped to 1..60 by the caller on
    /// commit; the VM allows transient out-of-range values during typing so
    /// edits aren't yanked.
    /// </summary>
    public int Fps
    {
        get => _fps;
        set => SetField(ref _fps, value);
    }

    /// <summary>Current frame iteration mode. Bound to the per-row ComboBox.</summary>
    public PlayMode PlayMode
    {
        get => _playMode;
        set => SetField(ref _playMode, value);
    }

    /// <summary>True when either Fps or PlayMode differs from the pet.json default.</summary>
    public bool IsOverridden => _fps != OriginalFps || _playMode != OriginalPlayMode;

    /// <summary>Sets Fps and PlayMode back to the pet.json defaults.</summary>
    public void ResetToDefault()
    {
        Fps = OriginalFps;
        PlayMode = OriginalPlayMode;
    }

    /// <summary>
    /// Splits a camelCase state key into a space-separated, title-cased label.
    /// Example: "runRight" -> "Run Right", "idle" -> "Idle".
    /// </summary>
    private static string HumanizeStateName(string stateName)
    {
        if (string.IsNullOrEmpty(stateName))
        {
            return stateName;
        }

        var builder = new StringBuilder(stateName.Length + 4);
        for (int i = 0; i < stateName.Length; i++)
        {
            char c = stateName[i];
            if (i == 0)
            {
                builder.Append(char.ToUpperInvariant(c));
                continue;
            }

            if (char.IsUpper(c) && !char.IsUpper(stateName[i - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
        if (propertyName == nameof(Fps) || propertyName == nameof(PlayMode))
        {
            OnPropertyChanged(nameof(IsOverridden));
        }
    }

    private void OnPropertyChanged(string? propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

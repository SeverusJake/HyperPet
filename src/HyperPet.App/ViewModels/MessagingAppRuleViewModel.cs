using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HyperPet.Core.Notifications;

namespace HyperPet.App.ViewModels;

public sealed class MessagingAppRuleViewModel : INotifyPropertyChanged
{
    private string _displayName;
    private bool _enabled;
    private ObservableCollection<string> _patterns;

    public MessagingAppRuleViewModel(MessagingAppRule rule)
    {
        _displayName = rule.DisplayName;
        _enabled = rule.Enabled;
        _patterns = new ObservableCollection<string>(rule.MatchPatterns ?? new List<string>());
        _patterns.CollectionChanged += (_, _) => OnPropertyChanged(nameof(PatternsDisplay));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayName
    {
        get => _displayName;
        set => SetField(ref _displayName, value);
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetField(ref _enabled, value);
    }

    public ObservableCollection<string> Patterns => _patterns;

    public string PatternsDisplay => string.Join(", ", _patterns);

    public MessagingAppRule ToModel()
    {
        return new MessagingAppRule
        {
            DisplayName = DisplayName,
            Enabled = Enabled,
            MatchPatterns = Patterns.ToList()
        };
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string? propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

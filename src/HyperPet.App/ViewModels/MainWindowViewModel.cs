using System.ComponentModel;
using System.Runtime.CompilerServices;
using HyperPet.Core.Pet;

namespace HyperPet.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private PetState _petState = PetState.Idle;
    private PetAlert? _currentAlert;
    private bool _isBubbleVisible;
    private bool _alertsPaused;

    public event PropertyChangedEventHandler? PropertyChanged;

    public PetState PetState
    {
        get => _petState;
        set => SetField(ref _petState, value);
    }

    public PetAlert? CurrentAlert
    {
        get => _currentAlert;
        set
        {
            if (SetField(ref _currentAlert, value))
            {
                IsBubbleVisible = value is not null;
            }
        }
    }

    public bool IsBubbleVisible
    {
        get => _isBubbleVisible;
        set => SetField(ref _isBubbleVisible, value);
    }

    public bool AlertsPaused
    {
        get => _alertsPaused;
        set => SetField(ref _alertsPaused, value);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

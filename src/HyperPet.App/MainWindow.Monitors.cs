using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using HyperPet.App.Views;
using HyperPet.Core.Settings;
using Microsoft.Win32;

namespace HyperPet.App;

public partial class MainWindow
{
    private readonly Func<IReadOnlyList<DesktopMonitor>> _monitorProvider;
    private IReadOnlyList<DesktopMonitor>? _monitors;
    private string _appliedMonitorId;
    private bool _monitorTracking;

    private WorkArea GetWorkArea(double left, double top)
        => GetWorkAreaForPoint(left + GetWindowWidth() / 2, top + GetWindowHeight() / 2);

    internal WorkArea GetWorkAreaForPoint(double left, double top)
    {
        _monitors ??= _monitorProvider();
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        DesktopMonitor? monitor = DesktopMonitors.Resolve(_monitors, _settings.PreferredMonitorId,
            left * dpi.DpiScaleX, top * dpi.DpiScaleY);
        if (monitor is not null) { return DesktopMonitors.ToDips(monitor, dpi.DpiScaleX, dpi.DpiScaleY); }
        Rect primary = SystemParameters.WorkArea;
        return new WorkArea(primary.Left, primary.Top, primary.Right, primary.Bottom);
    }

    public void ClampToWorkArea()
    {
        double left = double.IsFinite(Left) ? Left : 80;
        double top = double.IsFinite(Top) ? Top : 80;
        WorkArea workArea = GetWorkArea(left, top);
        Left = Math.Clamp(left, workArea.Left, Math.Max(workArea.Left, workArea.Right - GetWindowWidth()));
        Top = Math.Clamp(top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - GetWindowHeight()));
    }

    private void RestorePetPosition()
    {
        _monitors = _monitorProvider();
        var defaults = HyperPetSettings.CreateDefault();
        double left = _settings.RememberPetPosition && double.IsFinite(_settings.PetLeft) ? _settings.PetLeft : defaults.PetLeft;
        double top = _settings.RememberPetPosition && double.IsFinite(_settings.PetTop) ? _settings.PetTop : defaults.PetTop;
        Placement placement = PetPlacement.Compute(_settings, GetWindowWidth(), GetWindowHeight(), GetWorkArea(left, top));
        Left = placement.Left;
        Top = placement.Top;
    }

    private void ApplyMonitorPreference()
    {
        if (!string.Equals(_appliedMonitorId, _settings.PreferredMonitorId, StringComparison.OrdinalIgnoreCase))
        {
            _monitors = _monitorProvider();
            _appliedMonitorId = _settings.PreferredMonitorId;
            _roamController = null;
            _summoned = false;
            _summonWalking = false;
        }
        ClampToWorkArea();
    }

    internal void RefreshMonitorPlacement()
    {
        if (_tuckingAway) { return; }
        _monitors = _monitorProvider();
        ClampToWorkArea();
        _roamController = null;
        _summoned = false;
        _summonWalking = false;
        StartBehaviorMode();
    }

    private void StartMonitorTracking()
    {
        if (_monitorTracking) { return; }
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        DpiChanged += OnPetDpiChanged;
        _monitorTracking = true;
    }

    private void StopMonitorTracking()
    {
        if (!_monitorTracking) { return; }
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        DpiChanged -= OnPetDpiChanged;
        _monitorTracking = false;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => ScheduleMonitorRefresh();
    private void OnPetDpiChanged(object sender, DpiChangedEventArgs e) => ScheduleMonitorRefresh();

    private void ScheduleMonitorRefresh()
    {
        if (Dispatcher.HasShutdownStarted) { return; }
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (_monitorTracking && IsLoaded) { RefreshMonitorPlacement(); }
        }));
    }
}

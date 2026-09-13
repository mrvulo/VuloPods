using System.ComponentModel;
using System.Runtime.CompilerServices;
using VuloPods.Core.Models;

namespace VuloPods.App;

public sealed class PopupViewModel : INotifyPropertyChanged
{
    private string _modelName = "Kein Gerät gefunden";
    private string _leftText = "–";
    private string _rightText = "–";
    private string _caseText = "–";
    private int _leftPercent;
    private int _rightPercent;
    private int _casePercent;
    private bool _leftCharging;
    private bool _rightCharging;
    private bool _caseCharging;
    private bool _leftInEar;
    private bool _rightInEar;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ModelName { get => _modelName; private set => Set(ref _modelName, value); }
    public string LeftText { get => _leftText; private set => Set(ref _leftText, value); }
    public string RightText { get => _rightText; private set => Set(ref _rightText, value); }
    public string CaseText { get => _caseText; private set => Set(ref _caseText, value); }
    public int LeftPercent { get => _leftPercent; private set => Set(ref _leftPercent, value); }
    public int RightPercent { get => _rightPercent; private set => Set(ref _rightPercent, value); }
    public int CasePercent { get => _casePercent; private set => Set(ref _casePercent, value); }
    public bool LeftCharging { get => _leftCharging; private set => Set(ref _leftCharging, value); }
    public bool RightCharging { get => _rightCharging; private set => Set(ref _rightCharging, value); }
    public bool CaseCharging { get => _caseCharging; private set => Set(ref _caseCharging, value); }
    public bool LeftInEar { get => _leftInEar; private set => Set(ref _leftInEar, value); }
    public bool RightInEar { get => _rightInEar; private set => Set(ref _rightInEar, value); }

    public void Update(AirPodsAdvertisement? adv)
    {
        if (adv is null)
        {
            ModelName = "Kein Gerät gefunden";
            LeftText = RightText = CaseText = "–";
            LeftPercent = RightPercent = CasePercent = 0;
            LeftCharging = RightCharging = CaseCharging = false;
            LeftInEar = RightInEar = false;
            return;
        }

        ModelName = AirPodsModels.DisplayName(adv.Model);
        LeftText = StatusText.Percent(adv.LeftBattery);
        RightText = StatusText.Percent(adv.RightBattery);
        CaseText = StatusText.Percent(adv.CaseBattery);
        LeftPercent = adv.LeftBattery ?? 0;
        RightPercent = adv.RightBattery ?? 0;
        CasePercent = adv.CaseBattery ?? 0;
        LeftCharging = adv.LeftCharging;
        RightCharging = adv.RightCharging;
        CaseCharging = adv.CaseCharging;
        LeftInEar = adv.LeftInEar;
        RightInEar = adv.RightInEar;
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

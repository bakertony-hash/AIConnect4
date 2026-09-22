using System.ComponentModel;
using System.Runtime.CompilerServices;
using AIConnect4.App.OpenRouter;

namespace AIConnect4.App.ViewModels;

/// <summary>One dropdown row. Curated profiles plus a Custom sentinel that reveals the model-id box.</summary>
public sealed class CatalogOption
{
    private CatalogOption(string label, ModelProfile? profile, bool isCustom)
    {
        Label = label;
        Profile = profile;
        IsCustom = isCustom;
    }

    public string Label { get; }

    public ModelProfile? Profile { get; }

    public bool IsCustom { get; }

    public static CatalogOption From(ModelProfile profile) => new(profile.DisplayName, profile, isCustom: false);

    public static CatalogOption Custom { get; } = new("Custom…", profile: null, isCustom: true);

    public override string ToString() => Label;
}

public sealed class SideViewModel : INotifyPropertyChanged
{
    private CatalogOption _selectedOption;
    private string _customModelId = string.Empty;
    private Effort _effort = Effort.Medium;
    private Speed _speed = Speed.Default;
    private int _wins;
    private string _thinkingText = string.Empty;
    private string _lastDecisionText = "—";
    private string _totalDecisionText = "—";
    private string _lastAnswerText = "—";
    private bool _settingsEnabled = true;

    public SideViewModel(string title, ModelProfile defaultProfile)
    {
        Title = title;
        Options = [.. ModelCatalog.Curated.Select(CatalogOption.From), CatalogOption.Custom];
        _selectedOption = Options.First(option => option.Profile == defaultProfile);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; }

    public IReadOnlyList<CatalogOption> Options { get; }

    public CatalogOption SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (Set(ref _selectedOption, value))
            {
                Notify(nameof(CustomIdVisible));
                Notify(nameof(EffortEnabled));
                Notify(nameof(SpeedEnabled));
            }
        }
    }

    public string CustomModelId
    {
        get => _customModelId;
        set => Set(ref _customModelId, value);
    }

    public bool CustomIdVisible => SelectedOption.IsCustom;

    public Effort Effort
    {
        get => _effort;
        set => Set(ref _effort, value);
    }

    public Speed Speed
    {
        get => _speed;
        set => Set(ref _speed, value);
    }

    public IReadOnlyList<Effort> EffortChoices { get; } = Enum.GetValues<Effort>();

    public IReadOnlyList<Speed> SpeedChoices { get; } = Enum.GetValues<Speed>();

    public bool EffortEnabled => SettingsEnabled && ResolveProfile() is ModelProfile.Chat { SupportsEffort: true };

    public bool SpeedEnabled => SettingsEnabled && ResolveProfile() is ModelProfile.Chat { SupportsSpeed: true };

    public bool SettingsEnabled
    {
        get => _settingsEnabled;
        set
        {
            if (Set(ref _settingsEnabled, value))
            {
                Notify(nameof(EffortEnabled));
                Notify(nameof(SpeedEnabled));
            }
        }
    }

    public int Wins
    {
        get => _wins;
        set => Set(ref _wins, value);
    }

    public string ThinkingText
    {
        get => _thinkingText;
        set => Set(ref _thinkingText, value);
    }

    public string LastDecisionText
    {
        get => _lastDecisionText;
        set => Set(ref _lastDecisionText, value);
    }

    public string TotalDecisionText
    {
        get => _totalDecisionText;
        set => Set(ref _totalDecisionText, value);
    }

    public string LastAnswerText
    {
        get => _lastAnswerText;
        set => Set(ref _lastAnswerText, value);
    }

    public ModelProfile ResolveProfile()
    {
        if (SelectedOption.IsCustom)
        {
            var id = CustomModelId.Trim();
            if (string.IsNullOrEmpty(id))
            {
                throw new InvalidOperationException($"{Title} needs a custom model id.");
            }

            return ModelCatalog.Custom(id);
        }

        return SelectedOption.Profile ?? throw new InvalidOperationException($"{Title} has no model.");
    }

    public ChatTuning ResolveTuning(ModelProfile profile) =>
        profile is ModelProfile.Chat
            ? new ChatTuning(profile is ModelProfile.Chat { SupportsEffort: true } ? Effort : null, Speed)
            : ChatTuning.Omit;

    public void ResetSeriesDisplay()
    {
        Wins = 0;
        ThinkingText = string.Empty;
        LastDecisionText = "—";
        TotalDecisionText = "—";
        LastAnswerText = "—";
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        Notify(name);
        return true;
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AIConnect4.App.ViewModels;

/// <summary>One centre-column line: model display name and cumulative session decision time.</summary>
public sealed class ModelDecisionTotalRow : INotifyPropertyChanged
{
    private string _displayName;
    private string _totalText;
    private string _lineText;

    public ModelDecisionTotalRow(string modelId, string displayName, string totalText)
    {
        ModelId = modelId;
        _displayName = displayName;
        _totalText = totalText;
        _lineText = FormatLine(displayName, totalText);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ModelId { get; }

    public string DisplayName
    {
        get => _displayName;
        private set => Set(ref _displayName, value);
    }

    public string TotalText
    {
        get => _totalText;
        private set => Set(ref _totalText, value);
    }

    public string LineText
    {
        get => _lineText;
        private set => Set(ref _lineText, value);
    }

    public void Update(string displayName, string totalText)
    {
        DisplayName = displayName;
        TotalText = totalText;
        LineText = FormatLine(displayName, totalText);
    }

    private static string FormatLine(string displayName, string totalText) => $"{displayName}: {totalText}";

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}

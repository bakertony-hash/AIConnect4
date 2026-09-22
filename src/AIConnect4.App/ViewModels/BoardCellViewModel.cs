using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using AIConnect4.Core;

namespace AIConnect4.App.ViewModels;

public sealed class BoardCellViewModel : INotifyPropertyChanged
{
    private IBrush _fill = Brushes.Transparent;
    private IBrush _stroke = Brushes.DimGray;
    private double _strokeThickness = 1;

    public event PropertyChangedEventHandler? PropertyChanged;

    public IBrush Fill
    {
        get => _fill;
        set => Set(ref _fill, value);
    }

    public IBrush Stroke
    {
        get => _stroke;
        set => Set(ref _stroke, value);
    }

    public double StrokeThickness
    {
        get => _strokeThickness;
        set => Set(ref _strokeThickness, value);
    }

    public void Apply(Player? disc, bool lastMove, bool winning)
    {
        Fill = disc switch
        {
            Player.Red => Brushes.Crimson,
            Player.Yellow => Brushes.Gold,
            _ => Brushes.WhiteSmoke,
        };
        Stroke = winning ? Brushes.LimeGreen : lastMove ? Brushes.DodgerBlue : Brushes.DimGray;
        StrokeThickness = winning || lastMove ? 3 : 1;
    }

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

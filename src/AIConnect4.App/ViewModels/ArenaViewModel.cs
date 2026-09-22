using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Threading;
using AIConnect4.App.Composition;
using AIConnect4.App.OpenRouter;
using AIConnect4.Core;

namespace AIConnect4.App.ViewModels;

/// <summary>
/// Session owner for the three-column arena. Draft settings live here until Play; then a one-shot
/// <see cref="SeriesRunner"/> owns score and status. <see cref="Changed"/> events marshal onto the UI thread.
/// </summary>
public sealed class ArenaViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly TimeSpan WatchPause = TimeSpan.FromMilliseconds(400);

    private readonly HttpClient? _http;
    private readonly OpenRouterClient? _client;
    private readonly DispatcherTimer _thinkingTimer;
    private SeriesRunner? _runner;
    private CancellationTokenSource? _playCts;
    private int _gameCount = 1;
    private int _draws;
    private string _statusText;
    private string _keyHint;
    private bool _settingsEnabled = true;
    private bool _disposed;

    public ArenaViewModel()
    {
        var apiKey = OpenRouterHttp.ApiKeyFromEnvironment();
        HasApiKey = apiKey is not null;
        _keyHint = HasApiKey
            ? string.Empty
            : $"Set {OpenRouterHttp.ApiKeyVariable} before Play.";
        _statusText = HasApiKey ? "Ready" : "Ready (API key missing)";

        if (apiKey is not null)
        {
            _http = OpenRouterHttp.Create(apiKey);
            _client = new OpenRouterClient(_http);
        }

        Red = new SideViewModel("Red", ModelCatalog.DefaultRed);
        Yellow = new SideViewModel("Yellow", ModelCatalog.DefaultYellow);
        Cells = new ObservableCollection<BoardCellViewModel>(
            Enumerable.Range(0, Board.Rows * Board.Columns).Select(_ => new BoardCellViewModel()));
        foreach (var cell in Cells)
        {
            cell.Apply(disc: null, lastMove: false, winning: false);
        }

        PlayCommand = new RelayCommand(Play, () => CanPlay);
        PauseCommand = new RelayCommand(Pause, () => CanPause);
        ResumeCommand = new RelayCommand(Resume, () => CanResume);
        NewSeriesCommand = new RelayCommand(NewSeries, () => CanNewSeries);

        _thinkingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _thinkingTimer.Tick += (_, _) => RefreshThinking();
        _thinkingTimer.Start();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool HasApiKey { get; }

    public SideViewModel Red { get; }

    public SideViewModel Yellow { get; }

    public ObservableCollection<BoardCellViewModel> Cells { get; }

    public int GameCount
    {
        get => _gameCount;
        set
        {
            var clamped = Math.Max(1, value);
            if (Set(ref _gameCount, clamped))
            {
                RaiseCommands();
            }
        }
    }

    public int Draws
    {
        get => _draws;
        private set => Set(ref _draws, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    public string KeyHint
    {
        get => _keyHint;
        private set => Set(ref _keyHint, value);
    }

    public bool SettingsEnabled
    {
        get => _settingsEnabled;
        private set
        {
            if (Set(ref _settingsEnabled, value))
            {
                Red.SettingsEnabled = value;
                Yellow.SettingsEnabled = value;
            }
        }
    }

    public bool CanPlay =>
        HasApiKey
        && SettingsEnabled
        && _runner is null
        && !_disposed;

    public bool CanPause => _runner?.Status is SeriesStatus.Running;

    public bool CanResume => _runner?.Status is SeriesStatus.Paused;

    public bool CanNewSeries => _runner is not null || !SettingsEnabled;

    public ICommand PlayCommand { get; }

    public ICommand PauseCommand { get; }

    public ICommand ResumeCommand { get; }

    public ICommand NewSeriesCommand { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _thinkingTimer.Stop();
        CancelPlay();
        DetachRunner();
        _http?.Dispose();
        RaiseCommands();
    }

    private void Play()
    {
        if (!CanPlay || _client is null)
        {
            return;
        }

        ModelProfile redProfile;
        ModelProfile yellowProfile;
        try
        {
            redProfile = Red.ResolveProfile();
            yellowProfile = Yellow.ResolveProfile();
        }
        catch (InvalidOperationException ex)
        {
            StatusText = ex.Message;
            return;
        }

        var config = SeriesConfig.Of(GameCount);
        var red = MoveSources.Create(_client, redProfile, Red.ResolveTuning(redProfile));
        var yellow = MoveSources.Create(_client, yellowProfile, Yellow.ResolveTuning(yellowProfile));
        var pacing = new MatchPacing(TimeProvider.System, ct => Task.Delay(WatchPause, ct));

        DetachRunner();
        _runner = new SeriesRunner(red, yellow, config, pacing);
        _runner.Changed += OnRunnerChanged;
        SettingsEnabled = false;
        _playCts = new CancellationTokenSource();
        RaiseCommands();
        Mirror();

        var token = _playCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await _runner.PlayAsync(token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await Dispatcher.UIThread.InvokeAsync(() => StatusText = $"Series fault: {ex.Message}");
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RaiseCommands();
                    Mirror();
                });
            }
        }, token);
    }

    private void Pause() => _runner?.Pause();

    private void Resume() => _runner?.Resume();

    private void NewSeries()
    {
        CancelPlay();
        DetachRunner();
        Red.ResetSeriesDisplay();
        Yellow.ResetSeriesDisplay();
        Draws = 0;
        ClearBoard();
        SettingsEnabled = true;
        StatusText = HasApiKey ? "Ready" : "Ready (API key missing)";
        RaiseCommands();
    }

    private void OnRunnerChanged() => Dispatcher.UIThread.Post(Mirror);

    private void Mirror()
    {
        if (_runner is null)
        {
            return;
        }

        Red.Wins = _runner.Score.RedWins;
        Yellow.Wins = _runner.Score.YellowWins;
        Draws = _runner.Score.Draws;
        StatusText = FormatStatus(_runner);
        MirrorBoard(_runner.CurrentGame);
        MirrorSide(Red, Player.Red, _runner);
        MirrorSide(Yellow, Player.Yellow, _runner);
        RaiseCommands();
    }

    private void RefreshThinking()
    {
        if (_runner?.CurrentGame is not { } game || game.Thinking is null)
        {
            return;
        }

        var side = game.Thinking == Player.Red ? Red : Yellow;
        side.ThinkingText = FormatDuration(game.ElapsedThinking);
    }

    private static void MirrorSide(SideViewModel side, Player player, SeriesRunner series)
    {
        side.TotalDecisionText = FormatDuration(series.TotalDecisionTime(player));
        if (series.CurrentGame is { } game)
        {
            side.LastDecisionText = game.LastDecisionTime(player) is { } last
                ? FormatDuration(last)
                : "—";
            var move = game.Moves.LastOrDefault(m => m.Player == player);
            side.LastAnswerText = move is null ? "—" : $"Column {move.Column} ({move.Origin})";
            if (game.Thinking == player)
            {
                side.ThinkingText = FormatDuration(game.ElapsedThinking);
            }
            else if (game.Thinking is null)
            {
                side.ThinkingText = string.Empty;
            }
        }
        else
        {
            side.LastDecisionText = "—";
            side.LastAnswerText = "—";
            side.ThinkingText = string.Empty;
        }
    }

    private void MirrorBoard(GameRunner? game)
    {
        if (game is null)
        {
            ClearBoard();
            return;
        }

        var board = game.Board;
        var last = board.LastMove?.Position;
        var winning = board.Result is GameResult.Win win
            ? win.Line.Cells.ToHashSet()
            : null;

        for (var visualRow = 0; visualRow < Board.Rows; visualRow++)
        {
            var boardRow = Board.Rows - 1 - visualRow;
            for (var columnIndex = 0; columnIndex < Board.Columns; columnIndex++)
            {
                var column = Column.All[columnIndex];
                var position = new BoardPosition(boardRow, column);
                var index = visualRow * Board.Columns + columnIndex;
                var isLast = last == position;
                var isWin = winning?.Contains(position) == true;
                Cells[index].Apply(board.Cell(boardRow, column), isLast, isWin);
            }
        }
    }

    private void ClearBoard()
    {
        foreach (var cell in Cells)
        {
            cell.Apply(disc: null, lastMove: false, winning: false);
        }
    }

    private static string FormatStatus(SeriesRunner series) => series.Status switch
    {
        SeriesStatus.Ready => "Ready",
        SeriesStatus.Running running => FormatRunning(series, running.GameNumber, paused: false),
        SeriesStatus.Paused paused => FormatRunning(series, paused.GameNumber, paused: true),
        SeriesStatus.Finished => $"Series finished. Red {series.Score.RedWins}, Yellow {series.Score.YellowWins}, draws {series.Score.Draws}.",
        SeriesStatus.Aborted aborted => $"Series aborted on game {aborted.GameNumber}: {DescribeAbort(aborted.Reason)}",
        _ => throw new UnreachableException(),
    };

    private static string FormatRunning(SeriesRunner series, int gameNumber, bool paused)
    {
        var prefix = paused ? "Paused" : "Running";
        var game = series.CurrentGame;
        var turn = game is null
            ? string.Empty
            : game.Status switch
            {
                GameStatus.Finished finished => finished.Result switch
                {
                    GameResult.Win win => $"{win.Winner} wins.",
                    GameResult.Draw => "Draw.",
                    _ => throw new UnreachableException(),
                },
                GameStatus.Aborted aborted => DescribeAbort(aborted.Reason),
                _ => $"{game.ToMove} to move.",
            };
        return $"{prefix}: game {gameNumber} of {series.Config.GameCount}. {turn}".Trim();
    }

    private static string DescribeAbort(AbortReason reason) => reason switch
    {
        AbortReason.Cancelled => "Cancelled.",
        AbortReason.MoveRejected rejected => $"{rejected.Side} move rejected: {rejected.Failure.Describe()}",
        _ => throw new UnreachableException(),
    };

    private static string FormatDuration(TimeSpan span) =>
        span.TotalSeconds < 10
            ? span.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s"
            : span.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";

    private void CancelPlay()
    {
        try
        {
            _playCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _playCts?.Dispose();
        _playCts = null;
    }

    private void DetachRunner()
    {
        if (_runner is null)
        {
            return;
        }

        _runner.Changed -= OnRunnerChanged;
        _runner = null;
    }

    private void RaiseCommands()
    {
        Notify(nameof(CanPlay));
        Notify(nameof(CanPause));
        Notify(nameof(CanResume));
        Notify(nameof(CanNewSeries));
        (PlayCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (PauseCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ResumeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NewSeriesCommand as RelayCommand)?.RaiseCanExecuteChanged();
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

internal sealed class RelayCommand(Action execute, Func<bool> canExecute) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute();

    public void Execute(object? parameter) => execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

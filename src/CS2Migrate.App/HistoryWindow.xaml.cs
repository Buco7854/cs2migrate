using System.Collections.ObjectModel;
using System.Windows;
using CS2Migrate.Core.Models;

namespace CS2Migrate.App;

/// <summary>
/// Browses the archives kept for one account and lets any of them be put back, whole or file
/// by file.
/// </summary>
public partial class HistoryWindow : Window
{
    private readonly HistoryViewModel _viewModel;

    internal HistoryWindow(string accountName, IReadOnlyList<RestorePoint> restorePoints)
    {
        InitializeComponent();
        _viewModel = new HistoryViewModel(accountName, restorePoints);
        DataContext = _viewModel;
        ThemeService.RegisterWindow(this);
    }

    /// <summary>The point the user confirmed, or null when the dialog was dismissed.</summary>
    internal RestorePoint? ChosenPoint { get; private set; }

    internal IReadOnlyList<string> ChosenFileNames { get; private set; } = [];

    private void SelectAll_Click(object sender, RoutedEventArgs e) => _viewModel.SetAllSelected(true);

    private void SelectNone_Click(object sender, RoutedEventArgs e) => _viewModel.SetAllSelected(false);

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        var point = _viewModel.SelectedPoint;
        if (point is null)
        {
            return;
        }

        var names = point.Files.Where(file => file.IsSelected).Select(file => file.Name).ToArray();
        if (names.Length == 0)
        {
            return;
        }

        if (MessageBox.Show(
                this,
                LanguageService.Format(
                    "ConfirmHistoryRestoreMessage",
                    names.Length,
                    _viewModel.AccountName,
                    point.Timestamp),
                LanguageService.Text("ConfirmHistoryRestoreTitle"),
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning) != MessageBoxResult.OK)
        {
            return;
        }

        ChosenPoint = point.Point;
        ChosenFileNames = names;
        DialogResult = true;
    }
}

internal sealed class HistoryViewModel : ObservableObject
{
    private RestorePointViewModel? _selectedPoint;

    public HistoryViewModel(string accountName, IReadOnlyList<RestorePoint> restorePoints)
    {
        AccountName = accountName;
        RestorePoints = new ObservableCollection<RestorePointViewModel>(
            restorePoints.Select(point => new RestorePointViewModel(point)));
        SelectedPoint = RestorePoints.FirstOrDefault();
    }

    public string AccountName { get; }
    public ObservableCollection<RestorePointViewModel> RestorePoints { get; }
    public string HeaderTitle => LanguageService.Format("HistoryHeader", AccountName);
    public bool IsEmpty => RestorePoints.Count == 0;

    public RestorePointViewModel? SelectedPoint
    {
        get => _selectedPoint;
        set
        {
            var previous = _selectedPoint;
            if (!SetProperty(ref _selectedPoint, value))
            {
                return;
            }

            // Ticking a single file has to re-evaluate whether anything is left to restore.
            if (previous is not null)
            {
                foreach (var file in previous.Files)
                {
                    file.PropertyChanged -= OnFileSelectionChanged;
                }
            }

            if (value is not null)
            {
                foreach (var file in value.Files)
                {
                    file.PropertyChanged += OnFileSelectionChanged;
                }
            }

            OnPropertyChanged(nameof(CanRestore));
        }
    }

    private void OnFileSelectionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        OnPropertyChanged(nameof(CanRestore));

    public bool CanRestore => SelectedPoint?.Files.Any(file => file.IsSelected) == true;

    public void SetAllSelected(bool selected)
    {
        if (SelectedPoint is null)
        {
            return;
        }

        foreach (var file in SelectedPoint.Files)
        {
            file.IsSelected = selected;
        }

        OnPropertyChanged(nameof(CanRestore));
    }
}

internal sealed class RestorePointViewModel
{
    public RestorePointViewModel(RestorePoint point)
    {
        Point = point;
        Files = new ObservableCollection<RestoreFileViewModel>(
            point.Files.Select(file => new RestoreFileViewModel(file)));
    }

    public RestorePoint Point { get; }
    public ObservableCollection<RestoreFileViewModel> Files { get; }

    public string Title => LanguageService.Text(Point.Kind switch
    {
        RestorePointKind.ManualBackup => "KindManualBackup",
        RestorePointKind.AutomaticSafetyCopy => "KindSafetyCopy",
        RestorePointKind.BeforeFriendSession => "KindFriendSession",
        _ => "KindBeforeMigration"
    });

    public string Timestamp => Point.CreatedUtc.LocalDateTime.ToString("f", LanguageService.Culture);

    public string Summary => Files.Count == 1
        ? LanguageService.Text("HistoryOneFile")
        : LanguageService.Format("HistoryManyFiles", Files.Count);
}

internal sealed class RestoreFileViewModel(RestorePointFile file) : ObservableObject
{
    private bool _isSelected = true;

    public string Name => file.Name;

    public string Detail => $"{file.Category} · {FormatBytes(file.Length)}";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024d:0.#} KB",
        _ => $"{bytes / 1024d / 1024d:0.#} MB"
    };
}

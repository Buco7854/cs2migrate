using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CS2Migrate.Core;
using CS2Migrate.Core.Models;
using Microsoft.Win32;

namespace CS2Migrate.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        ThemeService.RegisterWindow(this);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Load();
        _viewModel.IsLoaded = true;
    }

    private void Window_Activated(object? sender, EventArgs e) => _viewModel.RefreshRuntimeState();

    private void Refresh_Click(object sender, RoutedEventArgs e) => _viewModel.Load(_viewModel.SteamDirectory);

    private void ChooseSteamFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = LanguageService.Text("ChooseSteamFolderDialog"),
            InitialDirectory = Directory.Exists(_viewModel.SteamDirectory) ? _viewModel.SteamDirectory : string.Empty,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.Load(dialog.FolderName);
        }
    }

    private void Swap_Click(object sender, RoutedEventArgs e) => _viewModel.SwapAccounts();

    private void Language_Click(object sender, RoutedEventArgs e) => _viewModel.ChangeLanguage();

    private async void CloseSteam_Click(object sender, RoutedEventArgs e) => await _viewModel.CloseSteamSafelyAsync();

    private async void Migrate_Click(object sender, RoutedEventArgs e)
    {
        var confirmation = _viewModel.CreateConfirmation();
        if (confirmation is null)
        {
            return;
        }

        if (MessageBox.Show(this, confirmation.Value.Message, confirmation.Value.Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            return;
        }

        // The engine always backs up the files it replaces, but an account that has never
        // been snapshotted has nothing to fall back to for the rest of its configuration.
        if (_viewModel.ShouldOfferTargetBackup)
        {
            var targetName = _viewModel.SelectedTarget!.DisplayName;
            var answer = MessageBox.Show(
                this,
                LanguageService.Format("OfferBackupMessage", targetName),
                LanguageService.Format("OfferBackupTitle", targetName),
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (answer == MessageBoxResult.Cancel)
            {
                return;
            }

            if (answer == MessageBoxResult.Yes)
            {
                await _viewModel.BackupTargetAsync();
                if (!_viewModel.CanMigrate)
                {
                    return;
                }
            }
        }

        await _viewModel.MigrateAsync();
    }

    private async void BackupTarget_Click(object sender, RoutedEventArgs e)
    {
        var targetName = _viewModel.SelectedTarget?.DisplayName;
        if (targetName is null ||
            MessageBox.Show(
                this,
                LanguageService.Format("ConfirmBackupMessage", targetName),
                LanguageService.Text("ConfirmBackupTitle"),
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            return;
        }

        await _viewModel.BackupTargetAsync();
    }

    private void OpenBackups_Click(object sender, RoutedEventArgs e) => _viewModel.OpenBackups();

    private async void History_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new HistoryWindow(
            [.. _viewModel.Accounts],
            _viewModel.SelectedTarget,
            _viewModel.LoadRestorePoints)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true || dialog.ChosenPoint is null || dialog.ChosenAccount is null)
        {
            return;
        }

        await _viewModel.RestoreFromHistoryAsync(dialog.ChosenAccount, dialog.ChosenPoint, dialog.ChosenFileNames);
    }
}

internal sealed class MainWindowViewModel : ObservableObject
{
    private readonly SteamAccountDiscovery _discovery = new();
    private readonly IProcessInspector _processInspector = new ProcessInspector();
    private readonly MigrationEngine _migrationEngine;
    private readonly CloudRecoveryService _cloudRecoveryService = new();
    private readonly RestorePointService _restorePointService;
    private readonly AccountBackupService _accountBackupService;
    private AccountCardViewModel? _selectedSource;
    private AccountCardViewModel? _selectedTarget;
    private bool _includeGameplay = true;
    private bool _includeKeybinds = true;
    private bool _includeVideo = true;
    private bool _includeAutoexec = true;
    private bool _isTemporarySession;
    private bool _isBusy;
    private string? _busyLabelKey;
    private string _steamDirectory = string.Empty;
    private string _environmentStatus = string.Empty;
    private StatusSeverity _environmentSeverity = StatusSeverity.Caution;
    private bool _hasBlockingProcesses;
    private string _safetyTitle = string.Empty;
    private string _safetyDetail = string.Empty;
    private StatusSeverity _safetySeverity = StatusSeverity.Informational;
    private string _previewSummary = string.Empty;
    private string _previewSize = "0 B";
    private string _activityTitle = string.Empty;
    private string _activityDetail = string.Empty;
    private string _migrationButtonText = string.Empty;
    private CloudRecoveryCandidate? _pendingRecovery;
    private TemporarySessionRecovery? _pendingTemporarySession;
    private AccountBackup? _latestTargetBackup;

    public MainWindowViewModel()
    {
        _migrationEngine = new MigrationEngine(_processInspector);
        _accountBackupService = new AccountBackupService(_processInspector);
        _restorePointService = new RestorePointService(_processInspector);
        ResetLocalizedDefaults();
    }

    public ObservableCollection<AccountCardViewModel> Accounts { get; } = [];

    /// <summary>Version and the date of the running executable, so a stale download shows up.</summary>
    public string BuildStamp { get; } = CreateBuildStamp();
    public ObservableCollection<PreviewFileViewModel> PreviewFiles { get; } = [];
    public bool IsLoaded { get; set; }
    public string LanguageButtonLabel => LanguageService.Current == AppLanguage.English ? "FR" : "EN";
    public bool HasPendingRecovery => _pendingRecovery is not null;
    public bool HasPendingTemporarySession => _pendingTemporarySession is not null;

    public string SteamDirectory
    {
        get => _steamDirectory;
        private set => SetProperty(ref _steamDirectory, value);
    }

    public AccountCardViewModel? SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (!SetProperty(ref _selectedSource, value))
            {
                return;
            }

            if (value is not null && ReferenceEquals(value, SelectedTarget))
            {
                SelectedTarget = Accounts.FirstOrDefault(account => !ReferenceEquals(account, value));
            }

            RefreshPreview();
        }
    }

    public AccountCardViewModel? SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            if (!SetProperty(ref _selectedTarget, value))
            {
                return;
            }

            if (value is not null && ReferenceEquals(value, SelectedSource))
            {
                SelectedSource = Accounts.FirstOrDefault(account => !ReferenceEquals(account, value));
            }

            RefreshBackupState();
            RefreshPreview();
        }
    }

    public bool IncludeGameplay
    {
        get => _includeGameplay;
        set { if (SetProperty(ref _includeGameplay, value)) RefreshPreview(); }
    }

    public bool IncludeKeybinds
    {
        get => _includeKeybinds;
        set { if (SetProperty(ref _includeKeybinds, value)) RefreshPreview(); }
    }

    public bool IncludeVideo
    {
        get => _includeVideo;
        set { if (SetProperty(ref _includeVideo, value)) RefreshPreview(); }
    }

    public bool IncludeAutoexec
    {
        get => _includeAutoexec;
        set { if (SetProperty(ref _includeAutoexec, value)) RefreshPreview(); }
    }

    public bool IsTemporarySession
    {
        get => _isTemporarySession;
        set => SetProperty(ref _isTemporarySession, value);
    }

    public string EnvironmentStatus { get => _environmentStatus; private set => SetProperty(ref _environmentStatus, value); }
    public StatusSeverity EnvironmentSeverity { get => _environmentSeverity; private set => SetProperty(ref _environmentSeverity, value); }
    public bool HasBlockingProcesses
    {
        get => _hasBlockingProcesses;
        private set
        {
            if (SetProperty(ref _hasBlockingProcesses, value))
            {
                OnPropertyChanged(nameof(CanMigrate));
                OnPropertyChanged(nameof(CanBackupTarget));
            }
        }
    }
    public string SafetyTitle { get => _safetyTitle; private set => SetProperty(ref _safetyTitle, value); }
    public string SafetyDetail { get => _safetyDetail; private set => SetProperty(ref _safetyDetail, value); }
    public StatusSeverity SafetySeverity { get => _safetySeverity; private set => SetProperty(ref _safetySeverity, value); }
    public string PreviewSummary { get => _previewSummary; private set => SetProperty(ref _previewSummary, value); }
    public string PreviewSize { get => _previewSize; private set => SetProperty(ref _previewSize, value); }
    public string ActivityTitle { get => _activityTitle; private set => SetProperty(ref _activityTitle, value); }
    public string ActivityDetail { get => _activityDetail; private set => SetProperty(ref _activityDetail, value); }
    public string MigrationButtonText { get => _migrationButtonText; private set => SetProperty(ref _migrationButtonText, value); }
    public bool CanMigrate => !_isBusy && !HasBlockingProcesses &&
                              (HasPendingTemporarySession || GetPreview()?.Files.Count > 0);
    public bool CanBackupTarget => !_isBusy && !HasBlockingProcesses && SelectedTarget?.Account.PortableFileCount > 0;
    public bool CanOpenHistory => !_isBusy && Accounts.Count > 0;

    /// <summary>
    /// True when a plain migration is about to overwrite an account that has no snapshot of
    /// its own yet, so the user can be offered one before anything is written.
    /// </summary>
    public bool ShouldOfferTargetBackup =>
        _latestTargetBackup is null &&
        _pendingRecovery is null &&
        _pendingTemporarySession is null &&
        SelectedTarget?.Account.PortableFileCount > 0;

    private string BackupRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CS2Migrate",
        "Backups");

    private string SteamPathPreference => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CS2Migrate",
        "steam-path.txt");

    public void ChangeLanguage()
    {
        LanguageService.Toggle();
        OnPropertyChanged(nameof(LanguageButtonLabel));
        ResetLocalizedDefaults();
        Load(string.IsNullOrWhiteSpace(SteamDirectory) ? null : SteamDirectory);
    }

    public void RefreshRuntimeState()
    {
        if (!IsLoaded || _isBusy)
        {
            return;
        }

        RefreshBackupState();
        RefreshSafetyState();
        RefreshPreview();
    }

    public (string Message, string Title)? CreateConfirmation()
    {
        if (_pendingTemporarySession is not null)
        {
            return (
                LanguageService.Format(
                    "ConfirmFriendRestoreMessage",
                    _pendingTemporarySession.Target.DisplayName,
                    _pendingTemporarySession.ChangedFileCount),
                LanguageService.Text("ConfirmFriendRestoreTitle"));
        }

        var preview = GetPreview();
        if (preview is null || SelectedSource is null || SelectedTarget is null)
        {
            return null;
        }

        return (
            LanguageService.Format(
                IsTemporarySession ? "ConfirmFriendSessionMessage" : "ConfirmMigrationMessage",
                preview.Files.Count,
                SelectedSource.DisplayName,
                SelectedTarget.DisplayName),
            LanguageService.Text(IsTemporarySession ? "ConfirmFriendSessionTitle" : "ConfirmMigrationTitle"));
    }

    public void Load(string? requestedSteamDirectory = null)
    {
        try
        {
            var steamDirectory = !string.IsNullOrWhiteSpace(requestedSteamDirectory)
                ? Path.GetFullPath(requestedSteamDirectory)
                : SteamPathDetector.Find() ?? TryReadSteamPathPreference();

            if (!SteamPathDetector.IsSteamDirectory(steamDirectory))
            {
                Accounts.Clear();
                ClearBackupState();
                SteamDirectory = steamDirectory ?? string.Empty;
                EnvironmentStatus = LanguageService.Text("SteamFolderNeeded");
                EnvironmentSeverity = StatusSeverity.Caution;
                ActivityTitle = LanguageService.Text("SteamNotFound");
                ActivityDetail = LanguageService.Text("SteamNotFoundDetail");
                RefreshSafetyState();
                RefreshPreview();
                return;
            }

            SteamDirectory = steamDirectory!;
            if (!string.IsNullOrWhiteSpace(requestedSteamDirectory))
            {
                TryWriteSteamPathPreference(SteamDirectory);
            }
            // Reloading rebuilds the account view models, so remember which accounts were
            // picked. Silently moving the selection after a migration or a restore would let
            // someone act on a pair they never chose.
            var previousSource = SelectedSource?.Account.SteamId64;
            var previousTarget = SelectedTarget?.Account.SteamId64;

            var discovered = _discovery.Discover(SteamDirectory).Select(account => new AccountCardViewModel(account)).ToArray();
            Accounts.Clear();
            foreach (var account in discovered)
            {
                Accounts.Add(account);
            }

            SelectedSource = FindAccount(previousSource)
                ?? Accounts.FirstOrDefault(account => account.Account.PortableFileCount > 0)
                ?? Accounts.FirstOrDefault();
            SelectedTarget = FindAccount(previousTarget) is { } restoredTarget && !ReferenceEquals(restoredTarget, SelectedSource)
                ? restoredTarget
                : Accounts.FirstOrDefault(account => !ReferenceEquals(account, SelectedSource));
            EnvironmentStatus = Accounts.Count == 1
                ? LanguageService.Text("AccountsFoundOne")
                : LanguageService.Format("AccountsFoundMany", Accounts.Count);
            EnvironmentSeverity = Accounts.Count >= 2 ? StatusSeverity.Success : StatusSeverity.Caution;
            ActivityTitle = Accounts.Count >= 2
                ? LanguageService.Text("AccountsLoaded")
                : LanguageService.Text("SecondAccountNeeded");
            ActivityDetail = Accounts.Count >= 2
                ? LanguageService.Text("AccountsLoadedDetail")
                : LanguageService.Text("SecondAccountDetail");
            RefreshBackupState();
            RefreshSafetyState();
            RefreshPreview();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Accounts.Clear();
            ClearBackupState();
            EnvironmentStatus = LanguageService.Text("SteamDataUnavailable");
            EnvironmentSeverity = StatusSeverity.Critical;
            ActivityTitle = LanguageService.Text("CouldNotReadAccounts");
            ActivityDetail = exception.Message;
            RefreshSafetyState();
            RefreshPreview();
        }
    }

    private AccountCardViewModel? FindAccount(ulong? steamId) => steamId is null
        ? null
        : Accounts.FirstOrDefault(account => account.Account.SteamId64 == steamId);

    public void SwapAccounts()
    {
        var source = SelectedSource;
        var target = SelectedTarget;
        SelectedSource = null;
        SelectedTarget = source;
        SelectedSource = target;
        RefreshPreview();
    }

    public async Task CloseSteamSafelyAsync()
    {
        if (string.IsNullOrWhiteSpace(SteamDirectory))
        {
            return;
        }

        try
        {
            ActivityTitle = LanguageService.Text("AskingSteamToClose");
            ActivityDetail = LanguageService.Text("SteamShutdownDetail");
            Process.Start(new ProcessStartInfo(Path.Combine(SteamDirectory, "steam.exe"), "-shutdown")
            {
                UseShellExecute = true
            });

            for (var attempt = 0; attempt < 40; attempt++)
            {
                await Task.Delay(500);
                if (_processInspector.GetBlockingProcesses().Count == 0)
                {
                    break;
                }
            }

            RefreshBackupState();
            RefreshSafetyState();
            ActivityTitle = HasBlockingProcesses
                ? LanguageService.Text("SteamStillClosing")
                : LanguageService.Text("SteamClosed");
            ActivityDetail = HasBlockingProcesses
                ? LanguageService.Text("WaitForSteam")
                : LanguageService.Text("MigrationUnlocked");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            ActivityTitle = LanguageService.Text("SteamCloseFailed");
            ActivityDetail = LanguageService.Format("SteamCloseFailedDetail", exception.Message);
        }
    }

    public MigrationPreview? GetPreview()
    {
        if (SelectedSource is null || SelectedTarget is null)
        {
            return null;
        }

        try
        {
            return _migrationEngine.Preview(BuildRequest());
        }
        catch (MigrationException)
        {
            return null;
        }
    }

    public async Task MigrateAsync()
    {
        var temporaryRecovery = _pendingTemporarySession;
        var startingFriendSession = temporaryRecovery is null && IsTemporarySession;
        SetBusy(true, "Migrating");
        try
        {
            var progress = CreateProgressReporter();
            MigrationResult result;
            if (temporaryRecovery is not null)
            {
                result = await _accountBackupService.RestoreTemporarySessionAsync(
                    temporaryRecovery,
                    BackupRoot,
                    progress);
            }
            else
            {
                result = await _migrationEngine.MigrateAsync(
                    BuildRequest() with
                    {
                        Purpose = startingFriendSession
                            ? MigrationPurpose.TemporaryFriendSession
                            : MigrationPurpose.Standard
                    },
                    progress);
            }

            Load(SteamDirectory);
            if (temporaryRecovery is not null)
            {
                ActivityTitle = LanguageService.Text("FriendRestoreComplete");
                ActivityDetail = LanguageService.Format("FriendRestoreCompleteDetail", temporaryRecovery.Target.DisplayName);
            }
            else if (startingFriendSession)
            {
                ActivityTitle = LanguageService.Text("FriendSessionReady");
                ActivityDetail = LanguageService.Format("FriendSessionReadyDetail", result.FileCount);
            }
            else
            {
                ActivityTitle = LanguageService.Text("MigrationComplete");
                ActivityDetail = LanguageService.Format("MigrationCompleteDetail", result.FileCount);
            }
        }
        catch (MigrationException exception)
        {
            ActivityTitle = LanguageService.Text("MigrationStopped");
            ActivityDetail = exception.InnerException is null
                ? LanguageService.TranslateMigrationError(exception.Message)
                : $"{LanguageService.TranslateMigrationError(exception.Message)} {exception.InnerException.Message}";
            RefreshSafetyState();
        }
        catch (OperationCanceledException)
        {
            ActivityTitle = LanguageService.Text("MigrationCancelled");
            ActivityDetail = LanguageService.Text("MigrationCancelledDetail");
        }
        finally
        {
            SetBusy(false);
            RefreshPreview();
        }
    }

    public async Task BackupTargetAsync()
    {
        if (SelectedTarget is null)
        {
            return;
        }

        SetBusy(true, "BackingUp");
        try
        {
            var backup = await _accountBackupService.CreateManualBackupAsync(SelectedTarget.Account, BackupRoot);
            _latestTargetBackup = backup;
            ActivityTitle = LanguageService.Text("BackupComplete");
            ActivityDetail = LanguageService.Format("BackupCompleteDetail", backup.Account.DisplayName, backup.FileCount);
        }
        catch (MigrationException exception)
        {
            ActivityTitle = LanguageService.Text("BackupStopped");
            ActivityDetail = LanguageService.TranslateMigrationError(exception.Message);
        }
        finally
        {
            SetBusy(false);
            RefreshBackupState();
            RefreshSafetyState();
        }
    }

    /// <summary>Every archived version of an account, newest first.</summary>
    public IReadOnlyList<RestorePoint> LoadRestorePoints(AccountCardViewModel account) =>
        _restorePointService.FindRestorePoints(account.Account, BackupRoot);

    public async Task RestoreFromHistoryAsync(
        AccountCardViewModel account,
        RestorePoint restorePoint,
        IReadOnlyList<string> fileNames)
    {
        SetBusy(true, "Restoring");
        try
        {
            var result = await _restorePointService.RestoreAsync(
                account.Account,
                restorePoint,
                fileNames,
                BackupRoot,
                CreateProgressReporter());
            Load(SteamDirectory);
            ActivityTitle = LanguageService.Text("HistoryRestoreComplete");
            ActivityDetail = LanguageService.Format(
                "HistoryRestoreCompleteDetail",
                result.FileCount,
                account.DisplayName);
        }
        catch (MigrationException exception)
        {
            ActivityTitle = LanguageService.Text("MigrationStopped");
            ActivityDetail = LanguageService.TranslateMigrationError(exception.Message);
        }
        finally
        {
            SetBusy(false);
            RefreshBackupState();
            RefreshSafetyState();
            RefreshPreview();
        }
    }

    public void OpenBackups()
    {
        Directory.CreateDirectory(BackupRoot);
        Process.Start(new ProcessStartInfo("explorer.exe", BackupRoot) { UseShellExecute = true });
    }

    private Progress<MigrationProgress> CreateProgressReporter() => new(update =>
    {
        var localizedStage = LanguageService.Text($"Progress_{update.Stage}");
        ActivityTitle = localizedStage.StartsWith("Progress_", StringComparison.Ordinal)
            ? update.Stage
            : localizedStage;
        ActivityDetail = LanguageService.Format(
            "ProgressDetail",
            update.Detail,
            update.Completed + 1,
            Math.Max(update.Total, 1));
    });

    private void RefreshSafetyState()
    {
        var blockers = _processInspector.GetBlockingProcesses();
        HasBlockingProcesses = blockers.Count > 0;
        if (_pendingTemporarySession is not null)
        {
            SafetyTitle = LanguageService.Format("FriendRestoreTitle", _pendingTemporarySession.Target.DisplayName);
            SafetyDetail = HasBlockingProcesses
                ? LanguageService.Text("FriendRestoreRunningDetail")
                : LanguageService.Text("FriendRestoreReadyDetail");
            SafetySeverity = StatusSeverity.Informational;
            UpdateMigrationButtonText();
            OnPropertyChanged(nameof(CanMigrate));
            return;
        }

        if (HasBlockingProcesses)
        {
            SafetyTitle = LanguageService.Format(
                "CloseProcesses",
                string.Join(LanguageService.Text("ProcessJoiner"), blockers));
            SafetyDetail = LanguageService.Text("CloseProcessesDetail");
            SafetySeverity = StatusSeverity.Caution;
        }
        else
        {
            SafetyTitle = LanguageService.Text("SafeToMigrate");
            SafetyDetail = LanguageService.Text("SafeToMigrateDetail");
            SafetySeverity = StatusSeverity.Success;
        }

        UpdateMigrationButtonText();
        OnPropertyChanged(nameof(CanMigrate));
    }

    private void RefreshPreview()
    {
        PreviewFiles.Clear();
        if (_pendingTemporarySession is not null)
        {
            foreach (var fileName in _pendingTemporarySession.FileNames)
            {
                PreviewFiles.Add(new PreviewFileViewModel(fileName, LanguageService.Text("RestoreAction")));
            }

            PreviewSummary = LanguageService.Format(
                "FriendRestorePreview",
                _pendingTemporarySession.Target.DisplayName,
                _pendingTemporarySession.ChangedFileCount);
            PreviewSize = LanguageService.Text("ProtectedBackup");
            OnPropertyChanged(nameof(CanMigrate));
            return;
        }

        var preview = GetPreview();
        if (preview is null)
        {
            PreviewSummary = Accounts.Count < 2
                ? LanguageService.Text("ChooseOrAddAccounts")
                : LanguageService.Text("ChooseDifferentAccounts");
            PreviewSize = "0 B";
        }
        else
        {
            foreach (var file in preview.Files)
            {
                PreviewFiles.Add(new PreviewFileViewModel(
                    file.Name,
                    LanguageService.Text(file.ReplacesExisting ? "ReplaceAction" : "NewAction")));
            }

            PreviewSummary = preview.Files.Count == 0
                ? LanguageService.Text("NoMatchingSourceFiles")
                : preview.Files.Count == 1
                    ? LanguageService.Format("PreviewOneFile", preview.ReplacedCount, preview.NewCount)
                    : LanguageService.Format("PreviewManyFiles", preview.Files.Count, preview.ReplacedCount, preview.NewCount);
            PreviewSize = FormatBytes(preview.TotalBytes);
        }

        OnPropertyChanged(nameof(CanMigrate));
    }

    private MigrationRequest BuildRequest() => new(
        SelectedSource?.Account ?? throw new MigrationException(LanguageService.Text("ErrorChooseSource")),
        SelectedTarget?.Account ?? throw new MigrationException(LanguageService.Text("ErrorChooseTarget")),
        SelectedCategories,
        BackupRoot);

    private MigrationCategory SelectedCategories =>
        (IncludeGameplay ? MigrationCategory.Gameplay : MigrationCategory.None) |
        (IncludeKeybinds ? MigrationCategory.Keybinds : MigrationCategory.None) |
        (IncludeVideo ? MigrationCategory.Video : MigrationCategory.None) |
        (IncludeAutoexec ? MigrationCategory.Autoexec : MigrationCategory.None);

    private void SetBusy(bool value, string? labelKey = null)
    {
        _isBusy = value;
        _busyLabelKey = value ? labelKey : null;
        UpdateMigrationButtonText();
        OnPropertyChanged(nameof(CanMigrate));
        OnPropertyChanged(nameof(CanBackupTarget));
        OnPropertyChanged(nameof(CanOpenHistory));
    }

    private void UpdateMigrationButtonText()
    {
        MigrationButtonText = _isBusy
            ? LanguageService.Text(_busyLabelKey ?? "Migrating")
            : LanguageService.Text(
                _pendingTemporarySession is not null ? "RestoreFriendSettings" : "MigrateSettings");
    }

    private void RefreshBackupState()
    {
        var accounts = Accounts.Select(account => account.Account).ToArray();
        var previousCloudArchive = _pendingRecovery?.ArchiveDirectory;
        var previousTemporaryArchive = _pendingTemporarySession?.ArchiveDirectory;
        var previousManualArchive = _latestTargetBackup?.ArchiveDirectory;

        _pendingTemporarySession = _accountBackupService.FindPendingTemporarySession(accounts, BackupRoot);
        _pendingRecovery = _pendingTemporarySession is null
            ? _cloudRecoveryService.FindLatestMismatch(accounts, BackupRoot)
            : null;
        _latestTargetBackup = SelectedTarget is null
            ? null
            : _accountBackupService.FindLatestManualBackup(SelectedTarget.Account, BackupRoot);

        if (!string.Equals(previousCloudArchive, _pendingRecovery?.ArchiveDirectory, StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(HasPendingRecovery));
        }

        if (!string.Equals(previousTemporaryArchive, _pendingTemporarySession?.ArchiveDirectory, StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(HasPendingTemporarySession));
        }

        if (!string.Equals(previousManualArchive, _latestTargetBackup?.ArchiveDirectory, StringComparison.Ordinal))
        {
            }

        if (_pendingTemporarySession is not null)
        {
            ActivityTitle = LanguageService.Format("FriendRestoreDetected", _pendingTemporarySession.Target.DisplayName);
            ActivityDetail = LanguageService.Text("FriendRestoreDetectedDetail");
        }
        else if (_pendingRecovery is not null)
        {
            ActivityTitle = LanguageService.Format("SettingsChangedTitle", _pendingRecovery.ChangedFileCount);
            ActivityDetail = LanguageService.Text("SettingsChangedDetail");
        }

        UpdateMigrationButtonText();
        OnPropertyChanged(nameof(CanMigrate));
        OnPropertyChanged(nameof(CanBackupTarget));
    }

    private void ClearBackupState()
    {
        _pendingRecovery = null;
        _pendingTemporarySession = null;
        _latestTargetBackup = null;
        OnPropertyChanged(nameof(HasPendingRecovery));
        OnPropertyChanged(nameof(HasPendingTemporarySession));
        OnPropertyChanged(nameof(CanMigrate));
        OnPropertyChanged(nameof(CanBackupTarget));
        UpdateMigrationButtonText();
    }

    private void ResetLocalizedDefaults()
    {
        EnvironmentStatus = LanguageService.Text("LookingForSteam");
        SafetyTitle = LanguageService.Text("CheckingSteam");
        SafetyDetail = LanguageService.Text("MigrationAvailableClosed");
        PreviewSummary = LanguageService.Text("ChooseTwoAccounts");
        ActivityTitle = LanguageService.Text("ReadyWhenYouAre");
        ActivityDetail = LanguageService.Text("NothingChanged");
        UpdateMigrationButtonText();
    }

    private string? TryReadSteamPathPreference()
    {
        try
        {
            return File.Exists(SteamPathPreference)
                ? File.ReadAllText(SteamPathPreference).Trim()
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void TryWriteSteamPathPreference(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SteamPathPreference)!);
            File.WriteAllText(SteamPathPreference, path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Automatic discovery will still run on the next launch.
        }
    }

    private static string CreateBuildStamp()
    {
        var version = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                return $"v{version} · {File.GetLastWriteTime(path).ToString("g", LanguageService.Culture)}";
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The version alone is still enough to identify the build.
        }

        return $"v{version}";
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024d:0.#} KB",
        _ => $"{bytes / 1024d / 1024d:0.#} MB"
    };
}

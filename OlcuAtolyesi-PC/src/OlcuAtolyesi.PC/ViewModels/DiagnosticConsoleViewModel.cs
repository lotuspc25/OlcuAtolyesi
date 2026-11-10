using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using OlcuAtolyesi.Core.Diagnostics;
using OlcuAtolyesi.Core.Mvvm;

namespace OlcuAtolyesi.PC.ViewModels;

/// <summary>
/// PC ve Android tanılama olaylarını canlı izleyen, filtreleyen,
/// arayan ve dışa aktaran Merkezi Tanılama Konsolu ViewModel sınıfı.
/// </summary>
public sealed class DiagnosticConsoleViewModel : ObservableObject, IDisposable
{
    private readonly DiagnosticLogHub _logHub;
    private readonly Dispatcher? _dispatcher;
    private readonly ObservableCollection<DiagnosticLogEntry> _logEntries;
    private readonly ICollectionView _filteredView;
    private readonly object _collectionLock = new();

    private DiagnosticLogEntry? _selectedEntry;
    private string _searchText = string.Empty;
    private string _selectedLevelFilter = "Tümü";
    private string _selectedSourceFilter = "Tümü";
    private bool _isAutoScrollEnabled = true;

    private string _phoneServerUrl = "http://192.168.1.100:8088";
    private string _phonePin = string.Empty;
    private bool _isFetchingAndroidLogs;
    private string _statusMessage = "Tanılama konsolu hazır.";
    private int _displayedCount;
    private int _totalCount;
    private bool _isDisposed;

    public event EventHandler? RequestScrollToEnd;

    public DiagnosticConsoleViewModel(
        DiagnosticLogHub? logHub = null,
        Dispatcher? dispatcher = null,
        bool isSync = false)
    {
        _logHub = logHub ?? DiagnosticLogHub.Instance;
        _dispatcher = isSync ? null : (dispatcher ?? Application.Current?.Dispatcher);

        _logEntries = new ObservableCollection<DiagnosticLogEntry>();
        BindingOperations.EnableCollectionSynchronization(_logEntries, _collectionLock);
        _filteredView = CollectionViewSource.GetDefaultView(_logEntries);
        _filteredView.Filter = FilterPredicate;

        LevelFilterOptions = ["Tümü", "DEBUG", "INFO", "EVENT", "WARN", "ERROR", "CRITICAL"];
        SourceFilterOptions = ["Tümü", "PC", "Android"];

        // Komutlar
        ClearViewCommand = new RelayCommand(ClearView);
        CopySelectedCommand = new RelayCommand(CopySelected, () => SelectedEntry != null);
        CopyAllCommand = new RelayCommand(CopyAll, () => _logEntries.Count > 0);
        ExportReportCommand = new RelayCommand(ExportReport);
        FetchAndroidLogsCommand = new RelayCommand(async () => await FetchAndroidLogsAsync(), () => !IsFetchingAndroidLogs);
        RefreshCommand = new RelayCommand(LoadRecentEntries);

        // Mevcut kayıtları yükle ve dinleyiciyi başlat
        LoadRecentEntries();
        _logHub.EntryLogged += OnEntryLogged;
    }

    public ObservableCollection<DiagnosticLogEntry> LogEntries => _logEntries;
    public ICollectionView FilteredView => _filteredView;

    public IReadOnlyList<string> LevelFilterOptions { get; }
    public IReadOnlyList<string> SourceFilterOptions { get; }

    public DiagnosticLogEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetProperty(ref _selectedEntry, value))
            {
                OnPropertyChanged(nameof(HasSelectedEntry));
                OnPropertyChanged(nameof(SelectedEntryDetailText));
                (CopySelectedCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedEntry => _selectedEntry != null;

    public string SelectedEntryDetailText
    {
        get
        {
            if (_selectedEntry == null)
            {
                return "Ayrıntılarını görüntülemek için listeden bir tanılama kaydı seçin.";
            }

            return _selectedEntry.FormatLine();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _filteredView.Refresh();
                UpdateCounts();
            }
        }
    }

    public string SelectedLevelFilter
    {
        get => _selectedLevelFilter;
        set
        {
            if (SetProperty(ref _selectedLevelFilter, value))
            {
                _filteredView.Refresh();
                UpdateCounts();
            }
        }
    }

    public string SelectedSourceFilter
    {
        get => _selectedSourceFilter;
        set
        {
            if (SetProperty(ref _selectedSourceFilter, value))
            {
                _filteredView.Refresh();
                UpdateCounts();
            }
        }
    }

    public bool IsAutoScrollEnabled
    {
        get => _isAutoScrollEnabled;
        set => SetProperty(ref _isAutoScrollEnabled, value);
    }

    public string PhoneServerUrl
    {
        get => _phoneServerUrl;
        set => SetProperty(ref _phoneServerUrl, value);
    }

    public string PhonePin
    {
        get => _phonePin;
        set => SetProperty(ref _phonePin, value);
    }

    public bool IsFetchingAndroidLogs
    {
        get => _isFetchingAndroidLogs;
        private set
        {
            if (SetProperty(ref _isFetchingAndroidLogs, value))
            {
                (FetchAndroidLogsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public int DisplayedCount
    {
        get => _displayedCount;
        private set => SetProperty(ref _displayedCount, value);
    }

    public int TotalCount
    {
        get => _totalCount;
        private set => SetProperty(ref _totalCount, value);
    }

    public string LogDirectory => _logHub.LogDirectory;

    public ICommand ClearViewCommand { get; }
    public ICommand CopySelectedCommand { get; }
    public ICommand CopyAllCommand { get; }
    public ICommand ExportReportCommand { get; }
    public ICommand FetchAndroidLogsCommand { get; }
    public ICommand RefreshCommand { get; }

    public void LoadRecentEntries()
    {
        lock (_collectionLock)
        {
            _logEntries.Clear();
            foreach (var entry in _logHub.GetRecentEntries())
            {
                _logEntries.Add(entry);
            }

            if (_dispatcher != null && !_dispatcher.CheckAccess())
            {
                _dispatcher.BeginInvoke(() => _filteredView.Refresh());
            }
            else
            {
                try { _filteredView.Refresh(); } catch { }
            }

            UpdateCounts();
        }

        if (_isAutoScrollEnabled)
        {
            RequestScrollToEnd?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnEntryLogged(object? sender, DiagnosticLogEntry entry)
    {
        void Add()
        {
            lock (_collectionLock)
            {
                _logEntries.Add(entry);
                while (_logEntries.Count > 2500)
                {
                    _logEntries.RemoveAt(0);
                }
                UpdateCounts();
            }

            if (_isAutoScrollEnabled)
            {
                RequestScrollToEnd?.Invoke(this, EventArgs.Empty);
            }
        }

        if (_dispatcher != null && !_dispatcher.CheckAccess() && !_dispatcher.HasShutdownStarted)
        {
            try
            {
                _dispatcher.BeginInvoke(DispatcherPriority.Normal, Add);
            }
            catch
            {
                Add();
            }
        }
        else
        {
            Add();
        }
    }

    private bool FilterPredicate(object item)
    {
        if (item is not DiagnosticLogEntry entry)
        {
            return false;
        }

        // 1. Kaynak Filtresi
        if (_selectedSourceFilter == "PC" && entry.Source != DiagnosticSource.PC)
        {
            return false;
        }
        if (_selectedSourceFilter == "Android" && entry.Source != DiagnosticSource.Android)
        {
            return false;
        }

        // 2. Seviye Filtresi
        if (_selectedLevelFilter != "Tümü" &&
            !string.Equals(entry.Level.ToString(), _selectedLevelFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // 3. Metin Arama
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var q = _searchText.Trim();
            var matches = (entry.Message?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                          (entry.Module?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                          (entry.EventCode?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                          (entry.CorrelationId?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                          (entry.Details?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);

            if (!matches)
            {
                return false;
            }
        }

        return true;
    }

    public void ClearView()
    {
        lock (_collectionLock)
        {
            _logEntries.Clear();
            SelectedEntry = null;
            UpdateCounts();
        }
        StatusMessage = "Görünüm temizlendi (kalıcı log dosyaları korundu).";
    }

    public void CopySelected()
    {
        if (SelectedEntry == null) return;
        try
        {
            Clipboard.SetText(SelectedEntry.FormatLine());
            StatusMessage = "Seçili kayıt panoya kopyalandı.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Panoya kopyalanamadı: {ex.Message}";
        }
    }

    public void CopyAll()
    {
        List<DiagnosticLogEntry> filteredList;
        lock (_collectionLock)
        {
            filteredList = _logEntries.Where(FilterPredicate).ToList();
        }
        if (filteredList.Count == 0) return;

        try
        {
            var text = string.Join(Environment.NewLine, filteredList.Select(e => e.FormatLine()));
            Clipboard.SetText(text);
            StatusMessage = $"{filteredList.Count} adet kayıt panoya kopyalandı.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Panoya kopyalanamadı: {ex.Message}";
        }
    }

    public void ExportReport()
    {
        try
        {
            var defaultFileName = $"OlcuAtolyesi_Tanilama_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var targetPath = Path.Combine(desktopDir, defaultFileName);

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Tanılama Raporunu Dışa Aktar",
                Filter = "Metin Dosyası (*.txt)|*.txt|Tüm Dosyalar (*.*)|*.*",
                FileName = defaultFileName,
                InitialDirectory = desktopDir
            };

            if (dialog.ShowDialog() == true)
            {
                targetPath = dialog.FileName;
            }
            else
            {
                return;
            }

            List<DiagnosticLogEntry> entries;
            lock (_collectionLock)
            {
                entries = _logEntries.Where(FilterPredicate).ToList();
            }
            _logHub.ExportReport(targetPath, entries);
            StatusMessage = $"Rapor başarıyla kaydedildi: {Path.GetFileName(targetPath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Rapor dışa aktarma hatası: {ex.Message}";
        }
    }

    public async Task<int> FetchAndroidLogsAsync(string? serverUrl = null, string? pin = null)
    {
        var targetUrl = serverUrl ?? PhoneServerUrl;
        var targetPin = pin ?? PhonePin;

        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            StatusMessage = "Lütfen geçerli bir telefon sunucu adresi girin (ör. http://192.168.1.100:8088).";
            return 0;
        }

        IsFetchingAndroidLogs = true;
        StatusMessage = "Telefondan loglar alınıyor...";

        try
        {
            var cleanBase = targetUrl.Trim().TrimEnd('/');
            var query = string.IsNullOrWhiteSpace(targetPin)
                ? $"{cleanBase}/logs?limit=200"
                : $"{cleanBase}/logs?pin={Uri.EscapeDataString(targetPin)}&limit=200";

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            var response = await client.GetAsync(query);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                StatusMessage = "Yetkisiz erişim: PIN kodu geçersiz veya eşleşmiyor.";
                _logHub.Log(
                    DiagnosticLevel.Warn,
                    "DiagnosticConsole",
                    "ANDROID_LOGS_UNAUTHORIZED",
                    "Telefondan log çekme isteği 401 Yetkisiz Erişim ile reddedildi.");
                return 0;
            }

            if (!response.IsSuccessStatusCode)
            {
                StatusMessage = $"Telefondan log alma hatası: HTTP {(int)response.StatusCode}";
                return 0;
            }

            var json = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var list = JsonSerializer.Deserialize<List<DiagnosticLogEntry>>(json, options);

            if (list == null || list.Count == 0)
            {
                StatusMessage = "Telefonda henüz yeni log kaydı bulunmuyor.";
                return 0;
            }

            var count = _logHub.IngestRemoteEntries(list);
            StatusMessage = $"{count} adet Android log kaydı başarıyla alındı ve konsola aktarıldı.";
            return count;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Telefona bağlanılamadı: {ex.Message}";
            _logHub.Log(
                DiagnosticLevel.Error,
                "DiagnosticConsole",
                "ANDROID_LOGS_FETCH_ERROR",
                $"Telefondan log alma hatası: {ex.Message}",
                details: ex.ToString());
            return 0;
        }
        finally
        {
            IsFetchingAndroidLogs = false;
        }
    }

    private void UpdateCounts()
    {
        lock (_collectionLock)
        {
            TotalCount = _logEntries.Count;
            DisplayedCount = _logEntries.Count(FilterPredicate);
        }
        (CopyAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _logHub.EntryLogged -= OnEntryLogged;
    }
}

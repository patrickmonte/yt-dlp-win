using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using Forms = System.Windows.Forms;
using WpfMessageBox = System.Windows.MessageBox;

namespace YtDlpGui;

public partial class MainWindow : Window
{
    private readonly YtDlpService _service = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly YtDlpSettings _settings = new();
    public ObservableCollection<DownloadItem> Downloads { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        PresetComboBox.ItemsSource = new[] { "best", "mp4", "mp3" };
        PresetComboBox.SelectedIndex = 0;
        ModelComboBox.ItemsSource = YtDlpModel.All;
        ModelComboBox.SelectedIndex = 0;
        UpdateModelHint();
        PathTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads";
    }

    private void BrowsePath_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog { SelectedPath = PathTextBox.Text,
            Description = "Escolha a pasta de destino" };
        if (dialog.ShowDialog() == Forms.DialogResult.OK) PathTextBox.Text = dialog.SelectedPath;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var lines = UrlsTextBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
        var urls = lines.Where(IsSupportedUrl).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var invalid = lines.Except(urls, StringComparer.OrdinalIgnoreCase).ToList();
        if (invalid.Count > 0)
        {
            StatusText.Text = $"{invalid.Count} URL(s) ignorada(s): use http:// ou https:// com endereço completo.";
        }
        if (urls.Count == 0)
        {
            WpfMessageBox.Show("Nenhuma URL válida foi encontrada.\n\nExemplo: https://www.youtube.com/watch?v=...",
                "Verifique as URLs", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(PathTextBox.Text) || !Directory.Exists(PathTextBox.Text))
        {
            WpfMessageBox.Show("Escolha uma pasta de destino existente antes de adicionar downloads.",
                "Pasta de destino", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var model = ModelComboBox.SelectedItem as YtDlpModel ?? YtDlpModel.All[0];
        foreach (var url in urls)
            Downloads.Add(new DownloadItem(url, PathTextBox.Text,
                PresetComboBox.SelectedItem?.ToString() ?? "best", model));
        UrlsTextBox.Clear();
        StatusText.Text = $"{urls.Count} item(ns) adicionado(s).";
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        if (Downloads.Count == 0) { Add_Click(sender, e); if (Downloads.Count == 0) return; }
        if (!TryReadAdvancedSettings()) return;
        try
        {
            SetButtons(false);
            var executable = await _service.EnsureYtDlpAsync(new Progress<string>(s => StatusText.Text = s), _shutdown.Token);
            foreach (var item in Downloads.Where(x => x.Status is "Aguardando" or "ERROR"))
            {
                item.Status = "Processando";
                try
                {
                    await _service.DownloadAsync(item, executable, _settings,
                        new Progress<string>(s => UpdateProgress(item, s)), _shutdown.Token);
                    item.Progress = "100%"; item.Status = "Concluído";
                }
                catch (OperationCanceledException) { item.Status = "Cancelado"; }
                catch (InvalidOperationException ex) { SetItemError(item, ex.Message); }
                catch (IOException ex) { SetItemError(item, ex.Message); }
                catch (System.ComponentModel.Win32Exception ex) { SetItemError(item, ex.Message); }
            }
            StatusText.Text = "Downloads concluídos.";
        }
        catch (HttpRequestException ex) { WpfMessageBox.Show("Não foi possível baixar o yt-dlp. Verifique sua conexão e tente novamente.\n\n" + ex.Message, "Falha de rede", MessageBoxButton.OK, MessageBoxImage.Error); }
        catch (IOException ex) { WpfMessageBox.Show("Não foi possível acessar um arquivo ou pasta.\n\n" + ex.Message, "Falha de arquivo", MessageBoxButton.OK, MessageBoxImage.Error); }
        catch (InvalidOperationException ex) { WpfMessageBox.Show("O yt-dlp não pôde ser preparado.\n\n" + ex.Message, "Falha ao preparar", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { SetButtons(true); }
    }

    private void UpdateProgress(DownloadItem item, string serialized)
    {
        try
        {
            using var doc = JsonDocument.Parse(serialized);
            var values = doc.RootElement.EnumerateArray().Select(x => x.GetString() ?? "").ToArray();
            if (values.Length != 6) return;
            item.Size = values[1]; item.Progress = values[2]; item.Speed = values[3]; item.Eta = values[4];
            item.Title = values[5]; item.Status = values[0] == "finished" ? "Convertendo" : "Baixando";
        }
        catch (JsonException) { }
    }

    private void ClearDownloads_Click(object sender, RoutedEventArgs e) => Downloads.Clear();
    private void ClearUrls_Click(object sender, RoutedEventArgs e) => UrlsTextBox.Clear();
    private void ModelComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UpdateModelHint();
    private void UpdateModelHint()
    {
        if (ModelHintTextBlock is not null && ModelComboBox.SelectedItem is YtDlpModel model)
            ModelHintTextBlock.Text = model.Hint;
    }
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
    private void OpenBinFolder_Click(object sender, RoutedEventArgs e) => OpenFolder(_service.BinDirectory);
    private void OpenLogFolder_Click(object sender, RoutedEventArgs e) => OpenFolder(_service.LogDirectory);
    private static void OpenFolder(string path) { Directory.CreateDirectory(path); Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true }); }
    private void About_Click(object sender, RoutedEventArgs e) => WpfMessageBox.Show("yt-dlp-gui para Windows\nInterface WPF para yt-dlp.", "Sobre");
    private void SetButtons(bool enabled) { foreach (var button in FindVisualChildren<System.Windows.Controls.Button>(this)) button.IsEnabled = enabled; }
    private bool TryReadAdvancedSettings()
    {
        _settings.IgnoreErrors = IgnoreErrorsCheckBox.IsChecked == true;
        _settings.AbortOnError = AbortOnErrorCheckBox.IsChecked == true;
        if (_settings.IgnoreErrors && _settings.AbortOnError)
        {
            WpfMessageBox.Show("Escolha apenas uma política: ignorar erros ou interromper no primeiro erro.",
                "Configuração avançada", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        _settings.FlatPlaylist = FlatPlaylistCheckBox.IsChecked == true;
        _settings.LiveFromStart = LiveFromStartCheckBox.IsChecked == true;
        _settings.MarkWatched = MarkWatchedCheckBox.IsChecked == true;
        _settings.WaitForVideo = WaitForVideoTextBox.Text.Trim();
        if (_settings.WaitForVideo.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(
                _settings.WaitForVideo, @"^\d+(-\d+)?$"))
        {
            WpfMessageBox.Show("A espera deve ser um número de segundos ou intervalo, por exemplo: 30 ou 30-60.",
                "Configuração avançada", MessageBoxButton.OK, MessageBoxImage.Warning);
            WaitForVideoTextBox.Focus();
            return false;
        }
        _settings.JsRuntime = JsRuntimeTextBox.Text.Trim();
        _settings.ConfigLocation = ConfigLocationTextBox.Text.Trim();
        _settings.PluginDirectory = PluginDirectoryTextBox.Text.Trim();
        _settings.RemoteComponents = RemoteComponentsTextBox.Text.Trim();
        return true;
    }

    private static bool IsSupportedUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
        !string.IsNullOrWhiteSpace(uri.Host) && !uri.Host.Contains(' ');
    private void SetItemError(DownloadItem item, string message)
    {
        item.Status = "ERRO";
        StatusText.Text = string.IsNullOrWhiteSpace(message)
            ? "O download falhou. Consulte a saída do yt-dlp para mais detalhes."
            : message;
    }
    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in FindVisualChildren<T>(child)) yield return nested;
        }
    }
    protected override void OnClosed(EventArgs e) { _shutdown.Cancel(); _shutdown.Dispose(); base.OnClosed(e); }
}

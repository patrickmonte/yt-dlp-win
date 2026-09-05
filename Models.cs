using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YtDlpGui;

public sealed class DownloadItem : INotifyPropertyChanged
{
    private string _title = "";
    private string _size = "";
    private string _progress = "";
    private string _status = "Aguardando";
    private string _speed = "";
    private string _eta = "";

    public DownloadItem(string url, string path, string preset, YtDlpModel model)
    {
        Url = url;
        Path = path;
        Preset = preset;
        Model = model;
    }

    public string Url { get; }
    public string Path { get; }
    public string Preset { get; }
    public YtDlpModel Model { get; }
    public string Title { get => _title; set => Set(ref _title, value); }
    public string Size { get => _size; set => Set(ref _size, value); }
    public string Progress { get => _progress; set => Set(ref _progress, value); }
    public string Status { get => _status; set => Set(ref _status, value); }
    public string Speed { get => _speed; set => Set(ref _speed, value); }
    public string Eta { get => _eta; set => Set(ref _eta, value); }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

}

public sealed class YtDlpSettings
{
    public bool IgnoreErrors { get; set; }
    public bool AbortOnError { get; set; }
    public bool FlatPlaylist { get; set; }
    public bool LiveFromStart { get; set; }
    public bool MarkWatched { get; set; }
    public string WaitForVideo { get; set; } = "";
    public string JsRuntime { get; set; } = "";
    public string ConfigLocation { get; set; } = "";
    public string PluginDirectory { get; set; } = "";
    public string RemoteComponents { get; set; } = "";
}

public sealed class YtDlpModel
{
    public YtDlpModel(string name, string hint, params string[] arguments)
    {
        Name = name;
        Hint = hint;
        Arguments = arguments;
    }

    public string Name { get; }
    public string Hint { get; }
    public IReadOnlyList<string> Arguments { get; }

    public static IReadOnlyList<YtDlpModel> All { get; } = new[]
    {
        new YtDlpModel("Personalizado", "Usa apenas o preset e as configurações avançadas selecionadas."),
        new YtDlpModel("Nome fixo", "Salva como video.ext, mantendo a extensão correta.",
            "-o", "video.%(ext)s"),
        new YtDlpModel("Título original", "Usa o título do vídeo como nome do arquivo.",
            "-o", "%(title)s.%(ext)s"),
        new YtDlpModel("Título seguro", "Remove caracteres especiais para facilitar o uso em qualquer sistema.",
            "--restrict-filenames", "-o", "%(title)s.%(ext)s"),
        new YtDlpModel("Playlist numerada", "Cria uma pasta da playlist e prefixa cada vídeo com sua posição.",
            "-o", "%(playlist)s/%(playlist_index)s - %(title)s.%(ext)s"),
        new YtDlpModel("Organizar por ano", "Separa os vídeos em pastas pelo ano de envio.",
            "-o", "%(upload_date>%Y)s/%(title)s.%(ext)s"),
        new YtDlpModel("Canal e playlists", "Organiza por canal, playlist e ordem do vídeo.",
            "-o", "%(uploader)s/%(playlist)s/%(playlist_index)s - %(title)s.%(ext)s"),
        new YtDlpModel("Melhor vídeo e áudio", "Baixa os melhores fluxos separados e mescla quando possível.",
            "-f", "bv+ba/b"),
        new YtDlpModel("Melhor MP4", "Prioriza vídeo MP4 e áudio M4A, usando outra opção se não existir.",
            "-f", "bv*[ext=mp4]+ba[ext=m4a]/b[ext=mp4]/bv*+ba/b"),
        new YtDlpModel("Até 480p", "Escolhe a melhor qualidade até 480p.",
            "-f", "bv*[height<=480]+ba/b[height<=480] / wv*+ba/w"),
        new YtDlpModel("Até 50 MB", "Limita o arquivo a 50 MB quando houver uma opção compatível.",
            "-f", "b[filesize<50M] / w"),
        new YtDlpModel("Melhor protocolo", "Prefere protocolos de entrega mais eficientes.",
            "-S", "proto"),
        new YtDlpModel("Codec H.264/H.265", "Prefere codecs H.264 ou H.265 e recua para a melhor opção disponível.",
            "-f", "(bv*[vcodec~='^((he|a)vc|h26[45])']+ba) / (bv*+ba/b)"),
        new YtDlpModel("720p com maior FPS", "Escolhe até 720p, priorizando a maior taxa de quadros.",
            "-S", "res:720,fps")
    };
}

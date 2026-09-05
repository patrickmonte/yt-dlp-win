using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace YtDlpGui;

public sealed class YtDlpService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };
    private readonly string _binDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "yt-dlp-gui", "bin");
    private readonly string _logDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "yt-dlp-gui", "logs");

    public string BinDirectory => _binDirectory;
    public string LogDirectory => _logDirectory;

    public async Task<string> EnsureYtDlpAsync(IProgress<string>? progress, CancellationToken token)
    {
        Directory.CreateDirectory(_binDirectory);
        Directory.CreateDirectory(_logDirectory);
        var local = Path.Combine(_binDirectory, "yt-dlp.exe");
        if (File.Exists(local)) return local;

        progress?.Report("Baixando yt-dlp...");
        using var response = await Http.GetAsync(
            "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe",
            HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = File.Create(local);
        await input.CopyToAsync(output, token);
        return local;
    }

    public async Task DownloadAsync(DownloadItem item, string executable, YtDlpSettings settings,
        IProgress<string>? progress,
        CancellationToken token)
    {
        var psi = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true };
        psi.ArgumentList.Add("--newline");
        psi.ArgumentList.Add("--no-simulate");
        psi.ArgumentList.Add("--progress");
        psi.ArgumentList.Add("--progress-template");
        psi.ArgumentList.Add("%(progress.status)s__SEP__%(progress._total_bytes_estimate_str)s__SEP__%(progress._percent_str)s__SEP__%(progress._speed_str)s__SEP__%(progress._eta_str)s__SEP__%(info.title)s");
        psi.ArgumentList.Add("-P");
        psi.ArgumentList.Add(item.Path);
        foreach (var argument in PresetArguments(item.Preset)) psi.ArgumentList.Add(argument);
        foreach (var argument in item.Model.Arguments) psi.ArgumentList.Add(argument);
        foreach (var argument in AdvancedArguments(settings)) psi.ArgumentList.Add(argument);
        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add(item.Url);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.Start();
        using var registration = token.Register(() =>
        {
            try
            {
                if (!process.HasExited) process.Kill(true);
            }
            catch (InvalidOperationException) { }
            catch (Win32Exception) { }
        });
        var errors = new List<string>();
        var outputTask = ReadOutputAsync(process.StandardOutput, item, progress, token);
        var errorTask = ReadErrorsAsync(process.StandardError, errors, token);
        await process.WaitForExitAsync(token);
        await Task.WhenAll(outputTask, errorTask);
        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors).Trim());
    }

    private static async Task ReadOutputAsync(StreamReader reader, DownloadItem item, IProgress<string>? progress,
        CancellationToken token)
    {
        while (await reader.ReadLineAsync(token) is { } line)
        {
            if (line.Contains("__SEP__", StringComparison.Ordinal))
            {
                var values = line.Split("__SEP__", 6);
                if (values.Length == 6) progress?.Report(JsonSerializer.Serialize(values));
            }
            else if (line.StartsWith("[Merger]", StringComparison.Ordinal) ||
                     line.StartsWith("[ExtractAudio]", StringComparison.Ordinal))
                item.Status = "Convertendo";
        }
    }

    private static async Task ReadErrorsAsync(StreamReader reader, List<string> errors, CancellationToken token)
    {
        while (await reader.ReadLineAsync(token) is { } line)
            if (line.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase)) errors.Add(line);
    }

    private static IEnumerable<string> PresetArguments(string preset) => preset switch
    {
        "mp4" => new[] { "-f", "bv*[vcodec^=avc]+ba[ext=m4a]/b" },
        "mp3" => new[] { "--extract-audio", "--audio-format", "mp3", "--audio-quality", "0" },
        _ => new[] { "-f", "bv*[ext=mp4]+ba[ext=m4a]/b[ext=mp4]/bv*+ba/b" }
    };

    private static IEnumerable<string> AdvancedArguments(YtDlpSettings settings)
    {
        if (settings.IgnoreErrors) yield return "--ignore-errors";
        if (settings.AbortOnError) yield return "--abort-on-error";
        if (settings.FlatPlaylist) yield return "--flat-playlist";
        if (settings.LiveFromStart) yield return "--live-from-start";
        if (settings.MarkWatched) yield return "--mark-watched";
        if (!string.IsNullOrWhiteSpace(settings.WaitForVideo))
        {
            yield return "--wait-for-video";
            yield return settings.WaitForVideo.Trim();
        }
        if (!string.IsNullOrWhiteSpace(settings.JsRuntime))
        {
            yield return "--js-runtimes";
            yield return settings.JsRuntime.Trim();
        }
        if (!string.IsNullOrWhiteSpace(settings.ConfigLocation))
        {
            yield return "--config-locations";
            yield return settings.ConfigLocation.Trim();
        }
        if (!string.IsNullOrWhiteSpace(settings.PluginDirectory))
        {
            yield return "--plugin-dirs";
            yield return settings.PluginDirectory.Trim();
        }
        foreach (var component in settings.RemoteComponents.Split(',', StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            yield return "--remote-components";
            yield return component;
        }
    }
}

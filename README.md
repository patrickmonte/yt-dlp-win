# yt-dlp-gui para Windows

Reimplementação em C#/.NET 8 da interface gráfica do projeto
[dsymbol/yt-dlp-gui](https://github.com/dsymbol/yt-dlp-gui), usando WPF e compatível com
Windows 10/11.

## Recursos

- URLs múltiplas, uma por linha.
- Escolha da pasta de destino.
- Presets `best`, `mp4` e `mp3`.
- Modelos inspirados nos exemplos do yt-dlp, com hint explicativo e argumentos
  aplicados por item da fila.
- Fila de downloads com título, tamanho, progresso, velocidade, ETA e status.
- Download automático do executável `yt-dlp.exe` em
  `%LOCALAPPDATA%\yt-dlp-gui\bin`.
- Menus para abrir as pastas de binários/logs, limpar URLs e consultar informações.
- Configurações avançadas para política de erros, playlists, lives, espera por vídeos,
  runtimes JavaScript, plugins e componentes remotos.
- Validação de URLs por linha, mensagens de erro orientadas à ação e botões com ícones.

## Compilação

Requer o .NET 8 SDK e o workload de desktop do Windows:

```powershell
dotnet build .\yt-dlp-win.csproj --configuration Release
dotnet run --project .\yt-dlp-win.csproj
```

Para os presets que fazem conversão ou mesclagem, instale `ffmpeg.exe` e
`ffprobe.exe` no `PATH` do Windows ou em `%LOCALAPPDATA%\yt-dlp-gui\bin`.

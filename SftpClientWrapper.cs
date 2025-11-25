using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;
using System.IO;

namespace FtpSyncApp;

/// <summary>
/// Informations minimales sur un fichier distant remontées par le serveur.
/// </summary>
public class RemoteFileInfo
{
    public string FullPath { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
}

/// <summary>
/// Interface abstraite pour un client distant (SFTP aujourd'hui,
/// potentiellement FTP plus tard).
/// </summary>
public interface IRemoteClient : IDisposable
{
    Task ConnectAsync();
    Task<IDictionary<string, RemoteFileInfo>> ListFilesRecursiveAsync(string root, CancellationToken token);
    Task UploadFileAsync(string localPath, string remotePath, CancellationToken token);
    Task DeleteFileAsync(string remotePath, CancellationToken token);
    Task EnsureDirectoryAsync(string remoteDirectory, CancellationToken token);

    /// <summary>
    /// Crée un fichier distant VIDE.
    /// Utilisé pour les “stubs” Bunny (extensions .mp4, .ts, etc.).
    /// </summary>
    Task CreateEmptyFileAsync(string remotePath, CancellationToken token);
}

/// <summary>
/// Implémentation SFTP du client distant basée sur SSH.NET.
/// </summary>
public sealed class SftpClientWrapper : IRemoteClient
{
    private readonly SftpClient _client;

    public SftpClientWrapper(string host, int port, string username, string password)
    {
        _client = new SftpClient(host, port, username, password);
    }

    /// <inheritdoc />
    public Task ConnectAsync()
    {
        return Task.Run(() => _client.Connect());
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_client.IsConnected)
        {
            _client.Disconnect();
        }

        _client.Dispose();
    }

    /// <inheritdoc />
    public async Task<IDictionary<string, RemoteFileInfo>> ListFilesRecursiveAsync(
        string root,
        CancellationToken token)
    {
        var result = new Dictionary<string, RemoteFileInfo>(StringComparer.OrdinalIgnoreCase);
        var normalizedRoot = NormalizePath(root);

        await Task.Run(() => Walk(normalizedRoot), token);

        void Walk(string path)
        {
            if (token.IsCancellationRequested)
                return;

            foreach (var entry in _client.ListDirectory(path))
            {
                if (entry.Name is "." or "..")
                    continue;

                var fullPath = NormalizePath(entry.FullName);

                if (entry.IsDirectory)
                {
                    Walk(fullPath);
                }
                else
                {
                    result[fullPath] = new RemoteFileInfo
                    {
                        FullPath = fullPath,
                        Size = entry.Attributes.Size,
                        LastWriteTimeUtc = entry.LastWriteTimeUtc
                    };
                }
            }
        }

        return result;
    }

    /// <inheritdoc />
    public Task UploadFileAsync(string localPath, string remotePath, CancellationToken token)
    {
        var normalizedRemote = NormalizePath(remotePath);

        return Task.Run(() =>
        {
            var info = new FileInfo(localPath);
            using var stream = File.OpenRead(localPath);

            // Upload (overwrite = true)
            _client.UploadFile(stream, normalizedRemote, true);

            // On force la date distante à être la même que celle du fichier local
            _client.SetLastWriteTime(normalizedRemote, info.LastWriteTimeUtc);
        }, token);
    }

    /// <inheritdoc />
    public Task DeleteFileAsync(string remotePath, CancellationToken token)
    {
        var normalizedRemote = NormalizePath(remotePath);
        return Task.Run(() => _client.DeleteFile(normalizedRemote), token);
    }

    /// <inheritdoc />
    public Task EnsureDirectoryAsync(string remoteDirectory, CancellationToken token)
    {
        var normalizedDir = NormalizePath(remoteDirectory);

        return Task.Run(() =>
        {
            var parts = normalizedDir.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var current = "/";

            foreach (var part in parts)
            {
                current = current.EndsWith("/") ? current + part : current + "/" + part;

                if (!_client.Exists(current))
                {
                    _client.CreateDirectory(current);
                }
            }
        }, token);
    }

    /// <inheritdoc />
    public Task CreateEmptyFileAsync(string remotePath, CancellationToken token)
    {
        var normalized = NormalizePath(remotePath);

        return Task.Run(() =>
        {
            // On crée un flux SFTP vide → fichier = 0 octet
            using var stream = _client.Create(normalized);
            // NE RIEN ÉCRIRE : stub Bunny
        }, token);
    }

    /// <summary>
    /// Normalise un chemin pour l'usage SFTP : séparateur "/", commence par "/".
    /// </summary>
    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "/";

        var p = path.Replace('\\', '/');
        if (!p.StartsWith("/"))
            p = "/" + p;

        return p;
    }
}

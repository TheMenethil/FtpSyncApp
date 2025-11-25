using System;

namespace FtpSyncApp;

/// <summary>
/// Paramètres persistés entre les lancements de l'application.
/// Stocké en JSON dans %APPDATA%\FtpSyncApp\settings.json.
/// </summary>
public class AppSettings
{
    public string LocalRoot { get; set; } = string.Empty;
    public string RemoteRoot { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Indique si la connexion doit utiliser SFTP (sinon FTP).
    /// </summary>
    public bool UseSftp { get; set; } = true;

    /// <summary>
    /// Indique si le mot de passe doit être mémorisé.
    /// </summary>
    public bool RememberPassword { get; set; }

    /// <summary>
    /// Mot de passe mémorisé (optionnel, seulement si RememberPassword = true).
    /// </summary>
    public string? Password { get; set; }
}
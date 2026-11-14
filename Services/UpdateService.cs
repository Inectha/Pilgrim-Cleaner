// Lumina Cleaner - Enterprise Windows Disk Cleaner & Optimizer
// Copyright (C) 2026 Pilgrim Software
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LuminaCleaner.Interop;
using LuminaCleaner.Models;

namespace LuminaCleaner.Services;

/// <summary>
/// Queries the official GitHub Releases API for newer Pilgrim Cleaner releases,
/// verifies the downloaded Inno Setup installer against SHA256SUMS.txt before execution,
/// and coordinates a silent update + application restart.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    public const string LatestReleaseApiUrl = "https://api.github.com/repos/Inectha/Pilgrim-Cleaner/releases/latest";
    public const string InstallerAssetName = "PilgrimCleanerSetup.exe";
    public const string ChecksumsAssetName = "SHA256SUMS.txt";

    private static readonly HttpClient SharedHttpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PilgrimCleaner", AppInfo.Version));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            using var response = await SharedHttpClient.GetAsync(LatestReleaseApiUrl, HttpCompletionOption.ResponseContentRead, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult
                {
                    IsUpdateAvailable = false,
                    ErrorMessage = $"GitHub Releases API returned HTTP {(int)response.StatusCode}."
                };
            }

            string json = await response.Content.ReadAsStringAsync(cts.Token);
            return ParseReleaseJson(json, AppInfo.Version);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult
            {
                IsUpdateAvailable = false,
                ErrorMessage = $"Could not check for updates: {ex.Message}"
            };
        }
    }

    public static UpdateCheckResult ParseReleaseJson(string json, string currentVersionString)
    {
        if (!TryParseSemanticVersion(currentVersionString, out Version? currentVersion) || currentVersion is null)
        {
            currentVersion = new Version(0, 0, 0);
        }

        using var doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        if (root.TryGetProperty("draft", out JsonElement draftEl) && draftEl.ValueKind == JsonValueKind.True)
        {
            return new UpdateCheckResult { IsUpdateAvailable = false, CurrentVersion = currentVersion.ToString(3) };
        }

        if (root.TryGetProperty("prerelease", out JsonElement preEl) && preEl.ValueKind == JsonValueKind.True)
        {
            return new UpdateCheckResult { IsUpdateAvailable = false, CurrentVersion = currentVersion.ToString(3) };
        }

        string tagName = root.TryGetProperty("tag_name", out JsonElement tagEl) && tagEl.ValueKind == JsonValueKind.String
            ? tagEl.GetString() ?? string.Empty
            : string.Empty;

        if (!TryParseSemanticVersion(tagName, out Version? latestVersion) || latestVersion is null)
        {
            return new UpdateCheckResult
            {
                IsUpdateAvailable = false,
                CurrentVersion = currentVersion.ToString(3),
                ErrorMessage = "Latest release tag is not a valid semantic version."
            };
        }

        string releaseUrl = root.TryGetProperty("html_url", out JsonElement urlEl) && urlEl.ValueKind == JsonValueKind.String
            ? urlEl.GetString() ?? string.Empty
            : string.Empty;

        string releaseNotes = root.TryGetProperty("body", out JsonElement bodyEl) && bodyEl.ValueKind == JsonValueKind.String
            ? bodyEl.GetString() ?? string.Empty
            : string.Empty;

        string installerUrl = string.Empty;
        string checksumsUrl = string.Empty;
        string digestSha256 = string.Empty;
        long installerSize = 0;

        if (root.TryGetProperty("assets", out JsonElement assetsEl) && assetsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement asset in assetsEl.EnumerateArray())
            {
                string name = asset.TryGetProperty("name", out JsonElement nameEl) && nameEl.ValueKind == JsonValueKind.String
                    ? nameEl.GetString() ?? string.Empty
                    : string.Empty;

                string downloadUrl = asset.TryGetProperty("browser_download_url", out JsonElement dlEl) && dlEl.ValueKind == JsonValueKind.String
                    ? dlEl.GetString() ?? string.Empty
                    : string.Empty;

                if (string.Equals(name, InstallerAssetName, StringComparison.OrdinalIgnoreCase))
                {
                    if (IsTrustedGitHubUrl(downloadUrl))
                    {
                        installerUrl = downloadUrl;
                    }

                    if (asset.TryGetProperty("size", out JsonElement sizeEl) && sizeEl.TryGetInt64(out long sz))
                    {
                        installerSize = sz;
                    }

                    if (asset.TryGetProperty("digest", out JsonElement digestEl) && digestEl.ValueKind == JsonValueKind.String)
                    {
                        string rawDigest = digestEl.GetString() ?? string.Empty;
                        if (rawDigest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                        {
                            string candidate = rawDigest["sha256:".Length..].Trim();
                            if (IsValidSha256Hex(candidate))
                            {
                                digestSha256 = candidate.ToUpperInvariant();
                            }
                        }
                    }
                }
                else if (string.Equals(name, ChecksumsAssetName, StringComparison.OrdinalIgnoreCase))
                {
                    if (IsTrustedGitHubUrl(downloadUrl))
                    {
                        checksumsUrl = downloadUrl;
                    }
                }
            }
        }

        bool isNewer = latestVersion > currentVersion && !string.IsNullOrEmpty(installerUrl);

        return new UpdateCheckResult
        {
            IsUpdateAvailable = isNewer,
            CurrentVersion = currentVersion.ToString(3),
            LatestVersion = latestVersion.ToString(3),
            LatestTag = $"v{latestVersion.ToString(3)}",
            ReleaseNotes = releaseNotes,
            ReleaseUrl = IsTrustedGitHubUrl(releaseUrl) ? releaseUrl : string.Empty,
            InstallerDownloadUrl = installerUrl,
            ChecksumsDownloadUrl = checksumsUrl,
            ExpectedSha256 = digestSha256,
            InstallerSizeBytes = installerSize
        };
    }

    public async Task<string> DownloadAndVerifyInstallerAsync(
        UpdateCheckResult update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (update is null || !IsTrustedGitHubUrl(update.InstallerDownloadUrl))
        {
            throw new InvalidOperationException("Refusing to download update from an untrusted or missing URL.");
        }

        string expectedHash = update.ExpectedSha256;

        // Always prefer SHA256SUMS.txt from the release when available.
        if (IsTrustedGitHubUrl(update.ChecksumsDownloadUrl))
        {
            string sumsContent = await SharedHttpClient.GetStringAsync(update.ChecksumsDownloadUrl, cancellationToken);
            string? parsedHash = ParseSha256ForAsset(sumsContent, InstallerAssetName);
            if (!string.IsNullOrEmpty(parsedHash))
            {
                expectedHash = parsedHash;
            }
        }

        if (!IsValidSha256Hex(expectedHash))
        {
            throw new InvalidDataException("Refusing to install update without a valid 64-character SHA-256 checksum.");
        }

        string updateDir = Path.Combine(Path.GetTempPath(), $"PilgrimCleaner-Update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(updateDir);

        var dirInfo = new DirectoryInfo(updateDir);
        if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Refusing to use a reparse point directory for update staging.");
        }

        string destinationPath = Path.Combine(updateDir, InstallerAssetName);

        try
        {
            using var response = await SharedHttpClient.GetAsync(
                update.InstallerDownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            long? totalBytes = response.Content.Headers.ContentLength ?? (update.InstallerSizeBytes > 0 ? update.InstallerSizeBytes : null);

            // Hold an exclusive write lock for both downloading and SHA-256 verification.
            await using (var fileStream = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true))
            {
                await using (var networkStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                {
                    byte[] buffer = new byte[81920];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await networkStream.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        totalRead += bytesRead;

                        if (totalBytes is > 0)
                        {
                            double pct = Math.Clamp((double)totalRead / totalBytes.Value * 95.0, 0.0, 95.0);
                            progress?.Report(pct);
                        }
                    }
                }

                await fileStream.FlushAsync(cancellationToken);
                VerifyStreamSha256(fileStream, expectedHash);
            }

            progress?.Report(100.0);
            return destinationPath;
        }
        catch
        {
            TryCleanupStagingFile(destinationPath, updateDir);
            throw;
        }
    }

    public static void VerifyStreamSha256(Stream stream, string expectedSha256Hex)
    {
        if (!IsValidSha256Hex(expectedSha256Hex))
        {
            throw new InvalidDataException("Expected SHA-256 checksum is invalid.");
        }

        stream.Position = 0;
        byte[] actualBytes = SHA256.HashData(stream);
        byte[] expectedBytes = Convert.FromHexString(expectedSha256Hex.Trim());

        if (!CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes))
        {
            string actualHex = Convert.ToHexString(actualBytes);
            throw new InvalidDataException(
                $"SHA-256 checksum mismatch for downloaded installer (expected {expectedSha256Hex.ToUpperInvariant()}, got {actualHex}). Update aborted.");
        }
    }

    public bool LaunchInstallerAndRequestRestart(string verifiedInstallerPath, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(verifiedInstallerPath) || !File.Exists(verifiedInstallerPath))
        {
            errorMessage = "Verified installer file was not found.";
            return false;
        }

        string? parentDir = Path.GetDirectoryName(verifiedInstallerPath);
        if (string.IsNullOrEmpty(parentDir))
        {
            errorMessage = "Invalid installer directory.";
            return false;
        }

        var parentInfo = new DirectoryInfo(parentDir);
        if (!parentInfo.Exists || (parentInfo.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            errorMessage = "Refusing to launch installer from a reparse point directory.";
            return false;
        }

        try
        {
            var setupPsi = new ProcessStartInfo
            {
                FileName = verifiedInstallerPath,
                Arguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /relaunch=1",
                UseShellExecute = true
            };

            using Process? setupProcess = Process.Start(setupPsi);
            if (setupProcess is null)
            {
                errorMessage = "Installer process could not be started.";
                return false;
            }

            // Start a background watcher using SystemTools.PowerShell (-EncodedCommand with Base64 data)
            // so the temporary setup folder is cleaned up and Pilgrim Cleaner is guaranteed to restart
            // once the Inno Setup process finishes.
            try
            {
                string defaultInstalledExe = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Pilgrim Cleaner",
                    "PilgrimCleaner.exe");

                string script = BuildRelaunchScript(setupProcess.Id, parentDir, defaultInstalledExe);
                var watcherPsi = new ProcessStartInfo
                {
                    FileName = SystemTools.PowerShell,
                    Arguments = "-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -EncodedCommand " +
                                Convert.ToBase64String(Encoding.Unicode.GetBytes(script)),
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using Process? watcher = Process.Start(watcherPsi);
            }
            catch (Exception watcherEx)
            {
                Debug.WriteLine($"Update cleanup/relaunch watcher warning: {watcherEx.Message}");
            }

            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED (UAC prompt declined)
        {
            errorMessage = "Administrator permission (UAC) was declined. Update cancelled.";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to launch installer: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Builds an injection-safe PowerShell script that waits for the installer PID to finish,
    /// cleans up the staging folder, and starts PilgrimCleaner.exe if Inno Setup did not already relaunch it.
    /// External strings are passed exclusively as Base64 literals.
    /// </summary>
    public static string BuildRelaunchScript(int setupProcessId, string stagingDirectory, string installedExePath)
    {
        string dirB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(stagingDirectory ?? string.Empty));
        string exeB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(installedExePath ?? string.Empty));

        return $@"
$ErrorActionPreference = 'SilentlyContinue'
$setupPid = {setupProcessId}
$stagingDir = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String('{dirB64}'))
$exePath = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String('{exeB64}'))

Wait-Process -Id $setupPid -Timeout 600 -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

if ($stagingDir -and (Test-Path -LiteralPath $stagingDir)) {{
    Remove-Item -LiteralPath $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
}}

if (-not (Get-Process -Name 'PilgrimCleaner' -ErrorAction SilentlyContinue)) {{
    if ($exePath -and (Test-Path -LiteralPath $exePath)) {{
        Start-Process -FilePath $exePath
    }}
}}
";
    }

    public static string? ParseSha256ForAsset(string sha256SumsContent, string assetFileName)
    {
        if (string.IsNullOrWhiteSpace(sha256SumsContent) || string.IsNullOrWhiteSpace(assetFileName))
        {
            return null;
        }

        string[] lines = sha256SumsContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.Length < 66)
            {
                continue;
            }

            // Format produced by build-and-release.yml: "<64-hex-sha256>  PilgrimCleanerSetup.exe"
            Match match = Regex.Match(line, @"^(?<hash>[0-9a-fA-F]{64})\s+\*?(?<file>.+)$");
            if (!match.Success)
            {
                continue;
            }

            string file = Path.GetFileName(match.Groups["file"].Value.Trim());
            if (string.Equals(file, assetFileName, StringComparison.OrdinalIgnoreCase))
            {
                return match.Groups["hash"].Value.ToUpperInvariant();
            }
        }

        return null;
    }

    public static bool TryParseSemanticVersion(string? raw, out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string cleaned = raw.Trim();
        if (cleaned.StartsWith('v') || cleaned.StartsWith('V'))
        {
            cleaned = cleaned[1..];
        }

        int dashIdx = cleaned.IndexOf('-');
        if (dashIdx > 0)
        {
            // Pre-release suffix (e.g. 1.2.3-beta1) is not treated as a stable release version.
            return false;
        }

        if (!Version.TryParse(cleaned, out Version? parsed) || parsed is null)
        {
            return false;
        }

        int major = Math.Max(0, parsed.Major);
        int minor = Math.Max(0, parsed.Minor);
        int build = Math.Max(0, parsed.Build);
        version = new Version(major, minor, build);
        return true;
    }

    public static bool IsTrustedGitHubUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string host = uri.Host.ToLowerInvariant();
        return host is "github.com" or "api.github.com" or "objects.githubusercontent.com";
    }

    public static bool IsValidSha256Hex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || hex.Trim().Length != 64)
        {
            return false;
        }

        foreach (char c in hex.Trim())
        {
            bool isHex = (c >= '0' && c <= '9') ||
                         (c >= 'a' && c <= 'f') ||
                         (c >= 'A' && c <= 'F');
            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }

    private static void TryCleanupStagingFile(string filePath, string dirPath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            if (Directory.Exists(dirPath))
            {
                Directory.Delete(dirPath, recursive: false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to clean up staging update file: {ex.Message}");
        }
    }
}

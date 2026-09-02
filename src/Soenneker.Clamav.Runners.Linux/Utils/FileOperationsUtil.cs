using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Soenneker.Clamav.Runners.Linux.Utils.Abstract;
using Soenneker.GitHub.Repositories.Releases.Abstract;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.File.Abstract;
using Soenneker.Utils.Process.Abstract;

namespace Soenneker.Clamav.Runners.Linux.Utils;

public sealed class FileOperationsUtil : IFileOperationsUtil
{
    private const string Owner = "Cisco-Talos";
    private const string Repository = "clamav";
    private static readonly string[] _assetPatterns = [".linux.x86_64.deb"];

    private readonly ILogger<FileOperationsUtil> _logger;
    private readonly IDirectoryUtil _directoryUtil;
    private readonly IGitHubRepositoriesReleasesUtil _releasesUtil;
    private readonly IFileUtil _fileUtil;
    private readonly IProcessUtil _processUtil;

    public FileOperationsUtil(ILogger<FileOperationsUtil> logger, IDirectoryUtil directoryUtil,
        IGitHubRepositoriesReleasesUtil releasesUtil, IFileUtil fileUtil, IProcessUtil processUtil)
    {
        _logger = logger;
        _directoryUtil = directoryUtil;
        _releasesUtil = releasesUtil;
        _fileUtil = fileUtil;
        _processUtil = processUtil;
    }

    public async ValueTask<string> Process(CancellationToken cancellationToken = default)
    {
        string downloadDirectory = await _directoryUtil.CreateTempDirectory(cancellationToken);
        string? asset = await _releasesUtil.DownloadReleaseAssetByNamePattern(Owner, Repository, downloadDirectory,
            _assetPatterns, cancellationToken);

        if (asset is null)
            throw new FileNotFoundException("Could not find the Linux x64 Debian package in the latest stable ClamAV release.");

        string extractDirectory = await _directoryUtil.CreateTempDirectory(cancellationToken);
        await _processUtil.Start("dpkg-deb", extractDirectory,
            $"--extract {Quote(asset)} {Quote(extractDirectory)}", log: false, cancellationToken: cancellationToken);

        string[] scanners = Directory.GetFiles(extractDirectory, "clamscan", SearchOption.AllDirectories);
        if (scanners.Length != 1)
            throw new FileNotFoundException("The ClamAV package did not contain exactly one clamscan executable.");

        string binDirectory = Path.GetDirectoryName(scanners[0])!;
        string stageDirectory = Directory.GetParent(binDirectory)!.FullName;

        string freshclamPath = Path.Combine(binDirectory, "freshclam");
        if (!await _fileUtil.Exists(freshclamPath, cancellationToken))
            throw new FileNotFoundException("The ClamAV package did not contain freshclam.", freshclamPath);

        RemoveDevelopmentFiles(stageDirectory);
        MaterializeSymbolicLinks(stageDirectory);

        await _fileUtil.Write(Path.Combine(stageDirectory, "SOURCE.txt"),
            $"Official release package from https://github.com/{Owner}/{Repository}/releases/latest{Environment.NewLine}" +
            $"Asset: {Path.GetFileName(asset)}{Environment.NewLine}" +
            $"Symbolic links are materialized for NuGet compatibility.{Environment.NewLine}",
            log: false, cancellationToken);

        _logger.LogInformation("Prepared Linux x64 ClamAV runtime at {StageDirectory}", stageDirectory);
        return stageDirectory;
    }

    private static void RemoveDevelopmentFiles(string stageDirectory)
    {
        foreach (string directory in new[]
                 {
                     Path.Combine(stageDirectory, "include"),
                     Path.Combine(stageDirectory, "share", "man"),
                     Path.Combine(stageDirectory, "lib", "pkgconfig")
                 })
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }

        foreach (string pattern in new[] { "*.a", "*.la" })
        {
            foreach (string file in Directory.EnumerateFiles(stageDirectory, pattern, SearchOption.AllDirectories))
                File.Delete(file);
        }
    }

    private static void MaterializeSymbolicLinks(string stageDirectory)
    {
        foreach (string path in Directory.EnumerateFiles(stageDirectory, "*", SearchOption.AllDirectories))
        {
            var file = new FileInfo(path);
            if (file.LinkTarget is null)
                continue;

            FileSystemInfo? target = file.ResolveLinkTarget(returnFinalTarget: true);
            if (target is not FileInfo targetFile)
                throw new IOException($"Could not resolve symbolic link '{path}'.");

            file.Delete();
            targetFile.CopyTo(path);
        }
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}

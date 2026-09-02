using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Soenneker.Clamav.Runners.Linux.Utils.Abstract;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
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
        string downloadDirectory = await _directoryUtil.CreateTempDirectory(cancellationToken).NoSync();
        string? asset = await _releasesUtil.DownloadReleaseAssetByNamePattern(Owner, Repository, downloadDirectory,
            _assetPatterns, cancellationToken).NoSync();

        if (asset is null)
            throw new FileNotFoundException("Could not find the Linux x64 Debian package in the latest stable ClamAV release.");

        string assetName = Path.GetFileName(asset);
        string releaseTag = GetReleaseTag(assetName, ".linux.x86_64.deb");

        string extractDirectory = await _directoryUtil.CreateTempDirectory(cancellationToken).NoSync();
        await _processUtil.Start("dpkg-deb", extractDirectory,
            $"--extract {Quote(asset)} {Quote(extractDirectory)}", log: false, cancellationToken: cancellationToken).NoSync();

        string[] files = await _fileUtil.GetAllFileNamesInDirectoryRecursively(extractDirectory, log: false, cancellationToken).NoSync();
        string[] scanners = files.Where(static file => Path.GetFileName(file).Equals("clamscan", StringComparison.Ordinal)).ToArray();
        if (scanners.Length != 1)
            throw new FileNotFoundException("The ClamAV package did not contain exactly one clamscan executable.");

        string binDirectory = Path.GetDirectoryName(scanners[0])!;
        string stageDirectory = Directory.GetParent(binDirectory)!.FullName;

        string freshclamPath = Path.Combine(binDirectory, "freshclam");
        if (!await _fileUtil.Exists(freshclamPath, cancellationToken).NoSync())
            throw new FileNotFoundException("The ClamAV package did not contain freshclam.", freshclamPath);

        await RemoveDevelopmentFiles(stageDirectory, cancellationToken).NoSync();
        await MaterializeSymbolicLinks(stageDirectory, cancellationToken).NoSync();

        await _fileUtil.Write(Path.Combine(stageDirectory, "SOURCE.txt"),
            $"Upstream project: https://github.com/{Owner}/{Repository}{Environment.NewLine}" +
            $"Release: https://github.com/{Owner}/{Repository}/releases/tag/{releaseTag}{Environment.NewLine}" +
            $"Binary asset: {assetName}{Environment.NewLine}" +
            $"Corresponding source: https://github.com/{Owner}/{Repository}/releases/download/{releaseTag}/{releaseTag}.tar.gz{Environment.NewLine}" +
            $"License: GPL-2.0-only; see COPYING.txt and COPYING/ in this directory.{Environment.NewLine}" +
            $"Symbolic links are materialized for NuGet compatibility.{Environment.NewLine}",
            log: false, cancellationToken).NoSync();

        _logger.LogInformation("Prepared Linux x64 ClamAV runtime at {StageDirectory}", stageDirectory);
        return stageDirectory;
    }

    public async ValueTask PrepareFreshclamRuntime(string stageDirectory, CancellationToken cancellationToken = default)
    {
        string binDirectory = Path.Combine(stageDirectory, "bin");
        string[] executables = Directory.GetFiles(binDirectory);

        foreach (string executable in executables)
        {
            if (!Path.GetFileName(executable).Equals("freshclam", StringComparison.Ordinal))
                await _fileUtil.Delete(executable, log: false, cancellationToken: cancellationToken).NoSync();
        }

        await _directoryUtil.DeleteIfExists(Path.Combine(stageDirectory, "sbin"), cancellationToken).NoSync();
        _logger.LogInformation("Reduced Linux runtime to FreshClam and supporting files at {StageDirectory}", stageDirectory);
    }

    private async ValueTask RemoveDevelopmentFiles(string stageDirectory, CancellationToken cancellationToken)
    {
        string[] files = await _fileUtil.GetAllFileNamesInDirectoryRecursively(stageDirectory, log: false, cancellationToken).NoSync();

        foreach (string directory in new[]
                 {
                     Path.Combine(stageDirectory, "include"),
                     Path.Combine(stageDirectory, "share", "man"),
                     Path.Combine(stageDirectory, "lib", "pkgconfig")
                 })
        {
            await _directoryUtil.DeleteIfExists(directory, cancellationToken).NoSync();
        }

        foreach (string file in files)
        {
            string extension = Path.GetExtension(file);
            if (extension.Equals(".a", StringComparison.Ordinal) || extension.Equals(".la", StringComparison.Ordinal))
                await _fileUtil.Delete(file, log: false, cancellationToken: cancellationToken).NoSync();
        }
    }

    private async ValueTask MaterializeSymbolicLinks(string stageDirectory, CancellationToken cancellationToken)
    {
        foreach (string path in Directory.EnumerateFiles(stageDirectory, "*", SearchOption.AllDirectories))
        {
            var file = new FileInfo(path);
            if (file.LinkTarget is null)
                continue;

            FileSystemInfo? target = file.ResolveLinkTarget(returnFinalTarget: true);
            if (target is not FileInfo targetFile)
                throw new IOException($"Could not resolve symbolic link '{path}'.");

            await _fileUtil.Delete(path, ignoreMissing: false, log: false, cancellationToken).NoSync();
            await _fileUtil.Copy(targetFile.FullName, path, log: false, cancellationToken).NoSync();
        }
    }

    private static string GetReleaseTag(string assetName, string suffix)
    {
        if (!assetName.StartsWith("clamav-", StringComparison.Ordinal) || !assetName.EndsWith(suffix, StringComparison.Ordinal))
            throw new InvalidDataException($"Could not determine the ClamAV release tag from asset '{assetName}'.");

        return assetName[..^suffix.Length];
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Soenneker.Clamav.Runners.Linux.Utils.Abstract;
using Soenneker.Extensions.ValueTask;
using Soenneker.Managers.Runners.Abstract;

namespace Soenneker.Clamav.Runners.Linux;

public sealed class ConsoleHostedService : IHostedService
{
    private readonly ILogger<ConsoleHostedService> _logger;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly IFileOperationsUtil _fileOperationsUtil;
    private readonly IRunnersManager _runnersManager;
    private int? _exitCode;

    public ConsoleHostedService(ILogger<ConsoleHostedService> logger, IHostApplicationLifetime appLifetime,
        IFileOperationsUtil fileOperationsUtil, IRunnersManager runnersManager)
    {
        _logger = logger;
        _appLifetime = appLifetime;
        _fileOperationsUtil = fileOperationsUtil;
        _runnersManager = runnersManager;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _appLifetime.ApplicationStarted.Register(() => Task.Run(async () =>
        {
            try
            {
                string stageDirectory = await _fileOperationsUtil.Process(cancellationToken).NoSync();
                await _runnersManager.PushIfChangesNeededForDirectory(Path.Combine(Constants.RuntimeIdentifier, "clamav"), stageDirectory,
                    Constants.Library, $"https://github.com/soenneker/{Constants.Library}", false, cancellationToken).NoSync();
                await _runnersManager.PushIfChangesNeededForDirectory(Path.Combine(Constants.RuntimeIdentifier, "freshclam"), stageDirectory,
                    Constants.FreshclamLibrary, $"https://github.com/soenneker/{Constants.FreshclamLibrary}", false, cancellationToken).NoSync();
                _exitCode = 0;
            }
            catch (Exception exception)
            {
                if (Debugger.IsAttached)
                    Debugger.Break();

                _logger.LogError(exception, "Could not update the Linux ClamAV packages");
                _exitCode = 1;
            }
            finally
            {
                _appLifetime.StopApplication();
            }
        }, cancellationToken));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Environment.ExitCode = _exitCode.GetValueOrDefault(-1);
        return Task.CompletedTask;
    }
}

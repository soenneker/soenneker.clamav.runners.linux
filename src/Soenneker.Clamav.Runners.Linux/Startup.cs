using Microsoft.Extensions.DependencyInjection;
using Soenneker.Clamav.Runners.Linux.Utils;
using Soenneker.Clamav.Runners.Linux.Utils.Abstract;
using Soenneker.GitHub.Repositories.Releases.Registrars;
using Soenneker.Managers.Runners.Registrars;
using Soenneker.Utils.Directory.Registrars;
using Soenneker.Utils.File.Registrars;
using Soenneker.Utils.Process.Registrars;

namespace Soenneker.Clamav.Runners.Linux;

public static class Startup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddHostedService<ConsoleHostedService>()
                .AddSingleton<IFileOperationsUtil, FileOperationsUtil>()
                .AddDirectoryUtilAsSingleton()
                .AddFileUtilAsSingleton()
                .AddProcessUtilAsSingleton()
                .AddGitHubRepositoriesReleasesUtilAsSingleton()
                .AddRunnersManagerAsSingleton();
    }
}

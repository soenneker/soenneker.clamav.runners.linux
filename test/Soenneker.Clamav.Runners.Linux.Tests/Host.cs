using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Soenneker.TestHosts.Unit;
using Soenneker.Utils.Test;

namespace Soenneker.Clamav.Runners.Linux.Tests;

public sealed class Host : UnitTestHost
{
    public override Task InitializeAsync()
    {
        Services.AddLogging(builder => builder.AddSerilog(dispose: false));
        IConfiguration configuration = TestUtil.BuildConfig();
        Services.AddSingleton(configuration);
        return base.InitializeAsync();
    }
}

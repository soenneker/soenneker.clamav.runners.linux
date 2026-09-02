using Soenneker.Tests.HostedUnit;

namespace Soenneker.Clamav.Runners.Linux.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class ClamavLinuxRunnerTests : HostedUnitTest
{
    public ClamavLinuxRunnerTests(Host host) : base(host)
    {
    }

    [Test]
    public void Default()
    {
    }
}

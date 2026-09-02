using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Clamav.Runners.Linux.Utils.Abstract;

public interface IFileOperationsUtil
{
    /// <summary>
    /// Downloads and extracts the latest stable official ClamAV Linux x64 distribution.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The directory containing the runtime files to package.</returns>
    ValueTask<string> Process(CancellationToken cancellationToken = default);
}

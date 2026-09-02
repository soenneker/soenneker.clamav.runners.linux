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

    /// <summary>
    /// Reduces an extracted ClamAV runtime to FreshClam and its supporting files.
    /// </summary>
    /// <param name="stageDirectory">The extracted runtime directory.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    ValueTask PrepareFreshclamRuntime(string stageDirectory, CancellationToken cancellationToken = default);
}

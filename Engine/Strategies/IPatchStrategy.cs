using System.Threading.Tasks;
using HmaxUnlocker.Engine.Logging;
using HmaxUnlocker.Engine.Models;

namespace HmaxUnlocker.Engine.Strategies
{
    public interface IPatchStrategy
    {
        ChipsetType SupportedChipset { get; }
        Task<PatchResult> ExecuteBypassAsync(EfsPaths paths, string targetCsc, ILogger logger);
        Task<LockStatus> DetectStatusAsync(EfsPaths paths, ILogger logger);
        Task<byte[]> BackupNvAsync(EfsPaths paths, ILogger logger);
        Task<bool> RestoreNvAsync(EfsPaths paths, byte[] backup, ILogger logger);
    }
}

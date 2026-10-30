using System.Threading.Tasks;
using HmaxUnlocker.Engine.Communication;
using HmaxUnlocker.Engine.Logging;
using HmaxUnlocker.Engine.Models;

namespace HmaxUnlocker.Engine.Strategies
{
    public class ExynosStrategy : IPatchStrategy
    {
        private readonly IAdbShell _adb;
        private readonly QualcommStrategy _qcom;
        public ChipsetType SupportedChipset => ChipsetType.Exynos;

        public ExynosStrategy(IAdbShell adb)
        {
            _adb = adb;
            _qcom = new QualcommStrategy(adb);
        }

        public Task<PatchResult> ExecuteBypassAsync(EfsPaths paths, string targetCsc, ILogger logger)
            => _qcom.ExecuteBypassAsync(paths, targetCsc, logger);

        public Task<LockStatus> DetectStatusAsync(EfsPaths paths, ILogger logger)
            => _qcom.DetectStatusAsync(paths, logger);

        public Task<byte[]> BackupNvAsync(EfsPaths paths, ILogger logger)
            => _qcom.BackupNvAsync(paths, logger);

        public Task<bool> RestoreNvAsync(EfsPaths paths, byte[] backup, ILogger logger)
            => _qcom.RestoreNvAsync(paths, backup, logger);
    }
}

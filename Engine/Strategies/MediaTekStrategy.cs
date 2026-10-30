using System;
using System.Text;
using System.Threading.Tasks;
using HmaxUnlocker.Engine.Communication;
using HmaxUnlocker.Engine.Efs;
using HmaxUnlocker.Engine.Logging;
using HmaxUnlocker.Engine.Models;

namespace HmaxUnlocker.Engine.Strategies
{
    public class MediaTekStrategy : IPatchStrategy
    {
        // قنوات AT المعروفة على منصات MTK (تختلف حسب الجهاز/الإصدار)
        private static readonly string[] AtChannelCandidates = {
            "/dev/mtk_modem",
            "/dev/ril-at",
            "/dev/ttyC0",
            "/dev/smd0",
        };

        private readonly IAdbShell _adb;
        public ChipsetType SupportedChipset => ChipsetType.MediaTek;

        public MediaTekStrategy(IAdbShell adb) => _adb = adb;

        public async Task<PatchResult> ExecuteBypassAsync(EfsPaths paths, string targetCsc, ILogger logger)
        {
            try
            {
                bool anyFailure = false;

                if (!string.IsNullOrEmpty(paths.MpsCodePath))
                {
                    bool mpsOk = await _adb.WriteFileAsync(paths.MpsCodePath, targetCsc);
                    logger.Info($"MTK: MPS={targetCsc} at {paths.MpsCodePath}: {(mpsOk ? "OK" : "FAILED")}");
                    if (!mpsOk) anyFailure = true;
                }

                bool atOk = await ExecuteAtCommandsAsync(targetCsc, logger);

                if (!string.IsNullOrEmpty(paths.CarrierConfigPath))
                {
                    logger.Info($"MTK: Patching carrier config at {paths.CarrierConfigPath}");
                    await _adb.ExecuteRootAsync(
                        $"find {paths.CarrierConfigPath} -name '*.xml' -exec sed -i 's/locked/unlocked/g' {{}} \\;");
                }

                if (!string.IsNullOrEmpty(paths.TelephonyPropPath))
                {
                    var modifier = new EfsFileModifier();
                    byte[] existing = await _adb.ReadFileAsync(paths.TelephonyPropPath);
                    string original = existing.Length > 0 ? Encoding.ASCII.GetString(existing).Replace("\0", "") : "";
                    string merged = modifier.UpsertProps(
                        original, modifier.BuildTelephonyProps(), "# patched by HMAX UNLOCKER PRO");
                    bool telOk = await _adb.WriteFileAsync(paths.TelephonyPropPath, merged);
                    logger.Info($"MTK: telephony.prop merged: {(telOk ? "OK" : "FAILED")}");
                    if (!telOk) anyFailure = true;
                }

                logger.Info("MTK: Restarting RIL services...");
                await new SimStateController(_adb, logger).RestartRilAsync();

                if (!atOk)
                    logger.Warn("MTK: AT commands were NOT delivered (no known AT channel on this device)");

                logger.Info(anyFailure
                    ? "MTK bypass completed WITH FAILURES (see log above)"
                    : "MediaTek bypass completed");
                return anyFailure ? PatchResult.PartialSuccess : PatchResult.Success;
            }
            catch (Exception ex)
            {
                logger.Error($"MTK bypass failed: {ex.Message}");
                return PatchResult.Failed;
            }
        }

        public async Task<LockStatus> DetectStatusAsync(EfsPaths paths, ILogger logger)
        {
            string originalCarrier = "";
            string currentCsc = "";

            if (!string.IsNullOrEmpty(paths.CidDatPath))
            {
                byte[] data = await _adb.ReadFileAsync(paths.CidDatPath);
                originalCarrier = Encoding.ASCII.GetString(data).Trim().Replace("\0", "");
            }

            if (!string.IsNullOrEmpty(paths.MpsCodePath))
            {
                byte[] data = await _adb.ReadFileAsync(paths.MpsCodePath);
                currentCsc = Encoding.ASCII.GetString(data).Trim().Replace("\0", "");
            }

            string mtkLock = await _adb.ExecuteAsync("getprop persist.mtk_simlock_policy 2>/dev/null");
            if (mtkLock.Contains("1") || mtkLock.Contains("locked"))
                return LockStatus.FactoryLocked;

            if (!string.IsNullOrEmpty(originalCarrier) && originalCarrier != currentCsc)
                return LockStatus.RelockBypassed;

            if (!string.IsNullOrEmpty(originalCarrier) && originalCarrier == currentCsc)
                return LockStatus.OfficiallyUnlocked;

            return LockStatus.Unknown;
        }

        public async Task<byte[]> BackupNvAsync(EfsPaths paths, ILogger logger)
        {
            if (!string.IsNullOrEmpty(paths.NvramPath))
            {
                logger.Info($"MTK: Backing up NVRAM from {paths.NvramPath}");
                await _adb.ExecuteRootAsync($"tar czf /data/local/tmp/nvram_backup.tar.gz {paths.NvramPath}/");
                return await _adb.ReadFileAsync("/data/local/tmp/nvram_backup.tar.gz");
            }
            return Array.Empty<byte>();
        }

        public async Task<bool> RestoreNvAsync(EfsPaths paths, byte[] backup, ILogger logger)
        {
            if (backup.Length > 0)
            {
                logger.Info("MTK: Restoring NVRAM...");
                bool pushed = await _adb.WriteFileAsync("/data/local/tmp/nvram_restore.tar.gz", backup);
                if (!pushed)
                {
                    logger.Error("MTK: NVRAM restore upload failed");
                    return false;
                }
                await _adb.ExecuteRootAsync("cd / && tar xzf /data/local/tmp/nvram_restore.tar.gz");
                await _adb.ExecuteRootAsync("rm -f /data/local/tmp/nvram_restore.tar.gz");
                logger.Info("MTK: NVRAM restored");
                return true;
            }
            return false;
        }

        /// <summary>
        /// يرسل أوامر AT إلى قناة المودم. يتحقق أولاً من وجود عقدة القناة فعلياً
        /// ويفشل بصدق إن لم توجد بدلاً من ادعاء النجاح.
        /// </summary>
        private async Task<bool> ExecuteAtCommandsAsync(string targetCsc, ILogger logger)
        {
            // اكتشاف أول قناة موجودة فعلاً على هذا الجهاز
            string channel = null;
            foreach (var candidate in AtChannelCandidates)
            {
                string probe = await _adb.ExecuteRootAsync($"test -e {candidate} && echo FOUND");
                if (probe.Contains("FOUND"))
                {
                    channel = candidate;
                    break;
                }
            }

            if (channel == null)
            {
                logger.Warn("MTK: No known AT channel found on this device (/dev/mtk_modem, /dev/ril-at, /dev/ttyC0, /dev/smd0)");
                return false;
            }

            logger.Info($"MTK: Using AT channel: {channel}");
            string[] atCommands = {
                "AT+EPDNG=1",
                $"AT+EFSC=\"{targetCsc}\"",
                "AT+ESMLC=0",
                "AT+EPDNG=0",
            };

            foreach (var cmd in atCommands)
            {
                string result = await _adb.ExecuteRootAsync(
                    $"echo -e '{cmd}\\r' > {channel} 2>&1");
                bool delivered = string.IsNullOrWhiteSpace(result)
                                 || (!result.Contains("Permission denied") && !result.Contains("No such"));
                logger.Info($"MTK AT: {cmd} → {(delivered ? "sent (unverified response)" : "FAILED: " + result.Trim())}");
                if (!delivered) return false;
            }

            return true;
        }
    }
}

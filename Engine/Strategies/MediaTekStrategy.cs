using System;
using System.Linq;
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
                if (paths == null) throw new ArgumentNullException(nameof(paths));
                targetCsc = NormalizeCsc(targetCsc);

                bool anyFailure = false;
                int attemptedOperations = 0;

                if (!string.IsNullOrEmpty(paths.MpsCodePath))
                {
                    attemptedOperations++;
                    bool mpsOk = await _adb.WriteFileAsync(paths.MpsCodePath, targetCsc);
                    logger.Info($"MTK: MPS={targetCsc} at {paths.MpsCodePath}: {(mpsOk ? "OK" : "FAILED")}");
                    if (!mpsOk) anyFailure = true;
                }

                attemptedOperations++;
                bool atOk = await ExecuteAtCommandsAsync(targetCsc, logger);
                if (!atOk) anyFailure = true;

                if (!string.IsNullOrEmpty(paths.CarrierConfigPath))
                {
                    attemptedOperations++;
                    logger.Info($"MTK: Patching carrier config at {paths.CarrierConfigPath}");
                    string configResult = await _adb.ExecuteRootAsync(
                        $"find '{paths.CarrierConfigPath}' -name '*.xml' -exec sed -i 's/locked/unlocked/g' {{}} \\; && echo HMAX_CONFIG_OK");
                    if (!configResult.Contains("HMAX_CONFIG_OK"))
                    {
                        logger.Error("MTK: Carrier config patch failed: " + configResult.Trim());
                        anyFailure = true;
                    }
                }

                if (!string.IsNullOrEmpty(paths.TelephonyPropPath))
                {
                    attemptedOperations++;
                    var modifier = new EfsFileModifier();
                    long originalSize = await _adb.GetFileSizeAsync(paths.TelephonyPropPath);
                    byte[] existing = await _adb.ReadFileAsync(paths.TelephonyPropPath);
                    if (originalSize > 0 && existing.Length == 0)
                    {
                        logger.Error("MTK: Could not read telephony.prop; the original file was not overwritten.");
                        anyFailure = true;
                    }
                    else
                    {
                        string original = existing.Length > 0 ? Encoding.ASCII.GetString(existing).Replace("\0", "") : "";
                        string merged = modifier.UpsertProps(
                            original, modifier.BuildTelephonyProps(), "# patched by HMAX UNLOCKER PRO");
                        bool telOk = await _adb.WriteFileAsync(paths.TelephonyPropPath, merged);
                        logger.Info($"MTK: telephony.prop merged: {(telOk ? "OK" : "FAILED")}");
                        if (!telOk) anyFailure = true;
                    }
                }

                if (attemptedOperations == 1 && !atOk)
                {
                    logger.Error("MTK: No supported files or AT channel were available; nothing was changed.");
                    return PatchResult.PathNotFound;
                }

                logger.Info("MTK: Restarting RIL services...");
                bool restarted = await new SimStateController(_adb, logger).RestartRilAsync();
                if (!restarted) anyFailure = true;

                if (!atOk)
                    logger.Warn("MTK: AT commands were NOT delivered or verified on this device");

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
            if (paths == null) return LockStatus.Unknown;

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

            var simStatus = await new SimStateController(_adb, logger).EvaluateLockStatusFromSimAsync();
            if (simStatus == LockStatus.FactoryLocked)
                return simStatus;

            string mtkLock = (await _adb.ExecuteAsync("getprop persist.mtk_simlock_policy")).Trim();
            if (mtkLock == "1" || mtkLock.Equals("locked", StringComparison.OrdinalIgnoreCase))
                return LockStatus.FactoryLocked;

            if (!string.IsNullOrEmpty(originalCarrier) && !string.IsNullOrEmpty(currentCsc)
                && originalCarrier != currentCsc)
                return LockStatus.RelockBypassed;

            return LockStatus.Unknown;
        }

        public async Task<byte[]> BackupNvAsync(EfsPaths paths, ILogger logger)
        {
            const string archive = "/data/local/tmp/nvram_backup.tar.gz";
            if (paths == null || string.IsNullOrEmpty(paths.NvramPath))
                return Array.Empty<byte>();

            try
            {
                logger.Info($"MTK: Backing up NVRAM from {paths.NvramPath}");
                string created = await _adb.ExecuteRootAsync(
                    $"tar czf {archive} -C / '{paths.NvramPath.TrimStart('/')}' && echo HMAX_NV_BACKUP_OK");
                if (!created.Contains("HMAX_NV_BACKUP_OK"))
                {
                    logger.Error("MTK: NVRAM archive creation failed: " + created.Trim());
                    return Array.Empty<byte>();
                }
                return await _adb.ReadFileAsync(archive);
            }
            finally
            {
                await _adb.ExecuteRootAsync($"rm -f {archive}");
            }
        }

        public async Task<bool> RestoreNvAsync(EfsPaths paths, byte[] backup, ILogger logger)
        {
            const string archive = "/data/local/tmp/nvram_restore.tar.gz";
            if (backup == null || backup.Length == 0)
                return false;

            try
            {
                logger.Info("MTK: Restoring NVRAM...");
                if (!await _adb.WriteFileAsync(archive, backup))
                {
                    logger.Error("MTK: NVRAM restore upload failed");
                    return false;
                }

                string extracted = await _adb.ExecuteRootAsync(
                    $"cd / && tar xzf {archive} && echo HMAX_NV_RESTORE_OK");
                if (!extracted.Contains("HMAX_NV_RESTORE_OK"))
                {
                    logger.Error("MTK: NVRAM extraction failed: " + extracted.Trim());
                    return false;
                }

                logger.Info("MTK: NVRAM restored");
                return true;
            }
            finally
            {
                await _adb.ExecuteRootAsync($"rm -f {archive}");
            }
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
                    $"printf '%s\\r' '{cmd}' > {channel} && echo HMAX_AT_SENT");
                bool delivered = result.Contains("HMAX_AT_SENT");
                logger.Info($"MTK AT: {cmd} → {(delivered ? "write completed (modem response unverified)" : "FAILED: " + result.Trim())}");
                if (!delivered) return false;
            }

            return true;
        }

        private static string NormalizeCsc(string targetCsc)
        {
            string csc = (targetCsc ?? "").Trim().ToUpperInvariant();
            if (csc.Length != 3 || csc.Any(c => c < 'A' || c > 'Z'))
                throw new ArgumentException("CSC must contain exactly three letters.", nameof(targetCsc));
            return csc;
        }
    }
}

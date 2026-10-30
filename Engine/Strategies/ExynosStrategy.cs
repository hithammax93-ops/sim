using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HmaxUnlocker.Engine.Communication;
using HmaxUnlocker.Engine.Efs;
using HmaxUnlocker.Engine.Logging;
using HmaxUnlocker.Engine.Models;

namespace HmaxUnlocker.Engine.Strategies
{
    public class ExynosStrategy : IPatchStrategy
    {
        private readonly IAdbShell _adb;
        public ChipsetType SupportedChipset => ChipsetType.Exynos;

        public ExynosStrategy(IAdbShell adb) => _adb = adb;

        public async Task<PatchResult> ExecuteBypassAsync(EfsPaths paths, string targetCsc, ILogger logger)
        {
            try
            {
                if (paths == null) throw new ArgumentNullException(nameof(paths));
                targetCsc = NormalizeCsc(targetCsc);

                var modifier = new EfsFileModifier();
                int attempted = 0;
                bool failed = false;

                if (!string.IsNullOrEmpty(paths.MpsCodePath))
                {
                    attempted++;
                    bool ok = await _adb.WriteFileAsync(paths.MpsCodePath, modifier.BuildMpsCodeContent(targetCsc));
                    logger.Info($"Exynos: MPS write: {(ok ? "OK" : "FAILED")}");
                    if (!ok) failed = true;
                }

                if (!string.IsNullOrEmpty(paths.PnPath))
                {
                    attempted++;
                    bool ok = await _adb.WriteFileAsync(paths.PnPath, "0");
                    logger.Info($"Exynos: PN write: {(ok ? "OK" : "FAILED")}");
                    if (!ok) failed = true;
                }

                if (!string.IsNullOrEmpty(paths.TelephonyPropPath))
                {
                    attempted++;
                    bool ok = await MergePropFileAsync(paths.TelephonyPropPath, modifier.BuildTelephonyProps());
                    logger.Info($"Exynos: telephony.prop merge: {(ok ? "OK" : "FAILED")}");
                    if (!ok) failed = true;
                }

                if (!string.IsNullOrEmpty(paths.FactoryPropPath))
                {
                    attempted++;
                    bool ok = await MergePropFileAsync(paths.FactoryPropPath, modifier.BuildCscProps(targetCsc, null));
                    logger.Info($"Exynos: factory.prop merge: {(ok ? "OK" : "FAILED")}");
                    if (!ok) failed = true;
                }

                if (attempted == 0)
                {
                    logger.Error("Exynos: No supported target files were found; nothing was changed.");
                    return PatchResult.PathNotFound;
                }

                bool restarted = await new SimStateController(_adb, logger).RestartRilAsync();
                if (!restarted)
                {
                    logger.Warn("Exynos: RIL restart could not be verified.");
                    failed = true;
                }

                return failed ? PatchResult.PartialSuccess : PatchResult.Success;
            }
            catch (Exception ex)
            {
                logger.Error("Exynos: Operation failed: " + ex.Message);
                return PatchResult.Failed;
            }
        }

        public async Task<LockStatus> DetectStatusAsync(EfsPaths paths, ILogger logger)
        {
            if (paths == null) return LockStatus.Unknown;

            var simStatus = await new SimStateController(_adb, logger).EvaluateLockStatusFromSimAsync();
            if (simStatus == LockStatus.FactoryLocked)
                return simStatus;

            string original = await ReadStringFileAsync(paths.CidDatPath);
            string current = await ReadStringFileAsync(paths.MpsCodePath);
            string pn = await ReadStringFileAsync(paths.PnPath);
            long nvSize = string.IsNullOrEmpty(paths.NvDataPath)
                ? -1 : await _adb.GetFileSizeAsync(paths.NvDataPath);

            if (!string.IsNullOrEmpty(original) && !string.IsNullOrEmpty(current)
                && original != current && (pn == "0" || nvSize == 0))
                return LockStatus.RelockBypassed;

            return LockStatus.Unknown;
        }

        public async Task<byte[]> BackupNvAsync(EfsPaths paths, ILogger logger)
        {
            if (paths == null || string.IsNullOrEmpty(paths.NvDataPath))
            {
                logger.Warn("Exynos: nv_data.bin path was not found.");
                return Array.Empty<byte>();
            }

            logger.Info($"Exynos: Backing up NV: {paths.NvDataPath}");
            return await _adb.ReadFileAsync(paths.NvDataPath);
        }

        public async Task<bool> RestoreNvAsync(EfsPaths paths, byte[] backup, ILogger logger)
        {
            if (paths == null || string.IsNullOrEmpty(paths.NvDataPath) || backup == null || backup.Length == 0)
                return false;

            bool ok = await _adb.WriteFileAsync(paths.NvDataPath, backup);
            if (!ok) return false;

            string md5Path = paths.NvDataPath + ".md5";
            if (await _adb.FileExistsAsync(md5Path)
                && !await _adb.WriteFileAsync(md5Path, Md5Hex(backup)))
                return false;

            logger.Info("Exynos: NV restored and companion MD5 updated when present.");
            return true;
        }

        private async Task<bool> MergePropFileAsync(string remotePath, System.Collections.Generic.IDictionary<string, string> props)
        {
            long originalSize = await _adb.GetFileSizeAsync(remotePath);
            byte[] existing = await _adb.ReadFileAsync(remotePath);
            if (originalSize > 0 && existing.Length == 0)
                return false;

            string original = existing.Length == 0
                ? "" : Encoding.ASCII.GetString(existing).Replace("\0", "").Trim();
            string merged = new EfsFileModifier().UpsertProps(
                original, props, "# patched by HMAX UNLOCKER PRO");
            return await _adb.WriteFileAsync(remotePath, merged);
        }

        private async Task<string> ReadStringFileAsync(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            byte[] data = await _adb.ReadFileAsync(path);
            return data.Length == 0 ? "" : Encoding.ASCII.GetString(data).Trim().Replace("\0", "");
        }

        private static string NormalizeCsc(string targetCsc)
        {
            string csc = (targetCsc ?? "").Trim().ToUpperInvariant();
            if (csc.Length != 3 || csc.Any(c => c < 'A' || c > 'Z'))
                throw new ArgumentException("CSC must contain exactly three letters.", nameof(targetCsc));
            return csc;
        }

        private static string Md5Hex(byte[] data)
        {
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(data);
                var text = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) text.Append(b.ToString("x2"));
                return text.ToString();
            }
        }
    }
}

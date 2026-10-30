using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using HmaxUnlocker.Engine.Communication;
using HmaxUnlocker.Engine.Efs;
using HmaxUnlocker.Engine.Logging;
using HmaxUnlocker.Engine.Models;

namespace HmaxUnlocker.Engine.Strategies
{
    public class QualcommStrategy : IPatchStrategy
    {
        private readonly IAdbShell _adb;
        public ChipsetType SupportedChipset => ChipsetType.Qualcomm;

        public QualcommStrategy(IAdbShell adb) => _adb = adb;

        public async Task<PatchResult> ExecuteBypassAsync(
            EfsPaths paths, string targetCsc, ILogger logger)
        {
            try
            {
                var modifier = new EfsFileModifier();
                bool anyFailure = false;

                // Step 1: Clear QCNv_data.bin (وأيضاً ملف md5 المرافق — بقاءه يسبب كشف التلاعب)
                if (!string.IsNullOrEmpty(paths.QcnvDataPath))
                {
                    logger.Info($"  [Step 1/5] Clearing NV: {paths.QcnvDataPath}");
                    bool nvOk = await _adb.WriteFileAsync(paths.QcnvDataPath, Array.Empty<byte>());
                    logger.Info($"  [Step 1/5] NV cleared: {(nvOk ? "OK" : "FAILED")}");
                    if (!nvOk) anyFailure = true;

                    string md5Path = paths.QcnvDataPath + ".md5";
                    if (await _adb.FileExistsAsync(md5Path))
                    {
                        bool md5Ok = await _adb.WriteFileAsync(md5Path, Array.Empty<byte>());
                        logger.Info($"  [Step 1/5] Companion {md5Path} cleared: {(md5Ok ? "OK" : "FAILED")}");
                        if (!md5Ok) anyFailure = true;
                    }
                }
                else
                {
                    logger.Warn("  [Step 1/5] QCNv path not found — skipped");
                }

                // Step 2: Patch MPS code (كود CSC في mps_code.dat)
                if (!string.IsNullOrEmpty(paths.MpsCodePath))
                {
                    logger.Info($"  [Step 2/5] Writing MPS='{targetCsc}' → {paths.MpsCodePath}");
                    bool mpsOk = await _adb.WriteFileAsync(paths.MpsCodePath, modifier.BuildMpsCodeContent(targetCsc));
                    logger.Info($"  [Step 2/5] MPS written: {(mpsOk ? "OK" : "FAILED")}");
                    if (!mpsOk) anyFailure = true;
                }
                else
                {
                    logger.Warn("  [Step 2/5] MPS path not found — skipped");
                }

                // Step 3: Suppress SIM lock notification
                if (!string.IsNullOrEmpty(paths.PnPath))
                {
                    logger.Info($"  [Step 3/5] Writing pn=0 → {paths.PnPath}");
                    bool pnOk = await _adb.WriteFileAsync(paths.PnPath, "0");
                    logger.Info($"  [Step 3/5] PN written: {(pnOk ? "OK" : "FAILED")}");
                    if (!pnOk) anyFailure = true;
                }
                else
                {
                    logger.Warn("  [Step 3/5] PN path not found — skipped");
                }

                // Step 4: telephony.prop — دمج لا استبدال (المحتوى الأصلي محفوظ)
                if (!string.IsNullOrEmpty(paths.TelephonyPropPath))
                {
                    logger.Info($"  [Step 4/5] Merging telephony.prop → {paths.TelephonyPropPath}");
                    bool telOk = await MergePropFileAsync(
                        paths.TelephonyPropPath, modifier.BuildTelephonyProps(),
                        "# patched by HMAX UNLOCKER PRO");
                    logger.Info($"  [Step 4/5] telephony.prop merged: {(telOk ? "OK" : "FAILED")}");
                    if (!telOk) anyFailure = true;
                }
                else
                {
                    logger.Warn("  [Step 4/5] Telephony prop path not found — skipped");
                }

                // Step 5: factory.prop — دمج خصائص CSC (تُقرأ عند الإقلاع)
                if (!string.IsNullOrEmpty(paths.FactoryPropPath))
                {
                    logger.Info($"  [Step 5/5] Merging factory.prop (CSC={targetCsc}) → {paths.FactoryPropPath}");
                    bool facOk = await MergePropFileAsync(
                        paths.FactoryPropPath, modifier.BuildCscProps(targetCsc, null),
                        "# patched by HMAX UNLOCKER PRO");
                    logger.Info($"  [Step 5/5] factory.prop merged: {(facOk ? "OK" : "FAILED")}");
                    if (!facOk) anyFailure = true;
                }
                else
                {
                    logger.Warn("  [Step 5/5] Factory prop path not found — skipped");
                }

                // إعادة تشغيل خدمات RIL لتطبيق التغييرات
                logger.Info("  Restarting RIL services to apply changes...");
                await new SimStateController(_adb, logger).RestartRilAsync();

                if (anyFailure)
                {
                    logger.Error("  Qualcomm bypass completed WITH FAILURES (see log above)");
                    return PatchResult.PartialSuccess;
                }

                logger.Info("  Qualcomm bypass completed successfully");
                return PatchResult.Success;
            }
            catch (Exception ex)
            {
                logger.Error($"  Qualcomm bypass failed: {ex.Message}");
                return PatchResult.Failed;
            }
        }

        /// <summary>
        /// يقرأ ملف الخصائص من الجهاز، يدمج المفاتيح الجديدة مع الحفاظ على الأسطر الأصلية،
        /// ثم يعيد كتابته مع المالك/الصلاحيات الأصلية.
        /// </summary>
        private async Task<bool> MergePropFileAsync(
            string remotePath, IDictionary<string, string> props, string marker)
        {
            byte[] existing = await _adb.ReadFileAsync(remotePath);
            string original = existing.Length > 0 ? StripNonPropNoise(Encoding.ASCII.GetString(existing)) : "";

            var modifier = new EfsFileModifier();
            string merged = modifier.UpsertProps(original, props, marker);
            return await _adb.WriteFileAsync(remotePath, merged);
        }

        // يزيل البايتات الصفرية ونهايات الأسطر الزائدة القادمة من قراءة الملفات الثنائية الصغيرة
        private static string StripNonPropNoise(string s)
        {
            return (s ?? "").Replace("\0", "").Trim();
        }

        /// <summary>
        /// Detect lock status with step-by-step logging
        /// </summary>
        public async Task<LockStatus> DetectStatusAsync(EfsPaths paths, ILogger logger)
        {
            try
            {
                // Read CID (original carrier)
                logger.Info("  [Detect 1/4] Reading CID (original carrier)...");
                string originalCarrier = await ReadStringFileAsync(paths.CidDatPath);
                logger.Info($"  [Detect 1/4] CID = '{originalCarrier}'");
                // Read MPS (current CSC)
                logger.Info("  [Detect 2/4] Reading MPS code (current CSC)...");
                string currentCsc = await ReadStringFileAsync(paths.MpsCodePath);
                logger.Info($"  [Detect 2/4] MPS = '{currentCsc}'");
                // Read QCNv size
                logger.Info("  [Detect 3/4] Reading QCNv_data.bin size...");
                long qcnvSize = -1;
                if (!string.IsNullOrEmpty(paths.QcnvDataPath))
                {
                    qcnvSize = await _adb.GetFileSizeAsync(paths.QcnvDataPath);
                    logger.Info($"  [Detect 3/4] QCNv size = {qcnvSize} bytes");
                }
                else
                {
                    logger.Info("  [Detect 3/4] QCNv path not found");
                }
                // Read PN
                logger.Info("  [Detect 4/4] Reading pn file...");
                string pnValue = await ReadStringFileAsync(paths.PnPath);
                logger.Info($"  [Detect 4/4] PN = '{pnValue}'");
                // Decision matrix
                bool carrierMismatch = !string.IsNullOrEmpty(originalCarrier)
                                    && !string.IsNullOrEmpty(currentCsc)
                                    && originalCarrier != currentCsc;
                bool nvCleared = qcnvSize == 0;
                logger.Info($"  [Detect] Carrier mismatch: {carrierMismatch}, NV cleared: {nvCleared}");
                if (carrierMismatch && nvCleared) return LockStatus.RelockBypassed;
                if (!carrierMismatch && !nvCleared) return LockStatus.FactoryLocked;
                if (!carrierMismatch && nvCleared) return LockStatus.OfficiallyUnlocked;
                if (carrierMismatch && !nvCleared) return LockStatus.FactoryLocked;
                return LockStatus.Unknown;
            }
            catch (Exception ex)
            {
                logger.Error($"  [Detect] Error: {ex.Message}");
                return LockStatus.Unknown;
            }
        }

        public async Task<byte[]> BackupNvAsync(EfsPaths paths, ILogger logger)
        {
            if (!string.IsNullOrEmpty(paths.QcnvDataPath))
            {
                logger.Info($"  Backing up NV: {paths.QcnvDataPath}");
                return await _adb.ReadFileAsync(paths.QcnvDataPath);
            }
            return Array.Empty<byte>();
        }

        public async Task<bool> RestoreNvAsync(EfsPaths paths, byte[] backup, ILogger logger)
        {
            if (!string.IsNullOrEmpty(paths.QcnvDataPath) && backup.Length > 0)
            {
                logger.Info($"  Restoring NV: {paths.QcnvDataPath}");
                bool ok = await _adb.WriteFileAsync(paths.QcnvDataPath, backup);
                // استعادة ملف md5 أيضاً إن وُجد ضمن البيانات لا يمكن، لذا نتركه ممسوحاً
                logger.Info($"  Restore NV: {(ok ? "OK" : "FAILED")}");
                return ok;
            }
            return false;
        }

        private async Task<string> ReadStringFileAsync(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            byte[] data = await _adb.ReadFileAsync(path);
            return data.Length > 0
                ? Encoding.ASCII.GetString(data).Trim().Replace("\0", "")
                : "";
        }
    }
}

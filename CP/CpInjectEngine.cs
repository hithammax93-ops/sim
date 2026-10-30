using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HmaxUnlocker.Engine.Communication;
using HmaxUnlocker.Engine.Detection;
using HmaxUnlocker.Engine.Logging;
using HmaxUnlocker.Engine.Models;

namespace HmaxUnlocker.Engine.Cp
{
    public class CpInjectResult
    {
        public PatchResult Result { get; set; } = PatchResult.Failed;
        public List<string> ActionsPerformed { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// حقن ملف CP / إعدادات الناقل (carrier config) على الجهاز — التنفيذ الحقيقي:
    /// 1) نسخ الملف الأصلي احتياطياً عبر adb pull قبل أي تعديل (إجباري — يتوقف إن فشل)
    /// 2) دفع الملف الجديد عبر adb push مع إعادة تطبيق المالك/الصلاحيات/restorecon
    /// 3) التحقق الفعلي بمطابقة الحجم على الجهاز
    /// حقن MCC/MNC يُنفَّذ بعدها منفصلاً عبر UniversalBypassEngine.InjectMccMncAsync.
    /// </summary>
    public class CpInjectEngine
    {
        // مسارات إعدادات الناقل القابلة للكتابة (أحادية الجهاز — قد لا يوجد إلا أحدها)
        private static readonly string[] CarrierConfigDirCandidates = {
            "/mnt/vendor/efs/carrier",
            "/efs/carrier",
            "/data/vendor/modem/mcfg",
            "/nvdata/mcfg",
            "/vendor/firmware-modem/image/modem_pr/mcfg/configs", // للقراءة فقط عادةً (dm-verity)
        };

        private readonly IAdbShell _adb;
        private readonly ILogger _logger;

        public CpInjectEngine(IAdbShell adb, ILogger logger)
        {
            _adb = adb;
            _logger = logger;
        }

        /// <summary>
        /// يقترح مساراً وجهةً افتراضياً للملف بناءً على ما هو موجود فعلاً على الجهاز.
        /// </summary>
        public async Task<string> DiscoverDefaultTargetAsync(string localFileName)
        {
            var paths = await new PathDiscoveryEngine(_adb).DiscoverAsync(ChipsetType.Unknown);
            string dir = paths.CarrierConfigPath;

            if (string.IsNullOrEmpty(dir))
            {
                foreach (var candidate in CarrierConfigDirCandidates)
                {
                    if (await _adb.DirectoryExistsAsync(candidate))
                    {
                        dir = candidate;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(dir))
                return "/mnt/vendor/efs/" + localFileName;

            return dir.TrimEnd('/') + "/" + localFileName;
        }

        public async Task<CpInjectResult> InjectCpFileAsync(string localCpFilePath, string remoteTargetPath)
        {
            var result = new CpInjectResult();
            try
            {
                // ── فحوص ما قبل التنفيذ ──
                if (string.IsNullOrEmpty(localCpFilePath) || !File.Exists(localCpFilePath))
                {
                    result.Errors.Add("Local CP file not found: " + localCpFilePath);
                    return result;
                }

                long localSize = new FileInfo(localCpFilePath).Length;
                if (localSize <= 0)
                {
                    result.Errors.Add("Local CP file is empty");
                    return result;
                }

                if (string.IsNullOrWhiteSpace(remoteTargetPath))
                {
                    result.Errors.Add("Remote target path is empty");
                    return result;
                }
                remoteTargetPath = remoteTargetPath.Trim();

                if (!await _adb.IsDeviceConnectedAsync())
                {
                    result.Result = PatchResult.DeviceNotConnected;
                    result.Errors.Add("No device connected");
                    return result;
                }
                if (!await _adb.HasRootAccessAsync())
                {
                    result.Result = PatchResult.RootRequired;
                    result.Errors.Add("Root access not available");
                    return result;
                }

                // ── 1. نسخة احتياطية إجبارية من الملف الأصلي ──
                string backupDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Backups",
                    "CpBackup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));

                if (await _adb.FileExistsAsync(remoteTargetPath))
                {
                    Directory.CreateDirectory(backupDir);
                    string localBackup = Path.Combine(
                        backupDir, "original_" + Path.GetFileName(remoteTargetPath));

                    bool backed = await _adb.PullFileAsync(remoteTargetPath, localBackup);
                    long backupSize = File.Exists(localBackup) ? new FileInfo(localBackup).Length : 0;

                    if (backed && backupSize > 0)
                    {
                        result.ActionsPerformed.Add(
                            $"Backed up original: {remoteTargetPath} -> {localBackup} ({backupSize} bytes)");
                        _logger.Info($"[CP] Original backed up ({backupSize} bytes)");
                    }
                    else
                    {
                        // لا نكتب فوق ملف لا نستطيع حفظ نسخة منه — حماية للجهاز
                        result.Errors.Add("Could NOT back up the original file — injection aborted to protect the device");
                        _logger.Error("[CP] Backup of original FAILED — injection aborted");
                        return result;
                    }
                }
                else
                {
                    result.ActionsPerformed.Add("No existing file at target (fresh injection)");
                }

                // ── 2. الدفع الفعلي (المالك/الصلاحيات/restorecon تُطبَّق داخل PushFileAsync) ──
                _logger.Info($"[CP] Pushing {Path.GetFileName(localCpFilePath)} ({localSize} bytes) -> {remoteTargetPath}");
                bool pushed = await _adb.PushFileAsync(localCpFilePath, remoteTargetPath);
                if (!pushed)
                {
                    result.Errors.Add(
                        $"Push failed: {remoteTargetPath} — the target filesystem may be read-only " +
                        "(vendor/firmware-modem partitions are read-only even for root due to dm-verity). " +
                        "Use an EFS-backed writable path or a Magisk overlay.");
                    _logger.Error("[CP] Push FAILED");
                    return result;
                }
                result.ActionsPerformed.Add($"Injected CP file: {remoteTargetPath}");

                // ── 3. تحقق فعلي بمطابقة الحجم ──
                long remoteSize = await _adb.GetFileSizeAsync(remoteTargetPath);
                if (remoteSize != localSize)
                {
                    result.Errors.Add(
                        $"Size mismatch after injection (local {localSize} bytes, remote {remoteSize} bytes)");
                    _logger.Error("[CP] Verification FAILED");
                    return result;
                }
                result.ActionsPerformed.Add($"Verified on device: {remoteSize} bytes");

                result.Result = PatchResult.Success;
                _logger.Info("[CP] CP file injection completed and verified");
            }
            catch (Exception ex)
            {
                result.Result = PatchResult.Failed;
                result.Errors.Add(ex.Message);
                _logger.Error("[CP] Fatal: " + ex.Message);
            }
            return result;
        }
    }
}

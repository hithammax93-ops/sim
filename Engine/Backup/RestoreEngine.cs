using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HmaxUnlocker.Engine.Communication;
using HmaxUnlocker.Engine.Efs;
using HmaxUnlocker.Engine.Logging;
using HmaxUnlocker.Engine.Models;

namespace HmaxUnlocker.Engine.Backup
{
    public class RestoreResult
    {
        public PatchResult Result { get; set; } = PatchResult.Failed;
        public List<string> RestoredFiles { get; set; } = new List<string>();
        public List<string> FailedFiles { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// استرجاع نسخة احتياطية إلى مساراتها الأصلية على الجهاز:
    /// يمشي على شجرة المجلد المعكوسة (efs/... ، mnt/... بنفس أسماء الجهاز)
    /// ويدفع كل ملف إلى مساره الأصلي عبر adb push مع إعادة تطبيق
    /// المالك/الصلاحيات/SELinux، ثم يعيد تشغيل خدمات RIL.
    /// يُرسَل ملف البيانات قبل ملف md5 المرافق له.
    /// </summary>
    public class RestoreEngine
    {
        // الجذور المسموح استرجاعها فقط — حماية من دفع ملفات غريبة إلى الجهاز
        private static readonly HashSet<string> AllowedRoots = new HashSet<string>
        {
            "efs", "mnt", "persist", "nvdata", "data"
        };

        private readonly IAdbShell _adb;
        private readonly ILogger _logger;

        public RestoreEngine(IAdbShell adb, ILogger logger)
        {
            _adb = adb;
            _logger = logger;
        }

        public async Task<RestoreResult> ExecuteAsync(string backupDir)
        {
            var result = new RestoreResult();
            try
            {
                if (string.IsNullOrEmpty(backupDir) || !Directory.Exists(backupDir))
                {
                    result.Errors.Add("Backup folder not found: " + backupDir);
                    return result;
                }

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

                // اجمع الملفات داخل الشجرة المعكوسة وحوّلها إلى مسارات الجهاز
                var targets = new List<KeyValuePair<string, string>>(); // local → remote
                foreach (string local in Directory.GetFiles(backupDir, "*", SearchOption.AllDirectories))
                {
                    string rel = local
                        .Substring(backupDir.Length)
                        .TrimStart('\\', '/')
                        .Replace('\\', '/');

                    if (rel.Length == 0 || rel.Contains(".."))
                        continue;

                    string root = rel.Split('/')[0];
                    if (!AllowedRoots.Contains(root))
                        continue; // manifest/device_info وغيرها لا تُدفع للجهاز

                    targets.Add(new KeyValuePair<string, string>(local, "/" + rel));
                }

                if (targets.Count == 0)
                {
                    result.Errors.Add(
                        "No restorable device files found in this folder. " +
                        "Restore works with mirrored-structure backups " +
                        "(efs/..., mnt/... with the same names as on the phone).");
                    return result;
                }

                // ملفات البيانات قبل ملفات md5 المرافقة
                var ordered = targets
                    .OrderBy(t => t.Value.EndsWith(".md5", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                    .ThenBy(t => t.Value, StringComparer.Ordinal)
                    .ToList();

                _logger.Info($"[Restore] Restoring {ordered.Count} file(s) to original device paths...");
                foreach (var target in ordered)
                {
                    bool ok = await _adb.PushFileAsync(target.Key, target.Value);
                    if (ok)
                    {
                        result.RestoredFiles.Add(target.Value);
                        _logger.Info($"[Restore]   Restored: {target.Value}");
                    }
                    else
                    {
                        result.FailedFiles.Add(target.Value);
                        _logger.Error($"[Restore]   FAILED: {target.Value}");
                    }
                }

                // إعادة تشغيل RIL لتطبيق الملفات المستعادة؛ فشله يجعل النتيجة جزئية لا ناجحة بالكامل.
                bool rilRestartFailed = false;
                if (result.RestoredFiles.Count > 0)
                {
                    _logger.Info("[Restore] Restarting RIL services...");
                    bool restarted = await new SimStateController(_adb, _logger).RestartRilAsync();
                    if (!restarted)
                    {
                        rilRestartFailed = true;
                        result.Errors.Add("Files were restored, but RIL restart could not be verified. Reboot is required.");
                    }
                }

                result.Result = result.FailedFiles.Count == 0 && !rilRestartFailed
                    ? PatchResult.Success
                    : (result.RestoredFiles.Count > 0 ? PatchResult.PartialSuccess : PatchResult.Failed);
            }
            catch (Exception ex)
            {
                result.Result = PatchResult.Failed;
                result.Errors.Add(ex.Message);
                _logger.Error("[Restore] Fatal: " + ex.Message);
            }
            return result;
        }
    }
}

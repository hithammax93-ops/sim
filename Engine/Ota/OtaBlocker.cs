using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HmaxUnlocker.Engine.Communication;
using HmaxUnlocker.Engine.Logging;
using HmaxUnlocker.Engine.Models;

namespace HmaxUnlocker.Engine.Ota
{
    public class OtaBlockResult
    {
        public PatchResult Result { get; set; } = PatchResult.Failed;
        public List<string> HandledPackages { get; set; } = new List<string>();
        public List<string> SkippedPackages { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// منع تحديثات النظام (OTA) — الجزء الناقص من بروتوكول منع الريليك:
    /// التحديث التلقائي هو أكبر سبب لإعادة تطبيق قفل المشغّل بعد الفتح.
    /// الآلية الحقيقية: تعطيل حزم FOTA/SOAgent المسؤولة عن فحص وتنزيل وتثبيت
    /// التحديثات، مع التحقق من نتيجة كل أمر pm، مع إمكانية الاسترجاع الكامل.
    /// </summary>
    public class OtaBlocker
    {
        // حزم التحديثات المعروفة على أجهزة سامسونج (تُتخطى الحزم غير الموجودة)
        private static readonly string[] OtaPackages = {
            "com.wssyncmldm",                       // FOTA DM client — محرك التحديث
            "com.sec.android.soagent",              // SOAgent — فحص التحديثات
            "com.samsung.android.dqagent",          // DQAgent — قناة تقرير/تنزيل
            "com.ws.dm",                            // WS DM client
            "com.samsung.android.app.updatecenter", // Update Center (One UI 6+)
            "com.samsung.sdm.sdmviewer",            // SDM viewer (حسب المشغّل)
            "com.samsung.sdm.sdmsearch",            // SDM search (حسب المشغّل)
        };

        private readonly IAdbShell _adb;
        private readonly ILogger _logger;

        public OtaBlocker(IAdbShell adb, ILogger logger)
        {
            _adb = adb;
            _logger = logger;
        }

        /// <summary>تعطيل حزم OTA مع التحقق من كل أمر.</summary>
        public async Task<OtaBlockResult> DisableAsync()
        {
            var result = new OtaBlockResult();
            if (!await PreflightAsync(result)) return result;

            _logger.Info("[OTA] Disabling FOTA / SOAgent update packages...");
            foreach (var pkg in OtaPackages)
            {
                if (!await PackageExistsAsync(pkg))
                {
                    result.SkippedPackages.Add(pkg);
                    continue;
                }

                string output = await _adb.ExecuteRootAsync($"pm disable-user --user 0 {pkg}");
                if (output.IndexOf("disabled", StringComparison.OrdinalIgnoreCase) >= 0
                    && output.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    _logger.Info($"[OTA]   Blocked: {pkg}");
                    result.HandledPackages.Add(pkg);
                }
                else
                {
                    _logger.Error($"[OTA]   Failed to block {pkg}: {output.Trim()}");
                    result.Errors.Add($"disable failed: {pkg}");
                }
            }

            result.Result = result.Errors.Count == 0 && result.HandledPackages.Count > 0
                ? PatchResult.Success
                : (result.HandledPackages.Count > 0 ? PatchResult.PartialSuccess : PatchResult.Failed);
            _logger.Info($"[OTA] Blocked {result.HandledPackages.Count} update packages, " +
                         $"{result.SkippedPackages.Count} not present, {result.Errors.Count} failed");
            return result;
        }

        /// <summary>إعادة تفعيل حزم OTA (استرجاع كامل).</summary>
        public async Task<OtaBlockResult> RestoreAsync()
        {
            var result = new OtaBlockResult();
            if (!await PreflightAsync(result)) return result;

            _logger.Info("[OTA] Re-enabling FOTA / SOAgent update packages...");
            foreach (var pkg in OtaPackages)
            {
                if (!await PackageExistsAsync(pkg))
                {
                    result.SkippedPackages.Add(pkg);
                    continue;
                }

                string output = await _adb.ExecuteRootAsync($"pm enable {pkg}");
                if (output.IndexOf("enabled", StringComparison.OrdinalIgnoreCase) >= 0
                    && output.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    _logger.Info($"[OTA]   Restored: {pkg}");
                    result.HandledPackages.Add(pkg);
                }
                else
                {
                    _logger.Error($"[OTA]   Failed to restore {pkg}: {output.Trim()}");
                    result.Errors.Add($"enable failed: {pkg}");
                }
            }

            result.Result = result.Errors.Count == 0 && result.HandledPackages.Count > 0
                ? PatchResult.Success
                : (result.HandledPackages.Count > 0 ? PatchResult.PartialSuccess : PatchResult.Failed);
            _logger.Info($"[OTA] Restored {result.HandledPackages.Count} update packages");
            return result;
        }

        private async Task<bool> PreflightAsync(OtaBlockResult result)
        {
            if (!await _adb.IsDeviceConnectedAsync())
            {
                result.Result = PatchResult.DeviceNotConnected;
                result.Errors.Add("No device connected");
                return false;
            }
            if (!await _adb.HasRootAccessAsync())
            {
                result.Result = PatchResult.RootRequired;
                result.Errors.Add("Root access not available");
                return false;
            }
            return true;
        }

        private async Task<bool> PackageExistsAsync(string packageName)
        {
            string list = await _adb.ExecuteAsync($"pm list packages {packageName}");
            string expected = "package:" + packageName;
            return list.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(line => string.Equals(line.Trim(), expected, StringComparison.Ordinal));
        }
    }
}

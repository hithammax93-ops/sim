using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using HmaxUnlocker.Engine.Communication;
using HmaxUnlocker.Engine.Logging;
using HmaxUnlocker.Engine.Models;

namespace HmaxUnlocker.Engine.Samsung
{
    public class SamsungPackageManager
    {
        private readonly IAdbShell _adb;
        private readonly ILogger _logger;

        private static readonly string[] PACKAGES_TO_DISABLE = {
            "com.samsung.android.app.omcagent",
            "com.android.carrierdefaultapp",
            "com.samsung.android.knox.pushmanager",
            "com.knox.vpn.proxyhandler",
            "com.samsung.android.knox.app.networkfilter",
            "com.samsung.android.knox.er",
            "com.samsung.android.knox.analytics.uploader",
            "com.samsung.android.knox.knnr",
            "com.sec.enterprise.knox.cloudmdm.smdms",
            "com.samsung.android.cidmanager",          // تمت إضافتها لمنع إشعار Phone locked
            "com.sec.android.app.carrierlock"          // تمت إضافتها لمنع قفل المشغل
        };

        private static readonly string[] PACKAGES_TO_FREEZE = {
            "com.samsung.android.knox.attestation",
            "com.samsung.android.knox.kpecore",
            "com.samsung.android.knox.zt.framework",
        };

        private static readonly string[] PACKAGES_TO_CLEAR = {
            "com.samsung.android.cidmanager",
            "com.samsung.android.app.omcagent",
            "com.android.carrierconfig",
        };

        public SamsungPackageManager(IAdbShell adb, ILogger logger)
        {
            _adb = adb;
            _logger = logger;
        }

        public async Task<PackageBypassResult> ExecuteBypassAsync()
        {
            var result = new PackageBypassResult();

            _logger.Info("Disabling carrier and Knox enforcement packages...");
            foreach (var pkg in PACKAGES_TO_DISABLE)
            {
                if (!await PackageExistsAsync(pkg)) continue;

                // pm disable-user يطبع الحالة الجديدة عند النجاح — نتحقق منها فعلياً
                string output = await _adb.ExecuteRootAsync($"pm disable-user --user 0 {pkg}");
                if (ContainsIgnoreCase(output, "disabled") && !ContainsIgnoreCase(output, "Exception"))
                {
                    _logger.Info($"  Disabled: {pkg}");
                    result.DisabledPackages.Add(pkg);
                }
                else
                {
                    _logger.Error($"  Failed to disable {pkg}: {output.Trim()}");
                    result.Errors.Add($"disable failed: {pkg}");
                }
            }

            _logger.Info("Freezing Knox attestation packages...");
            foreach (var pkg in PACKAGES_TO_FREEZE)
            {
                if (!await PackageExistsAsync(pkg)) continue;

                string output = await _adb.ExecuteRootAsync($"pm hide {pkg}");
                if (ContainsIgnoreCase(output, "true") && !ContainsIgnoreCase(output, "Exception"))
                {
                    _logger.Info($"  Frozen: {pkg}");
                    result.FrozenPackages.Add(pkg);
                }
                else
                {
                    _logger.Error($"  Failed to freeze {pkg}: {output.Trim()}");
                    result.Errors.Add($"freeze failed: {pkg}");
                }
            }

            _logger.Info("Clearing carrier configuration data...");
            foreach (var pkg in PACKAGES_TO_CLEAR)
            {
                if (!await PackageExistsAsync(pkg)) continue;

                // pm clear يطبع Success عند النجاح
                string output = await _adb.ExecuteRootAsync($"pm clear {pkg}");
                if (output.IndexOf("Success", StringComparison.Ordinal) >= 0)
                {
                    _logger.Info($"  Cleared: {pkg}");
                    result.ClearedPackages.Add(pkg);
                }
                else
                {
                    _logger.Error($"  Failed to clear {pkg}: {output.Trim()}");
                    result.Errors.Add($"clear failed: {pkg}");
                }
            }

            foreach (var proc in new[] {
                "com.samsung.android.cidmanager",
                "com.samsung.android.app.omcagent",
                "com.android.carrierconfig",
                "com.samsung.android.knox.attestation",
                "com.sec.android.app.carrierlock"
            })
            {
                await _adb.ExecuteRootAsync($"am force-stop {proc} 2>/dev/null");
            }

            return result;
        }

        public async Task<string> ReadCurrentCscAsync()
        {
            string csc = await _adb.ExecuteRootAsync(
                "cat /data/user/0/com.samsung.android.cidmanager/shared_prefs/cidmanager.xml 2>/dev/null");
            if (!string.IsNullOrWhiteSpace(csc))
            {
                int idx = csc.IndexOf("current_csc");
                if (idx >= 0)
                {
                    int valIdx = csc.IndexOf("value=\"", idx);
                    if (valIdx >= 0)
                    {
                        int start = valIdx + 7;
                        int end = csc.IndexOf("\"", start);
                        if (end > start) return csc.Substring(start, end - start);
                    }
                }
            }

            csc = (await _adb.ExecuteAsync("getprop persist.omc.salescode")).Trim();
            if (!string.IsNullOrWhiteSpace(csc)) return csc;

            byte[] mpsData = await _adb.ReadFileAsync("/mnt/vendor/efs/mps_code.dat");
            if (mpsData.Length > 0) return Encoding.ASCII.GetString(mpsData).Trim().Replace("\0", "");

            return "UNKNOWN";
        }

        public async Task<string> ReadOriginalCarrierAsync()
        {
            byte[] cidData = await _adb.ReadFileAsync("/efs/imei/cid.dat");
            if (cidData.Length > 0) return Encoding.ASCII.GetString(cidData).Trim().Replace("\0", "");
            return "UNKNOWN";
        }

        private async Task<bool> PackageExistsAsync(string packageName)
        {
            string result = await _adb.ExecuteAsync($"pm list packages {packageName}");
            return result.Contains(packageName);
        }

        private static bool ContainsIgnoreCase(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle))
                return false;
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}

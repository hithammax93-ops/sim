using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HmaxUnlocker.Engine.Communication;
using HmaxUnlocker.Engine.Detection;
using HmaxUnlocker.Engine.Efs;
using HmaxUnlocker.Engine.Logging;
using HmaxUnlocker.Engine.Models;
using HmaxUnlocker.Engine.Samsung;
using HmaxUnlocker.Engine.Strategies;

namespace HmaxUnlocker.Engine
{
    public class UniversalBypassEngine
    {
        private readonly IAdbShell _adb;
        private readonly ILogger _logger;

        public UniversalBypassEngine(IAdbShell adb, ILogger logger)
        {
            _adb = adb;
            _logger = logger;
        }

        public async Task<BypassReport> ExecuteAsync(string targetCsc = "XAA")
        {
            var report = new BypassReport();
            try
            {
                targetCsc = NormalizeCsc(targetCsc);

                // ═══════════════════════════════════════
                // Phase 1: Pre-flight checks
                // ═══════════════════════════════════════
                _logger.Info("=== Phase 1: Pre-flight checks ===");
                _logger.Info("  Checking device connection...");
                if (!await _adb.IsDeviceConnectedAsync())
                {
                    report.Result = PatchResult.DeviceNotConnected;
                    report.Errors.Add("No device connected");
                    _logger.Error("  No device connected!");
                    return report;
                }
                _logger.Info("  Device connected: OK");
                _logger.Info("  Checking root access...");
                if (!await _adb.HasRootAccessAsync())
                {
                    report.Result = PatchResult.RootRequired;
                    report.Errors.Add("Root access not available");
                    _logger.Error("  Root access not available!");
                    return report;
                }
                _logger.Info("  Root access: OK");

                // ═══════════════════════════════════════
                // Phase 2: Device detection
                // ═══════════════════════════════════════
                _logger.Info("=== Phase 2: Device detection ===");
                var detector = new ChipsetDetector(_adb);
                report.Device = await detector.DetectDeviceAsync();
                _logger.Info($"  Device: {report.Device.Brand} {report.Device.Model}");
                _logger.Info($"  Chipset: {report.Device.Chipset} ({report.Device.BoardPlatform})");
                _logger.Info($"  Android: {report.Device.AndroidVersion}");
                if (report.Device.Chipset != ChipsetType.Qualcomm
                    && report.Device.Chipset != ChipsetType.MediaTek
                    && report.Device.Chipset != ChipsetType.Exynos)
                {
                    report.Result = PatchResult.UnsupportedDevice;
                    report.Errors.Add($"Chipset '{report.Device.Chipset}' is not supported by a verified strategy");
                    _logger.Error("  No verified strategy exists for this chipset — cannot proceed.");
                    return report;
                }
                _logger.Info("  Device detection: OK");

                // ═══════════════════════════════════════
                // Phase 3: Path discovery
                // ═══════════════════════════════════════
                _logger.Info("=== Phase 3: Path discovery ===");
                var pathEngine = new PathDiscoveryEngine(_adb);
                report.Paths = await pathEngine.DiscoverAsync(report.Device.Chipset);
                foreach (var p in report.Paths.AllDiscoveredPaths)
                    _logger.Info($"  Found: {p}");
                if (string.IsNullOrEmpty(report.Paths.MpsCodePath))
                {
                    report.Result = PatchResult.PathNotFound;
                    report.Errors.Add("Could not locate MPS code file");
                    _logger.Error("  MPS code file not found!");
                    return report;
                }
                _logger.Info("  Path discovery: OK");

                // ═══════════════════════════════════════
                // Phase 4: Pre-patch status
                // ═══════════════════════════════════════
                _logger.Info("=== Phase 4: Pre-patch status ===");
                try
                {
                    var strategy = PatchStrategyFactory.Create(report.Device.Chipset, _adb);
                    _logger.Info("  Detecting lock status (may take 10-15 seconds)...");
                    report.StatusBefore = await strategy.DetectStatusAsync(report.Paths, _logger);
                    _logger.Info($"  Status before: {report.StatusBefore}");
                }
                catch (TimeoutException tex)
                {
                    _logger.Error($"  Phase 4 TIMEOUT: {tex.Message}");
                    report.Errors.Add($"Phase 4 timeout: {tex.Message}");
                    report.Result = PatchResult.Failed;
                    return report;
                }
                catch (Exception ex)
                {
                    _logger.Error($"  Phase 4 ERROR: {ex.Message}");
                    report.Errors.Add($"Phase 4 error: {ex.Message}");
                    report.StatusBefore = LockStatus.Unknown;
                }

                // ═══════════════════════════════════════
                // Phase 5: NV safety backup (saved to disk — إجباري)
                // المرحلة 6 ستمسح NV نهائياً، لذلك لا نكمل أبداً بدون نسخة محفوظة فعلياً
                // ═══════════════════════════════════════
                _logger.Info("=== Phase 5: NV safety backup ===");
                try
                {
                    bool nvWillBeCleared = report.Device.Chipset == ChipsetType.Qualcomm
                        && !string.IsNullOrEmpty(report.Paths.QcnvDataPath);
                    long nvSizeOnDevice = nvWillBeCleared
                        ? await _adb.GetFileSizeAsync(report.Paths.QcnvDataPath)
                        : -1;

                    if (!nvWillBeCleared)
                    {
                        _logger.Info("  This strategy will not clear QCNv — no NV safety copy is required by this step");
                    }
                    else if (nvSizeOnDevice == 0)
                    {
                        _logger.Info("  NV file is already empty (previous bypass?) — nothing to protect");
                        report.ActionsPerformed.Add("NV already empty — no backup needed");
                    }
                    else
                    {
                        var strategy = PatchStrategyFactory.Create(report.Device.Chipset, _adb);
                        byte[] nvBackup = await strategy.BackupNvAsync(report.Paths, _logger);

                        if (nvBackup == null || nvBackup.Length == 0)
                        {
                            report.Result = PatchResult.Failed;
                            report.Errors.Add(
                                "NV backup read FAILED (file exists but could not be read) — " +
                                "aborting before NV reset. Run 'Backup Files' first, then retry.");
                            _logger.Error("  NV read returned 0 bytes while file is non-empty — ABORTING to protect the device!");
                            return report;
                        }

                        // حفظ فعلي على القرص — هذا هو الفرق الجوهري: النسخة تبقى بعد إغلاق البرنامج
                        string backupDir = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory, "Backups",
                            "NVSafety_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                        Directory.CreateDirectory(backupDir);

                        const string nvFileName = "QCNv_data.bin.safety";
                        string nvFile = Path.Combine(backupDir, nvFileName);
                        File.WriteAllBytes(nvFile, nvBackup);

                        report.ActionsPerformed.Add(
                            $"NV safety backup saved: {nvFile} ({nvBackup.Length} bytes)");
                        _logger.Info($"  NV safety backup SAVED: {nvFile}");
                        _logger.Info($"  Size: {nvBackup.Length} bytes — keep this file, it is your only way back");
                    }
                }
                catch (Exception ex)
                {
                    report.Result = PatchResult.Failed;
                    report.Errors.Add($"NV backup failed: {ex.Message} — aborting before NV reset.");
                    _logger.Error($"  NV backup FAILED: {ex.Message} — ABORTING (NV must never be cleared without a saved copy)!");
                    return report;
                }

                // ═══════════════════════════════════════
                // Phase 6: Execute bypass
                // ═══════════════════════════════════════
                _logger.Info("=== Phase 6: Execute bypass ===");
                var bypassStrategy = PatchStrategyFactory.Create(report.Device.Chipset, _adb);
                report.Result = await bypassStrategy.ExecuteBypassAsync(report.Paths, targetCsc, _logger);
                if (report.Result == PatchResult.Success)
                    _logger.Info("  EFS/NV bypass: OK");
                else
                {
                    _logger.Error($"  EFS/NV bypass: FAILED ({report.Result}); additional device changes were skipped.");
                    return report;
                }

                // ═══════════════════════════════════════
                // Phase 6.5: Samsung package bypass
                // ═══════════════════════════════════════
                if (report.Device.IsSamsung)
                {
                    _logger.Info("=== Phase 6.5: Samsung package bypass ===");
                    try
                    {
                        var pkgManager = new SamsungPackageManager(_adb, _logger);
                        report.PackageResult = await pkgManager.ExecuteBypassAsync();
                        report.ActionsPerformed.Add($"Disabled {report.PackageResult.DisabledPackages.Count} packages");
                        _logger.Info($"  Disabled: {report.PackageResult.DisabledPackages.Count} packages");
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"  Samsung package bypass failed: {ex.Message} (continuing...)");
                    }
                }

                // ═══════════════════════════════════════
                // Phase 7: Post-patch verification
                // ═══════════════════════════════════════
                _logger.Info("=== Phase 7: Post-patch verification ===");
                await Task.Delay(2000);
                try
                {
                    var verifyStrategy = PatchStrategyFactory.Create(report.Device.Chipset, _adb);
                    report.StatusAfter = await verifyStrategy.DetectStatusAsync(report.Paths, _logger);
                    _logger.Info($"  Status after: {report.StatusAfter}");
                }
                catch (Exception ex)
                {
                    _logger.Warn($"  Post-patch verification failed: {ex.Message}");
                    report.StatusAfter = LockStatus.Unknown;
                }

                _logger.Info("=== Bypass Complete ===");
            }
            catch (Exception ex)
            {
                report.Result = PatchResult.Failed;
                report.Errors.Add($"Unhandled exception: {ex.Message}");
                _logger.Error($"Fatal error: {ex.Message}");
            }
            return report;
        }

        /// <summary>
        /// حقن MCC/MNC حقيقي: خصائص persist عبر root مع تحقق بالقراءة لكل خاصية،
        /// ثم إعادة تشغيل خدمات RIL فعلياً ومراقبة حالة الشريحة حتى LOADED.
        /// </summary>
        public async Task<bool> InjectMccMncAsync(NetworkEntry network)
        {
            try
            {
                if (network == null)
                {
                    _logger.Error("[Inject] No network selected for injection!");
                    return false;
                }
                if (!IsValidNetwork(network))
                {
                    _logger.Error("[Inject] Invalid MCC/MNC or operator name; injection aborted.");
                    return false;
                }

                // فحص مسبق إجباري: بدون اتصال ورووت حقيقي لا ينفَّذ شيء
                if (!await _adb.IsDeviceConnectedAsync())
                {
                    _logger.Error("[Inject] No device connected — MCC/MNC injection aborted!");
                    return false;
                }
                if (!await _adb.HasRootAccessAsync())
                {
                    _logger.Error("[Inject] Root access not available — setprop commands would be denied. Injection aborted!");
                    return false;
                }

                string fullCode = network.Mcc + network.Mnc; // مثال: 42101
                _logger.Info($"[Inject] Injecting MCC/MNC: {network.Operator} ({fullCode})");

                // 1. خصائص مستدامة (persist.*) — تُكتب فعلياً وتُقرأ للتحقق
                _logger.Info("[Inject] Setting persistent properties (root + read-back verification)...");
                bool codeSaved = await SetVerifiedPropAsync("persist.sys.hmax.mccmnc", fullCode);
                bool markerSaved = await SetVerifiedPropAsync("persist.sys.hmax.bypassed", "1");
                if (!codeSaved || !markerSaved)
                {
                    _logger.Error("[Inject] Persistent properties were not verified; injection aborted.");
                    return false;
                }

                // 2. خصائص التشغيل اللحظية (gsm.*) — root فقط؛ يعيد RIL كتابتها لاحقاً
                _logger.Info("[Inject] Setting runtime operator properties (transient until RIL refresh)...");
                await SetVerifiedPropAsync("gsm.sim.operator.numeric", fullCode, expectTransient: true);
                await SetVerifiedPropAsync("gsm.operator.numeric", fullCode, expectTransient: true);
                await SetVerifiedPropAsync("gsm.sim.operator.alpha", network.Operator ?? "", expectTransient: true);
                await SetVerifiedPropAsync("gsm.operator.alpha", network.Operator ?? "", expectTransient: true);

                // 3. إعادة تشغيل خدمات RIL فعلياً ومراقبة حالة الشريحة
                _logger.Info("[Inject] Restarting RIL and watching SIM state...");
                var sim = new SimStateController(_adb, _logger);
                bool loaded = await sim.RestartRilAndVerifyLoadedAsync(20);

                string simState = await sim.GetRawSimStateAsync();
                _logger.Info($"[Inject] Resulting SIM state: {simState}");

                if (loaded)
                {
                    _logger.Info("[Inject] Success! SIM state reached LOADED.");
                    return true;
                }

                if (simState.Contains("NETWORK_LOCKED"))
                {
                    _logger.Error("[Inject] SIM is still NETWORK_LOCKED — the carrier lock is enforced in " +
                                  "modem NV. Run the full Relock Bypass (QCNv reset) and reboot the device.");
                }
                else if (simState.Contains("ABSENT"))
                {
                    _logger.Error("[Inject] No SIM detected (ABSENT). Insert the SIM and retry.");
                }
                else
                {
                    _logger.Warn("[Inject] SIM state did not reach LOADED within timeout. State: " + simState);
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error("[Inject] Injection error: " + ex.Message);
                return false;
            }
        }

        /// <summary>يضبط خاصية عبر root ثم يقرأها للتأكد من تطبيقها فعلاً.</summary>
        private async Task<bool> SetVerifiedPropAsync(string key, string value, bool expectTransient = false)
        {
            string escapedValue = (value ?? "").Replace("'", "'\\''");
            await _adb.ExecuteRootAsync($"setprop {key} '{escapedValue}'");
            string readBack = (await _adb.ExecuteAsync($"getprop {key}")).Trim();

            if (readBack == value)
            {
                _logger.Info($"[Inject]   setprop {key}={value}: verified");
                return true;
            }

            // خصائص gsm.* يعيد RIL كتابتها فوراً؛ فشل القراءة هنا متوقع وليس نجاحاً كاذباً
            _logger.Warn(expectTransient
                ? $"[Inject]   setprop {key}={value}: overwritten by RIL (expected for gsm.* props)"
                : $"[Inject]   setprop {key}={value}: NOT applied (read back '{readBack}')");
            return false;
        }

        public async Task<LockStatus> CheckLockStatusAsync()
        {
            _logger.Info("=== Checking lock status ===");
            if (!await _adb.IsDeviceConnectedAsync())
                throw new InvalidOperationException("No device connected.");
            if (!await _adb.HasRootAccessAsync())
                throw new InvalidOperationException("Root access is required to read lock-related files.");

            var detector = new ChipsetDetector(_adb);
            var devInfo = await detector.DetectDeviceAsync();
            var pathEngine = new PathDiscoveryEngine(_adb);
            var paths = await pathEngine.DiscoverAsync(devInfo.Chipset);
            var strategy = PatchStrategyFactory.Create(devInfo.Chipset, _adb);
            var status = await strategy.DetectStatusAsync(paths, _logger);
            _logger.Info($"  Lock status: {status}");
            return status;
        }

        private static string NormalizeCsc(string targetCsc)
        {
            string csc = (targetCsc ?? "").Trim().ToUpperInvariant();
            if (csc.Length != 3 || csc.Any(c => c < 'A' || c > 'Z'))
                throw new ArgumentException("CSC must contain exactly three letters.", nameof(targetCsc));
            return csc;
        }

        private static bool IsValidNetwork(NetworkEntry network)
        {
            return !string.IsNullOrWhiteSpace(network.Operator)
                && network.Operator.Length <= 100
                && !network.Operator.Contains("\r") && !network.Operator.Contains("\n")
                && !string.IsNullOrEmpty(network.Mcc) && network.Mcc.Length == 3
                && network.Mcc.All(char.IsDigit)
                && !string.IsNullOrEmpty(network.Mnc)
                && (network.Mnc.Length == 2 || network.Mnc.Length == 3)
                && network.Mnc.All(char.IsDigit);
        }

        public async Task<BypassReport> ExecuteBackupModifyInjectAsync(string targetCsc = "XAA")
        {
            _logger.Info("Starting Backup-Modify-Inject workflow...");
            var engine = new BackupModifyInjectEngine(_adb, _logger);
            return await engine.ExecuteAsync(targetCsc);
        }
    }
}

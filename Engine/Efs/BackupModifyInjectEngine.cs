using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HmaxUnlocker.Engine.Communication;
using HmaxUnlocker.Engine.Detection;
using HmaxUnlocker.Engine.Logging;
using HmaxUnlocker.Engine.Models;
using HmaxUnlocker.Engine.Samsung;

namespace HmaxUnlocker.Engine.Efs
{
    /// <summary>
    /// سير عمل حقيقي: نسخ احتياطي فعلي (adb pull) ← تعديل محلي يحافظ على المحتوى الأصلي
    /// ← حقن فعلي (adb push مع إعادة تطبيق المالك/الصلاحيات و SELinux)
    /// ← إعادة تشغيل خدمات RIL ← تحقق صادق من حالة الشريحة.
    /// </summary>
    public class BackupModifyInjectEngine
    {
        private readonly IAdbShell _adb;
        private readonly ILogger _logger;
        private readonly EfsFileModifier _modifier;
        private readonly SimStateController _simController;

        public BackupModifyInjectEngine(IAdbShell adb, ILogger logger)
        {
            _adb = adb;
            _logger = logger;
            _modifier = new EfsFileModifier();
            _simController = new SimStateController(adb, logger);
        }

        public async Task<BypassReport> ExecuteAsync(string targetCsc, List<string> selectedMccMnc = null)
        {
            var report = new BypassReport();

            try
            {
                targetCsc = NormalizeCsc(targetCsc);

                // 0. فحوص مسبقة حقيقية
                _logger?.Info("[BMI] Step 0/5: Pre-flight checks...");
                if (!await _adb.IsDeviceConnectedAsync())
                {
                    report.Result = PatchResult.DeviceNotConnected;
                    report.Errors.Add("No device connected");
                    _logger?.Error("[BMI] No device connected!");
                    return report;
                }
                if (!await _adb.HasRootAccessAsync())
                {
                    report.Result = PatchResult.RootRequired;
                    report.Errors.Add("Root access not available");
                    _logger?.Error("[BMI] Root access not available!");
                    return report;
                }

                report.StatusBefore = await _simController.EvaluateLockStatusFromSimAsync();
                _logger?.Info($"[BMI] Status before: {report.StatusBefore}");

                var detector = new ChipsetDetector(_adb);
                report.Device = await detector.DetectDeviceAsync();
                var pathEngine = new PathDiscoveryEngine(_adb);
                report.Paths = await pathEngine.DiscoverAsync(report.Device.Chipset);

                if (string.IsNullOrEmpty(report.Paths.MpsCodePath))
                {
                    report.Result = PatchResult.PathNotFound;
                    report.Errors.Add("Could not locate mps_code.dat on device");
                    _logger?.Error("[BMI] mps_code.dat not found!");
                    return report;
                }

                string whitelist = NormalizeWhitelist(selectedMccMnc);

                // 1. نسخ احتياطي حقيقي عبر adb pull
                _logger?.Info("[BMI] Step 1/5: Backing up EFS files (real adb pull)...");
                string backupDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Backups",
                    DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(backupDir);

                var backupSpecs = BuildBackupList(report.Paths);
                int backed = 0, failed = 0;
                var manifestLines = new List<string>();

                foreach (var spec in backupSpecs)
                {
                    if (!await _adb.FileExistsAsync(spec.RemotePath))
                    {
                        manifestLines.Add($"{spec.RemotePath} -> (not present on device)");
                        continue;
                    }

                    string localPath = Path.Combine(backupDir, spec.LocalName);
                    bool ok = await _adb.PullFileAsync(spec.RemotePath, localPath);
                    long size = File.Exists(localPath) ? new FileInfo(localPath).Length : 0;

                    if (ok && size > 0)
                    {
                        backed++;
                        manifestLines.Add($"{spec.RemotePath} -> {spec.LocalName} ({size} bytes)");
                        _logger?.Info($"[BMI]   Backed up: {spec.RemotePath} ({size} bytes)");
                    }
                    else
                    {
                        failed++;
                        manifestLines.Add($"{spec.RemotePath} -> FAILED");
                        report.Errors.Add($"Backup failed: {spec.RemotePath}");
                        _logger?.Error($"[BMI]   Backup FAILED: {spec.RemotePath}");
                    }
                }

                WriteBackupManifest(backupDir, manifestLines, backed, failed);
                report.ActionsPerformed.Add($"Backup: {backed} files pulled to {backupDir} ({failed} failed)");
                _logger?.Info($"[BMI] Backup done: {backed} ok, {failed} failed");
                if (failed > 0 || backed == 0)
                {
                    report.Result = PatchResult.Failed;
                    report.Errors.Add("Backup was incomplete; no device files were modified.");
                    _logger?.Error("[BMI] Incomplete backup — modification aborted for safety.");
                    return report;
                }

                // 2. التعديل محلياً على النسخ المسحوبة (مع الحفاظ على المحتوى الأصلي)
                _logger?.Info("[BMI] Step 2/5: Modifying carrier files locally...");
                string mpsContent = _modifier.BuildMpsCodeContent(targetCsc);

                string telephonyModified = ModifyPropFileFromBackup(
                    backupDir, "telephony.prop", report.Paths.TelephonyPropPath,
                    _modifier.BuildTelephonyProps());

                string factoryModified = ModifyPropFileFromBackup(
                    backupDir, "factory.prop", report.Paths.FactoryPropPath,
                    _modifier.BuildCscProps(targetCsc, whitelist));

                // 3. الحقن الفعلي: كتابة عبر push مع المالك/الصلاحيات الأصلية
                _logger?.Info("[BMI] Step 3/5: Injecting modified files to device...");
                bool allInjectionsOk = await _adb.WriteFileAsync(report.Paths.MpsCodePath, mpsContent);
                LogInjection(report, "mps_code.dat", allInjectionsOk);

                if (telephonyModified != null)
                {
                    bool telOk = await WriteModifiedPropFileAsync(
                        backupDir, "telephony.prop", report.Paths.TelephonyPropPath);
                    LogInjection(report, "telephony.prop", telOk);
                    if (!telOk) allInjectionsOk = false;
                }

                if (factoryModified != null)
                {
                    bool facOk = await WriteModifiedPropFileAsync(
                        backupDir, "factory.prop", report.Paths.FactoryPropPath);
                    LogInjection(report, "factory.prop", facOk);
                    if (!facOk) allInjectionsOk = false;
                }

                // خصائص قابلة للضبط فعلياً وقت التشغيل (لا يمكن ضبط ro.* عبر setprop)
                _logger?.Info("[BMI] Setting runtime properties (root, verified)...");
                if (!await SetVerifiedPropAsync("persist.omc.salescode", targetCsc)) allInjectionsOk = false;
                if (!await SetVerifiedPropAsync("persist.sys.hmax.bypassed", "1")) allInjectionsOk = false;
                if (!string.IsNullOrEmpty(whitelist)
                    && !await SetVerifiedPropAsync("persist.sys.hmax.netlist", whitelist))
                    allInjectionsOk = false;

                if (!allInjectionsOk)
                {
                    report.Result = PatchResult.PartialSuccess;
                    report.Errors.Add("One or more injections failed; additional device changes were skipped.");
                    return report;
                }

                // 3.5 حزم سامسونج (تعطيل حقيقي مع تحقق)
                bool packageStepFailed = false;
                if (report.Device.IsSamsung)
                {
                    try
                    {
                        var pkgManager = new SamsungPackageManager(_adb, _logger);
                        report.PackageResult = await pkgManager.ExecuteBypassAsync();
                        report.ActionsPerformed.Add(
                            $"Disabled {report.PackageResult.DisabledPackages.Count} packages");
                        if (report.PackageResult.Errors.Count > 0)
                        {
                            packageStepFailed = true;
                            report.Errors.AddRange(report.PackageResult.Errors);
                        }
                    }
                    catch (Exception ex)
                    {
                        packageStepFailed = true;
                        report.Errors.Add("Samsung package bypass failed: " + ex.Message);
                        _logger?.Warn($"[BMI] Samsung package bypass failed: {ex.Message}");
                    }
                }

                // 4. إعادة تشغيل RIL والتحقق الفعلي من حالة الشريحة
                _logger?.Info("[BMI] Step 4/5: Restarting RIL and verifying SIM state...");
                bool isLoaded = await _simController.RestartRilAndVerifyLoadedAsync(15);
                report.StatusAfter = await _simController.EvaluateLockStatusFromSimAsync();
                _logger?.Info($"[BMI] Status after: {report.StatusAfter}");

                // 5. نتيجة صادقة
                _logger?.Info("[BMI] Step 5/5: Finalizing report...");
                if (isLoaded && !packageStepFailed)
                {
                    report.ActionsPerformed.Add("SIM state verified as LOADED after RIL restart.");
                    report.Result = PatchResult.Success;
                }
                else
                {
                    report.ActionsPerformed.Add(isLoaded
                        ? "SIM reached LOADED, but one or more Samsung package operations failed."
                        : "Files injected. SIM did not reach LOADED after RIL restart — " +
                          "the carrier lock is enforced in modem NV; reboot the device and/or " +
                          "run the full Relock Bypass (QCNv reset) if the SIM is still NETWORK_LOCKED.");
                    report.Result = PatchResult.PartialSuccess;
                }
            }
            catch (Exception ex)
            {
                report.Errors.Add(ex.Message);
                report.Result = PatchResult.Failed;
                _logger?.Error($"[BMI] Fatal error: {ex.Message}");
            }

            return report;
        }

        private sealed class BackupSpec
        {
            public string RemotePath;
            public string LocalName;
        }

        private static List<BackupSpec> BuildBackupList(EfsPaths paths)
        {
            var specs = new List<BackupSpec>();
            void Add(string remote, string local)
            {
                if (!string.IsNullOrEmpty(remote))
                    specs.Add(new BackupSpec { RemotePath = remote, LocalName = local });
            }

            Add(paths.MpsCodePath, "mps_code.dat");
            Add(paths.CidDatPath, "cid.dat");
            Add(paths.QcnvDataPath, "QCNv_data.bin");
            if (!string.IsNullOrEmpty(paths.QcnvDataPath))
                Add(paths.QcnvDataPath + ".md5", "QCNv_data.bin.md5");
            Add(paths.NvDataPath, "nv_data.bin");
            if (!string.IsNullOrEmpty(paths.NvDataPath))
                Add(paths.NvDataPath + ".md5", "nv_data.bin.md5");
            Add(paths.TelephonyPropPath, "telephony.prop");
            Add(paths.FactoryPropPath, "factory.prop");
            Add(paths.PnPath, "pn");
            return specs;
        }

        private void WriteBackupManifest(string backupDir, List<string> lines, int ok, int fail)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("HMAX UNLOCKER PRO - Backup/Modify/Inject Manifest");
                sb.AppendLine($"Date: {DateTime.Now}");
                sb.AppendLine($"Files backed up: {ok}");
                sb.AppendLine($"Files failed: {fail}");
                sb.AppendLine("─────────────────────────────────────");
                foreach (var line in lines)
                    sb.AppendLine(line);
                File.WriteAllText(Path.Combine(backupDir, "backup_manifest.txt"), sb.ToString());
            }
            catch (Exception ex)
            {
                _logger?.Warn($"[BMI] Could not write manifest: {ex.Message}");
            }
        }

        /// <summary>
        /// يبني النسخة المعدلة من ملف خصائص اعتماداً على النسخة الاحتياطية المسحوبة،
        /// ويحفظها محلياً باسم <paramref name="name"/>.modified. يعيد null إن لم يتوفر الأصل.
        /// </summary>
        private string ModifyPropFileFromBackup(
            string backupDir, string name, string remotePath, IDictionary<string, string> props)
        {
            if (string.IsNullOrEmpty(remotePath))
                return null;

            string backupPath = Path.Combine(backupDir, name);
            if (!File.Exists(backupPath))
            {
                _logger?.Warn($"[BMI] No backup copy of {name} — skipping its modification");
                return null;
            }

            string original = File.ReadAllText(backupPath);
            string modified = _modifier.UpsertProps(
                original, props, $"# patched by HMAX UNLOCKER PRO - {DateTime.Now:yyyy-MM-dd HH:mm}");

            _modifier.WriteLocalFile(Path.Combine(backupDir, name + ".modified"), modified);
            _logger?.Info($"[BMI]   Modified {name} ({props.Count} keys upserted, original lines preserved)");
            return modified;
        }

        private async Task<bool> WriteModifiedPropFileAsync(
            string backupDir, string name, string remotePath)
        {
            string modifiedPath = Path.Combine(backupDir, name + ".modified");
            return File.Exists(modifiedPath) && await _adb.PushFileAsync(modifiedPath, remotePath);
        }

        private void LogInjection(BypassReport report, string name, bool ok)
        {
            if (ok)
            {
                report.ActionsPerformed.Add($"Injected: {name}");
                _logger?.Info($"[BMI]   Injected {name}: OK");
            }
            else
            {
                report.Errors.Add($"Injection failed: {name}");
                _logger?.Error($"[BMI]   Injected {name}: FAILED");
            }
        }

        private static string NormalizeCsc(string targetCsc)
        {
            string csc = (targetCsc ?? "").Trim().ToUpperInvariant();
            if (csc.Length != 3 || csc.Any(c => c < 'A' || c > 'Z'))
                throw new ArgumentException("CSC must contain exactly three letters.", nameof(targetCsc));
            return csc;
        }

        private static string NormalizeWhitelist(List<string> selectedMccMnc)
        {
            if (selectedMccMnc == null || selectedMccMnc.Count == 0)
                return "";

            var codes = selectedMccMnc.Select(c => (c ?? "").Trim()).ToList();
            if (codes.Any(c => (c.Length != 5 && c.Length != 6) || !c.All(char.IsDigit)))
                throw new ArgumentException("Each MCC/MNC code must contain five or six digits.", nameof(selectedMccMnc));
            return string.Join(",", codes);
        }

        /// <summary>يضبط خاصية عبر root ثم يتحقق منها بقراءتها فعلياً.</summary>
        private async Task<bool> SetVerifiedPropAsync(string key, string value)
        {
            await _adb.ExecuteRootAsync($"setprop {key} {value}");
            string readBack = (await _adb.ExecuteAsync($"getprop {key}")).Trim();
            bool ok = readBack == value;
            if (ok)
                _logger?.Info($"[BMI]   setprop {key}={value}: verified");
            else
                _logger?.Warn($"[BMI]   setprop {key}={value}: NOT applied (read back '{readBack}')");
            return ok;
        }
    }
}

using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using HmaxUnlocker.Engine;
using HmaxUnlocker.Engine.Communication;
using HmaxUnlocker.Engine.Detection;
using HmaxUnlocker.Engine.Info;
using HmaxUnlocker.Engine.Logging;
using HmaxUnlocker.Engine.Models;
using HmaxUnlocker.Engine.Network;

namespace HmaxUnlocker.Forms
{
    public partial class Form1 : Form
    {
        private IAdbShell _adb;
        private ILogger _logger;
        private UniversalBypassEngine _engine;
        private DeviceInfoReader _infoReader;
        private NetworkStore _networkStore; // تم توحيد التسمية هنا
        private MccMncDatabase _mccDatabase;

        public Form1()
        {
            InitializeComponent();
            InitializeEngine();
            WireUpEvents();
            InitUI();
        }

        private void InitializeEngine()
        {
            _adb = new AdbShell();
            _logger = new WinFormsLogger(txtLogOutput);
            _engine = new UniversalBypassEngine(_adb, _logger);
            _infoReader = new DeviceInfoReader(_adb, _logger);
            _networkStore = new NetworkStore();
            _mccDatabase = new MccMncDatabase();
        }

        private void WireUpEvents()
        {
            btnReadInfo.Click += BtnReadInfo_Click;
            btnCheckLock.Click += BtnCheckLock_Click;
            btnRelockBypass.Click += btnRelockBypass_Click;
            btnBackup.Click += BtnBackup_Click;
            btnNetworkConfig.Click += BtnNetworkConfig_Click;
            btnCpInject.Click += BtnCpInject_Click; // CP Inject — خاصية مضافة منفصلة
            btnBlockOta.Click += BtnBlockOta_Click; // Block OTA — منع/استرجاع تحديثات النظام
            btnRestoreBackup.Click += BtnRestoreBackup_Click; // Restore — استرجاع نسخة لمساراتها الأصلية
            btnReboot.Click += BtnReboot_Click; // Reboot — عادي/ريكفري/داونلود
        }

        private void InitUI()
        {
            AppendLog("HMAX UNLOCKER PRO - Ready", Color.FromArgb(137, 180, 250));
            AppendLog("Waiting for device...", Color.FromArgb(166, 173, 200));
        }

        private async void BtnReadInfo_Click(object sender, EventArgs e)
        {
            SetBusy(true);
            AppendLog("--- Read Info ---", Color.FromArgb(137, 180, 250));
            try
            {
                var info = await _infoReader.ReadAllAsync();
                if (!info.IsConnected) { AppendLog("No device!", Color.FromArgb(243, 139, 168)); return; }

                txtManufacturer.Text = info.Manufacturer;
                txtModel.Text = info.Model;
                txtCodeName.Text = info.CodeName;
                txtIMEI1.Text = info.IMEI1;
                txtIMEI2.Text = info.IMEI2;
                txtAndroidVersion.Text = info.AndroidVersion;
                txtBaseband.Text = info.Baseband;
                txtBootloader.Text = info.Bootloader;
                txtSoftware.Text = info.Software;
                txtSecurityPatch.Text = info.SecurityPatch;
                txtBoard.Text = info.Board;
                txtHardware.Text = info.Hardware;
                txtCpuArch.Text = info.CpuArch;
                txtWarrantyVoid.Text = info.WarrantyVoid;
                txtKGState.Text = info.KGState;
                txtSELinux.Text = info.SELinux;
                txtSIM.Text = info.SIM;
                txtSIMStatus.Text = info.SIMStatus;
                txtUSBConfig.Text = info.USBConfig;
                txtServiceProvider.Text = info.ServiceProvider;

                lblConnectionStatus.Text = "Connected: " + info.DeviceID;
                lblConnectionStatus.ForeColor = Color.FromArgb(166, 227, 161);
                lblLockStatus.Text = "Lock: " + info.LockStatus;

                AppendLog("Read complete!", Color.FromArgb(166, 173, 200));
            }
            catch (Exception ex) { AppendLog("Error: " + ex.Message, Color.FromArgb(243, 139, 168)); }
            finally { SetBusy(false); }
        }

        private async void BtnCheckLock_Click(object sender, EventArgs e)
        {
            SetBusy(true);
            AppendLog("--- Check Lock ---", Color.FromArgb(249, 226, 175));
            try
            {
                var status = await _engine.CheckLockStatusAsync();
                lblLockStatus.Text = "Lock: " + status;
                AppendLog("Status: " + status, Color.FromArgb(249, 226, 175));
            }
            catch (Exception ex) { AppendLog("Error: " + ex.Message, Color.FromArgb(243, 139, 168)); }
            finally { SetBusy(false); }
        }

        private async void btnRelockBypass_Click(object sender, EventArgs e)
        {
            if (_networkStore.SelectedNetworks.Count == 0)
            {
                MessageBox.Show("Please select at least one network first using Network Config!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedNetwork = _networkStore.SelectedNetworks[0]; // استخدام المتحدث الصحيح

            // ═══ بوابة التأكيد قبل عملية مدمرة (سلامة) ═══
            var gate = MessageBox.Show(
                "Relock Bypass performs DESTRUCTIVE operations:\n\n" +
                "  •  Clears QCNv_data.bin (modem NV) permanently\n" +
                "  •  Rewrites mps_code.dat / telephony.prop / factory.prop\n" +
                "  •  Disables Samsung carrier packages\n\n" +
                "The tool saves a safety NV copy automatically before clearing,\n" +
                "but a FULL backup is strongly recommended.\n\n" +
                "YES     =  I already made a backup — proceed\n" +
                "NO      =  Take a quick backup now, then proceed\n" +
                "CANCEL  =  Abort",
                "Relock Bypass — Confirmation",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

            if (gate == DialogResult.Cancel)
            {
                AppendLog("Relock Bypass cancelled by user.", Color.FromArgb(166, 173, 200));
                return;
            }

            if (gate == DialogResult.No)
            {
                // نسخة احتياطية سريعة إجبارية قبل المتابعة — فشلها يوقف العملية
                AppendLog("--- Quick backup before bypass ---", Color.FromArgb(203, 166, 247));
                try
                {
                    SetBusy(true);
                    var backupEngine = new Engine.Backup.BackupEngine(_adb, _logger);
                    string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
                    string dir = await backupEngine.ExecuteAsync(path);
                    if (backupEngine.LastSucceededFileCount == 0 || backupEngine.LastFailedFileCount > 0)
                    {
                        AppendLog("Quick backup was incomplete — bypass aborted.", Color.FromArgb(243, 139, 168));
                        MessageBox.Show("The quick backup was incomplete. Bypass aborted for safety.",
                            "Aborted", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    AppendLog("Quick backup saved: " + dir, Color.FromArgb(166, 227, 161));
                }
                catch (Exception bex)
                {
                    AppendLog("Quick backup FAILED: " + bex.Message, Color.FromArgb(243, 139, 168));
                    MessageBox.Show("Backup failed — bypass aborted for safety. Check the device connection and root, then retry.",
                        "Aborted", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                finally
                {
                    SetBusy(false);
                }
            }

            SetBusy(true);
            AppendLog("--- Relock Bypass & MCC/MNC Injection ---", Color.FromArgb(166, 227, 161));

            try
            {
                var report = await _engine.ExecuteAsync("XAA");
                foreach (string error in report.Errors)
                    AppendLog("ERROR: " + error, Color.FromArgb(243, 139, 168));
                if (report.PackageResult != null)
                    foreach (string error in report.PackageResult.Errors)
                        AppendLog("PACKAGE ERROR: " + error, Color.FromArgb(249, 226, 175));

                if (report.Result != PatchResult.Success)
                {
                    AppendLog($"EFS/NV step did not succeed ({report.Result}); MCC/MNC injection skipped.",
                        Color.FromArgb(243, 139, 168));
                    MessageBox.Show("The EFS/NV operation did not complete successfully. See the log; no success was assumed.",
                        "Operation incomplete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                bool success = await _engine.InjectMccMncAsync(selectedNetwork);
                if (success)
                {
                    lblLockStatus.Text = "SIM: LOADED after verified injection";
                    lblLockStatus.ForeColor = Color.FromArgb(166, 227, 161);
                    AppendLog("EFS/NV step and MCC/MNC injection completed; SIM reached LOADED.", Color.FromArgb(166, 227, 161));
                    MessageBox.Show("The operations completed and the SIM reached LOADED. This does not by itself prove an official carrier unlock.",
                        "Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    AppendLog("EFS/NV step succeeded, but MCC/MNC injection or SIM verification failed.", Color.FromArgb(249, 226, 175));
                    MessageBox.Show("The EFS/NV step succeeded, but MCC/MNC injection or SIM verification failed. Check the log.",
                        "Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                AppendLog("Error during bypass: " + ex.Message, Color.FromArgb(243, 139, 168));
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void BtnBackup_Click(object sender, EventArgs e)
        {
            SetBusy(true);
            AppendLog("--- Backup ---", Color.FromArgb(203, 166, 247));
            try
            {
                var backupEngine = new Engine.Backup.BackupEngine(_adb, _logger);
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
                string dir = await backupEngine.ExecuteAsync(path);
                if (backupEngine.LastSucceededFileCount == 0)
                {
                    AppendLog("Backup failed: no device files were saved.", Color.FromArgb(243, 139, 168));
                    MessageBox.Show("Backup failed. No device files were saved; check the log.",
                        "Backup", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else if (backupEngine.LastFailedFileCount > 0)
                {
                    AppendLog($"Backup is incomplete: {backupEngine.LastSucceededFileCount} saved, " +
                        $"{backupEngine.LastFailedFileCount} failed. Folder: {dir}",
                        Color.FromArgb(249, 226, 175));
                    MessageBox.Show("The backup is incomplete. Do not rely on it for a destructive operation; see the log.",
                        "Incomplete backup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    AppendLog($"Backup saved: {dir} ({backupEngine.LastSucceededFileCount} files)",
                        Color.FromArgb(166, 227, 161));
                }
            }
            catch (Exception ex) { AppendLog("Error: " + ex.Message, Color.FromArgb(243, 139, 168)); }
            finally { SetBusy(false); }
        }

        private void BtnNetworkConfig_Click(object sender, EventArgs e)
        {
            AppendLog("--- Network Config ---", Color.FromArgb(148, 226, 213));
            using (var form = new NetworkConfigForm(_mccDatabase, _networkStore))
            {
                form.ShowDialog(this);
            }
            lblSelectedNetworks.Text = "Networks: " + _networkStore.GetSelectedSummary();
        }

        // ═══════════════════════════════════════════════════════════════
        // CP Inject — خاصية حقن ملف CP/إعدادات الناقل ثم حقن MCC/MNC
        // (وحدة مستقلة مضافة إلى HmaxUnlockerPro)
        // ═══════════════════════════════════════════════════════════════
        private async void BtnCpInject_Click(object sender, EventArgs e)
        {
            if (_networkStore.SelectedNetworks.Count == 0)
            {
                MessageBox.Show("Please select at least one network first using Network Config!",
                    "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            AppendLog("--- CP Inject (CP file + MCC/MNC injection) ---", Color.FromArgb(250, 179, 135));
            AppendLog("CP Inject module: added to HmaxUnlockerPro — real file injection with backup + verification",
                Color.FromArgb(250, 179, 135));

            // 1. اختيار ملف CP من الحاسوب
            string localCpFile;
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Select CP / carrier config file to inject";
                ofd.Filter = "All files (*.*)|*.*";
                if (ofd.ShowDialog() != DialogResult.OK)
                    return;
                localCpFile = ofd.FileName;
            }

            // 2. اقتراح المسار الوجهة بناءً على الموجود فعلاً على الجهاز
            SetBusy(true);
            string defaultTarget;
            try
            {
                var cpEngine = new Engine.Cp.CpInjectEngine(_adb, _logger);
                defaultTarget = await cpEngine.DiscoverDefaultTargetAsync(Path.GetFileName(localCpFile));
            }
            catch (Exception ex)
            {
                AppendLog("CP target discovery failed: " + ex.Message, Color.FromArgb(243, 139, 168));
                MessageBox.Show("Could not inspect the device for a CP target. Check ADB/root and retry.",
                    "CP Inject", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                SetBusy(false);
            }

            string remoteTarget = PromptDialog.Show(
                "CP Injection Target",
                "Full remote path where the CP file will be injected:",
                defaultTarget);
            if (string.IsNullOrWhiteSpace(remoteTarget))
                return;
            remoteTarget = remoteTarget.Trim();

            // 3. حقن ملف CP الفعلي (نسخة احتياطية إجبارية + دفع + تحقق بالحجم)
            SetBusy(true);
            try
            {
                var cpEngine = new Engine.Cp.CpInjectEngine(_adb, _logger);
                var cpResult = await cpEngine.InjectCpFileAsync(localCpFile, remoteTarget);

                foreach (var action in cpResult.ActionsPerformed)
                    AppendLog("  " + action, Color.FromArgb(166, 227, 161));
                foreach (var err in cpResult.Errors)
                    AppendLog("  ERROR: " + err, Color.FromArgb(243, 139, 168));

                if (cpResult.Result != PatchResult.Success)
                {
                    AppendLog("CP injection FAILED — MCC/MNC injection skipped.", Color.FromArgb(243, 139, 168));
                    MessageBox.Show("CP injection failed. See log for details.", "CP Inject",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 4. حقن MCC/MNC للشبكة المختارة (خصائص root مُتحقَّق منها + إعادة تشغيل RIL + مراقبة SIM)
                var network = _networkStore.SelectedNetworks[0];
                AppendLog($"CP injected OK — now injecting MCC/MNC: {network.Operator} ({network.FullCode})",
                    Color.FromArgb(250, 179, 135));
                bool simOk = await _engine.InjectMccMncAsync(network);

                if (simOk)
                {
                    lblLockStatus.Text = "Lock Status: Injected (SIM LOADED)";
                    lblLockStatus.ForeColor = Color.FromArgb(166, 227, 161);
                    MessageBox.Show("CP file injected and verified, and SIM reached LOADED after MCC/MNC injection.",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("CP file was injected and verified, but the SIM did not reach LOADED. " +
                        "Check the log — if the SIM is NETWORK_LOCKED the lock lives in modem NV " +
                        "(needs server-side unlock + Relock Bypass + reboot).", "Notice",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                AppendLog("CP Inject error: " + ex.Message, Color.FromArgb(243, 139, 168));
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void AppendLog(string msg, Color color)
        {
            if (txtLogOutput.InvokeRequired)
            {
                txtLogOutput.Invoke(new Action(() => AppendLog(msg, color)));
                return;
            }
            txtLogOutput.SelectionStart = txtLogOutput.TextLength;
            txtLogOutput.SelectionColor = color;
            txtLogOutput.AppendText(msg + Environment.NewLine);
            txtLogOutput.ScrollToCaret();
        }

        private void SetBusy(bool busy)
        {
            btnReadInfo.Enabled = !busy;
            btnCheckLock.Enabled = !busy;
            btnRelockBypass.Enabled = !busy;
            btnBackup.Enabled = !busy;
            btnRestoreBackup.Enabled = !busy;
            btnNetworkConfig.Enabled = !busy;
            btnCpInject.Enabled = !busy;
            btnBlockOta.Enabled = !busy;
            btnReboot.Enabled = !busy;
            progressBar.MarqueeAnimationSpeed = busy ? 30 : 0;
        }

        // ═══════════════════════════════════════════════════════════════
        // Restore Backup — استرجاع نسخة احتياطية إلى مساراتها الأصلية
        // (النسخ الاحتياطي يحفظ نفس مسارات وأسماء الملفات كما على الهاتف)
        // ═══════════════════════════════════════════════════════════════
        private async void BtnRestoreBackup_Click(object sender, EventArgs e)
        {
            AppendLog("--- Restore Backup ---", Color.FromArgb(180, 142, 247));

            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select the backup folder to restore " +
                    "(must contain mirrored device paths like efs\\imei\\cid.dat)";
                string defaultDir = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Backups");
                if (Directory.Exists(defaultDir))
                    fbd.SelectedPath = defaultDir;

                if (fbd.ShowDialog() != DialogResult.OK)
                    return;

                var confirm = MessageBox.Show(
                    "Files from this backup will be pushed back to their ORIGINAL " +
                    "paths on the device (efs/..., mnt/vendor/efs/...).\n\n" +
                    "Current EFS content at those paths will be overwritten.\n\nContinue?",
                    "Restore Backup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes)
                    return;

                SetBusy(true);
                try
                {
                    var engine = new Engine.Backup.RestoreEngine(_adb, _logger);
                    var result = await engine.ExecuteAsync(fbd.SelectedPath);

                    foreach (var f in result.RestoredFiles)
                        AppendLog("  Restored: " + f, Color.FromArgb(166, 227, 161));
                    foreach (var f in result.FailedFiles)
                        AppendLog("  FAILED: " + f, Color.FromArgb(243, 139, 168));
                    foreach (var err in result.Errors)
                        AppendLog("  ERROR: " + err, Color.FromArgb(243, 139, 168));

                    if (result.Result == PatchResult.Success)
                    {
                        AppendLog($"Restore complete: {result.RestoredFiles.Count} files. Reboot the device to apply fully.",
                            Color.FromArgb(166, 227, 161));
                        MessageBox.Show(
                            $"Restored {result.RestoredFiles.Count} files to original paths.\nReboot the device to apply.",
                            "Restore", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else if (result.Result == PatchResult.PartialSuccess)
                    {
                        AppendLog($"Restore partially done: {result.RestoredFiles.Count} ok, {result.FailedFiles.Count} failed.",
                            Color.FromArgb(249, 226, 175));
                        MessageBox.Show(
                            $"Restored {result.RestoredFiles.Count} files, {result.FailedFiles.Count} failed. See log.",
                            "Restore", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        MessageBox.Show("Restore failed. See log for details.", "Restore",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    AppendLog("Restore error: " + ex.Message, Color.FromArgb(243, 139, 168));
                    MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    SetBusy(false);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Reboot — إعادة تشغيل الجهاز (عادي / ريكفري / وضع الداونلود)
        // ═══════════════════════════════════════════════════════════════
        private async void BtnReboot_Click(object sender, EventArgs e)
        {
            string mode = RebootDialog.Pick();
            if (mode == null)
                return;

            string label = mode == "" ? "Normal" : mode;
            AppendLog($"--- Reboot ({label}) ---", Color.FromArgb(166, 173, 200));

            SetBusy(true);
            try
            {
                string output = await _adb.RebootAsync(mode == "" ? null : mode);
                AppendLog("Reboot command sent. Wait for the device to come back...",
                    Color.FromArgb(166, 173, 200));
            }
            catch (Exception ex)
            {
                AppendLog("Reboot error: " + ex.Message, Color.FromArgb(243, 139, 168));
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Block OTA — منع تحديثات النظام أو استرجاعها
        // (التحديث التلقائي أكبر سبب لإعادة تطبيق القفل بعد الفتح)
        // ═══════════════════════════════════════════════════════════════
        private async void BtnBlockOta_Click(object sender, EventArgs e)
        {
            var choice = MessageBox.Show(
                "YES  = Block OTA updates (disable FOTA/SOAgent packages)\n" +
                "NO   = Restore OTA updates (re-enable them)\n" +
                "CANCEL = do nothing",
                "Block OTA", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            if (choice == DialogResult.Cancel)
                return;

            bool block = choice == DialogResult.Yes;
            AppendLog(block ? "--- Block OTA ---" : "--- Restore OTA ---",
                Color.FromArgb(242, 143, 173));

            SetBusy(true);
            try
            {
                var ota = new Engine.Ota.OtaBlocker(_adb, _logger);
                var result = block ? await ota.DisableAsync() : await ota.RestoreAsync();

                foreach (var pkg in result.HandledPackages)
                    AppendLog($"  {(block ? "Blocked" : "Restored")}: {pkg}", Color.FromArgb(166, 227, 161));
                foreach (var pkg in result.SkippedPackages)
                    AppendLog($"  Not present (skipped): {pkg}", Color.FromArgb(166, 173, 200));
                foreach (var err in result.Errors)
                    AppendLog("  ERROR: " + err, Color.FromArgb(243, 139, 168));

                string action = block ? "Blocked" : "Restored";
                string summary = $"{action} {result.HandledPackages.Count} update package(s); " +
                    $"{result.SkippedPackages.Count} not present, {result.Errors.Count} failed.";
                if (result.Result == PatchResult.Success)
                {
                    summary += block
                        ? " The detected OTA packages were disabled successfully."
                        : " The detected OTA packages were enabled successfully.";
                    AppendLog(summary, Color.FromArgb(166, 227, 161));
                    MessageBox.Show(summary, block ? "OTA Blocked" : "OTA Restored",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (result.Result == PatchResult.PartialSuccess)
                {
                    summary += " The operation was incomplete; see the log.";
                    AppendLog(summary, Color.FromArgb(249, 226, 175));
                    MessageBox.Show(summary, "OTA partially completed",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    summary += " No OTA package was changed successfully.";
                    AppendLog(summary, Color.FromArgb(243, 139, 168));
                    MessageBox.Show(summary, "OTA operation failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                AppendLog("Block OTA error: " + ex.Message, Color.FromArgb(243, 139, 168));
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }
    }
}

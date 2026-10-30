namespace HmaxUnlocker.Forms
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            System.Drawing.Color clrBackground    = System.Drawing.Color.FromArgb(30, 30, 46);
            System.Drawing.Color clrPanel         = System.Drawing.Color.FromArgb(49, 50, 68);
            System.Drawing.Color clrBorder        = System.Drawing.Color.FromArgb(69, 71, 90);
            System.Drawing.Color clrTextPrimary   = System.Drawing.Color.FromArgb(205, 214, 244);
            System.Drawing.Color clrTextSecondary = System.Drawing.Color.FromArgb(166, 173, 200);
            System.Drawing.Color clrAccentBlue    = System.Drawing.Color.FromArgb(137, 180, 250);
            System.Drawing.Color clrAccentRed     = System.Drawing.Color.FromArgb(243, 139, 168);
            System.Drawing.Color clrAccentGreen   = System.Drawing.Color.FromArgb(166, 227, 161);
            System.Drawing.Color clrAccentYellow  = System.Drawing.Color.FromArgb(249, 226, 175);
            System.Drawing.Color clrAccentMauve   = System.Drawing.Color.FromArgb(203, 166, 247);
            System.Drawing.Color clrAccentTeal    = System.Drawing.Color.FromArgb(148, 226, 213);
            System.Drawing.Color clrInputBg       = System.Drawing.Color.FromArgb(58, 59, 83);
            System.Drawing.Color clrLogBg         = System.Drawing.Color.FromArgb(23, 23, 36);

            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = clrBackground;
            this.ClientSize = new System.Drawing.Size(960, 740);
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular);
            this.ForeColor = clrTextPrimary;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "  HMAX UNLOCKER PRO";

            // Header
            pnlHeader = new System.Windows.Forms.Panel();
            pnlHeader.Name = "pnlHeader";
            pnlHeader.Location = new System.Drawing.Point(0, 0);
            pnlHeader.Size = new System.Drawing.Size(960, 50);
            pnlHeader.BackColor = clrPanel;

            lblTitle = new System.Windows.Forms.Label();
            lblTitle.Name = "lblTitle";
            lblTitle.Text = "HMAX UNLOCKER PRO";
            lblTitle.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            lblTitle.ForeColor = clrAccentRed;
            lblTitle.Location = new System.Drawing.Point(15, 10);
            lblTitle.AutoSize = true;

            lblConnectionStatus = new System.Windows.Forms.Label();
            lblConnectionStatus.Name = "lblConnectionStatus";
            lblConnectionStatus.Text = "Disconnected";
            lblConnectionStatus.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            lblConnectionStatus.ForeColor = clrTextSecondary;
            lblConnectionStatus.Location = new System.Drawing.Point(780, 16);
            lblConnectionStatus.AutoSize = true;

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblConnectionStatus);

            // Device Info Group
            grpDeviceInfo = new System.Windows.Forms.GroupBox();
            grpDeviceInfo.Name = "grpDeviceInfo";
            grpDeviceInfo.Text = "  Device Information  ";
            grpDeviceInfo.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            grpDeviceInfo.ForeColor = clrAccentBlue;
            grpDeviceInfo.Location = new System.Drawing.Point(12, 58);
            grpDeviceInfo.Size = new System.Drawing.Size(936, 310);
            grpDeviceInfo.BackColor = clrPanel;
            grpDeviceInfo.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

            System.Drawing.Font labelFont = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Bold);
            System.Drawing.Font valueFont = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Regular);
            System.Drawing.Size txtSize = new System.Drawing.Size(280, 25);
            int col1X = 20, col1TxtX = 140, col2X = 470, col2TxtX = 590;
            int rowH = 28, startY = 28;

            // Left column
            lblManufacturer = CreateLabel("Manufacturer:", col1X, startY, labelFont, clrTextSecondary);
            txtManufacturer = CreateTextBox("txtManufacturer", col1TxtX, startY, txtSize, valueFont, clrInputBg);
            lblModel = CreateLabel("Model:", col1X, startY + rowH, labelFont, clrTextSecondary);
            txtModel = CreateTextBox("txtModel", col1TxtX, startY + rowH, txtSize, valueFont, clrInputBg);
            lblCodeName = CreateLabel("Code Name:", col1X, startY + rowH*2, labelFont, clrTextSecondary);
            txtCodeName = CreateTextBox("txtCodeName", col1TxtX, startY + rowH*2, txtSize, valueFont, clrInputBg);
            lblIMEI1 = CreateLabel("IMEI 1:", col1X, startY + rowH*3, labelFont, clrTextSecondary);
            txtIMEI1 = CreateTextBox("txtIMEI1", col1TxtX, startY + rowH*3, txtSize, valueFont, clrInputBg);
            lblIMEI2 = CreateLabel("IMEI 2:", col1X, startY + rowH*4, labelFont, clrTextSecondary);
            txtIMEI2 = CreateTextBox("txtIMEI2", col1TxtX, startY + rowH*4, txtSize, valueFont, clrInputBg);
            lblAndroidVersion = CreateLabel("Android Ver:", col1X, startY + rowH*5, labelFont, clrTextSecondary);
            txtAndroidVersion = CreateTextBox("txtAndroidVersion", col1TxtX, startY + rowH*5, txtSize, valueFont, clrInputBg);
            lblBaseband = CreateLabel("Baseband:", col1X, startY + rowH*6, labelFont, clrTextSecondary);
            txtBaseband = CreateTextBox("txtBaseband", col1TxtX, startY + rowH*6, txtSize, valueFont, clrInputBg);
            lblBootloader = CreateLabel("Bootloader:", col1X, startY + rowH*7, labelFont, clrTextSecondary);
            txtBootloader = CreateTextBox("txtBootloader", col1TxtX, startY + rowH*7, txtSize, valueFont, clrInputBg);
            lblSoftware = CreateLabel("Software:", col1X, startY + rowH*8, labelFont, clrTextSecondary);
            txtSoftware = CreateTextBox("txtSoftware", col1TxtX, startY + rowH*8, txtSize, valueFont, clrInputBg);
            lblSecurityPatch = CreateLabel("Security Patch:", col1X, startY + rowH*9, labelFont, clrTextSecondary);
            txtSecurityPatch = CreateTextBox("txtSecurityPatch", col1TxtX, startY + rowH*9, txtSize, valueFont, clrInputBg);

            // Right column
            lblBoard = CreateLabel("Board:", col2X, startY, labelFont, clrTextSecondary);
            txtBoard = CreateTextBox("txtBoard", col2TxtX, startY, txtSize, valueFont, clrInputBg);
            lblHardware = CreateLabel("Hardware:", col2X, startY + rowH, labelFont, clrTextSecondary);
            txtHardware = CreateTextBox("txtHardware", col2TxtX, startY + rowH, txtSize, valueFont, clrInputBg);
            lblCpuArch = CreateLabel("CPU Arch:", col2X, startY + rowH*2, labelFont, clrTextSecondary);
            txtCpuArch = CreateTextBox("txtCpuArch", col2TxtX, startY + rowH*2, txtSize, valueFont, clrInputBg);
            lblWarrantyVoid = CreateLabel("Warranty Void:", col2X, startY + rowH*3, labelFont, clrTextSecondary);
            txtWarrantyVoid = CreateTextBox("txtWarrantyVoid", col2TxtX, startY + rowH*3, txtSize, valueFont, clrInputBg);
            lblKGState = CreateLabel("KG State:", col2X, startY + rowH*4, labelFont, clrTextSecondary);
            txtKGState = CreateTextBox("txtKGState", col2TxtX, startY + rowH*4, txtSize, valueFont, clrInputBg);
            lblSELinux = CreateLabel("SELinux:", col2X, startY + rowH*5, labelFont, clrTextSecondary);
            txtSELinux = CreateTextBox("txtSELinux", col2TxtX, startY + rowH*5, txtSize, valueFont, clrInputBg);
            lblSIM = CreateLabel("SIM:", col2X, startY + rowH*6, labelFont, clrTextSecondary);
            txtSIM = CreateTextBox("txtSIM", col2TxtX, startY + rowH*6, txtSize, valueFont, clrInputBg);
            lblSIMStatus = CreateLabel("SIM Status:", col2X, startY + rowH*7, labelFont, clrTextSecondary);
            txtSIMStatus = CreateTextBox("txtSIMStatus", col2TxtX, startY + rowH*7, txtSize, valueFont, clrInputBg);
            lblUSBConfig = CreateLabel("USB Config:", col2X, startY + rowH*8, labelFont, clrTextSecondary);
            txtUSBConfig = CreateTextBox("txtUSBConfig", col2TxtX, startY + rowH*8, txtSize, valueFont, clrInputBg);
            lblServiceProvider = CreateLabel("Service Provider:", col2X, startY + rowH*9, labelFont, clrTextSecondary);
            txtServiceProvider = CreateTextBox("txtServiceProvider", col2TxtX, startY + rowH*9, txtSize, valueFont, clrInputBg);

            grpDeviceInfo.Controls.AddRange(new System.Windows.Forms.Control[] {
                lblManufacturer, txtManufacturer, lblModel, txtModel, lblCodeName, txtCodeName,
                lblIMEI1, txtIMEI1, lblIMEI2, txtIMEI2, lblAndroidVersion, txtAndroidVersion,
                lblBaseband, txtBaseband, lblBootloader, txtBootloader, lblSoftware, txtSoftware,
                lblSecurityPatch, txtSecurityPatch, lblBoard, txtBoard, lblHardware, txtHardware,
                lblCpuArch, txtCpuArch, lblWarrantyVoid, txtWarrantyVoid, lblKGState, txtKGState,
                lblSELinux, txtSELinux, lblSIM, txtSIM, lblSIMStatus, txtSIMStatus,
                lblUSBConfig, txtUSBConfig, lblServiceProvider, txtServiceProvider
            });

            // Buttons Panel (صفّان)
            pnlButtons = new System.Windows.Forms.Panel();
            pnlButtons.Name = "pnlButtons";
            pnlButtons.Location = new System.Drawing.Point(12, 375);
            pnlButtons.Size = new System.Drawing.Size(936, 118);
            pnlButtons.BackColor = clrPanel;

            int btnW = 175, btnH = 45, btnGap = 12, btnStartX = 10;
            int row1Y = 10, row2Y = 10 + 45 + 8;

            // الصف الأول
            btnReadInfo = CreateButton("btnReadInfo", "Read Info", btnStartX, row1Y, btnW, btnH, clrAccentBlue, clrBackground);
            btnCheckLock = CreateButton("btnCheckLock", "Check Lock", btnStartX + (btnW + btnGap), row1Y, btnW, btnH, clrAccentYellow, clrBackground);
            btnRelockBypass = CreateButton("btnRelockBypass", "Relock Bypass", btnStartX + (btnW + btnGap)*2, row1Y, btnW, btnH, clrAccentGreen, clrBackground);
            btnBackup = CreateButton("btnBackup", "Backup Files", btnStartX + (btnW + btnGap)*3, row1Y, btnW, btnH, clrAccentMauve, clrBackground);
            // Restore Backup — استرجاع نسخة احتياطية لمساراتها الأصلية على الجهاز
            btnRestoreBackup = CreateButton("btnRestoreBackup", "Restore Backup", btnStartX + (btnW + btnGap)*4, row1Y, btnW, btnH,
                System.Drawing.Color.FromArgb(180, 142, 247), clrBackground);

            // الصف الثاني
            btnNetworkConfig = CreateButton("btnNetworkConfig", "Network Config", btnStartX, row2Y, btnW, btnH, clrAccentTeal, clrBackground);
            // CP Inject — خاصية مضافة مستقلة (حقن ملف CP + MCC/MNC)
            btnCpInject = CreateButton("btnCpInject", "CP Inject", btnStartX + (btnW + btnGap), row2Y, btnW, btnH,
                System.Drawing.Color.FromArgb(250, 179, 135), clrBackground);
            // Block OTA — منع/استرجاع تحديثات النظام (إكمال بروتوكول منع الريليك)
            btnBlockOta = CreateButton("btnBlockOta", "Block OTA", btnStartX + (btnW + btnGap)*2, row2Y, btnW, btnH,
                System.Drawing.Color.FromArgb(242, 143, 173), clrBackground);
            // Reboot — إعادة تشغيل عادي/ريكفري/داونلود
            btnReboot = CreateButton("btnReboot", "Reboot Device", btnStartX + (btnW + btnGap)*3, row2Y, btnW, btnH,
                System.Drawing.Color.FromArgb(166, 173, 200), clrBackground);

            pnlButtons.Controls.AddRange(new System.Windows.Forms.Control[] {
                btnReadInfo, btnCheckLock, btnRelockBypass, btnBackup, btnRestoreBackup,
                btnNetworkConfig, btnCpInject, btnBlockOta, btnReboot });

            // Status Bar
            pnlStatusBar = new System.Windows.Forms.Panel();
            pnlStatusBar.Name = "pnlStatusBar";
            pnlStatusBar.Location = new System.Drawing.Point(12, 500);
            pnlStatusBar.Size = new System.Drawing.Size(936, 30);
            pnlStatusBar.BackColor = clrBorder;

            lblLockStatus = new System.Windows.Forms.Label();
            lblLockStatus.Name = "lblLockStatus";
            lblLockStatus.Text = "Lock Status: --";
            lblLockStatus.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            lblLockStatus.ForeColor = clrAccentYellow;
            lblLockStatus.Location = new System.Drawing.Point(15, 6);
            lblLockStatus.AutoSize = true;

            lblSelectedNetworks = new System.Windows.Forms.Label();
            lblSelectedNetworks.Name = "lblSelectedNetworks";
            lblSelectedNetworks.Text = "Selected Networks: None";
            lblSelectedNetworks.Font = new System.Drawing.Font("Segoe UI", 9F);
            lblSelectedNetworks.ForeColor = clrTextSecondary;
            lblSelectedNetworks.Location = new System.Drawing.Point(500, 7);
            lblSelectedNetworks.AutoSize = true;

            pnlStatusBar.Controls.Add(lblLockStatus);
            pnlStatusBar.Controls.Add(lblSelectedNetworks);

            // Log Output
            grpLogOutput = new System.Windows.Forms.GroupBox();
            grpLogOutput.Name = "grpLogOutput";
            grpLogOutput.Text = "  Log Output  ";
            grpLogOutput.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            grpLogOutput.ForeColor = clrAccentBlue;
            grpLogOutput.Location = new System.Drawing.Point(12, 537);
            grpLogOutput.Size = new System.Drawing.Size(936, 186);
            grpLogOutput.BackColor = clrPanel;
            grpLogOutput.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

            txtLogOutput = new System.Windows.Forms.RichTextBox();
            txtLogOutput.Name = "txtLogOutput";
            txtLogOutput.Location = new System.Drawing.Point(10, 22);
            txtLogOutput.Size = new System.Drawing.Size(916, 154);
            txtLogOutput.BackColor = clrLogBg;
            txtLogOutput.ForeColor = clrTextPrimary;
            txtLogOutput.Font = new System.Drawing.Font("Consolas", 9F);
            txtLogOutput.BorderStyle = System.Windows.Forms.BorderStyle.None;
            txtLogOutput.Multiline = true;
            txtLogOutput.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            txtLogOutput.ReadOnly = true;
            txtLogOutput.WordWrap = false;

            grpLogOutput.Controls.Add(txtLogOutput);

            // Progress Bar
            progressBar = new System.Windows.Forms.ProgressBar();
            progressBar.Name = "progressBar";
            progressBar.Location = new System.Drawing.Point(12, 728);
            progressBar.Size = new System.Drawing.Size(936, 5);
            progressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            progressBar.MarqueeAnimationSpeed = 0;
            progressBar.BackColor = clrBorder;

            this.Controls.Add(pnlHeader);
            this.Controls.Add(grpDeviceInfo);
            this.Controls.Add(pnlButtons);
            this.Controls.Add(pnlStatusBar);
            this.Controls.Add(grpLogOutput);
            this.Controls.Add(progressBar);

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label CreateLabel(string text, int x, int y, System.Drawing.Font font, System.Drawing.Color foreColor)
        {
            var lbl = new System.Windows.Forms.Label();
            lbl.Text = text;
            lbl.Font = font;
            lbl.ForeColor = foreColor;
            lbl.Location = new System.Drawing.Point(x, y + 4);
            lbl.AutoSize = true;
            return lbl;
        }

        private System.Windows.Forms.TextBox CreateTextBox(string name, int x, int y, System.Drawing.Size size, System.Drawing.Font font, System.Drawing.Color backColor)
        {
            var txt = new System.Windows.Forms.TextBox();
            txt.Name = name;
            txt.Location = new System.Drawing.Point(x, y);
            txt.Size = size;
            txt.Font = font;
            txt.BackColor = backColor;
            txt.ForeColor = System.Drawing.Color.FromArgb(205, 214, 244);
            txt.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            txt.ReadOnly = true;
            return txt;
        }

        private System.Windows.Forms.Button CreateButton(string name, string text, int x, int y, int w, int h, System.Drawing.Color backColor, System.Drawing.Color foreColor)
        {
            var btn = new System.Windows.Forms.Button();
            btn.Name = name;
            btn.Text = text;
            btn.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            btn.Size = new System.Drawing.Size(w, h);
            btn.Location = new System.Drawing.Point(x, y);
            btn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btn.BackColor = backColor;
            btn.ForeColor = foreColor;
            btn.FlatAppearance.BorderSize = 0;
            btn.Cursor = System.Windows.Forms.Cursors.Hand;
            return btn;
        }

        #endregion

        // Header
        private System.Windows.Forms.Panel pnlHeader;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblConnectionStatus;

        // Device Info
        private System.Windows.Forms.GroupBox grpDeviceInfo;
        private System.Windows.Forms.Label lblManufacturer;
        private System.Windows.Forms.TextBox txtManufacturer;
        private System.Windows.Forms.Label lblModel;
        private System.Windows.Forms.TextBox txtModel;
        private System.Windows.Forms.Label lblCodeName;
        private System.Windows.Forms.TextBox txtCodeName;
        private System.Windows.Forms.Label lblIMEI1;
        private System.Windows.Forms.TextBox txtIMEI1;
        private System.Windows.Forms.Label lblIMEI2;
        private System.Windows.Forms.TextBox txtIMEI2;
        private System.Windows.Forms.Label lblAndroidVersion;
        private System.Windows.Forms.TextBox txtAndroidVersion;
        private System.Windows.Forms.Label lblBaseband;
        private System.Windows.Forms.TextBox txtBaseband;
        private System.Windows.Forms.Label lblBootloader;
        private System.Windows.Forms.TextBox txtBootloader;
        private System.Windows.Forms.Label lblSoftware;
        private System.Windows.Forms.TextBox txtSoftware;
        private System.Windows.Forms.Label lblSecurityPatch;
        private System.Windows.Forms.TextBox txtSecurityPatch;
        private System.Windows.Forms.Label lblBoard;
        private System.Windows.Forms.TextBox txtBoard;
        private System.Windows.Forms.Label lblHardware;
        private System.Windows.Forms.TextBox txtHardware;
        private System.Windows.Forms.Label lblCpuArch;
        private System.Windows.Forms.TextBox txtCpuArch;
        private System.Windows.Forms.Label lblWarrantyVoid;
        private System.Windows.Forms.TextBox txtWarrantyVoid;
        private System.Windows.Forms.Label lblKGState;
        private System.Windows.Forms.TextBox txtKGState;
        private System.Windows.Forms.Label lblSELinux;
        private System.Windows.Forms.TextBox txtSELinux;
        private System.Windows.Forms.Label lblSIM;
        private System.Windows.Forms.TextBox txtSIM;
        private System.Windows.Forms.Label lblSIMStatus;
        private System.Windows.Forms.TextBox txtSIMStatus;
        private System.Windows.Forms.Label lblUSBConfig;
        private System.Windows.Forms.TextBox txtUSBConfig;
        private System.Windows.Forms.Label lblServiceProvider;
        private System.Windows.Forms.TextBox txtServiceProvider;

        // Buttons
        private System.Windows.Forms.Panel pnlButtons;
        private System.Windows.Forms.Button btnReadInfo;
        private System.Windows.Forms.Button btnCheckLock;
        private System.Windows.Forms.Button btnRelockBypass;
        private System.Windows.Forms.Button btnBackup;
        private System.Windows.Forms.Button btnNetworkConfig;
        private System.Windows.Forms.Button btnCpInject;
        private System.Windows.Forms.Button btnBlockOta;
        private System.Windows.Forms.Button btnRestoreBackup;
        private System.Windows.Forms.Button btnReboot;

        // Status Bar
        private System.Windows.Forms.Panel pnlStatusBar;
        private System.Windows.Forms.Label lblLockStatus;
        private System.Windows.Forms.Label lblSelectedNetworks;

        // Log
        private System.Windows.Forms.GroupBox grpLogOutput;
        private System.Windows.Forms.RichTextBox txtLogOutput;

        // Progress
        private System.Windows.Forms.ProgressBar progressBar;
    }
}

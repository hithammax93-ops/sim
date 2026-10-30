#!/usr/bin/env python3
"""Build the unified HmaxUnlockerPro C# project."""

import os
import re
import shutil
import zipfile
import json
from pathlib import Path

WORK10081 = Path("_tmp_src/work10081/HmaxUnlockerPro")
WORK10120 = Path("_tmp_src/work10120/HmaxUnlockerPro")
OUT = Path("HmaxUnlockerPro")


def write(path: Path, content: str):
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write(content)


def copy_and_replace_namespace(src: Path, dst: Path):
    """Copy a .cs file and replace HmaxUnlockerPro namespaces with HmaxUnlocker."""
    with open(src, "r", encoding="utf-8") as f:
        content = f.read()
    # Replace namespace HmaxUnlockerPro.Engine.* with HmaxUnlocker.Engine.*
    content = content.replace("HmaxUnlockerPro.Engine", "HmaxUnlocker.Engine")
    # Replace namespace HmaxUnlockerPro.Forms with HmaxUnlocker
    content = content.replace("HmaxUnlockerPro.Forms", "HmaxUnlocker")
    # Replace any remaining HmaxUnlockerPro with HmaxUnlocker (e.g. namespace HmaxUnlockerPro)
    content = content.replace("HmaxUnlockerPro", "HmaxUnlocker")
    write(dst, content)


def main():
    if OUT.exists():
        shutil.rmtree(OUT)
    OUT.mkdir(parents=True)

    # 1. Data file
    (OUT / "Data").mkdir(parents=True, exist_ok=True)
    shutil.copy(WORK10081 / "Data/mcc_mnc.json", OUT / "Data/mcc_mnc.json")

    # 2. Engine files from work-10120 (19 files)
    engine_files_10120 = [
        "Engine/Models/Enums.cs",
        "Engine/Models/DeviceModels.cs",
        "Engine/Logging/ILogger.cs",
        "Engine/Logging/ConsoleLogger.cs",
        "Engine/Logging/WinFormsLogger.cs",
        "Engine/Communication/IAdbShell.cs",
        "Engine/Communication/AdbShell.cs",
        "Engine/Detection/ChipsetDetector.cs",
        "Engine/Detection/PathDiscoveryEngine.cs",
        "Engine/Strategies/IPatchStrategy.cs",
        "Engine/Strategies/QualcommStrategy.cs",
        "Engine/Strategies/MediaTekStrategy.cs",
        "Engine/Strategies/ExynosStrategy.cs",
        "Engine/Strategies/PatchStrategyFactory.cs",
        "Engine/Samsung/SamsungPackageManager.cs",
        "Engine/Info/DeviceInfoReader.cs",
        "Engine/Backup/BackupEngine.cs",
        "Engine/Network/NetworkStore.cs",
        "Engine/Network/MccMncDatabase.cs",
    ]
    for rel in engine_files_10120:
        copy_and_replace_namespace(WORK10120 / rel, OUT / rel)

    # 3. Original 8 project files from work-10081
    # UniversalBypassEngine.cs
    copy_and_replace_namespace(WORK10081 / "Engine/UniversalBypassEngine.cs", OUT / "Engine/UniversalBypassEngine.cs")

    # Program.cs - replace fully
    program_cs = """using System;
using System.Windows.Forms;

namespace HmaxUnlocker
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
"""
    write(OUT / "Program.cs", program_cs)

    # Form1.cs - based on work-10081 with API fixes
    form1_cs = """using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using HmaxUnlocker.Engine;
using HmaxUnlocker.Engine.Backup;
using HmaxUnlocker.Engine.Communication;
using HmaxUnlocker.Engine.Info;
using HmaxUnlocker.Engine.Logging;
using HmaxUnlocker.Engine.Models;
using HmaxUnlocker.Engine.Network;

namespace HmaxUnlocker
{
    public partial class Form1 : System.Windows.Forms.Form
    {
        private AdbShell _adb;
        private WinFormsLogger _logger;
        private DeviceInfoReader _infoReader;
        private UniversalBypassEngine _bypassEngine;
        private BackupEngine _backupEngine;
        private NetworkStore _networkStore;
        private MccMncDatabase _mccMncDb;

        public Form1()
        {
            InitializeComponent();
            InitializeEngine();
            WireUpEvents();
            InitializeUI();
        }

        private void InitializeEngine()
        {
            _adb = new AdbShell("adb");
            _logger = new WinFormsLogger(txtLogOutput);
            _infoReader = new DeviceInfoReader(_adb, _logger);
            _bypassEngine = new UniversalBypassEngine(_adb, _logger);
            _backupEngine = new BackupEngine(_adb, _logger);
            _networkStore = new NetworkStore();
            _mccMncDb = new MccMncDatabase();

            string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "mcc_mnc.json");
            _mccMncDb.LoadFromFile(jsonPath);
        }

        private void WireUpEvents()
        {
            btnReadInfo.Click += BtnReadInfo_Click;
            btnCheckLock.Click += BtnCheckLock_Click;
            btnRelockBypass.Click += BtnRelockBypass_Click;
            btnBackup.Click += BtnBackup_Click;
            btnNetworkConfig.Click += BtnNetworkConfig_Click;
        }

        private void InitializeUI()
        {
            AppendLog("==============================================", Color.FromArgb(137, 180, 250));
            AppendLog("       HMAX UNLOCKER PRO - Ready", Color.FromArgb(243, 139, 168));
            AppendLog("==============================================", Color.FromArgb(137, 180, 250));
            AppendLog("");
            AppendLog("Waiting for device connection...", Color.FromArgb(166, 173, 200));
        }

        private async void BtnReadInfo_Click(object sender, EventArgs e)
        {
            SetBusyState(true);
            AppendLog("----------------------------------------------", Color.Gray);
            AppendLog("[Read Info] Starting device information read...", Color.FromArgb(137, 180, 250));

            try
            {
                var deviceInfo = await _infoReader.ReadAllAsync();
                PopulateDeviceInfo(deviceInfo);

                lblConnectionStatus.Text = deviceInfo.IsConnected ? "Connected" : "Disconnected";
                lblConnectionStatus.ForeColor = deviceInfo.IsConnected
                    ? Color.FromArgb(166, 227, 161) : Color.FromArgb(243, 139, 168);

                AppendLog("[Read Info] Device info read complete.", Color.FromArgb(166, 227, 161));
            }
            catch (Exception ex)
            {
                AppendLog($"[Read Info] Error: {ex.Message}", Color.FromArgb(243, 139, 168));
            }
            finally
            {
                SetBusyState(false);
            }
        }

        private async void BtnCheckLock_Click(object sender, EventArgs e)
        {
            SetBusyState(true);
            AppendLog("----------------------------------------------", Color.Gray);
            AppendLog("[Check Lock] Analyzing lock status...", Color.FromArgb(249, 226, 175));

            try
            {
                var lockStatus = await _bypassEngine.CheckLockStatusAsync();

                string statusText;
                Color statusColor;
                switch (lockStatus)
                {
                    case LockStatus.OfficiallyUnlocked:
                        statusText = "Officially Unlocked";
                        statusColor = Color.FromArgb(166, 227, 161);
                        break;
                    case LockStatus.RelockBypassed:
                        statusText = "Relock / Bypassed";
                        statusColor = Color.FromArgb(249, 226, 175);
                        break;
                    case LockStatus.FactoryLocked:
                        statusText = "Factory Locked";
                        statusColor = Color.FromArgb(243, 139, 168);
                        break;
                    default:
                        statusText = "Unknown";
                        statusColor = Color.FromArgb(166, 173, 200);
                        break;
                }

                lblLockStatus.Text = $"Lock Status: {statusText}";
                lblLockStatus.ForeColor = statusColor;

                AppendLog($"[Check Lock] Status: {statusText}", statusColor);
            }
            catch (Exception ex)
            {
                AppendLog($"[Check Lock] Error: {ex.Message}", Color.FromArgb(243, 139, 168));
            }
            finally
            {
                SetBusyState(false);
            }
        }

        private async void BtnRelockBypass_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "This will attempt to bypass the relock on the connected device.\\n\\n" +
                "Make sure you have a backup before proceeding.\\n\\nContinue?",
                "HMAX UNLOCKER PRO - Confirmation",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result != DialogResult.Yes) return;

            SetBusyState(true);
            AppendLog("----------------------------------------------", Color.Gray);
            AppendLog("[Relock Bypass] Starting bypass procedure...", Color.FromArgb(166, 227, 161));

            try
            {
                var bypassReport = await _bypassEngine.ExecuteAsync("XAA");

                AppendLog($"[Relock Bypass] Result: {bypassReport.Result}",
                    bypassReport.Result == PatchResult.Success ? Color.FromArgb(166, 227, 161) : Color.FromArgb(243, 139, 168));
                AppendLog($"[Relock Bypass] Before: {bypassReport.StatusBefore} -> After: {bypassReport.StatusAfter}", Color.FromArgb(205, 214, 244));

                foreach (var action in bypassReport.ActionsPerformed)
                    AppendLog($"  [OK] {action}", Color.FromArgb(166, 227, 161));

                foreach (var error in bypassReport.Errors)
                    AppendLog($"  [ERR] {error}", Color.FromArgb(243, 139, 168));
            }
            catch (Exception ex)
            {
                AppendLog($"[Relock Bypass] Error: {ex.Message}", Color.FromArgb(243, 139, 168));
            }
            finally
            {
                SetBusyState(false);
            }
        }

        private async void BtnBackup_Click(object sender, EventArgs e)
        {
            SetBusyState(true);
            AppendLog("----------------------------------------------", Color.Gray);
            AppendLog("[Backup] Starting relock file backup...", Color.FromArgb(203, 166, 247));

            try
            {
                string backupPath = AppDomain.CurrentDomain.BaseDirectory;
                string savedPath = await _backupEngine.ExecuteAsync(backupPath);

                AppendLog($"[Backup] Complete: {savedPath}", Color.FromArgb(166, 227, 161));
            }
            catch (Exception ex)
            {
                AppendLog($"[Backup] Error: {ex.Message}", Color.FromArgb(243, 139, 168));
            }
            finally
            {
                SetBusyState(false);
            }
        }

        private void BtnNetworkConfig_Click(object sender, EventArgs e)
        {
            AppendLog("----------------------------------------------", Color.Gray);
            AppendLog("[Network Config] Opening MCC/MNC selector...", Color.FromArgb(148, 226, 213));

            var networkForm = new NetworkConfigForm(_mccMncDb, _networkStore);
            networkForm.NetworksSaved += (sender2, args) =>
            {
                lblSelectedNetworks.Text = $"Selected Networks: {_networkStore.GetSummary()}";
                AppendLog($"[Network Config] {_networkStore.SelectedNetworks.Count} network(s) saved for unlock.", Color.FromArgb(148, 226, 213));
            };
            networkForm.Show(this);
        }

        public void AppendLog(string message, Color color)
        {
            if (txtLogOutput.InvokeRequired)
            {
                txtLogOutput.BeginInvoke(new Action(() => AppendLog(message, color)));
                return;
            }

            txtLogOutput.SelectionStart = txtLogOutput.TextLength;
            txtLogOutput.SelectionLength = 0;
            txtLogOutput.SelectionColor = color;
            txtLogOutput.AppendText(message + Environment.NewLine);
            txtLogOutput.ScrollToCaret();
        }

        private void SetBusyState(bool isBusy)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetBusyState(isBusy)));
                return;
            }

            btnReadInfo.Enabled = !isBusy;
            btnCheckLock.Enabled = !isBusy;
            btnRelockBypass.Enabled = !isBusy;
            btnBackup.Enabled = !isBusy;
            btnNetworkConfig.Enabled = !isBusy;
            progressBar.MarqueeAnimationSpeed = isBusy ? 30 : 0;
        }

        private void PopulateDeviceInfo(DeviceInfoModel info)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => PopulateDeviceInfo(info)));
                return;
            }

            txtManufacturer.Text    = info.Manufacturer;
            txtModel.Text           = info.Model;
            txtCodeName.Text        = info.CodeName;
            txtIMEI1.Text           = info.IMEI1;
            txtIMEI2.Text           = info.IMEI2;
            txtAndroidVersion.Text  = info.AndroidVersion;
            txtBaseband.Text        = info.Baseband;
            txtBootloader.Text      = info.Bootloader;
            txtSoftware.Text        = info.Software;
            txtSecurityPatch.Text   = info.SecurityPatch;
            txtBoard.Text           = info.Board;
            txtHardware.Text        = info.Hardware;
            txtCpuArch.Text         = info.CpuArch;
            txtWarrantyVoid.Text    = info.WarrantyVoid;
            txtKGState.Text         = info.KGState;
            txtSELinux.Text         = info.SELinux;
            txtSIM.Text             = info.SIM;
            txtSIMStatus.Text       = info.SIMStatus;
            txtUSBConfig.Text       = info.USBConfig;
            txtServiceProvider.Text = info.ServiceProvider;
        }
    }
}
"""
    write(OUT / "Forms/Form1.cs", form1_cs)

    # Form1.Designer.cs - copy and replace namespaces
    copy_and_replace_namespace(WORK10081 / "Forms/Form1.Designer.cs", OUT / "Forms/Form1.Designer.cs")

    # NetworkConfigForm.cs - copy, replace namespaces, and adapt to NetworkEntry model
    copy_and_replace_namespace(WORK10081 / "Forms/NetworkConfigForm.cs", OUT / "Forms/NetworkConfigForm.cs")
    ncf = (OUT / "Forms/NetworkConfigForm.cs").read_text(encoding="utf-8")
    ncf = ncf.replace("net.NetworkName", "net.Operator")
    (OUT / "Forms/NetworkConfigForm.cs").write_text(ncf, encoding="utf-8")

    # 4. Fix DeviceModels.cs: add missing properties that ChipsetDetector expects
    device_models_path = OUT / "Engine/Models/DeviceModels.cs"
    with open(device_models_path, "r", encoding="utf-8") as f:
        dm = f.read()

    # Add extra properties after IsSamsung if not present
    if "public string Brand" not in dm:
        dm = dm.replace(
            "        public bool IsSamsung { get; set; } = false;\n    }\n",
            """        public bool IsSamsung { get; set; } = false;
        public string Brand { get; set; } = "";
        public string BoardPlatform { get; set; } = "";
        public string BuildFingerprint { get; set; } = "";
        public string SerialNumber { get; set; } = "";
        public string BasebandVersion { get; set; } = "";
    }
"""
        )
    write(device_models_path, dm)

    # 4b. Add LoadFromFile overload to MccMncDatabase so Form1.cs compiles
    mcc_path = OUT / "Engine/Network/MccMncDatabase.cs"
    with open(mcc_path, "r", encoding="utf-8") as f:
        mcc = f.read()
    if "public void LoadFromFile" not in mcc:
        mcc = mcc.replace(
            "        public List<string> GetCountries()\n",
            """        public void LoadFromFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var loaded = JsonConvert.DeserializeObject<List<NetworkEntry>>(json);
                    if (loaded != null && loaded.Count > 0)
                        _allNetworks = loaded;
                }
            }
            catch
            {
                // Fallback to built-in networks
            }
        }

        public List<string> GetCountries()\n""")
    write(mcc_path, mcc)

    # 5. Write packages.config
    packages_config = """<?xml version=\"1.0\" encoding=\"utf-8\"?>
<packages>
  <package id=\"Newtonsoft.Json\" version=\"13.0.3\" targetFramework=\"net48\" />
</packages>
"""
    write(OUT / "packages.config", packages_config)

    # 6. Write HmaxUnlockerPro.csproj
    csproj_content = """<?xml version=\"1.0\" encoding=\"utf-8\"?>
<Project ToolsVersion=\"15.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">
  <Import Project=\"$(MSBuildExtensionsPath)\\$(MSBuildToolsVersion)\\Microsoft.Common.props\" Condition=\"Exists('$(MSBuildExtensionsPath)\\$(MSBuildToolsVersion)\\Microsoft.Common.props')\" />
  <PropertyGroup>
    <Configuration Condition=\" '$(Configuration)' == '' \">Debug</Configuration>
    <Platform Condition=\" '$(Platform)' == '' \">AnyCPU</Platform>
    <ProjectGuid>{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}</ProjectGuid>
    <OutputType>WinExe</OutputType>
    <RootNamespace>HmaxUnlocker</RootNamespace>
    <AssemblyName>HmaxUnlockerPro</AssemblyName>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <FileAlignment>512</FileAlignment>
    <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
  <PropertyGroup Condition=\" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' \">
    <PlatformTarget>AnyCPU</PlatformTarget>
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>bin\\Debug\\</OutputPath>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <PropertyGroup Condition=\" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' \">
    <PlatformTarget>AnyCPU</PlatformTarget>
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>bin\\Release\\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=\"System\" />
    <Reference Include=\"System.Core\" />
    <Reference Include=\"System.Drawing\" />
    <Reference Include=\"System.Windows.Forms\" />
    <Reference Include=\"System.Xml\" />
    <Reference Include=\"System.Xml.Linq\" />
    <Reference Include=\"System.Data\" />
    <Reference Include=\"System.Net.Http\" />
    <Reference Include=\"Newtonsoft.Json\">
      <HintPath>packages\\Newtonsoft.Json.13.0.3\\lib\\net45\\Newtonsoft.Json.dll</HintPath>
    </Reference>
  </ItemGroup>
  <ItemGroup>
    <Compile Include=\"Program.cs\" />
    <Compile Include=\"Forms\\Form1.cs\">
      <SubType>Form</SubType>
    </Compile>
    <Compile Include=\"Forms\\Form1.Designer.cs\">
      <DependentUpon>Form1.cs</DependentUpon>
    </Compile>
    <Compile Include=\"Forms\\NetworkConfigForm.cs\">
      <SubType>Form</SubType>
    </Compile>
    <Compile Include=\"Engine\\Communication\\IAdbShell.cs\" />
    <Compile Include=\"Engine\\Communication\\AdbShell.cs\" />
    <Compile Include=\"Engine\\Detection\\ChipsetDetector.cs\" />
    <Compile Include=\"Engine\\Detection\\PathDiscoveryEngine.cs\" />
    <Compile Include=\"Engine\\Strategies\\IPatchStrategy.cs\" />
    <Compile Include=\"Engine\\Strategies\\QualcommStrategy.cs\" />
    <Compile Include=\"Engine\\Strategies\\MediaTekStrategy.cs\" />
    <Compile Include=\"Engine\\Strategies\\ExynosStrategy.cs\" />
    <Compile Include=\"Engine\\Strategies\\PatchStrategyFactory.cs\" />
    <Compile Include=\"Engine\\Samsung\\SamsungPackageManager.cs\" />
    <Compile Include=\"Engine\\Info\\DeviceInfoReader.cs\" />
    <Compile Include=\"Engine\\Backup\\BackupEngine.cs\" />
    <Compile Include=\"Engine\\Network\\NetworkStore.cs\" />
    <Compile Include=\"Engine\\Network\\MccMncDatabase.cs\" />
    <Compile Include=\"Engine\\Logging\\ILogger.cs\" />
    <Compile Include=\"Engine\\Logging\\ConsoleLogger.cs\" />
    <Compile Include=\"Engine\\Logging\\WinFormsLogger.cs\" />
    <Compile Include=\"Engine\\Models\\Enums.cs\" />
    <Compile Include=\"Engine\\Models\\DeviceModels.cs\" />
    <Compile Include=\"Engine\\UniversalBypassEngine.cs\" />
  </ItemGroup>
  <ItemGroup>
    <None Include=\"Data\\mcc_mnc.json\">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Include=\"packages.config\" />
  </ItemGroup>
  <Import Project=\"$(MSBuildToolsPath)\\Microsoft.CSharp.targets\" />
</Project>
"""
    write(OUT / "HmaxUnlockerPro.csproj", csproj_content)

    # 7. Verify no HmaxUnlockerPro references remain in .cs files
    for path in OUT.rglob("*.cs"):
        text = path.read_text(encoding="utf-8")
        if "HmaxUnlockerPro" in text:
            print(f"WARNING: HmaxUnlockerPro still in {path}")

    print("Project built at", OUT)


if __name__ == "__main__":
    main()

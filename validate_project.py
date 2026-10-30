#!/usr/bin/env python3
"""Validate the HmaxUnlockerPro C# project structure."""

import os
import re
import zipfile
from pathlib import Path

OUT = Path("HmaxUnlockerPro")


def check_braces(file_path):
    """Simple brace balance check (does not handle strings/comments)."""
    text = file_path.read_text(encoding="utf-8")
    balance = 0
    for ch in text:
        if ch == "{":
            balance += 1
        elif ch == "}":
            balance -= 1
        if balance < 0:
            return False, "unbalanced braces"
    return balance == 0, "ok"


def main():
    issues = []

    # Required files
    required = [
        "HmaxUnlockerPro.csproj",
        "packages.config",
        "Program.cs",
        "Data/mcc_mnc.json",
        "Engine/UniversalBypassEngine.cs",
        "Forms/Form1.cs",
        "Forms/Form1.Designer.cs",
        "Forms/NetworkConfigForm.cs",
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

    for rel in required:
        if not (OUT / rel).exists():
            issues.append(f"Missing: {rel}")

    # Count files
    all_files = list(OUT.rglob("*"))
    files_only = [f for f in all_files if f.is_file()]
    print(f"Total files: {len(files_only)}")

    # Namespace consistency
    for path in OUT.rglob("*.cs"):
        text = path.read_text(encoding="utf-8")
        if "HmaxUnlockerPro" in text:
            issues.append(f"HmaxUnlockerPro reference in {path}")
        # Check namespace for forms and engine
        if path.parts[1] == "Forms":
            if "namespace HmaxUnlocker" not in text:
                issues.append(f"Forms file {path} missing HmaxUnlocker namespace")
        elif "Engine" in path.parts:
            if "namespace HmaxUnlocker.Engine" not in text:
                issues.append(f"Engine file {path} missing HmaxUnlocker.Engine namespace")

    # Brace balance
    for path in OUT.rglob("*.cs"):
        ok, msg = check_braces(path)
        if not ok:
            issues.append(f"Brace issue in {path}: {msg}")

    # Check .csproj references all .cs files and json
    csproj = (OUT / "HmaxUnlockerPro.csproj").read_text(encoding="utf-8")
    for path in OUT.rglob("*.cs"):
        rel = str(path.relative_to(OUT)).replace("/", "\\")
        if rel not in csproj:
            issues.append(f"csproj missing compile entry: {rel}")
    if "Data\\mcc_mnc.json" not in csproj:
        issues.append("csproj missing Data\\mcc_mnc.json")
    if "PreserveNewest" not in csproj:
        issues.append("csproj missing CopyToOutputDirectory for json")

    # Check Form1.Designer.cs has expected controls
    designer = (OUT / "Forms/Form1.Designer.cs").read_text(encoding="utf-8")
    textbox_names = [f"txt{m}" for m in [
        "Manufacturer", "Model", "CodeName", "IMEI1", "IMEI2",
        "AndroidVersion", "Baseband", "Bootloader", "Software", "SecurityPatch",
        "Board", "Hardware", "CpuArch", "WarrantyVoid", "KGState",
        "SELinux", "SIM", "SIMStatus", "USBConfig", "ServiceProvider"
    ]]
    for name in textbox_names:
        if name not in designer:
            issues.append(f"Missing textbox: {name}")
    buttons = ["btnReadInfo", "btnCheckLock", "btnRelockBypass", "btnBackup", "btnNetworkConfig"]
    for btn in buttons:
        if btn not in designer:
            issues.append(f"Missing button: {btn}")
    if "txtLogOutput" not in designer:
        issues.append("Missing RichTextBox: txtLogOutput")
    if "progressBar" not in designer:
        issues.append("Missing ProgressBar: progressBar")
    if "pnlStatusBar" not in designer:
        issues.append("Missing status bar: pnlStatusBar")

    # Check NetworkConfigForm has required controls
    ncf = (OUT / "Forms/NetworkConfigForm.cs").read_text(encoding="utf-8")
    for control in ["lstCountries", "lstNetworks", "lstSelected", "btnAdd", "btnRemove", "btnSave"]:
        if control not in ncf:
            issues.append(f"NetworkConfigForm missing {control}")
    if "MccMncDatabase" not in ncf:
        issues.append("NetworkConfigForm missing MccMncDatabase reference")

    # Check Program.cs
    program = (OUT / "Program.cs").read_text(encoding="utf-8")
    if "[STAThread]" not in program:
        issues.append("Program.cs missing [STAThread]")
    if "Application.Run(new Form1())" not in program:
        issues.append("Program.cs missing Application.Run(new Form1())")

    # packages.config
    pc = (OUT / "packages.config").read_text(encoding="utf-8")
    if 'id="Newtonsoft.Json"' not in pc or 'version="13.0.3"' not in pc:
        issues.append("packages.config missing Newtonsoft.Json 13.0.3")

    if issues:
        print("VALIDATION FAILED")
        for issue in issues:
            print("  -", issue)
        return 1
    else:
        print("VALIDATION PASSED")
        return 0


if __name__ == "__main__":
    raise SystemExit(main())

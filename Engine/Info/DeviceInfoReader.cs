using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HmaxUnlocker.Engine.Communication;
using HmaxUnlocker.Engine.Detection;
using HmaxUnlocker.Engine.Efs;
using HmaxUnlocker.Engine.Logging;
using HmaxUnlocker.Engine.Models;

namespace HmaxUnlocker.Engine.Info
{
    public class DeviceInfoReader
    {
        private readonly IAdbShell _adb;
        private readonly ILogger _logger;

        public DeviceInfoReader(IAdbShell adb, ILogger logger)
        {
            _adb = adb;
            _logger = logger;
        }

        public async Task<DeviceInfoModel> ReadAllAsync()
        {
            var info = new DeviceInfoModel();

            _logger.Info("Connecting to device...");
            info.IsConnected = await _adb.IsDeviceConnectedAsync();
            if (!info.IsConnected)
            {
                _logger.Error("No device connected!");
                return info;
            }

            info.DeviceID = Clean(await _adb.ExecuteAsync("getprop ro.serialno"));
            info.HasRoot = await _adb.HasRootAccessAsync();

            _logger.Info("Reading device information...");

            var tasks = new Dictionary<string, Task<string>>
            {
                ["manufacturer"] = _adb.ExecuteAsync("getprop ro.product.manufacturer"),
                ["model"]        = _adb.ExecuteAsync("getprop ro.product.model"),
                ["codename"]     = _adb.ExecuteAsync("getprop ro.product.device"),
                ["android"]      = _adb.ExecuteAsync("getprop ro.build.version.release"),
                ["baseband"]     = _adb.ExecuteAsync("getprop gsm.version.baseband"),
                ["bootloader"]   = _adb.ExecuteAsync("getprop ro.bootloader"),
                ["software"]     = _adb.ExecuteAsync("getprop ro.build.display.id"),
                ["secpatch"]     = _adb.ExecuteAsync("getprop ro.build.version.security_patch"),
                ["board"]        = _adb.ExecuteAsync("getprop ro.board.platform"),
                ["hardware"]     = _adb.ExecuteAsync("getprop ro.hardware"),
                ["cpuarch"]      = _adb.ExecuteAsync("getprop ro.product.cpu.abi"),
                ["warranty"]     = _adb.ExecuteAsync("getprop ro.boot.warranty_bit"),
                ["kg_state"]     = _adb.ExecuteAsync("getprop ro.boot.kg_state"),
                ["selinux"]      = _adb.ExecuteAsync("getenforce"),
                ["usb_config"]   = _adb.ExecuteAsync("getprop sys.usb.config"),
                ["sim_operator"] = _adb.ExecuteAsync("getprop gsm.sim.operator.alpha"),
                ["sim_state"]    = _adb.ExecuteAsync("getprop gsm.sim.state"),
            };

            await Task.WhenAll(tasks.Values);

            info.Manufacturer    = Clean(tasks["manufacturer"].Result);
            info.Model           = Clean(tasks["model"].Result);
            info.CodeName        = Clean(tasks["codename"].Result);
            info.AndroidVersion  = Clean(tasks["android"].Result);
            info.Baseband        = Clean(tasks["baseband"].Result);
            info.Bootloader      = Clean(tasks["bootloader"].Result);
            info.Software        = Clean(tasks["software"].Result);
            info.SecurityPatch   = Clean(tasks["secpatch"].Result);
            info.Board           = Clean(tasks["board"].Result);
            info.Hardware        = Clean(tasks["hardware"].Result);
            info.CpuArch         = Clean(tasks["cpuarch"].Result);
            info.WarrantyVoid    = ParseWarrantyBit(Clean(tasks["warranty"].Result));
            info.KGState         = ParseKGState(Clean(tasks["kg_state"].Result));
            info.SELinux         = Clean(tasks["selinux"].Result);
            info.USBConfig       = Clean(tasks["usb_config"].Result);
            info.SIM             = Clean(tasks["sim_operator"].Result);
            info.ServiceProvider = Clean(tasks["sim_operator"].Result);
            info.SIMStatus       = ParseSimStatus(Clean(tasks["sim_state"].Result));
            info.IsSamsung       = info.Manufacturer.IndexOf("samsung", StringComparison.OrdinalIgnoreCase) >= 0;

            _logger.Info("Reading IMEI...");
            info.IMEI1 = await ReadIMEI1Async();
            info.IMEI2 = await ReadIMEI2Async();

            var detector = new ChipsetDetector(_adb);
            var devInfo = await detector.DetectDeviceAsync();
            info.Chipset = devInfo.Chipset;

            _logger.Info("Detecting lock status...");
            info.OriginalCarrier = await ReadOriginalCarrierAsync();
            info.CurrentCSC = await ReadCurrentCSCAsync();
            info.LockStatus = await DetermineLockStatusAsync(info);

            _logger.Info("Device info read complete!");
            return info;
        }

        /// <summary>
        /// الطريقة الحقيقية لقراءة IMEI على أندرويد الحديث: service call iphonesubinfo
        /// ثم فك ترميز Parcel (UTF-16). dumpsys لم يعد يعيد IMEI.
        /// </summary>
        private async Task<string> ReadIMEI1Async()
        {
            string imei = ExtractImeiFromParcel(
                await _adb.ExecuteRootAsync("service call iphonesubinfo 1"));
            if (IsValidIMEI(imei)) return imei;

            imei = ExtractImeiFromParcel(
                await _adb.ExecuteRootAsync("service call iphonesubinfo 1 i32 0"));
            if (IsValidIMEI(imei)) return imei;

            imei = Clean(await _adb.ExecuteAsync("getprop ril.imei"));
            if (IsValidIMEI(imei)) return imei;

            return "N/A";
        }

        private async Task<string> ReadIMEI2Async()
        {
            string imei = ExtractImeiFromParcel(
                await _adb.ExecuteRootAsync("service call iphonesubinfo 1 i32 1"));
            if (IsValidIMEI(imei)) return imei;

            imei = Clean(await _adb.ExecuteAsync("getprop ril.imei2"));
            if (IsValidIMEI(imei)) return imei;

            return "N/A";
        }

        /// <summary>
        /// يفك IMEI من مخرجات service call: إما من العمود النصي المقتبس '...'
        /// أو من كلمات hex بحجم 32-بت (كل كلمة حرفان UTF-16 بترتيب low ثم high).
        /// </summary>
        internal static string ExtractImeiFromParcel(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";

            // 1) العمود النصي المقتبس إن وُجد: '...3.5...8.'
            var sb = new StringBuilder();
            foreach (Match q in Regex.Matches(raw, "'([^']*)'"))
            {
                foreach (char c in q.Groups[1].Value)
                    if (c >= '0' && c <= '9') sb.Append(c);
            }
            Match asciiDigits = Regex.Match(sb.ToString(), @"\d{14,16}");
            if (asciiDigits.Success)
                return asciiDigits.Value;

            // 2) فك كلمات hex: 00350033 → الحرف المنخفض أولاً ('3' ثم '5')
            var chars = new StringBuilder();
            foreach (Match m in Regex.Matches(raw, @"\b[0-9a-fA-F]{8}\b"))
            {
                string tok = m.Value;
                string lowHalf = tok.Substring(4, 4);  // أول حرفين في الذاكرة
                string highHalf = tok.Substring(0, 4); // ثاني حرفين في الذاكرة
                AppendUtf16Char(chars, lowHalf);
                AppendUtf16Char(chars, highHalf);
            }
            Match hexDigits = Regex.Match(chars.ToString(), @"\d{14,16}");
            return hexDigits.Success ? hexDigits.Value : "";
        }

        private static void AppendUtf16Char(StringBuilder sb, string fourHexDigits)
        {
            if (int.TryParse(fourHexDigits,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out int code))
            {
                if (code >= 0x20 && code <= 0x7e)
                    sb.Append((char)code);
            }
        }

        private async Task<string> ReadOriginalCarrierAsync()
        {
            byte[] cidData = await _adb.ReadFileAsync("/efs/imei/cid.dat");
            if (cidData.Length > 0)
            {
                string cid = Encoding.ASCII.GetString(cidData).Trim().Replace("\0", "");
                if (!string.IsNullOrEmpty(cid) && cid.Length <= 4) return cid;
            }
            return "N/A";
        }

        private async Task<string> ReadCurrentCSCAsync()
        {
            byte[] mpsData = await _adb.ReadFileAsync("/mnt/vendor/efs/mps_code.dat");
            if (mpsData.Length > 0)
            {
                string mps = Encoding.ASCII.GetString(mpsData).Trim().Replace("\0", "");
                if (!string.IsNullOrEmpty(mps) && mps.Length <= 4) return mps;
            }

            mpsData = await _adb.ReadFileAsync("/efs/imei/mps_code.dat");
            if (mpsData.Length > 0)
            {
                string mps = Encoding.ASCII.GetString(mpsData).Trim().Replace("\0", "");
                if (!string.IsNullOrEmpty(mps) && mps.Length <= 4) return mps;
            }

            string result = (await _adb.ExecuteAsync("getprop persist.omc.salescode")).Trim();
            if (!string.IsNullOrEmpty(result) && result.Length <= 4) return result;

            return "N/A";
        }

        private async Task<LockStatus> DetermineLockStatusAsync(DeviceInfoModel info)
        {
            var simStatus = await new SimStateController(_adb, _logger).EvaluateLockStatusFromSimAsync();
            if (simStatus == LockStatus.FactoryLocked)
                return simStatus;

            if (info.OriginalCarrier == "N/A" || info.CurrentCSC == "N/A"
                || info.OriginalCarrier == info.CurrentCSC)
                return LockStatus.Unknown;

            long qcnvSize = await _adb.GetFileSizeAsync("/mnt/vendor/efs/QCNv_data.bin");
            if (qcnvSize < 0)
                qcnvSize = await _adb.GetFileSizeAsync("/efs/QCNv_data.bin");
            if (qcnvSize == 0) return LockStatus.RelockBypassed;

            // اختلاف CSC وحده لا يثبت وجود قفل أو فتحه.
            return LockStatus.Unknown;
        }

        private string ParseSimStatus(string simState)
        {
            if (string.IsNullOrEmpty(simState)) return "Unknown";
            string[] states = simState.Split(',');
            var results = new List<string>();
            foreach (var state in states)
            {
                switch (state.Trim().ToUpper())
                {
                    case "READY":
                    case "SIM_READY":
                        results.Add("Connected"); break;
                    case "LOADED":
                        results.Add("Loaded"); break;
                    case "NOT_READY":
                        results.Add("Not Ready"); break;
                    case "ABSENT":
                        results.Add("No SIM"); break;
                    case "LOCKED":
                        results.Add("SIM Locked"); break;
                    case "NETWORK_LOCKED":
                        results.Add("Network Locked"); break;
                    case "PIN_REQUIRED":
                        results.Add("PIN Required"); break;
                    case "PUK_REQUIRED":
                        results.Add("PUK Required"); break;
                    default: results.Add(state.Trim()); break;
                }
            }
            return string.Join(" | ", results);
        }

        private string ParseWarrantyBit(string bit)
        {
            if (string.IsNullOrEmpty(bit)) return "N/A";
            return bit == "0" ? "Valid (0)" : bit == "1" ? "Void (1)" : $"Unknown ({bit})";
        }

        private string ParseKGState(string state)
        {
            if (string.IsNullOrEmpty(state)) return "N/A";
            switch (state.ToUpper())
            {
                case "LOCKED": return "Locked";
                case "UNLOCKED": return "Unlocked";
                case "ACTIVE": return "Active";
                case "PREACTIVE": return "Pre-Active";
                default: return state;
            }
        }

        private string Clean(string s) => s?.Trim().Replace("\r", "").Replace("\n", "") ?? "";

        private bool IsValidIMEI(string imei)
        {
            if (string.IsNullOrEmpty(imei)) return false;
            return Regex.IsMatch(imei, @"^\d{14,15}$");
        }
    }
}

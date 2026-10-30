using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HmaxUnlocker.Engine.Communication;
using HmaxUnlocker.Engine.Models;

namespace HmaxUnlocker.Engine.Detection
{
    public class ChipsetDetector
    {
        private readonly IAdbShell _adb;

        public ChipsetDetector(IAdbShell adb) => _adb = adb;

        public async Task<DeviceInfoModel> DetectDeviceAsync()
        {
            var info = new DeviceInfoModel();

            var tasks = new Dictionary<string, Task<string>>
            {
                ["brand"]       = _adb.ExecuteAsync("getprop ro.product.brand"),
                ["model"]       = _adb.ExecuteAsync("getprop ro.product.model"),
                ["platform"]    = _adb.ExecuteAsync("getprop ro.board.platform"),
                ["hardware"]    = _adb.ExecuteAsync("getprop ro.hardware"),
                ["chipname"]    = _adb.ExecuteAsync("getprop ro.chipname"),
                ["android"]     = _adb.ExecuteAsync("getprop ro.build.version.release"),
                ["fingerprint"] = _adb.ExecuteAsync("getprop ro.build.fingerprint"),
                ["serial"]      = _adb.ExecuteAsync("getprop ro.serialno"),
                ["baseband"]    = _adb.ExecuteAsync("getprop gsm.version.baseband"),
                ["soc"]         = _adb.ExecuteAsync("getprop ro.soc.manufacturer"),
                ["kernel"]      = _adb.ExecuteAsync("uname -a"),
            };

            await Task.WhenAll(tasks.Values);

            info.Brand           = Clean(tasks["brand"].Result);
            info.Model           = Clean(tasks["model"].Result);
            info.BoardPlatform   = Clean(tasks["platform"].Result);
            info.Hardware        = Clean(tasks["hardware"].Result);
            info.AndroidVersion  = Clean(tasks["android"].Result);
            info.BuildFingerprint = Clean(tasks["fingerprint"].Result);
            info.SerialNumber    = Clean(tasks["serial"].Result);
            info.BasebandVersion = Clean(tasks["baseband"].Result);
            info.IsSamsung       = info.Brand.IndexOf("samsung", StringComparison.OrdinalIgnoreCase) >= 0;

            string chipname = Clean(tasks["chipname"].Result);
            string soc      = Clean(tasks["soc"].Result);
            string kernel   = Clean(tasks["kernel"].Result);

            info.Chipset = ClassifyChipset(info.BoardPlatform, info.Hardware, chipname, soc, kernel);
            return info;
        }

        private ChipsetType ClassifyChipset(string platform, string hardware, string chipname, string soc, string kernel)
        {
            string all = $"{platform} {hardware} {chipname} {soc} {kernel}".ToLower();

            string[] qcomHints = { "qcom", "qualcomm", "snapdragon", "sdm", "msm", "apq",
                "pakala", "kalama", "pineapple", "cliffs", "crow", "taro", "diwali", "lahaina", "kona", "lito", "atoll" };
            if (MatchesAny(all, qcomHints)) return ChipsetType.Qualcomm;

            string[] mtkHints = { "mtk", "mediatek", "mt6", "elbrus", "kuro", "denver" };
            if (MatchesAny(all, mtkHints)) return ChipsetType.MediaTek;

            string[] exynosHints = { "exynos", "s5e", "universal" };
            if (MatchesAny(all, exynosHints)) return ChipsetType.Exynos;

            string[] unisocHints = { "unisoc", "spreadtrum", "sc9", "ud710", "tiger" };
            if (MatchesAny(all, unisocHints)) return ChipsetType.Unisoc;

            if (all.Contains("tensor") || all.Contains("gs101") || all.Contains("gs201"))
                return ChipsetType.GoogleTensor;

            return ChipsetType.Unknown;
        }

        private bool MatchesAny(string input, string[] patterns)
        {
            return patterns.Any(p => input.Contains(p));
        }

        private string Clean(string s)
        {
            return s?.Trim().Replace("\r", "").Replace("\n", "") ?? "";
        }
    }
}

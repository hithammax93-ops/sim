using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HmaxUnlocker.Engine.Models
{
    public class DeviceInfoModel
    {
        public string Manufacturer { get; set; } = "";
        public string Model { get; set; } = "";
        public string CodeName { get; set; } = "";
        public string IMEI1 { get; set; } = "";
        public string IMEI2 { get; set; } = "";
        public string AndroidVersion { get; set; } = "";
        public string Baseband { get; set; } = "";
        public string Bootloader { get; set; } = "";
        public string Software { get; set; } = "";
        public string SecurityPatch { get; set; } = "";
        public string Board { get; set; } = "";
        public string Hardware { get; set; } = "";
        public string CpuArch { get; set; } = "";
        public string WarrantyVoid { get; set; } = "";
        public string KGState { get; set; } = "";
        public string SELinux { get; set; } = "";
        public string SIM { get; set; } = "";
        public string SIMStatus { get; set; } = "";
        public string USBConfig { get; set; } = "";
        public string ServiceProvider { get; set; } = "";
        public LockStatus LockStatus { get; set; } = LockStatus.Unknown;
        public string OriginalCarrier { get; set; } = "";
        public string CurrentCSC { get; set; } = "";
        public string DeviceID { get; set; } = "";
        public bool IsConnected { get; set; } = false;
        public bool HasRoot { get; set; } = false;
        public ChipsetType Chipset { get; set; } = ChipsetType.Unknown;
        public bool IsSamsung { get; set; } = false;
        public string Brand { get; set; } = "";
        public string BoardPlatform { get; set; } = "";
        public string BuildFingerprint { get; set; } = "";
        public string SerialNumber { get; set; } = "";
        public string BasebandVersion { get; set; } = "";
    }

    public class EfsPaths
    {
        public string MpsCodePath { get; set; }
        public string CidDatPath { get; set; }
        public string QcnvDataPath { get; set; }
        public string NvDataPath { get; set; }
        public string PnPath { get; set; }
        public string TelephonyPropPath { get; set; }
        public string FactoryPropPath { get; set; }
        public string NvramPath { get; set; }
        public string CarrierConfigPath { get; set; }
        public string ImsNvPath { get; set; }
        public string EfsRoot { get; set; }
        public string VendorEfsRoot { get; set; }
        public List<string> AllDiscoveredPaths { get; set; } = new List<string>();
    }

    public class BypassReport
    {
        public DeviceInfoModel Device { get; set; }
        public EfsPaths Paths { get; set; }
        public LockStatus StatusBefore { get; set; }
        public LockStatus StatusAfter { get; set; }
        public PatchResult Result { get; set; }
        public List<string> ActionsPerformed { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
        public PackageBypassResult PackageResult { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class PackageBypassResult
    {
        public List<string> DisabledPackages { get; set; } = new List<string>();
        public List<string> FrozenPackages { get; set; } = new List<string>();
        public List<string> ClearedPackages { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class NetworkEntry
    {
        // أسماء JSON مطابقة لملف Data/mcc_mnc.json حتى تعمل القراءة حرفياً
        [JsonProperty("Country")]
        public string Country { get; set; }

        [JsonProperty("CountryCode")]
        public string CountryCode { get; set; }

        [JsonProperty("Flag")]
        public string Flag { get; set; }

        [JsonProperty("MCC")]
        public string Mcc { get; set; }

        [JsonProperty("MNC")]
        public string Mnc { get; set; }

        [JsonProperty("Operator")]
        public string NetworkName { get; set; }

        // اسم بديل تستخدمه أجزاء أخرى من الكود
        [JsonIgnore]
        public string Operator
        {
            get => NetworkName;
            set => NetworkName = value;
        }

        [JsonProperty("Type")]
        public string Type { get; set; }

        [JsonIgnore]
        public string FullCode => Mcc + Mnc;

        public override string ToString()
        {
            return $"{Flag} {NetworkName} (MCC:{Mcc} MNC:{Mnc})";
        }

        // المقارنة بالشبكة (MCC+MNC) وليس بمرجع الكائن، حتى لا تتكرر
        // الشبكة نفسها عند اختيارها من قوائم مختلفة
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            var other = obj as NetworkEntry;
            if (other == null) return false;
            return string.Equals(FullCode, other.FullCode, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            return (FullCode ?? "").GetHashCode();
        }
    }
}

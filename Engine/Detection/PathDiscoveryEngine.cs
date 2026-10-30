using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HmaxUnlocker.Engine.Communication;
using HmaxUnlocker.Engine.Models;

namespace HmaxUnlocker.Engine.Detection
{
    public class PathDiscoveryEngine
    {
        private readonly IAdbShell _adb;

        private static readonly string[] MPS_CANDIDATES = {
            "/efs/imei/mps_code.dat",
            "/mnt/vendor/efs/mps_code.dat",
            "/efs/mps_code.dat",
            "/vendor/efs/mps_code.dat",
            "/data/vendor/efs/mps_code.dat",
            "/nvdata/mps_code.dat",
        };

        private static readonly string[] CID_CANDIDATES = {
            "/efs/imei/cid.dat",
            "/mnt/vendor/efs/imei/cid.dat",
            "/efs/cid.dat",
            "/data/vendor/efs/cid.dat",
        };

        private static readonly string[] QCNV_CANDIDATES = {
            "/mnt/vendor/efs/QCNv_data.bin",
            "/efs/QCNv_data.bin",
            "/data/vendor/efs/QCNv_data.bin",
        };

        // nv_data.bin خاص بمنصات Exynos (مقابل QCNv في Qualcomm)
        private static readonly string[] NV_DATA_CANDIDATES = {
            "/efs/nv_data.bin",
            "/mnt/vendor/efs/nv_data.bin",
            "/persist/nv_data.bin",
        };

        private static readonly string[] PN_CANDIDATES = {
            "/efs/sec_efs/pn",
            "/mnt/vendor/efs/sec_efs/pn",
            "/efs/pn",
        };

        private static readonly string[] TELEPHONY_PROP_CANDIDATES = {
            "/mnt/vendor/efs/telephony.prop",
            "/efs/telephony.prop",
            "/data/vendor/efs/telephony.prop",
        };

        private static readonly string[] FACTORY_PROP_CANDIDATES = {
            "/mnt/vendor/efs/factory.prop",
            "/efs/factory.prop",
            "/data/vendor/efs/factory.prop",
        };

        private static readonly string[] EFS_ROOT_CANDIDATES = {
            "/efs",
            "/mnt/vendor/efs",
            "/data/vendor/efs",
            "/vendor/efs",
            "/nvdata",
            "/data/nvram",
        };

        private static readonly string[] CARRIER_CONFIG_CANDIDATES = {
            "/mnt/vendor/efs/carrier",
            "/efs/carrier",
            "/vendor/firmware-modem/image/modem_pr/mcfg/configs",
        };

        private static readonly string[] IMS_NV_CANDIDATES = {
            "/efs/sec_efs",
            "/mnt/vendor/efs/sec_efs",
        };

        private static readonly string[] NVRAM_CANDIDATES = {
            "/data/nvram",
            "/nvdata",
            "/proc/nvram",
            "/data/vendor/nvram",
        };

        public PathDiscoveryEngine(IAdbShell adb) => _adb = adb;

        public async Task<EfsPaths> DiscoverAsync(ChipsetType chipset)
        {
            var paths = new EfsPaths();

            paths.EfsRoot = await FindFirstExistingDirAsync(EFS_ROOT_CANDIDATES);
            paths.VendorEfsRoot = await FindFirstExistingDirAsync(new[] {
                "/mnt/vendor/efs", "/data/vendor/efs", "/vendor/efs"
            });

            paths.MpsCodePath       = await FindFirstExistingFileAsync(MPS_CANDIDATES);
            paths.CidDatPath        = await FindFirstExistingFileAsync(CID_CANDIDATES);
            paths.QcnvDataPath      = await FindFirstExistingFileAsync(QCNV_CANDIDATES);
            paths.NvDataPath        = await FindFirstExistingFileAsync(NV_DATA_CANDIDATES);
            paths.PnPath            = await FindFirstExistingFileAsync(PN_CANDIDATES);
            paths.TelephonyPropPath = await FindFirstExistingFileAsync(TELEPHONY_PROP_CANDIDATES);
            paths.FactoryPropPath   = await FindFirstExistingFileAsync(FACTORY_PROP_CANDIDATES);
            paths.CarrierConfigPath = await FindFirstExistingDirAsync(CARRIER_CONFIG_CANDIDATES);
            paths.ImsNvPath         = await FindFirstExistingDirAsync(IMS_NV_CANDIDATES);
            paths.NvramPath         = await FindFirstExistingDirAsync(NVRAM_CANDIDATES);

            if (chipset == ChipsetType.MediaTek && string.IsNullOrEmpty(paths.NvramPath))
                paths.NvramPath = await FindViaSearchAsync("/data", "nvram");

            paths.AllDiscoveredPaths = new List<string>
            {
                paths.EfsRoot, paths.VendorEfsRoot, paths.MpsCodePath,
                paths.CidDatPath, paths.QcnvDataPath, paths.NvDataPath, paths.PnPath,
                paths.TelephonyPropPath, paths.FactoryPropPath,
                paths.CarrierConfigPath, paths.ImsNvPath, paths.NvramPath
            }.Where(p => !string.IsNullOrEmpty(p)).ToList();

            return paths;
        }

        private async Task<string> FindFirstExistingFileAsync(string[] candidates)
        {
            foreach (var path in candidates)
                if (await _adb.FileExistsAsync(path)) return path;
            return null;
        }

        private async Task<string> FindFirstExistingDirAsync(string[] candidates)
        {
            foreach (var path in candidates)
                if (await _adb.DirectoryExistsAsync(path)) return path;
            return null;
        }

        private async Task<string> FindViaSearchAsync(string basePath, string keyword)
        {
            string result = await _adb.ExecuteRootAsync(
                $"find {basePath} -maxdepth 3 -type d -name '*{keyword}*' 2>/dev/null | head -1");
            string found = result?.Trim();
            return string.IsNullOrEmpty(found) ? null : found;
        }
    }
}

using System;
using HmaxUnlocker.Engine.Communication;
using HmaxUnlocker.Engine.Models;

namespace HmaxUnlocker.Engine.Strategies
{
    public static class PatchStrategyFactory
    {
        public static IPatchStrategy Create(ChipsetType chipset, IAdbShell adb)
        {
            switch (chipset)
            {
                case ChipsetType.Qualcomm:
                case ChipsetType.GoogleTensor:
                    return new QualcommStrategy(adb);
                case ChipsetType.Exynos:
                    return new ExynosStrategy(adb);
                case ChipsetType.MediaTek:
                case ChipsetType.Unisoc:
                    return new MediaTekStrategy(adb);
                default:
                    throw new NotSupportedException($"Chipset '{chipset}' is not yet supported.");
            }
        }
    }
}

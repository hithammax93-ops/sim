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
                    return new QualcommStrategy(adb);
                case ChipsetType.Exynos:
                    return new ExynosStrategy(adb);
                case ChipsetType.MediaTek:
                    return new MediaTekStrategy(adb);
                // لا نعيد استخدام استراتيجية منصة أخرى لمنصات لم تُختبر؛ ذلك يعطي دعماً وهمياً.
                default:
                    throw new NotSupportedException($"Chipset '{chipset}' is not yet supported.");
            }
        }
    }
}

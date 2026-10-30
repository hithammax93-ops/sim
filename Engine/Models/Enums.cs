namespace HmaxUnlocker.Engine.Models
{
    public enum ChipsetType
    {
        Unknown,
        Qualcomm,
        MediaTek,
        Exynos,
        Unisoc,
        GoogleTensor
    }

    public enum LockStatus
    {
        Unknown,
        OfficiallyUnlocked,
        FactoryLocked,
        RelockBypassed,
        SimLocked,
        CarrierLocked
    }

    public enum PatchResult
    {
        Success,
        PartialSuccess,
        Failed,
        UnsupportedDevice,
        RootRequired,
        PathNotFound,
        DeviceNotConnected
    }
}

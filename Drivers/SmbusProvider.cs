using ZenStates.Core.PawnIo;

namespace ZenStates.Core.Drivers
{
    /// <summary>
    /// Centralized SMBus driver provider.
    /// </summary>
    public static class SmbusProvider
    {
        /// <summary>
        /// Gets the singleton SMBus driver instance.
        /// </summary>
        public static SmbusDriverBase Instance
        {
            get { return LinuxAmdAccess.IsLinux ? (SmbusDriverBase)SmbusLinuxI2c.Instance : SmbusPiix4.Instance; }
        }
    }
}

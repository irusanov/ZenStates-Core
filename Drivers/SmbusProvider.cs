namespace ZenStates.Core.Drivers
{
    /// <summary>
    /// Centralized SMBus driver provider.
    /// </summary>
    internal static class SmbusProvider
    {
        /// <summary>
        /// Gets the singleton SMBus driver instance.
        /// </summary>
        internal static SmbusDriverBase Instance
        {
            get { return SmbusPiix4.Instance; }
        }
    }
}

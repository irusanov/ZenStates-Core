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
            get { return SmbusPiix4.Instance; }
        }
    }
}

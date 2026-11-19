using System.Diagnostics;
using ZenStates.Core.PawnIo;

namespace ZenStates.Core.Drivers
{
    /// <summary>
    /// Creates the APOB driver: the PawnIO AMDAPOB module (<see cref="AmdApob"/>) or InpOut physical memory reads
    /// (<see cref="ApobInpOut"/>). The caller owns the driver and disposes it.
    /// </summary>
    public static class ApobProvider
    {
        /// <summary>
        /// The driver of <paramref name="type"/>; with <see cref="ApobDriverType.Auto"/> the PawnIO module when it
        /// loads, otherwise InpOut. Null when the chosen driver is not available.
        /// </summary>
        /// <param name="topMemMsr">The TOP_MEM MSR (0xC001001A) for InpOut; 0 when it could not be read.</param>
        public static ApobDriverBase Create(ApobDriverType type, ulong topMemMsr = 0)
        {
            if (type != ApobDriverType.InpOut)
            {
                ApobDriverBase pawnIo = TryCreatePawnIo();
                if (pawnIo != null || type == ApobDriverType.PawnIo)
                    return pawnIo;
            }

            var inpOut = new ApobInpOut(topMemMsr);
            if (inpOut.IsAvailable)
                return inpOut;

            inpOut.Dispose();
            return null;
        }

        /// <summary>The driver chosen by <see cref="CoreOptions.ApobDriver"/> of the current options.</summary>
        public static ApobDriverBase CreateFromOptions(ulong topMemMsr = 0)
        {
            return Create(CoreOptions.Current.ApobDriver, topMemMsr);
        }

        private static ApobDriverBase TryCreatePawnIo()
        {
            AmdApob module = null;
            try
            {
                module = new AmdApob();
                if (module.IsAvailable)
                    return module;

                Debug.WriteLine("The PawnIO AMDAPOB module could not be loaded.");
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine("PawnIO AMDAPOB module: " + ex.Message);
            }

            module?.Dispose();
            return null;
        }
    }
}

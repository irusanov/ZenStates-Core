using System;
using ZenStates.Core.Hardware.Apob;

namespace ZenStates.Core.Drivers
{
    /// <summary>
    /// The APOB through physical memory reads with the InpOut driver. The table is located the same way as the
    /// AMDAPOB PawnIO module does it (the address recorded in the firmware image first, then the known addresses),
    /// so it works where the module can't be loaded, for example with Secure Boot.
    /// </summary>
    public sealed class ApobInpOut : ApobDriverBase
    {
        private readonly ulong _topMemMsr;

        /// <param name="topMemMsr">
        /// The TOP_MEM MSR (0xC001001A), which limits the probes to DRAM. 0 when it could not be read: TOP_MEM is
        /// then taken from the data fabric, or a fixed limit is used.
        /// </param>
        public ApobInpOut(ulong topMemMsr = 0)
        {
            _topMemMsr = topMemMsr;
        }

        private static IODriver Io => IODriver.Instance;

        public override ApobDriverType Type => ApobDriverType.InpOut;

        public override string Name => "InpOut";

        public override bool IsAvailable => Io != null;

        protected override ApobLocation LocateTable()
        {
            // TOP_MEM from the MSR, else from the data fabric (through ECAM), else the locator's fallback
            ulong topMem = ApobInpOutLocator.TopMemFromMsr(_topMemMsr);
            if (topMem == 0)
            {
                Trace.Add("TOP_MEM MSR not readable, trying the data fabric");
                byte[] mcfg = OHWM.FirmwareTable.GetTable(OHWM.FirmwareTable.Provider.ACPI, "MCFG");
                topMem = ApobInpOutLocator.TopMemFromDataFabric(ReadPhysicalDword, mcfg, Trace);
            }

            ApobInpOutLocator.Result result = ApobInpOutLocator.Locate(ReadPhysicalDword, topMem, Trace);
            if (result == null)
                return null;

            return new ApobLocation
            {
                Address = result.Address,
                Version = result.Version,
                TableSize = result.TableSize,
                HeaderSize = result.HeaderSize,
                Description = result.Describe(),
            };
        }

        protected override byte[] ReadTable(ApobLocation location)
        {
            return Io?.ReadMemory(new IntPtr(location.Address), unchecked((int)location.TableSize));
        }

        private static bool ReadPhysicalDword(ulong address, out uint value)
        {
            value = 0;
            IODriver io = Io;
            if (io == null || (address > uint.MaxValue && !Utils.Is64BitProcess))
                return false;

            return io.GetPhysLong(new UIntPtr(address), out value);
        }
    }
}

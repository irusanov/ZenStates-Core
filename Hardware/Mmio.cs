using System;
using System.Text;
using ZenStates.Core.Common;
using ZenStates.Core.Drivers;

namespace ZenStates.Core.Hardware
{
    /// <summary>
    /// The FCH MMIO window (0xFED80000) through InpOut: register access, the ACPI timer and the MMIO section of the debug
    /// report. The clock generator registers and everything else about BCLK are in <see cref="Bclk"/>.
    /// </summary>
    public class Mmio
    {
        // https://www.amd.com/system/files/TechDocs/52740_16h_Models_30h-3Fh_BKDG.pdf (page 859)
        internal const uint AMD_MMIO_BASE_ADDRESS = 0xFED80000;
        internal const uint MISC_BASE_ADDRESS = AMD_MMIO_BASE_ADDRESS + 0xE00;
        internal const uint SMBUS_BASE_ADDRESS = AMD_MMIO_BASE_ADDRESS + 0xA00;
        internal const uint SMUIO_BASE_ADDRESS = 0x0005A000;
        // internal const uint IOMUX_BASE = AMD_MMIO_BASE_ADDRESS + 0xD00;
        // FCH::PM::TmrValue (ACPI power management timer), 32-bit read-only count, PPR 55570 PMx0808
        internal const uint ACPI_PM_TIMER = AMD_MMIO_BASE_ADDRESS + 0x808;
        /// <summary>ACPI power management timer frequency as defined by the ACPI specification (derived from the 48 MHz crystal, not from CG1_PLL).</summary>
        public const double AcpiPmTimerHz = 3579545.0;
        internal const uint SMBUS_BASE_ADDRESS_REG = 0x300;
        // internal const uint IOMUX_LPCCLK1 = IOMUX_BASE + 0x1F;

        private static Mmio _instance;
        private readonly IODriver io;

        public static Mmio Instance => _instance;

        public Mmio()
        {
            if (IODriver.Instance == null)
            {
                return;
            }

            this.io = IODriver.Instance;
            _instance = this;
        }

        /// <summary>
        /// Clears <see cref="Instance"/> if it still points here, so nothing reaches the IO driver through it once
        /// <see cref="Cpu"/> has disposed that driver. Callers of <see cref="Instance"/> already null-check it.
        /// </summary>
        internal void Close()
        {
            if (ReferenceEquals(_instance, this))
                _instance = null;
        }

        /// <summary>True when the IO driver was there when this was created.</summary>
        public bool IsAvailable => io != null;

        /// <summary>
        /// Reads a 32-bit FCH register. A read that fails, or returns all ones (MMIO window disabled by PMx04[MmioEn]
        /// or not mapped), is reported as a failure instead of being decoded as valid register content.
        /// </summary>
        internal bool ReadRegister(uint address, out uint value)
        {
            value = 0;
            if (io == null || !io.GetPhysLong((UIntPtr)address, out uint raw) || raw == 0xFFFFFFFF)
                return false;

            value = raw;
            return true;
        }

        /// <summary>Writes a 32-bit FCH register. False when the IO driver is not there or the write failed.</summary>
        internal bool WriteRegister(uint address, uint value)
        {
            return io != null && io.SetPhysLong((UIntPtr)address, value);
        }

        private uint GetSmbusBaseAddress()
        {
            // PMx000[15:8] SmbusAsfIoBase: IO base address bits 15:8, a full 8-bit field (PPR 55570, reset 0Bh -> 0x0B00).
            if (!ReadRegister(AMD_MMIO_BASE_ADDRESS + SMBUS_BASE_ADDRESS_REG, out uint data) || data == 0)
                return 0;

            return ((data >> 8) & 0xFF) << 8;
        }

        /// <summary>Reads the 32-bit free running ACPI timer through the FCH MMIO window.</summary>
        public bool TryReadAcpiPmTimer(out uint value) => ReadRegister(ACPI_PM_TIMER, out value);

        public string GetReport()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(ReportBuilder.Heading("MMIO"));
            Bclk.Instance?.AppendRegisterReport(sb);
            sb.AppendLine();
            sb.AppendLine("-- Raw Data");

            for (int i = 0; i < 0x1000; i += 4)
            {
                if (io != null && io.GetPhysLong((UIntPtr)(AMD_MMIO_BASE_ADDRESS + i), out uint value))
                {
                    sb.AppendLine(string.Format("0x{0:X8}: 0x{1:X8}", AMD_MMIO_BASE_ADDRESS + i, value));
                }
            }

            return sb.ToString();
        }
    }
}

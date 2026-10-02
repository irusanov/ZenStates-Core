using System.Diagnostics;
using System.Text;
using ZenStates.Core.Drivers;
using ZenStates.Core.Hardware.DRAM.DDR5.Hub;
using ZenStates.Core.Hardware.DRAM.DDR5.Spd;

namespace ZenStates.Core.Hardware.DRAM.DDR5.Pmic
{
    /// <summary>
    /// Base of the DDR5 module PMICs (JESD301 family). It holds what every model reports - the VDD, VDDQ and VPP set
    /// points, the SWA / SWB / SWC regulators' measured voltage and power, temperature and faults - plus the register
    /// image and the bus access. Each model (<see cref="Pmic5100"/>, ...) maps its own registers onto these fields
    /// and adds its specific ones. The regulator fields keep the names of the former Ddr5PmicData.
    ///
    /// The PMIC sits on the module's local bus next to the SPD5118 hub, at 0x48 + the module's host ID
    /// (<see cref="Ddr5LocalDeviceAddress.Pmic"/>). Models are created by <see cref="Ddr5PmicFactory"/> from the
    /// PMIC type the SPD lists and the vendor ID the PMIC reports.
    /// </summary>
    public abstract class Ddr5Pmic
    {
        // Identification registers shared by the JESD301 family
        protected const byte REG_REVISION = 0x3B;       // [5:4] major - 1, [3:1] minor
        protected const byte REG_VENDOR_ID_0 = 0x3C;    // JEP106 continuation count (with parity)
        protected const byte REG_VENDOR_ID_1 = 0x3D;    // JEP106 manufacturer code (with parity)

        /// <summary>The model, as the SPD support device bytes encode it.</summary>
        public abstract Ddr5PmicType Type { get; }

        /// <summary>The PMIC was read (or parsed from a report) and decoded.</summary>
        public bool IsValid;

        /// <summary>7-bit address of the PMIC (0x48~0x4F).</summary>
        public byte I2cAddress;

        /// <summary>Address of the SPD hub of the same module (0x50~0x57).</summary>
        public byte SpdHubAddress;

        public byte VendorBank;
        public byte VendorCode;
        public string VendorName;
        public int RevisionMajor;
        public int RevisionMinor;

        /// <summary>Register image from 0x00, as read. Null for a PMIC parsed from a report that did not print it.</summary>
        public byte[] RawRegisters;

        // ---- Rails -----------------------------------------------------------

        /// <summary>Programmed set points of the module rails in millivolts.</summary>
        public int VddMv;
        public int VddqMv;
        public int VppMv;

        /// <summary>
        /// Measured (ADC) regulator outputs and inputs in millivolts, 0 when not measured. On a PMIC5100 SWA = VDD,
        /// SWB = VDDQ and SWC = VPP.
        /// </summary>
        public int SwaAdcMv;
        public int SwbAdcMv;
        public int SwcAdcMv;
        public int VinBulkMv;
        public int Vout18AdcMv;
        public int Vout10AdcMv;

        public string PmicTemperature;
        public bool HighTemperatureWarning;

        /// <summary>Faults the PMIC latched: input over-voltage, critical temperature shutdown, any output rail's power good.</summary>
        public bool VinBulkOverVoltage;
        public bool CriticalTemperatureShutdown;
        public bool PowerGoodFault;

        /// <summary>Power per regulator and in total, in watts.</summary>
        public double SwaW;
        public double SwbW;
        public double SwcW;
        public double TotalW;

        /// <summary>Number of registers read from 0x00.</summary>
        protected abstract int RegisterCount { get; }

        /// <summary>Highest VDD / VDDQ set point <see cref="SetVoltages"/> accepts.</summary>
        public abstract int MaxVddMv { get; }

        // ---- Reading ---------------------------------------------------------

        /// <summary>Reads the register image, decodes it and reads the live values. The bus must be on the module port.</summary>
        internal bool ReadNoLock(SmbusDriverBase smbus)
        {
            byte[] registers = new byte[RegisterCount];
            int failures = 0;

            for (int i = 0; i < registers.Length; i++)
            {
                if (!smbus.ReadByteDataNoLock(I2cAddress, (byte)i, out registers[i]))
                {
                    registers[i] = 0xFF;
                    failures++;
                }
            }

            if (failures == registers.Length)
                return false;

            if (failures > 0)
                Debug.WriteLine(string.Format("PMIC 0x{0:X2}: {1} register(s) could not be read.", I2cAddress, failures));

            RawRegisters = registers;
            Decode();
            IsValid = true;
            RefreshNoLock(smbus);
            return true;
        }

        /// <summary>Decodes <see cref="RawRegisters"/> again, e.g. after they were taken from a report.</summary>
        internal void DecodeRegisters()
        {
            if (RawRegisters != null && RawRegisters.Length >= RegisterCount)
                Decode();
        }

        /// <summary>Decodes <see cref="RawRegisters"/>. Models call the base first for the identification.</summary>
        protected virtual void Decode()
        {
            VendorBank = RawRegisters[REG_VENDOR_ID_0];
            VendorCode = RawRegisters[REG_VENDOR_ID_1];
            VendorName = ManufacturerMapping.Lookup(VendorBank, VendorCode);

            byte revision = RawRegisters[REG_REVISION];
            RevisionMajor = ((revision >> 4) & 0x03) + 1;
            RevisionMinor = (revision >> 1) & 0x07;
        }

        /// <summary>
        /// Re-reads what changes at runtime (set points, measured rails, temperature, power). The bus must be on the
        /// module port and the SMBus mutex held.
        /// </summary>
        internal abstract bool RefreshNoLock(SmbusDriverBase smbus);

        // ---- Writing ---------------------------------------------------------

        protected delegate bool BusAction(SmbusDriverBase smbus);

        /// <summary>Runs <paramref name="action"/> with the SMBus mutex held and the bus on the module port; restores the port.</summary>
        protected static bool RunOnModuleBus(BusAction action)
        {
            SmbusDriverBase smbus = SmbusProvider.Instance;
            if (smbus == null || !Mutexes.WaitSmbus(5000))
                return false;

            try
            {
                smbus.ChangePortNoLock(-1, out int savedPort);
                try
                {
                    return Spd5118Hub.SelectHubPortNoLock(smbus, true) && action(smbus);
                }
                finally
                {
                    if (savedPort >= 0)
                        smbus.ChangePortNoLock(savedPort);
                }
            }
            finally
            {
                Mutexes.ReleaseSmbus();
            }
        }

        /// <summary>Programs the VDD, VDDQ and VPP set points (mV). Takes the SMBus mutex.</summary>
        public bool SetVoltages(int vddMv, int vddqMv, int vppMv, out string error)
        {
            if (!IsValid || RawRegisters == null)
            {
                error = "PMIC registers are not available.";
                return false;
            }

            string writeError = null;
            bool ok = RunOnModuleBus(smbus => WriteVoltagesNoLock(smbus, vddMv, vddqMv, vppMv, out writeError));
            error = ok ? null : writeError ?? "The SMBus could not be accessed.";
            return ok;
        }

        /// <summary>Validates, encodes and writes the set points, then reads them back into this object.</summary>
        protected abstract bool WriteVoltagesNoLock(SmbusDriverBase smbus, int vddMv, int vddqMv, int vppMv, out string error);

        // ---- Output ----------------------------------------------------------

        /// <summary>The PMIC block of the debug report (see <see cref="Ddr5PmicDumpDecoder"/> for the way back).</summary>
        public override string ToString()
        {
            if (!IsValid)
                return "  PMIC: not detected\n";

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("  Vendor             : {0}\n", VendorName);
            sb.AppendFormat("  Device type        : {0}\n", Type);
            sb.AppendFormat("  Revision           : {0}.{1}\n", RevisionMajor, RevisionMinor);
            sb.AppendFormat("  I2C Address        : 0x{0:X2}\n", I2cAddress);

            AppendDetails(sb);

            if (RawRegisters != null && RawRegisters.Length > 0)
            {
                sb.AppendLine();
                sb.AppendFormat("  Raw registers      : 0x00-0x{0:X2}\n", RawRegisters.Length - 1);
                for (int row = 0; row < RawRegisters.Length; row += 16)
                {
                    sb.Append("   ");
                    for (int i = row; i < row + 16 && i < RawRegisters.Length; i++)
                        sb.AppendFormat(" {0:X2}", RawRegisters[i]);
                    sb.Append('\n');
                }
            }

            return sb.ToString();
        }

        /// <summary>The model specific lines, between the identification and the raw register dump.</summary>
        protected abstract void AppendDetails(StringBuilder sb);
    }
}

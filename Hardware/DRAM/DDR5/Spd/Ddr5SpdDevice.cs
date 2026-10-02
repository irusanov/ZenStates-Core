namespace ZenStates.Core.Hardware.DRAM.DDR5.Spd
{
    /// <summary>PMIC device type codes of the SPD support device bytes (JESD400-5 / JESD406-5 bytes 198~209).</summary>
    public enum Ddr5PmicType
    {
        Unknown = -1,
        PMIC5000 = 0,
        PMIC5010 = 1,
        PMIC5100 = 2,
        PMIC5020 = 3,
        PMIC5120 = 4,
        PMIC5200 = 5,
        PMIC5030 = 6,
    }

    /// <summary>
    /// A support device as the SPD lists it (bytes 194~213): SPD hub, PMIC0~2 or the thermal sensors. Each entry is the
    /// JEP106 manufacturer ID, a device type byte and a revision byte.
    /// </summary>
    public class Ddr5SpdDevice
    {
        /// <summary>"SPD", "PMIC0", "PMIC1", "PMIC2" or "TS".</summary>
        public string Role;

        public bool Installed;

        public byte MfgIdBank;
        public byte MfgIdCode;
        public string Manufacturer;

        /// <summary>Device type byte bits [3:0].</summary>
        public int TypeCode;
        public string TypeName;

        /// <summary>Major and minor revision, each a BCD nibble, e.g. "1.0".</summary>
        public string Revision;

        /// <summary>The PMIC model, for a PMIC entry that is installed; <see cref="Ddr5PmicType.Unknown"/> otherwise.</summary>
        public Ddr5PmicType PmicType
        {
            get
            {
                if (!Installed || Role == null || !Role.StartsWith("PMIC", System.StringComparison.Ordinal) || TypeCode > (int)Ddr5PmicType.PMIC5030)
                    return Ddr5PmicType.Unknown;
                return (Ddr5PmicType)TypeCode;
            }
        }

        public override string ToString()
        {
            if (!Installed)
                return "Not listed";

            string text = TypeName;
            if (!string.IsNullOrEmpty(Manufacturer))
                text += ", " + Manufacturer;
            if (!string.IsNullOrEmpty(Revision))
                text += ", rev " + Revision;
            return text;
        }
    }
}

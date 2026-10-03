namespace ZenStates.Core.Hardware.DRAM.DDR5.Pmic
{
    /// <summary>
    /// Richtek PMIC5100 (e.g. RTQ5132, JEP106 0x8A 0x8C) with its high-voltage mode:
    /// <list type="bullet">
    /// <item>R0x2B [5:4], reserved in JESD301-2 and the datasheet, read 11 with the rails in high-voltage mode and 00
    /// otherwise (verified at 1.100 / 1.430 / 1.450 / 1.540 V).</item>
    /// <item>VDD / VDDQ are written as the whole byte, (mV - 800) / 5. The PMIC turns a byte up to 0x7F into the JEDEC
    /// form (0x7E reads back as 0xFC, 1430 mV) and runs a larger one in high-voltage mode, reading back unchanged
    /// (0x82 = 1450 mV).</item>
    /// <item>The codes JESD301-2 reserves are defined: R0x20 SWA/SWB code 3 = 4.5 A, SWC codes 2 / 3 = 1.5 / 2.0 A,
    /// switching frequency codes 1 / 2 / 3 = 1000 / 1250 / 1500 KHz (RTQ5132 datasheet).</item>
    /// </list>
    /// </summary>
    public class Pmic5100Richtek : Pmic5100
    {
        public const byte VENDOR_BANK = 0x8A;
        public const byte VENDOR_CODE = 0x8C;

        private const byte HIGH_VOLTAGE_MASK = 0x30;   // R0x2B [5:4]

        public override int MaxVddMv
        {
            get { return HIGH_VOLTAGE_SWAB_MAX_MV; }
        }

        public static bool IsRichtek(byte vendorBank, byte vendorCode)
        {
            return vendorBank == VENDOR_BANK && vendorCode == VENDOR_CODE;
        }

        protected override bool TryReadHighVoltageFlag(out bool highVoltage)
        {
            highVoltage = false;

            // A report parsed without its register image has no flag; the measured rails decide then
            if (RawRegisters == null || RawRegisters.Length <= REG_LDO_SETTINGS)
                return false;

            highVoltage = (RawRegisters[REG_LDO_SETTINGS] & HIGH_VOLTAGE_MASK) != 0;
            return true;
        }

        protected override byte EncodeSwabVid(int mv, byte current)
        {
            if (!HighVoltageMode && mv <= JEDEC_SWAB_MAX_MV)
                return base.EncodeSwabVid(mv, current);

            return (byte)VidSteps(mv, SWAB_BASE_MV);
        }

        // 3.0 / 3.5 / 4.0 / 4.5 A
        protected override int DecodeSwabCurrentLimitMa(int code)
        {
            return 3000 + (code & 0x03) * 500;
        }

        // 0.5 / 1.0 / 1.5 / 2.0 A
        protected override int DecodeSwcCurrentLimitMa(int code)
        {
            return 500 + (code & 0x03) * 500;
        }

        // 750 / 1000 / 1250 / 1500 KHz
        protected override string DecodeSwitchingFrequency(int code)
        {
            return string.Format("{0} KHz", 750 + (code & 0x03) * 250);
        }
    }
}

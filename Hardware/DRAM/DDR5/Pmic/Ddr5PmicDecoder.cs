

using System;
using static ZenStates.Core.Hardware.DRAM.DDR5.Tables.JedecPmicRegisters;

namespace ZenStates.Core.Hardware.DRAM.DDR5.Pmic
{
    public static partial class Ddr5PmicDecoder
    {
        // Voltage formulas (JEDEC JESD301-2)
        private const int SWA_SWB_BASE = 800;   // mV base for VDD/VDDQ
        private const int SWC_BASE = 1500;      // mV base for VPP
        private const int VID_STEP = 5;         // mV per VID code

        // Highest set point the JEDEC 7-bit VID can express (800 + 127 x 5 mV).
        private const int JEDEC_SWAB_MAX_MV = 1435;
        // Richtek (JEP106 0x8A 0x8C): R0x2B[5:4], reserved in the RTQ5132 datasheet, read 11 with the
        // rails in high-voltage mode and 00 otherwise (verified at 1.100 / 1.430 / 1.450 / 1.540 V).
        private const byte RICHTEK_VENDOR_BANK = 0x8A;
        private const byte RICHTEK_VENDOR_CODE = 0x8C;
        private const byte RICHTEK_HIGH_VOLTAGE_MASK = 0x30;

        // SWA/SWB ADC accuracy outside 1050-1160 mV is 3 LSB x 15 mV (JESD301-2; RTQ5132 table 6).
        private const int ADC_TOLERANCE_MV = 45;

        /// <summary>Decode SWA/SWB VID to millivolts (JEDEC 7-bit: bits [7:1]).</summary>
        public static int SwabVid7ToMv(byte reg) { return SWA_SWB_BASE + ((reg >> 1) & 0x7F) * VID_STEP; }

        // In JEDEC mode only bits [7:1] of R0x21/R0x25/R0x27 are the VID; bit 0 selects the
        // power-good low-side threshold (-5% / -7.5%).

        /// <summary>
        /// Decode SWA/SWB VID to millivolts in high-voltage (OC) mode: the whole byte in 5 mV steps,
        /// 800-2075 mV. This mode is a vendor extension of OC PMICs (the RTQ5132 datasheet doesn't
        /// document it). On a Richtek PMIC, writing a byte above 0x7F this way switches it into
        /// high-voltage mode and the byte reads back unchanged (0x82 = 1450 mV, 0x94 = 1540 mV).
        /// </summary>
        public static int SwabVid8ToMv(byte reg) { return SWA_SWB_BASE + reg * VID_STEP; }

        /// <summary>Decode SWC VID to millivolts (JEDEC 7-bit: bits [7:1]).</summary>
        public static int SwcVid7ToMv(byte reg) { return SWC_BASE + ((reg >> 1) & 0x7F) * VID_STEP; }

        /// <summary>Decode SWC VID to millivolts in high-voltage mode (see SwabVid8ToMv).</summary>
        public static int SwcVid8ToMv(byte reg) { return SWC_BASE + reg * VID_STEP; }

        // Current limiter decode (R0x20)─────────────
        public static int DecodeSwabCurrentLimit(int code)
        {
            switch (code)
            {
                case 0: return 3000;
                case 1: return 4000;
                case 2: return 5000;
                default: return 6000;
            }
        }

        public static int DecodeSwcCurrentLimit(int code)
        {
            switch (code)
            {
                case 0: return 500;
                case 1: return 750;
                case 2: return 1000;
                default: return 1250;
            }
        }

        // OV threshold decode (R0x22/R0x26/R0x28 [5:4])
        public static string DecodeOvThreshold(int code)
        {
            switch (code)
            {
                case 0: return "+7.5%";
                case 1: return "+10%";
                case 2: return "+12.5%";
                default: return "Reserved";
            }
        }

        // UV threshold decode (R0x22/R0x26/R0x28 [3:2])
        public static string DecodeUvThreshold(int code)
        {
            switch (code)
            {
                case 0: return "-10%";
                case 1: return "-12.5%";
                case 2: return "-15%";
                default: return "Reserved";
            }
        }

        // PMIC temperature decode (R0x33 [7:5])
        public static string DecodePmicTemp(int code)
        {
            switch (code)
            {
                case 0: return "< 85 C";
                case 1: return "85 C";
                case 2: return "95 C";
                case 3: return "105 C";
                case 4: return "115 C";
                case 5: return "125 C";
                case 6: return "135 C";
                default: return "> 140 C";
            }
        }

        /// <summary>
        /// PMIC high-temperature warning threshold (R0x1B [2:0]).
        /// The codes line up 1:1 with the temperature codes reported in R0x33 [7:5].
        /// </summary>
        public static string DecodeHighTempWarningThreshold(int code)
        {
            switch (code & 0x07)
            {
                case 1: return "> 85 C";
                case 2: return "> 95 C";
                case 3: return "> 105 C";
                case 4: return "> 115 C";
                case 5: return "> 125 C";
                case 6: return "> 135 C";
                default: return "Reserved";
            }
        }

        public static string DecodeShutdownThreshold(int code)
        {
            switch (code & 0x07)
            {
                case 0: return "> 105 C";
                case 1: return "> 115 C";
                case 2: return "> 125 C";
                case 3: return "> 135 C";
                case 4: return "> 145 C";
                default: return "Reserved";
            }
        }

        public static string DecodeModeSelect(int code)
        {
            switch (code & 0x03)
            {
                case 2: return "COT; DCM";
                case 3: return "COT; Forced CCM";
                default: return "Reserved";
            }
        }

        public static string DecodeSwitchingFrequency(int code)
        {
            switch (code & 0x03)
            {
                case 0: return "750 KHz";
                default: return "Vendor specific";
            }
        }

        public static int DecodeLdo18Mv(byte reg)
        {
            switch ((reg >> 6) & 0x03)
            {
                case 0: return 1700;
                case 1: return 1800;
                case 2: return 1900;
                default: return 2000;
            }
        }

        public static int DecodeLdo10Mv(byte reg)
        {
            switch ((reg >> 1) & 0x03)
            {
                case 0: return 900;
                case 1: return 1000;
                case 2: return 1100;
                default: return 1200;
            }
        }

        public static string DecodeAdcInput(int code)
        {
            switch (code & 0x0F)
            {
                case 0x0: return "SWA Output Voltage";
                case 0x2: return "SWB Output Voltage";
                case 0x3: return "SWC Output Voltage";
                case 0x5: return "VIN_Bulk Input Voltage";
                case 0x8: return "VOUT_1.8V Output Voltage";
                case 0x9: return "VOUT_1.0V Output Voltage";
                default: return "Reserved";
            }
        }

        public static string DecodeAdcUpdateFrequency(int code)
        {
            switch (code & 0x03)
            {
                case 0: return "1 ms";
                case 1: return "2 ms";
                case 2: return "4 ms";
                default: return "8 ms";
            }
        }

        public static int DecodeAdcMv(int adcSelect, byte raw)
        {
            if (raw == 0)
                return 0;

            // JESD301-2 Table 137:
            // - SWA/SWB/SWC/VOUT_1.8V/VOUT_1.0V = 15 mV per LSB
            // - VIN_Bulk = 70 mV per LSB
            if (adcSelect == 0x5)
                return raw * 70;

            switch (adcSelect & 0x0F)
            {
                case 0x0:
                case 0x2:
                case 0x3:
                case 0x8:
                case 0x9:
                    return raw * 15;
                default:
                    return 0;
            }
        }

        // PMIC vendor identification
        public static string LookupPmicVendor(byte bank, byte code)
        {
            return ManufacturerMapping.Lookup(bank, code);
        }

        public static Ddr5PmicData Decode(byte pmicAddr, byte[] rawRegisters)
        {
            if (rawRegisters == null || rawRegisters.Length <= REG_NVM_LDO_SETTINGS)
                return new Ddr5PmicData { IsValid = false };

            Ddr5PmicData pd = new Ddr5PmicData();
            pd.IsValid = true;
            pd.I2cAddress = pmicAddr;
            pd.SpdHubAddress = unchecked((byte)(pmicAddr + SPD_PMIC_OFFSET));
            pd.RawRegisters = rawRegisters;

            // Vendor ID (R0x3C:R0x3D — JEP106 with parity)
            pd.VendorBank = pd.RawRegisters[REG_VENDOR_BANK];
            pd.VendorCode = pd.RawRegisters[REG_VENDOR_CODE];
            pd.VendorName = Ddr5PmicDecoder.LookupPmicVendor(pd.VendorBank, pd.VendorCode);

            // Revision (R0x3B)
            byte rev = pd.RawRegisters[REG_REVISION];
            pd.RevisionMajor = ((rev >> 4) & 0x03) + 1;
            pd.RevisionMinor = (rev >> 1) & 0x07;

            // VR Enable (R0x32 [7])
            pd.VrEnabled = (pd.RawRegisters[REG_VR_ENABLE] & 0x80) != 0;

            // ADC / telemetry mode
            byte reg30 = pd.RawRegisters[REG_TELEMETRY_SELECT];
            byte reg1A = pd.RawRegisters[0x1A];
            byte reg1B = pd.RawRegisters[0x1B];
            pd.AdcEnabled = (reg30 & 0x80) != 0;
            pd.AdcSelectedInput = Ddr5PmicDecoder.DecodeAdcInput((reg30 >> 3) & 0x0F);
            pd.AdcUpdateFrequency = Ddr5PmicDecoder.DecodeAdcUpdateFrequency(reg30 & 0x03);
            pd.TelemetryReportsPower = (reg1B & 0x40) != 0;
            pd.HighTemperatureWarningThreshold = Ddr5PmicDecoder.DecodeHighTempWarningThreshold(reg1B & 0x07);
            pd.TelemetryReportsTotalPower = (reg1A & 0x02) != 0;

            // PMIC temperature (R0x33 [7:5]) and LDO PG
            byte reg33 = pd.RawRegisters[REG_PMIC_TEMP];
            pd.PmicTemperature = Ddr5PmicDecoder.DecodePmicTemp((reg33 >> 5) & 0x07);
            pd.Vout10PowerGood = (reg33 & 0x04) == 0;

            //5 RW 0
            //R2B[5]: SWA_VOLTAGE_RANGE
            //SWA Output Voltage Range Selection
            //0 = Range: 800mV to 1435mV for SWA; 5mV step size
            //1 = Range: 600mV to 1235mV for SWA; 5mV step size
            //4 RW 0
            //R2B[4]: SWB_VOLTAGE_RANGE
            //SWB Output Voltage Range Selection
            //0 = Range: 800mV to 1435mV for SWB; 5mV step size
            //1 = Range: 600mV to 1235mV for SWB; 5mV step size
            //3 RW 0
            //R2B[3]: SWC_VOLTAGE_RANGE
            //SWC Output Voltage Range Selection
            //0 = Range: 800mV to 1435mV for SWC; 5mV step size
            //1 = Range: 600mV to 1235mV for SWC; 5mV step size

            //R2F[2]: WRITE_PROTECT_FUNCTION_CONTROL
            //PMIC Write Protect Function Control
            //0 = CAMP input signal determines the Write Protect Function
            //1 = Write Protect Function is disabled; All register write access is allowed independent
            //of CAMP input signal


            // LDO runtime / NVM defaults
            pd.Vout18SettingMv = Ddr5PmicDecoder.DecodeLdo18Mv(pd.RawRegisters[REG_LDO_SETTINGS]);
            pd.Vout10SettingMv = Ddr5PmicDecoder.DecodeLdo10Mv(pd.RawRegisters[REG_LDO_SETTINGS]);
            pd.Vout18SettingNvmMv = Ddr5PmicDecoder.DecodeLdo18Mv(pd.RawRegisters[REG_NVM_LDO_SETTINGS]);
            pd.Vout10SettingNvmMv = Ddr5PmicDecoder.DecodeLdo10Mv(pd.RawRegisters[REG_NVM_LDO_SETTINGS]);

            // Main rail voltage decode
            DecodeVoltageSettings(pd);

            // Telemetry raw registers
            pd.SwaTelemetryRaw = pd.RawRegisters[0x0C];
            pd.SwbTelemetryRaw = pd.RawRegisters[0x0E] & 0x3F;
            pd.SwcTelemetryRaw = pd.RawRegisters[0x0F] & 0x3F;

            // Current limiter
            byte clim = pd.RawRegisters[REG_CURRENT_LIMIT];
            pd.CurrentLimitRaw = clim;
            pd.SwaCurrentLimitMa = Ddr5PmicDecoder.DecodeSwabCurrentLimit((clim >> 6) & 0x03);
            pd.SwbCurrentLimitMa = Ddr5PmicDecoder.DecodeSwabCurrentLimit((clim >> 2) & 0x03);
            pd.SwcCurrentLimitMa = Ddr5PmicDecoder.DecodeSwcCurrentLimit(clim & 0x03);

            // Protection thresholds
            byte swaThresh = pd.RawRegisters[REG_SWA_THRESH];
            byte swbThresh = pd.RawRegisters[REG_SWB_THRESH];
            byte swcThresh = pd.RawRegisters[REG_SWC_THRESH];
            pd.VddOvThreshold = Ddr5PmicDecoder.DecodeOvThreshold((swaThresh >> 4) & 0x03);
            pd.VddUvThreshold = Ddr5PmicDecoder.DecodeUvThreshold((swaThresh >> 2) & 0x03);
            pd.VddqOvThreshold = Ddr5PmicDecoder.DecodeOvThreshold((swbThresh >> 4) & 0x03);
            pd.VddqUvThreshold = Ddr5PmicDecoder.DecodeUvThreshold((swbThresh >> 2) & 0x03);
            pd.VppOvThreshold = Ddr5PmicDecoder.DecodeOvThreshold((swcThresh >> 4) & 0x03);
            pd.VppUvThreshold = Ddr5PmicDecoder.DecodeUvThreshold((swcThresh >> 2) & 0x03);

            // Rail config
            byte reg29 = pd.RawRegisters[REG_RAIL_CONFIG_A];
            byte reg2A = pd.RawRegisters[REG_RAIL_CONFIG_B];
            byte reg2E = pd.RawRegisters[REG_SHUTDOWN_TEMP];
            pd.SwaMode = Ddr5PmicDecoder.DecodeModeSelect((reg29 >> 6) & 0x03);
            pd.SwaSwitchingFrequency = Ddr5PmicDecoder.DecodeSwitchingFrequency((reg29 >> 4) & 0x03);
            pd.SwbMode = Ddr5PmicDecoder.DecodeModeSelect((reg2A >> 6) & 0x03);
            pd.SwbSwitchingFrequency = Ddr5PmicDecoder.DecodeSwitchingFrequency((reg2A >> 4) & 0x03);
            pd.SwcMode = Ddr5PmicDecoder.DecodeModeSelect((reg2A >> 2) & 0x03);
            pd.SwcSwitchingFrequency = Ddr5PmicDecoder.DecodeSwitchingFrequency(reg2A & 0x03);
            pd.ShutdownTemperatureThreshold = Ddr5PmicDecoder.DecodeShutdownThreshold(reg2E & 0x07);

            // Status flags
            byte status0 = pd.RawRegisters[REG_STATUS_0];
            byte status1 = pd.RawRegisters[REG_STATUS_1];
            byte status2 = pd.RawRegisters[REG_STATUS_2];
            pd.VinBulkOverVoltage = (status0 & 0x01) != 0;
            pd.SwaPowerGoodFault = (status0 & 0x20) != 0;
            pd.SwbPowerGoodFault = (status0 & 0x08) != 0;
            pd.SwcPowerGoodFault = (status0 & 0x04) != 0;
            pd.HighTemperatureWarning = (status1 & 0x80) != 0;
            pd.CriticalTemperatureShutdown = (status0 & 0x40) != 0;
            pd.PecError = (status2 & 0x08) != 0;
            pd.ParityError = (status2 & 0x04) != 0;

            // High-voltage / OC mode can't be told from the VID registers alone (the two decodes
            // differ for every non-zero value); it is resolved from the measured rails once the ADC
            // has been read, see ResolveVoltageMode.
            ResolveVoltageMode(pd);

            //0 = CAMP input signal determines the Write Protect Function
            //1 = Write Protect Function is disabled; All register write access is allowed independent
            pd.WriteProtectFunctionControl = ((pd.RawRegisters[REG_WRITE_PROTECT_FUNCTION_CONTROL] >> 2) & 0x1) == 1 ? "Disabled" : "CAMP input signal";

            return pd;
        }

        /// <summary>
        /// Calculate power in watts from the raw telemetry registers and write the results to
        /// <see cref="Ddr5PmicData.SwaW"/>, <see cref="Ddr5PmicData.SwbW"/>,
        /// <see cref="Ddr5PmicData.SwcW"/> and <see cref="Ddr5PmicData.TotalW"/>.
        /// <para>
        /// Power mode (<see cref="Ddr5PmicData.TelemetryReportsPower"/> = true):
        /// each register LSB = 0.125 W (JESD301-2: SWA 8-bit max 31.875 W, SWB/SWC 6-bit max 7.875 W).
        /// When total power mode is also active, the SWA register holds the combined total
        /// and <see cref="Ddr5PmicData.SwaW"/> is set to zero.
        /// </para>
        /// <para>
        /// Current mode (<see cref="Ddr5PmicData.TelemetryReportsPower"/> = false):
        /// the raw code is the rail current in 125 mA steps (JESD301),
        /// and multiplied by the ADC-measured voltage, falling back to the programmed VID if ADC is unavailable.
        /// </para>
        /// </summary>
        /// <summary>
        /// Decodes the programmed rail voltages from R0x21 / R0x25 / R0x27 in
        /// <see cref="Ddr5PmicData.RawRegisters"/>, both as JEDEC and as high-voltage mode set points.
        /// </summary>
        public static void DecodeVoltageSettings(Ddr5PmicData pd)
        {
            if (pd?.RawRegisters == null || pd.RawRegisters.Length <= REG_SWC_VID)
                return;

            byte swaVid = pd.RawRegisters[REG_SWA_VID];
            byte swbVid = pd.RawRegisters[REG_SWB_VID];
            byte swcVid = pd.RawRegisters[REG_SWC_VID];
            pd.VddMv = SwabVid7ToMv(swaVid);
            pd.VddqMv = SwabVid7ToMv(swbVid);
            pd.VppMv = SwcVid7ToMv(swcVid);
            pd.VddMv8bit = SwabVid8ToMv(swaVid);
            pd.VddqMv8bit = SwabVid8ToMv(swbVid);
            pd.VppMv8bit = SwcVid8ToMv(swcVid);
        }

        /// <summary>
        /// Decides whether the SWA/SWB set points use the high-voltage (whole byte) VID encoding
        /// instead of the JEDEC 7-bit one. Richtek PMICs report it in R0x2B[5:4]. Other vendors don't
        /// document a flag, so there it is read from the ADC-measured VDD/VDDQ: above the JEDEC
        /// maximum it can only be high-voltage mode, otherwise the decode the measurement is closer to
        /// wins. Without either the JEDEC encoding is assumed.
        /// </summary>
        public static void ResolveVoltageMode(Ddr5PmicData pd)
        {
            if (pd == null)
                return;

            if (TryReadHighVoltageFlag(pd, out bool highVoltage))
            {
                pd.HighVoltageMode = highVoltage;
                return;
            }

            pd.HighVoltageMode =
                CloserTo8Bit(pd.SwaAdcMv, pd.VddMv, pd.VddMv8bit) ||
                CloserTo8Bit(pd.SwbAdcMv, pd.VddqMv, pd.VddqMv8bit);
        }

        /// <summary>Whether the PMIC is a Richtek part, the only vendor whose high-voltage mode is mapped.</summary>
        public static bool IsRichtek(Ddr5PmicData pd)
        {
            return pd != null && pd.VendorBank == RICHTEK_VENDOR_BANK && pd.VendorCode == RICHTEK_VENDOR_CODE;
        }

        /// <summary>Lowest VDD/VDDQ set point, and highest VPP/VDD/VDDQ set points this code can write.</summary>
        public const int SwabMinMv = SWA_SWB_BASE;
        public const int SwcMinMv = SWC_BASE;
        public const int SwcMaxMv = SWC_BASE + 127 * VID_STEP;          // 2135
        public const int SwabHighVoltageMaxMv = SWA_SWB_BASE + 255 * VID_STEP; // 2075

        /// <summary>
        /// Highest VDD/VDDQ set point that can be written to this PMIC: 2075 mV where high-voltage
        /// mode is known (Richtek), the JEDEC 1435 mV otherwise.
        /// </summary>
        public static int MaxSwabMv(Ddr5PmicData pd)
        {
            return IsRichtek(pd) ? SwabHighVoltageMaxMv : JEDEC_SWAB_MAX_MV;
        }

        /// <summary>
        /// Encodes VDD/VDDQ/VPP set points (mV, snapped to 5 mV) into R0x21/R0x25/R0x27 values for
        /// <see cref="Ddr5PmicReader.WriteVoltageRegisters"/>; the inverse of the decode above.
        /// <para>
        /// Richtek: VDD/VDDQ are written as the whole byte, (mV - 800) / 5. The PMIC turns a byte up
        /// to 0x7F into the JEDEC form (0x7E reads back as 0xFC, 1430 mV) and runs a larger byte in
        /// high-voltage mode, reading back unchanged (0x82 = 1450 mV), which is what
        /// <see cref="SwabVid8ToMv"/> decodes.
        /// </para>
        /// <para>
        /// Other vendors: JEDEC 7-bit VID in bits [7:1], up to 1435 mV; their high-voltage mode is
        /// not known, so higher set points are refused.
        /// </para>
        /// VPP always uses the JEDEC encoding. Wherever the JEDEC form is written, bit 0 (power-good
        /// threshold select) keeps its current value.
        /// </summary>
        public static bool TryEncodeVoltageSettings(Ddr5PmicData pd, int vddMv, int vddqMv, int vppMv,
            out byte vddReg, out byte vddqReg, out byte vppReg, out string error)
        {
            vddReg = vddqReg = vppReg = 0;
            error = null;

            if (pd == null || pd.RawRegisters == null || pd.RawRegisters.Length <= REG_SWC_VID)
            {
                error = "PMIC registers are not available.";
                return false;
            }

            int swabMax = MaxSwabMv(pd);
            if (!InRange(vddMv, SWA_SWB_BASE, swabMax) || !InRange(vddqMv, SWA_SWB_BASE, swabMax))
            {
                error = string.Format("VDD/VDDQ must be {0}-{1} mV for this PMIC.", SWA_SWB_BASE, swabMax);
                return false;
            }

            if (!InRange(vppMv, SWC_BASE, SwcMaxMv))
            {
                error = string.Format("VPP must be {0}-{1} mV.", SWC_BASE, SwcMaxMv);
                return false;
            }

            if (IsRichtek(pd))
            {
                vddReg = (byte)Steps(vddMv, SWA_SWB_BASE);
                vddqReg = (byte)Steps(vddqMv, SWA_SWB_BASE);
            }
            else
            {
                vddReg = JedecVid(vddMv, SWA_SWB_BASE, pd.RawRegisters[REG_SWA_VID]);
                vddqReg = JedecVid(vddqMv, SWA_SWB_BASE, pd.RawRegisters[REG_SWB_VID]);
            }

            vppReg = JedecVid(vppMv, SWC_BASE, pd.RawRegisters[REG_SWC_VID]);
            return true;
        }

        private static bool InRange(int mv, int min, int max)
        {
            return mv >= min && mv <= max;
        }

        // Whole 5 mV steps above the base, rounded to the nearest step.
        private static int Steps(int mv, int baseMv)
        {
            return (mv - baseMv + VID_STEP / 2) / VID_STEP;
        }

        private static byte JedecVid(int mv, int baseMv, byte current)
        {
            int code = Math.Min(127, Steps(mv, baseMv));
            return (byte)((code << 1) | (current & 0x01));
        }

        // Only for PMICs whose flag is known, and only when the registers were read (a replayed debug
        // report has the decoded values but no raw registers).
        private static bool TryReadHighVoltageFlag(Ddr5PmicData pd, out bool highVoltage)
        {
            highVoltage = false;

            if (pd.RawRegisters == null || pd.RawRegisters.Length <= REG_LDO_SETTINGS)
                return false;

            if (pd.VendorBank != RICHTEK_VENDOR_BANK || pd.VendorCode != RICHTEK_VENDOR_CODE)
                return false;

            highVoltage = (pd.RawRegisters[REG_LDO_SETTINGS] & RICHTEK_HIGH_VOLTAGE_MASK) != 0;
            return true;
        }

        private static bool CloserTo8Bit(int measuredMv, int mv7bit, int mv8bit)
        {
            if (measuredMv <= 0 || mv7bit == mv8bit)
                return false;

            if (measuredMv > JEDEC_SWAB_MAX_MV + ADC_TOLERANCE_MV)
                return true;

            return Math.Abs(measuredMv - mv8bit) < Math.Abs(measuredMv - mv7bit);
        }

        public static void DecodeTelemetryWatts(Ddr5PmicData pd)
        {
            const double POWER_STEP_W = 0.125; // JESD301-2: 125 mW per LSB

            if (pd.TelemetryReportsPower)
            {
                // Power mode: R0x0C / R0x0E / R0x0F report power directly, 0.125 W per LSB
                pd.SwaW = pd.SwaTelemetryRaw * POWER_STEP_W;
                pd.SwbW = pd.SwbTelemetryRaw * POWER_STEP_W;
                pd.SwcW = pd.SwcTelemetryRaw * POWER_STEP_W;

                if (pd.TelemetryReportsTotalPower)
                {
                    double reportedTotalW = pd.SwaW;
                    pd.TotalW = reportedTotalW;
                    pd.SwaW = Math.Max(0.0, reportedTotalW - pd.SwbW - pd.SwcW);
                }
                else
                {
                    pd.TotalW = pd.SwaW + pd.SwbW + pd.SwcW;
                }
            }
            else
            {
                // Current mode: R0x0C / R0x0E / R0x0F report the rail current, 125 mA per LSB
                // (JESD301 / RTQ5132 Tables 28, 30, 31 - all three rails use the same step).
                const double CURRENT_STEP_A = 0.125;
                double swaCurrentA = pd.SwaTelemetryRaw * CURRENT_STEP_A;
                double swbCurrentA = pd.SwbTelemetryRaw * CURRENT_STEP_A;
                double swcCurrentA = pd.SwcTelemetryRaw * CURRENT_STEP_A;

                // Prefer ADC-measured voltage; fall back to programmed VID
                double vddV = (pd.SwaAdcMv > 0 ? pd.SwaAdcMv : pd.VddMv) / 1000.0;
                double vddqV = (pd.SwbAdcMv > 0 ? pd.SwbAdcMv : pd.VddqMv) / 1000.0;
                double vppV = (pd.SwcAdcMv > 0 ? pd.SwcAdcMv : pd.VppMv) / 1000.0;

                pd.SwaW = Math.Round(swaCurrentA * vddV / POWER_STEP_W) * POWER_STEP_W;
                pd.SwbW = Math.Round(swbCurrentA * vddqV / POWER_STEP_W) * POWER_STEP_W;
                pd.SwaW = swaCurrentA * vddV;
                pd.SwbW = swbCurrentA * vddqV;
                pd.SwcW = swcCurrentA * vppV;
                pd.TotalW = pd.SwaW + pd.SwbW + pd.SwcW;
            }
        }
    }
}

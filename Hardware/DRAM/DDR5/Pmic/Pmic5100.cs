using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using ZenStates.Core.Drivers;
using ZenStates.Core.Hardware.DRAM.DDR5.Spd;

namespace ZenStates.Core.Hardware.DRAM.DDR5.Pmic
{
    /// <summary>
    /// PMIC5100, the client (UDIMM / SODIMM) DDR5 PMIC (JESD301-2): SWA = VDD, SWB = VDDQ, SWC = VPP, plus the
    /// 1.8 V and 1.0 V LDOs. Registers 0x00~0x51 are read.
    ///
    /// Set points: JEDEC 7-bit VID in bits [7:1] of R0x21 / R0x25 / R0x27 (SWA/SWB 800 + n x 5 mV, SWC 1500 + n x 5 mV),
    /// bit 0 selects the power good threshold. Overclocking PMICs also have a high-voltage mode where the whole byte is
    /// the VID (800 + n x 5 mV, up to 2075 mV). It can't be told from the VID register, so it is taken from a vendor
    /// flag where one is known (<see cref="TryReadHighVoltageFlag"/>, see <see cref="Pmic5100Richtek"/>), otherwise
    /// from the measured rails.
    /// </summary>
    public class Pmic5100 : Ddr5Pmic
    {
        // Error log of the previous power cycle (R0x04~R0x06, kept until erased)
        protected const byte REG_ERROR_LOG_GLOBAL = 0x04;    // [7] more than one error, [6] buck OV/UV, [5] VIN_Bulk OV, [4] critical temperature
        protected const byte REG_ERROR_LOG_POWER_ON = 0x05;  // [6] SWA, [4] SWB, [3] SWC power not good, [2:0] last power status
        protected const byte REG_ERROR_LOG_RAILS = 0x06;     // UVLO [7] SWA, [5] SWB, [4] SWC; OV [3] SWA, [1] SWB, [0] SWC

        // Status (R0x08~R0x0A), telemetry (R0x0C~R0x0F)
        protected const byte REG_STATUS_0 = 0x08;
        protected const byte REG_STATUS_1 = 0x09;
        protected const byte REG_STATUS_2 = 0x0A;
        protected const byte REG_SWA_TELEMETRY = 0x0C;
        protected const byte REG_SWB_TELEMETRY = 0x0E;
        protected const byte REG_SWC_TELEMETRY = 0x0F;

        // Configuration
        protected const byte REG_POWER_MODE_CFG = 0x1A;      // [4] quiescent state, [1] total power
        protected const byte REG_VIN_BULK_OV_CFG = 0x1B;     // [6] power instead of current, [4] PWR_GOOD mask, [3] GSI, [2:0] high temp warning
        protected const byte REG_CURRENT_LIMIT = 0x20;       // SWA [7:6], SWB [3:2], SWC [1:0]
        protected const byte REG_SWA_VID = 0x21;
        protected const byte REG_SWA_THRESHOLD = 0x22;       // [5:4] OV, [3:2] UV
        protected const byte REG_SWB_VID = 0x25;
        protected const byte REG_SWB_THRESHOLD = 0x26;
        protected const byte REG_SWC_VID = 0x27;
        protected const byte REG_SWC_THRESHOLD = 0x28;
        protected const byte REG_RAIL_CONFIG_A = 0x29;       // SWA mode [7:6], frequency [5:4]
        protected const byte REG_RAIL_CONFIG_B = 0x2A;       // SWB mode [7:6], frequency [5:4], SWC mode [3:2], frequency [1:0]
        protected const byte REG_LDO_SETTINGS = 0x2B;        // [7:6] 1.8 V LDO, [2:1] 1.0 V LDO
        protected const byte REG_SHUTDOWN_TEMP = 0x2E;       // [2:0]
        protected const byte REG_PROTECTION_MODE = 0x2F;     // [2] 0 = secure mode, 1 = programmable mode
        protected const byte REG_ADC_SELECT = 0x30;          // [7] enable, [6:3] input, [1:0] update frequency
        protected const byte REG_ADC_VALUE = 0x31;
        protected const byte REG_VR_ENABLE = 0x32;           // [7] VR enable, [6] I3C Basic, [5] PWR_GOOD input/output, [4:3] PWR_GOOD control
        protected const byte REG_PMIC_TEMP = 0x33;           // [7:5] temperature, [2] VOUT_1.0V power good (0 = good)
        protected const byte REG_NVM_LDO_SETTINGS = 0x51;

        private const byte R1A_TOTAL_POWER = 0x02;
        private const byte R1A_QUIESCENT_STATE = 0x10;
        private const byte R1B_POWER_METER = 0x40;
        private const byte R1B_POWER_GOOD_MASK = 0x10;
        private const byte R1B_GSI = 0x08;
        private const byte R2F_PROGRAMMABLE_MODE = 0x04;

        // ADC inputs (R0x30 [6:3])
        private const int ADC_SWA = 0x0;
        private const int ADC_SWB = 0x2;
        private const int ADC_SWC = 0x3;
        private const int ADC_VIN_BULK = 0x5;
        private const int ADC_VOUT_18 = 0x8;
        private const int ADC_VOUT_10 = 0x9;

        // JESD301-2: wait at least 9 ms between selecting an ADC input and reading it
        private const int ADC_SETTLE_MS = 9;

        protected const int SWAB_BASE_MV = 800;
        protected const int SWC_BASE_MV = 1500;
        protected const int VID_STEP_MV = 5;
        protected const int JEDEC_SWAB_MAX_MV = SWAB_BASE_MV + 127 * VID_STEP_MV;         // 1435
        protected const int HIGH_VOLTAGE_SWAB_MAX_MV = SWAB_BASE_MV + 255 * VID_STEP_MV;  // 2075
        public const int SwcMaxMv = SWC_BASE_MV + 127 * VID_STEP_MV;                      // 2135

        // SWA/SWB ADC accuracy outside 1050~1160 mV is 3 LSB x 15 mV
        private const int ADC_TOLERANCE_MV = 45;

        private const double POWER_STEP_W = 0.125;    // power mode: 125 mW per LSB
        private const double CURRENT_STEP_A = 0.125;  // current mode: 125 mA per LSB

        public override Ddr5PmicType Type
        {
            get { return Ddr5PmicType.PMIC5100; }
        }

        protected override int RegisterCount
        {
            get { return REG_NVM_LDO_SETTINGS + 1; }
        }

        public override int MaxVddMv
        {
            get { return JEDEC_SWAB_MAX_MV; }
        }

        // ---- PMIC5100 fields -------------------------------------------------

        public bool VrEnabled;
        public bool AdcEnabled;
        public string AdcSelectedInput;
        public string AdcUpdateFrequency;

        /// <summary>R0x1B [6]: the telemetry registers report power instead of current.</summary>
        public bool TelemetryReportsPower;
        /// <summary>R0x1A [1]: the SWA telemetry register reports the total power.</summary>
        public bool TelemetryReportsTotalPower;

        /// <summary>The SWA/SWB set points use the whole-byte (high-voltage) VID encoding.</summary>
        public bool HighVoltageMode;

        /// <summary>
        /// R0x2F [2] SECURE_MODE: "Secure" (protected registers locked) or "Programmable". Earlier reports printed it
        /// as "Write Protect: CAMP input signal / Disabled".
        /// </summary>
        public string PmicMode;

        /// <summary>R0x32 [6]: "I2C" or "I3C Basic".</summary>
        public string ManagementInterface;

        /// <summary>R0x32 [5:3]: PWR_GOOD pin type and control, e.g. "output, PMIC controlled".</summary>
        public string PowerGoodControl;

        /// <summary>R0x1A [4]: entering the quiescent state is enabled.</summary>
        public bool QuiescentStateEnabled;
        /// <summary>R0x1B [3]: the GSI_n (interrupt) output is enabled.</summary>
        public bool GsiEnabled;
        /// <summary>R0x1B [4]: the PWR_GOOD output is masked (does not report faults).</summary>
        public bool PowerGoodMasked;

        /// <summary>Set points decoded as high-voltage mode VIDs (whole byte).</summary>
        public int VddMv8bit;
        public int VddqMv8bit;
        public int VppMv8bit;

        public int Vout18SettingMv;
        public int Vout10SettingMv;
        public int Vout18SettingNvmMv;
        public int Vout10SettingNvmMv;
        public bool Vout10PowerGood;

        public int SwaTelemetryRaw;
        public int SwbTelemetryRaw;
        public int SwcTelemetryRaw;

        public byte CurrentLimitRaw;
        public int SwaCurrentLimitMa;
        public int SwbCurrentLimitMa;
        public int SwcCurrentLimitMa;

        public string VddOvThreshold;
        public string VddUvThreshold;
        public string VddqOvThreshold;
        public string VddqUvThreshold;
        public string VppOvThreshold;
        public string VppUvThreshold;

        public string HighTemperatureWarningThreshold;
        public string ShutdownTemperatureThreshold;

        public string SwaMode;
        public string SwbMode;
        public string SwcMode;
        public string SwaSwitchingFrequency;
        public string SwbSwitchingFrequency;
        public string SwcSwitchingFrequency;

        public bool SwaPowerGoodFault;
        public bool SwbPowerGoodFault;
        public bool SwcPowerGoodFault;
        public bool PecError;
        public bool ParityError;

        /// <summary>R0x09 [5]: the 1.8 V LDO output is not good.</summary>
        public bool Vout18PowerGoodFault;
        /// <summary>R0x09 [3] / [1] / [0]: the rail is above its high current (or power) warning threshold.</summary>
        public bool SwaHighCurrentWarning;
        public bool SwbHighCurrentWarning;
        public bool SwcHighCurrentWarning;
        /// <summary>R0x0A [7] / [5] / [4]: output over-voltage.</summary>
        public bool SwaOverVoltage;
        public bool SwbOverVoltage;
        public bool SwcOverVoltage;

        // ---- Error log of the previous power cycle (R0x04~R0x06) -------------

        /// <summary>R0x05 [2:0]: why the previous power cycle ended, e.g. "Normal power on".</summary>
        public string LastPowerOnStatus;
        /// <summary>R0x04 [7]: more than one error was logged since the log was last erased.</summary>
        public bool ErrorLogMultipleErrors;
        /// <summary>The logged errors: R0x04 [6:4] and the per-rail flags of R0x05 / R0x06, e.g. "SWA UVLO". Empty when none.</summary>
        public List<string> ErrorLog = new List<string>();

        // ---- Decoding --------------------------------------------------------

        protected override void Decode()
        {
            base.Decode();
            byte[] r = RawRegisters;

            byte vrConfig = r[REG_VR_ENABLE];
            VrEnabled = (vrConfig & 0x80) != 0;
            ManagementInterface = (vrConfig & 0x40) != 0 ? "I3C Basic" : "I2C";
            PowerGoodControl = PowerGoodControlName(vrConfig);

            byte adcSelect = r[REG_ADC_SELECT];
            AdcEnabled = (adcSelect & 0x80) != 0;
            AdcSelectedInput = AdcInputName((adcSelect >> 3) & 0x0F);
            AdcUpdateFrequency = AdcUpdateFrequencyName(adcSelect & 0x03);

            byte vinBulkConfig = r[REG_VIN_BULK_OV_CFG];
            TelemetryReportsPower = (vinBulkConfig & R1B_POWER_METER) != 0;
            TelemetryReportsTotalPower = (r[REG_POWER_MODE_CFG] & R1A_TOTAL_POWER) != 0;
            QuiescentStateEnabled = (r[REG_POWER_MODE_CFG] & R1A_QUIESCENT_STATE) != 0;
            PowerGoodMasked = (vinBulkConfig & R1B_POWER_GOOD_MASK) != 0;
            GsiEnabled = (vinBulkConfig & R1B_GSI) != 0;
            HighTemperatureWarningThreshold = HighTempWarningName(vinBulkConfig & 0x07);

            byte temperature = r[REG_PMIC_TEMP];
            PmicTemperature = TemperatureName((temperature >> 5) & 0x07);
            Vout10PowerGood = (temperature & 0x04) == 0;

            Vout18SettingMv = Ldo18Mv(r[REG_LDO_SETTINGS]);
            Vout10SettingMv = Ldo10Mv(r[REG_LDO_SETTINGS]);
            Vout18SettingNvmMv = Ldo18Mv(r[REG_NVM_LDO_SETTINGS]);
            Vout10SettingNvmMv = Ldo10Mv(r[REG_NVM_LDO_SETTINGS]);

            DecodeVoltageSettings();

            SwaTelemetryRaw = r[REG_SWA_TELEMETRY];
            SwbTelemetryRaw = r[REG_SWB_TELEMETRY] & 0x3F;
            SwcTelemetryRaw = r[REG_SWC_TELEMETRY] & 0x3F;

            CurrentLimitRaw = r[REG_CURRENT_LIMIT];
            SwaCurrentLimitMa = DecodeSwabCurrentLimitMa((CurrentLimitRaw >> 6) & 0x03);
            SwbCurrentLimitMa = DecodeSwabCurrentLimitMa((CurrentLimitRaw >> 2) & 0x03);
            SwcCurrentLimitMa = DecodeSwcCurrentLimitMa(CurrentLimitRaw & 0x03);

            VddOvThreshold = OvThresholdName((r[REG_SWA_THRESHOLD] >> 4) & 0x03);
            VddUvThreshold = UvThresholdName((r[REG_SWA_THRESHOLD] >> 2) & 0x03);
            VddqOvThreshold = OvThresholdName((r[REG_SWB_THRESHOLD] >> 4) & 0x03);
            VddqUvThreshold = UvThresholdName((r[REG_SWB_THRESHOLD] >> 2) & 0x03);
            VppOvThreshold = OvThresholdName((r[REG_SWC_THRESHOLD] >> 4) & 0x03);
            VppUvThreshold = UvThresholdName((r[REG_SWC_THRESHOLD] >> 2) & 0x03);

            byte railA = r[REG_RAIL_CONFIG_A];
            byte railB = r[REG_RAIL_CONFIG_B];
            SwaMode = ModeName((railA >> 6) & 0x03);
            SwaSwitchingFrequency = DecodeSwitchingFrequency((railA >> 4) & 0x03);
            SwbMode = ModeName((railB >> 6) & 0x03);
            SwbSwitchingFrequency = DecodeSwitchingFrequency((railB >> 4) & 0x03);
            SwcMode = ModeName((railB >> 2) & 0x03);
            SwcSwitchingFrequency = DecodeSwitchingFrequency(railB & 0x03);
            ShutdownTemperatureThreshold = ShutdownName(r[REG_SHUTDOWN_TEMP] & 0x07);

            byte status0 = r[REG_STATUS_0];
            VinBulkOverVoltage = (status0 & 0x01) != 0;
            SwaPowerGoodFault = (status0 & 0x20) != 0;
            SwbPowerGoodFault = (status0 & 0x08) != 0;
            SwcPowerGoodFault = (status0 & 0x04) != 0;
            CriticalTemperatureShutdown = (status0 & 0x40) != 0;
            PowerGoodFault = SwaPowerGoodFault || SwbPowerGoodFault || SwcPowerGoodFault;
            byte status1 = r[REG_STATUS_1];
            HighTemperatureWarning = (status1 & 0x80) != 0;
            Vout18PowerGoodFault = (status1 & 0x20) != 0;
            SwaHighCurrentWarning = (status1 & 0x08) != 0;
            SwbHighCurrentWarning = (status1 & 0x02) != 0;
            SwcHighCurrentWarning = (status1 & 0x01) != 0;

            byte status2 = r[REG_STATUS_2];
            SwaOverVoltage = (status2 & 0x80) != 0;
            SwbOverVoltage = (status2 & 0x20) != 0;
            SwcOverVoltage = (status2 & 0x10) != 0;
            PecError = (status2 & 0x08) != 0;
            ParityError = (status2 & 0x04) != 0;

            DecodeErrorLog();
            ResolveVoltageMode();

            PmicMode = (r[REG_PROTECTION_MODE] & R2F_PROGRAMMABLE_MODE) != 0 ? "Programmable" : "Secure";
        }

        /// <summary>The error log of the previous power cycle, R0x04~R0x06.</summary>
        private void DecodeErrorLog()
        {
            byte global = RawRegisters[REG_ERROR_LOG_GLOBAL];
            byte powerOn = RawRegisters[REG_ERROR_LOG_POWER_ON];
            byte rails = RawRegisters[REG_ERROR_LOG_RAILS];

            LastPowerOnStatus = LastPowerOnStatusName(powerOn & 0x07);
            ErrorLogMultipleErrors = (global & 0x80) != 0;

            ErrorLog = new List<string>();
            if ((global & 0x40) != 0) ErrorLog.Add("buck OV/UV");
            if ((global & 0x20) != 0) ErrorLog.Add("VIN_Bulk OV");
            if ((global & 0x10) != 0) ErrorLog.Add("critical temperature");

            AddRailErrors("SWA", (powerOn & 0x40) != 0, (rails & 0x80) != 0, (rails & 0x08) != 0);
            AddRailErrors("SWB", (powerOn & 0x10) != 0, (rails & 0x20) != 0, (rails & 0x02) != 0);
            AddRailErrors("SWC", (powerOn & 0x08) != 0, (rails & 0x10) != 0, (rails & 0x01) != 0);
        }

        private void AddRailErrors(string rail, bool powerNotGood, bool underVoltageLockout, bool overVoltage)
        {
            if (powerNotGood) ErrorLog.Add(rail + " power not good");
            if (underVoltageLockout) ErrorLog.Add(rail + " UVLO");
            if (overVoltage) ErrorLog.Add(rail + " OV");
        }

        /// <summary>The set points of R0x21 / R0x25 / R0x27, as JEDEC and as high-voltage mode VIDs.</summary>
        internal void DecodeVoltageSettings()
        {
            if (RawRegisters == null || RawRegisters.Length <= REG_SWC_VID)
                return;

            byte swa = RawRegisters[REG_SWA_VID];
            byte swb = RawRegisters[REG_SWB_VID];
            byte swc = RawRegisters[REG_SWC_VID];
            VddMv = SWAB_BASE_MV + (swa >> 1) * VID_STEP_MV;
            VddqMv = SWAB_BASE_MV + (swb >> 1) * VID_STEP_MV;
            VppMv = SWC_BASE_MV + (swc >> 1) * VID_STEP_MV;
            VddMv8bit = SWAB_BASE_MV + swa * VID_STEP_MV;
            VddqMv8bit = SWAB_BASE_MV + swb * VID_STEP_MV;
            VppMv8bit = SWC_BASE_MV + swc * VID_STEP_MV;
        }

        /// <summary>
        /// Decides whether the SWA/SWB set points use the high-voltage encoding: from the vendor flag where the model
        /// knows one, otherwise from the measured VDD / VDDQ (above the JEDEC maximum it can only be high-voltage mode,
        /// else the decode the measurement is closer to). Without either, JEDEC.
        /// </summary>
        internal void ResolveVoltageMode()
        {
            if (TryReadHighVoltageFlag(out bool highVoltage))
            {
                HighVoltageMode = highVoltage;
                return;
            }

            HighVoltageMode = CloserTo8Bit(SwaAdcMv, VddMv, VddMv8bit) || CloserTo8Bit(SwbAdcMv, VddqMv, VddqMv8bit);
        }

        /// <summary>A vendor register that tells the high-voltage mode. None in JESD301-2.</summary>
        protected virtual bool TryReadHighVoltageFlag(out bool highVoltage)
        {
            highVoltage = false;
            return false;
        }

        private static bool CloserTo8Bit(int measuredMv, int mv7bit, int mv8bit)
        {
            if (measuredMv <= 0 || mv7bit == mv8bit)
                return false;

            if (measuredMv > JEDEC_SWAB_MAX_MV + ADC_TOLERANCE_MV)
                return true;

            return Math.Abs(measuredMv - mv8bit) < Math.Abs(measuredMv - mv7bit);
        }

        /// <summary>Power per rail from the telemetry registers: reported directly, or current times the rail voltage.</summary>
        internal void DecodeTelemetryWatts()
        {
            if (TelemetryReportsPower)
            {
                if (TelemetryReportsTotalPower)
                {
                    // Only the SWA register is valid, and it holds the total; SWB / SWC keep stale values
                    TotalW = SwaTelemetryRaw * POWER_STEP_W;
                    SwaW = SwbW = SwcW = 0;
                    return;
                }

                SwaW = SwaTelemetryRaw * POWER_STEP_W;
                SwbW = SwbTelemetryRaw * POWER_STEP_W;
                SwcW = SwcTelemetryRaw * POWER_STEP_W;
                TotalW = SwaW + SwbW + SwcW;
                return;
            }

            // Measured rail voltage, the set point when not measured
            double vdd = (SwaAdcMv > 0 ? SwaAdcMv : VddMv) / 1000.0;
            double vddq = (SwbAdcMv > 0 ? SwbAdcMv : VddqMv) / 1000.0;
            double vpp = (SwcAdcMv > 0 ? SwcAdcMv : VppMv) / 1000.0;

            SwaW = SwaTelemetryRaw * CURRENT_STEP_A * vdd;
            SwbW = SwbTelemetryRaw * CURRENT_STEP_A * vddq;
            SwcW = SwcTelemetryRaw * CURRENT_STEP_A * vpp;
            TotalW = SwaW + SwbW + SwcW;
        }

        // ---- Live values -----------------------------------------------------

        internal override bool RefreshNoLock(SmbusDriverBase smbus)
        {
            // Set points first: they can change at runtime and the voltage mode is decided from them and the rails
            ReadVoltageSettingsNoLock(smbus);
            bool ok = ReadAdcVoltagesNoLock(smbus);
            ok |= ReadTemperatureNoLock(smbus);
            ok |= ReadTelemetryNoLock(smbus);
            return ok;
        }

        private void ReadVoltageSettingsNoLock(SmbusDriverBase smbus)
        {
            bool changed = false;
            byte[] registers = { REG_SWA_VID, REG_SWB_VID, REG_SWC_VID, REG_LDO_SETTINGS };
            for (int i = 0; i < registers.Length; i++)
            {
                byte reg = registers[i];
                if (smbus.ReadByteDataNoLock(I2cAddress, reg, out byte value) && RawRegisters[reg] != value)
                {
                    RawRegisters[reg] = value;
                    changed = true;
                }
            }

            if (changed)
                DecodeVoltageSettings();
        }

        private bool ReadAdcNoLock(SmbusDriverBase smbus, int input, byte updateFrequency, out int mv)
        {
            mv = 0;
            if (!smbus.WriteByteDataNoLock(I2cAddress, REG_ADC_SELECT, (byte)(0x80 | (input << 3) | updateFrequency)))
                return false;

            Thread.Sleep(ADC_SETTLE_MS);

            if (!smbus.ReadByteDataNoLock(I2cAddress, REG_ADC_VALUE, out byte raw))
                return false;

            // 70 mV per LSB for VIN_Bulk, 15 mV for the outputs
            mv = input == ADC_VIN_BULK ? raw * 70 : raw * 15;
            return true;
        }

        /// <summary>Measures the rails with the ADC, restoring its input selection afterwards.</summary>
        private bool ReadAdcVoltagesNoLock(SmbusDriverBase smbus)
        {
            bool saved = smbus.ReadByteDataNoLock(I2cAddress, REG_ADC_SELECT, out byte original);
            byte frequency = saved ? (byte)(original & 0x03) : (byte)0;
            bool any = false;

            try
            {
                int mv;
                if (ReadAdcNoLock(smbus, ADC_VIN_BULK, frequency, out mv)) { VinBulkMv = mv; any = true; }
                if (ReadAdcNoLock(smbus, ADC_SWA, frequency, out mv)) { SwaAdcMv = mv; any = true; }
                if (ReadAdcNoLock(smbus, ADC_SWB, frequency, out mv)) { SwbAdcMv = mv; any = true; }
                if (ReadAdcNoLock(smbus, ADC_SWC, frequency, out mv)) { SwcAdcMv = mv; any = true; }
                if (ReadAdcNoLock(smbus, ADC_VOUT_18, frequency, out mv)) { Vout18AdcMv = mv; any = true; }
                if (ReadAdcNoLock(smbus, ADC_VOUT_10, frequency, out mv)) { Vout10AdcMv = mv; any = true; }
            }
            finally
            {
                if (saved)
                    smbus.WriteByteDataNoLock(I2cAddress, REG_ADC_SELECT, original);
            }

            ResolveVoltageMode();
            return any;
        }

        /// <summary>
        /// Temperature (R0x33 [7:5]) and the high temperature warning (R0x09 [7], set by the PMIC against the
        /// threshold in R0x1B [2:0]; compared here when the status can't be read).
        /// </summary>
        private bool ReadTemperatureNoLock(SmbusDriverBase smbus)
        {
            if (!smbus.ReadByteDataNoLock(I2cAddress, REG_PMIC_TEMP, out byte temperature))
                return false;

            int code = (temperature >> 5) & 0x07;
            PmicTemperature = TemperatureName(code);

            if (smbus.ReadByteDataNoLock(I2cAddress, REG_STATUS_1, out byte status1))
            {
                HighTemperatureWarning = (status1 & 0x80) != 0;
            }
            else if (smbus.ReadByteDataNoLock(I2cAddress, REG_VIN_BULK_OV_CFG, out byte config))
            {
                int warning = config & 0x07;
                HighTemperatureWarningThreshold = HighTempWarningName(warning);
                if (warning >= 1 && warning <= 6)
                    HighTemperatureWarning = code >= warning;
            }

            return true;
        }

        private bool ReadTelemetryNoLock(SmbusDriverBase smbus)
        {
            bool ok = false;
            if (smbus.ReadByteDataNoLock(I2cAddress, REG_SWA_TELEMETRY, out byte swa)) { SwaTelemetryRaw = swa; ok = true; }
            if (smbus.ReadByteDataNoLock(I2cAddress, REG_SWB_TELEMETRY, out byte swb)) SwbTelemetryRaw = swb & 0x3F;
            if (smbus.ReadByteDataNoLock(I2cAddress, REG_SWC_TELEMETRY, out byte swc)) SwcTelemetryRaw = swc & 0x3F;
            if (smbus.ReadByteDataNoLock(I2cAddress, REG_POWER_MODE_CFG, out byte powerMode))
                TelemetryReportsTotalPower = (powerMode & R1A_TOTAL_POWER) != 0;
            if (smbus.ReadByteDataNoLock(I2cAddress, REG_VIN_BULK_OV_CFG, out byte config))
                TelemetryReportsPower = (config & R1B_POWER_METER) != 0;

            DecodeTelemetryWatts();
            return ok;
        }

        // ---- Writing ---------------------------------------------------------

        /// <summary>
        /// Encodes VDD / VDDQ / VPP set points (mV, rounded to 5 mV) into the R0x21 / R0x25 / R0x27 values. JEDEC 7-bit
        /// VIDs, keeping bit 0 (power good threshold); a model with a high-voltage mode overrides <see cref="EncodeSwabVid"/>.
        /// </summary>
        public bool TryEncodeVoltages(int vddMv, int vddqMv, int vppMv,
            out byte vddReg, out byte vddqReg, out byte vppReg, out string error)
        {
            vddReg = vddqReg = vppReg = 0;
            error = null;

            if (RawRegisters == null || RawRegisters.Length <= REG_SWC_VID)
            {
                error = "PMIC registers are not available.";
                return false;
            }

            if (vddMv < SWAB_BASE_MV || vddMv > MaxVddMv || vddqMv < SWAB_BASE_MV || vddqMv > MaxVddMv)
            {
                error = string.Format("VDD/VDDQ must be {0}-{1} mV for this PMIC.", SWAB_BASE_MV, MaxVddMv);
                return false;
            }

            if (vppMv < SWC_BASE_MV || vppMv > SwcMaxMv)
            {
                error = string.Format("VPP must be {0}-{1} mV.", SWC_BASE_MV, SwcMaxMv);
                return false;
            }

            vddReg = EncodeSwabVid(vddMv, RawRegisters[REG_SWA_VID]);
            vddqReg = EncodeSwabVid(vddqMv, RawRegisters[REG_SWB_VID]);
            vppReg = JedecVid(vppMv, SWC_BASE_MV, RawRegisters[REG_SWC_VID]);
            return true;
        }

        /// <summary>VDD / VDDQ register value for a set point; JEDEC 7-bit VID here.</summary>
        protected virtual byte EncodeSwabVid(int mv, byte current)
        {
            return JedecVid(mv, SWAB_BASE_MV, current);
        }

        /// <summary>Whole 5 mV steps above the base, rounded to the nearest step.</summary>
        protected static int VidSteps(int mv, int baseMv)
        {
            return (mv - baseMv + VID_STEP_MV / 2) / VID_STEP_MV;
        }

        private static byte JedecVid(int mv, int baseMv, byte current)
        {
            int code = Math.Min(127, VidSteps(mv, baseMv));
            return (byte)((code << 1) | (current & 0x01));
        }

        private bool IsProgrammableModeNoLock(SmbusDriverBase smbus, out string error)
        {
            error = null;
            if (!smbus.ReadByteDataNoLock(I2cAddress, REG_PROTECTION_MODE, out byte mode))
            {
                error = "The PMIC could not be read.";
                return false;
            }

            if ((mode & R2F_PROGRAMMABLE_MODE) == 0)
            {
                error = "The PMIC is in secure mode; its settings can't be changed.";
                return false;
            }

            return true;
        }

        protected override bool WriteVoltagesNoLock(SmbusDriverBase smbus, int vddMv, int vddqMv, int vppMv, out string error)
        {
            if (!TryEncodeVoltages(vddMv, vddqMv, vppMv, out byte vdd, out byte vddq, out byte vpp, out error))
                return false;

            if (!IsProgrammableModeNoLock(smbus, out error))
                return false;

            bool ok = smbus.WriteByteDataNoLock(I2cAddress, REG_SWA_VID, vdd);
            ok &= smbus.WriteByteDataNoLock(I2cAddress, REG_SWB_VID, vddq);
            ok &= smbus.WriteByteDataNoLock(I2cAddress, REG_SWC_VID, vpp);

            // What the PMIC took, and the voltage mode that goes with it
            ReadVoltageSettingsNoLock(smbus);
            ResolveVoltageMode();

            if (!ok)
                error = "Writing the PMIC failed.";
            return ok;
        }

        /// <summary>
        /// Makes the SWA telemetry register report the total power (R0x1B [6] power, then R0x1A [1] total), or switches
        /// back to per-rail current. Takes the SMBus mutex.
        /// </summary>
        public bool SetTotalPowerMode(bool enable)
        {
            return IsValid && RunOnModuleBus(smbus => SetTotalPowerModeNoLock(smbus, enable));
        }

        private bool SetTotalPowerModeNoLock(SmbusDriverBase smbus, bool enable)
        {
            if (!IsProgrammableModeNoLock(smbus, out string _))
                return false;

            if (!smbus.ReadByteDataNoLock(I2cAddress, REG_POWER_MODE_CFG, out byte r1a) ||
                !smbus.ReadByteDataNoLock(I2cAddress, REG_VIN_BULK_OV_CFG, out byte r1b))
                return false;

            // Power reporting has to be selected before total power, or R0x1A [1] is ignored
            byte newR1a = enable ? (byte)(r1a | R1A_TOTAL_POWER) : (byte)(r1a & ~R1A_TOTAL_POWER);
            byte newR1b = enable ? (byte)(r1b | R1B_POWER_METER) : (byte)(r1b & ~R1B_POWER_METER);

            if (newR1b != r1b && !smbus.WriteByteDataNoLock(I2cAddress, REG_VIN_BULK_OV_CFG, newR1b))
                return false;

            if (newR1a != r1a && !smbus.WriteByteDataNoLock(I2cAddress, REG_POWER_MODE_CFG, newR1a))
            {
                if (newR1b != r1b)
                    smbus.WriteByteDataNoLock(I2cAddress, REG_VIN_BULK_OV_CFG, r1b);
                return false;
            }

            if (!smbus.ReadByteDataNoLock(I2cAddress, REG_POWER_MODE_CFG, out byte verifyR1a) ||
                !smbus.ReadByteDataNoLock(I2cAddress, REG_VIN_BULK_OV_CFG, out byte verifyR1b))
                return false;

            TelemetryReportsTotalPower = (verifyR1a & R1A_TOTAL_POWER) != 0;
            TelemetryReportsPower = (verifyR1b & R1B_POWER_METER) != 0;
            return verifyR1a == newR1a && verifyR1b == newR1b;
        }

        // ---- Register value names (JESD301-2) --------------------------------

        /// <summary>SWA / SWB current limit (R0x20 [7:6] / [3:2]) in mA; 0 for a code JESD301-2 reserves.</summary>
        protected virtual int DecodeSwabCurrentLimitMa(int code)
        {
            switch (code & 0x03)
            {
                case 0: return 3000;
                case 1: return 3500;
                case 2: return 4000;
                default: return 0;
            }
        }

        /// <summary>SWC current limit (R0x20 [1:0]) in mA; 0 for a code JESD301-2 reserves.</summary>
        protected virtual int DecodeSwcCurrentLimitMa(int code)
        {
            switch (code & 0x03)
            {
                case 0: return 500;
                case 1: return 1000;
                default: return 0;
            }
        }

        /// <summary>Switching frequency (R0x29 / R0x2A); JESD301-2 only defines 750 KHz.</summary>
        protected virtual string DecodeSwitchingFrequency(int code)
        {
            return (code & 0x03) == 0 ? "750 KHz" : "Vendor specific";
        }

        private static string OvThresholdName(int code)
        {
            switch (code)
            {
                case 0: return "+7.5%";
                case 1: return "+10%";
                case 2: return "+12.5%";
                default: return "Reserved";
            }
        }

        private static string UvThresholdName(int code)
        {
            switch (code)
            {
                case 0: return "-10%";
                case 1: return "-12.5%";
                default: return "Reserved";
            }
        }

        private static string TemperatureName(int code)
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

        // Same codes as the temperature in R0x33 [7:5]
        private static string HighTempWarningName(int code)
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

        private static string ShutdownName(int code)
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

        private static string ModeName(int code)
        {
            switch (code & 0x03)
            {
                case 2: return "COT; DCM";
                case 3: return "COT; Forced CCM";
                default: return "Reserved";
            }
        }

        private static string LastPowerOnStatusName(int code)
        {
            switch (code & 0x07)
            {
                case 0: return "Normal power on";
                case 2: return "Buck regulator output over or under voltage";
                case 3: return "Critical temperature";
                case 4: return "VIN_Bulk input over voltage";
                default: return string.Format("Reserved ({0})", code & 0x07);
            }
        }

        private static string PowerGoodControlName(byte vrConfig)
        {
            string io = (vrConfig & 0x20) != 0 ? "input/output" : "output";
            switch ((vrConfig >> 3) & 0x03)
            {
                case 2: return io + ", forced low";
                case 3: return io + ", floating";
                default: return io + ", PMIC controlled";
            }
        }

        private static string FormatCurrentLimit(int ma)
        {
            return ma > 0 ? ma + " mA" : "Vendor-specific";
        }

        private static int Ldo18Mv(byte reg)
        {
            return 1700 + ((reg >> 6) & 0x03) * 100;
        }

        private static int Ldo10Mv(byte reg)
        {
            return 900 + ((reg >> 1) & 0x03) * 100;
        }

        private static string AdcInputName(int code)
        {
            switch (code & 0x0F)
            {
                case ADC_SWA: return "SWA Output Voltage";
                case ADC_SWB: return "SWB Output Voltage";
                case ADC_SWC: return "SWC Output Voltage";
                case ADC_VIN_BULK: return "VIN_Bulk Input Voltage";
                case ADC_VOUT_18: return "VOUT_1.8V Output Voltage";
                case ADC_VOUT_10: return "VOUT_1.0V Output Voltage";
                default: return "Reserved";
            }
        }

        private static string AdcUpdateFrequencyName(int code)
        {
            return string.Format("{0} ms", 1 << (code & 0x03));
        }

        // ---- Output (same layout as the PMIC block of earlier debug reports) --

        protected override void AppendDetails(StringBuilder sb)
        {
            sb.AppendFormat("  VR Enabled         : {0}\n", VrEnabled ? "Yes" : "No");
            sb.AppendFormat("  PMIC Temperature   : {0}\n", PmicTemperature);
            sb.AppendFormat("  High Temp Warning  : {0}\n", HighTemperatureWarningThreshold);
            sb.AppendFormat("  Shutdown Temp      : {0}\n", ShutdownTemperatureThreshold);
            sb.AppendFormat("  High Voltage Mode  : {0}\n", HighVoltageMode ? "Enabled" : "Disabled");
            sb.AppendFormat("  PMIC Mode          : {0}\n", PmicMode);
            if (!string.IsNullOrEmpty(ManagementInterface))
                sb.AppendFormat("  Interface          : {0}\n", ManagementInterface);
            if (!string.IsNullOrEmpty(PowerGoodControl))
                sb.AppendFormat("  PWR_GOOD           : {0}\n", PowerGoodControl);

            sb.AppendLine();
            AppendSetPoint(sb, "  VDD  (DRAM core)   : ", VddMv, VddMv8bit);
            AppendSetPoint(sb, "  VDDQ (I/O)         : ", VddqMv, VddqMv8bit);
            sb.AppendFormat("  VPP  (wordline)    : {0} mV ({1:F3} V)\n", VppMv, VppMv / 1000.0);

            AppendIfSet(sb, "  VIN_Bulk (ADC)     : ", VinBulkMv);
            AppendIfSet(sb, "  SWA ADC            : ", SwaAdcMv);
            AppendIfSet(sb, "  SWB ADC            : ", SwbAdcMv);
            AppendIfSet(sb, "  SWC ADC            : ", SwcAdcMv);
            AppendIfSet(sb, "  VOUT_1.8V (ADC)    : ", Vout18AdcMv);
            AppendIfSet(sb, "  VOUT_1.0V (ADC)    : ", Vout10AdcMv);
            AppendIfSet(sb, "  VOUT_1.8V setting  : ", Vout18SettingMv);
            AppendIfSet(sb, "  VOUT_1.0V setting  : ", Vout10SettingMv);
            if (Vout18SettingNvmMv > 0 || Vout10SettingNvmMv > 0)
                sb.AppendFormat("  NVM LDO defaults   : 1.8V={0} mV, 1.0V={1} mV\n", Vout18SettingNvmMv, Vout10SettingNvmMv);
            sb.AppendFormat("  VOUT_1.0V PG       : {0}\n", Vout10PowerGood ? "Good" : "Not Good");

            sb.AppendLine();
            sb.AppendFormat("  ADC enabled        : {0}\n", AdcEnabled ? "Yes" : "No");
            sb.AppendFormat("  ADC selected input : {0}\n", AdcSelectedInput);
            sb.AppendFormat("  ADC update freq    : {0}\n", AdcUpdateFrequency);
            sb.AppendFormat("  Telemetry mode     : {0}\n", TelemetryReportsPower ? "Power" : "Current");
            sb.AppendFormat("  Total power mode   : {0}\n", TelemetryReportsTotalPower ? "Yes" : "No");
            sb.AppendFormat("  SWA telemetry raw  : 0x{0:X2}\n", SwaTelemetryRaw);
            sb.AppendFormat("  SWB telemetry raw  : 0x{0:X2}\n", SwbTelemetryRaw);
            sb.AppendFormat("  SWC telemetry raw  : 0x{0:X2}\n", SwcTelemetryRaw);

            if (TotalW > 0)
            {
                sb.AppendLine();
                if (TelemetryReportsTotalPower)
                {
                    sb.AppendFormat("  Total power        : {0:F2} W\n", TotalW);
                }
                else
                {
                    sb.AppendFormat("  SWA power          : {0:F2} W\n", SwaW);
                    sb.AppendFormat("  SWB power          : {0:F2} W\n", SwbW);
                    sb.AppendFormat("  SWC power          : {0:F2} W\n", SwcW);
                    sb.AppendFormat("  Total power        : {0:F2} W\n", TotalW);
                }
            }

            sb.AppendLine();
            sb.AppendFormat("  Current limit (raw): 0x{0:X2}\n", CurrentLimitRaw);
            sb.AppendFormat("  SWA current limit  : {0}\n", FormatCurrentLimit(SwaCurrentLimitMa));
            sb.AppendFormat("  SWB current limit  : {0}\n", FormatCurrentLimit(SwbCurrentLimitMa));
            sb.AppendFormat("  SWC current limit  : {0}\n", FormatCurrentLimit(SwcCurrentLimitMa));
            sb.AppendFormat("  SWA OV / UV        : {0} / {1}\n", VddOvThreshold, VddUvThreshold);
            sb.AppendFormat("  SWB OV / UV        : {0} / {1}\n", VddqOvThreshold, VddqUvThreshold);
            sb.AppendFormat("  SWC OV / UV        : {0} / {1}\n", VppOvThreshold, VppUvThreshold);

            sb.AppendLine();
            sb.AppendFormat("  SWA mode / freq    : {0} / {1}\n", SwaMode, SwaSwitchingFrequency);
            sb.AppendFormat("  SWB mode / freq    : {0} / {1}\n", SwbMode, SwbSwitchingFrequency);
            sb.AppendFormat("  SWC mode / freq    : {0} / {1}\n", SwcMode, SwcSwitchingFrequency);

            sb.AppendLine();
            sb.AppendFormat("  VIN_Bulk OV        : {0}\n", VinBulkOverVoltage ? "Yes" : "No");
            sb.AppendFormat("  SWA PG fault       : {0}\n", SwaPowerGoodFault ? "Yes" : "No");
            sb.AppendFormat("  SWB PG fault       : {0}\n", SwbPowerGoodFault ? "Yes" : "No");
            sb.AppendFormat("  SWC PG fault       : {0}\n", SwcPowerGoodFault ? "Yes" : "No");
            sb.AppendFormat("  High temp warn     : {0}\n", HighTemperatureWarning ? "Yes" : "No");
            sb.AppendFormat("  Temp shutdown      : {0}\n", CriticalTemperatureShutdown ? "Yes" : "No");
            sb.AppendFormat("  PEC error          : {0}\n", PecError ? "Yes" : "No");
            sb.AppendFormat("  Parity error       : {0}\n", ParityError ? "Yes" : "No");
            if (Vout18PowerGoodFault)
                sb.Append("  VOUT_1.8V PG fault : Yes\n");
            AppendRails(sb, "  High current warn  : ", SwaHighCurrentWarning, SwbHighCurrentWarning, SwcHighCurrentWarning);
            AppendRails(sb, "  Output OV          : ", SwaOverVoltage, SwbOverVoltage, SwcOverVoltage);

            if (!string.IsNullOrEmpty(LastPowerOnStatus))
            {
                sb.AppendLine();
                sb.AppendFormat("  Last power cycle   : {0}\n", LastPowerOnStatus);
                if (ErrorLog != null && ErrorLog.Count > 0)
                    sb.AppendFormat("  Error history      : {0}{1}\n", string.Join(", ", ErrorLog.ToArray()),
                        ErrorLogMultipleErrors ? " (more than one error)" : "");
            }
        }

        // "SWA, SWC" for the flagged rails; nothing when none is
        private static void AppendRails(StringBuilder sb, string label, bool swa, bool swb, bool swc)
        {
            if (!swa && !swb && !swc)
                return;

            List<string> rails = new List<string>();
            if (swa) rails.Add("SWA");
            if (swb) rails.Add("SWB");
            if (swc) rails.Add("SWC");
            sb.AppendFormat("{0}{1}\n", label, string.Join(", ", rails.ToArray()));
        }

        // In high-voltage mode the set point is the whole-byte decode, followed by the JEDEC one
        private void AppendSetPoint(StringBuilder sb, string label, int mv7bit, int mv8bit)
        {
            if (HighVoltageMode && mv7bit != mv8bit)
            {
                sb.AppendFormat("{0}{1} mV ({2:F3} V) [high-voltage mode]\n", label, mv8bit, mv8bit / 1000.0);
                sb.AppendFormat("                       ({0} mV JEDEC 7-bit VID)\n", mv7bit);
            }
            else
            {
                sb.AppendFormat("{0}{1} mV ({2:F3} V)\n", label, mv7bit, mv7bit / 1000.0);
            }
        }

        private static void AppendIfSet(StringBuilder sb, string label, int mv)
        {
            if (mv > 0)
                sb.AppendFormat("{0}{1} mV ({2:F3} V)\n", label, mv, mv / 1000.0);
        }
    }
}

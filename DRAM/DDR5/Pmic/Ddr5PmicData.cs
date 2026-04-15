using System.Text;

namespace ZenStates.Core
{
    public class Ddr5PmicData
    {
        /// <summary>Whether the PMIC was detected and readable.</summary>
        public bool IsValid;

        /// <summary>I2C address the PMIC was found at (0x48-0x4F).</summary>
        public byte I2cAddress;

        /// <summary>Corresponding SPD hub address (I2cAddress + 0x08).</summary>
        public byte SpdHubAddress;

        // Vendor identification (R0x3C:R0x3D, JEDEC JEP106)

        /// <summary>JEDEC JEP106 bank byte with parity (R0x3C).</summary>
        public byte VendorBank;

        /// <summary>JEDEC JEP106 manufacturer code with parity (R0x3D).</summary>
        public byte VendorCode;

        /// <summary>Decoded vendor name from JEP106 database.</summary>
        public string VendorName;

        /// <summary>PMIC revision from R0x3B: major [5:4]+1, minor [3:1].</summary>
        public int RevisionMajor;
        public int RevisionMinor;

        // Operating state

        /// <summary>VR Enable status (R0x32 bit[7]).</summary>
        public bool VrEnabled;

        /// <summary>Whether ADC is enabled (R0x30 bit[7]).</summary>
        public bool AdcEnabled;

        /// <summary>ADC-selected input name from R0x30 [6:3].</summary>
        public string AdcSelectedInput;

        /// <summary>Current/power update frequency from R0x30 [1:0].</summary>
        public string AdcUpdateFrequency;

        /// <summary>Whether telemetry registers report power instead of current (R0x1B [6]).</summary>
        public bool TelemetryReportsPower;

        /// <summary>Whether total power mode is selected (R0x1A [1]).</summary>
        public bool TelemetryReportsTotalPower;

        // Voltage readings (JEDEC 7-bit VID)
        //    SWA/SWB: Vout = 800 + R[7:1] × 5 mV  (range 800-1435 mV)
        //    SWC:     Vout = 1500 + R[7:1] × 5 mV  (range 1500-2135 mV)

        /// <summary>VDD (SWA, DRAM core) in millivolts — JEDEC 7-bit decode.</summary>
        public int VddMv;
        /// <summary>VDDQ (SWB, I/O) in millivolts — JEDEC 7-bit decode.</summary>
        public int VddqMv;
        /// <summary>VPP (SWC, wordline pump) in millivolts — JEDEC 7-bit decode.</summary>
        public int VppMv;

        /// <summary>
        /// VDD in millivolts — 8-bit VID decode (vendor OC extension).
        /// Used when BIOS programs voltages above JEDEC max (1435 mV).
        /// Only meaningful if different from VddMv.
        /// </summary>
        public int VddMv8bit;
        /// <summary>VDDQ 8-bit decode (see VddMv8bit).</summary>
        public int VddqMv8bit;
        /// <summary>VPP 8-bit decode — should match VppMv for standard operation.</summary>
        public int VppMv8bit;

        // ADC-measured voltages (JESD301-2 R0x30/R0x31)
        public int VinBulkMv;
        public int SwaAdcMv;
        public int SwbAdcMv;
        public int SwcAdcMv;
        public int Vout18AdcMv;
        public int Vout10AdcMv;

        // LDO programmed voltages
        public int Vout18SettingMv;
        public int Vout10SettingMv;
        public int Vout18SettingNvmMv;
        public int Vout10SettingNvmMv;
        public bool Vout10PowerGood;

        // Current / power telemetry raw registers
        public int SwaTelemetryRaw;
        public int SwbTelemetryRaw;
        public int SwcTelemetryRaw;

        // Decoded power telemetry (watts)

        /// <summary>SWA (VDD) power in watts. Zero when total power mode is active.</summary>
        public double SwaW;
        /// <summary>SWB (VDDQ) power in watts.</summary>
        public double SwbW;
        /// <summary>SWC (VPP) power in watts.</summary>
        public double SwcW;
        /// <summary>
        /// Total DRAM power in watts.
        /// In total power mode this is read directly from the SWA telemetry register;
        /// otherwise it is the sum of SwaW + SwbW + SwcW.
        /// </summary>
        public double TotalW;

        // Current limiter settings (R0x20)

        /// <summary>SWA current limit in milliamps (R0x20 [7:6]).</summary>
        public int SwaCurrentLimitMa;
        /// <summary>SWB current limit in milliamps (R0x20 [3:2]).</summary>
        public int SwbCurrentLimitMa;
        /// <summary>SWC current limit in milliamps (R0x20 [1:0]).</summary>
        public int SwcCurrentLimitMa;

        // Protection thresholds (R0x22, R0x26, R0x28)

        /// <summary>SWA over-voltage threshold percentage (R0x22 [5:4]).</summary>
        public string VddOvThreshold;
        /// <summary>SWA under-voltage lockout percentage (R0x22 [3:2]).</summary>
        public string VddUvThreshold;
        /// <summary>SWB over-voltage threshold percentage (R0x26 [5:4]).</summary>
        public string VddqOvThreshold;
        /// <summary>SWB under-voltage lockout percentage (R0x26 [3:2]).</summary>
        public string VddqUvThreshold;
        /// <summary>SWC over-voltage threshold percentage (R0x28 [5:4]).</summary>
        public string VppOvThreshold;
        /// <summary>SWC under-voltage lockout percentage (R0x28 [3:2]).</summary>
        public string VppUvThreshold;

        // PMIC temperature / thresholds
        public string PmicTemperature;
        public string ShutdownTemperatureThreshold;

        // Regulator mode / frequency
        public string SwaMode;
        public string SwbMode;
        public string SwcMode;
        public string SwaSwitchingFrequency;
        public string SwbSwitchingFrequency;
        public string SwcSwitchingFrequency;

        // Fault / status flags
        public bool VinBulkOverVoltage;
        public bool SwaPowerGoodFault;
        public bool SwbPowerGoodFault;
        public bool SwcPowerGoodFault;
        public bool HighTemperatureWarning;
        public bool CriticalTemperatureShutdown;
        public bool PecError;
        public bool ParityError;

        public byte[] RawRegisters;

        public override string ToString()
        {
            if (!IsValid)
                return "  PMIC: not detected\n";

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("  Vendor             : {0}\n", VendorName);
            sb.AppendFormat("  Revision           : {0}.{1}\n", RevisionMajor, RevisionMinor);
            sb.AppendFormat("  I2C Address        : 0x{0:X2}\n", I2cAddress);
            sb.AppendFormat("  VR Enabled         : {0}\n", VrEnabled ? "Yes" : "No");
            sb.AppendFormat("  PMIC Temperature   : {0}\n", PmicTemperature);
            sb.AppendFormat("  Shutdown Temp      : {0}\n", ShutdownTemperatureThreshold);

            sb.AppendLine();

            // VDD
            if (VddMv != VddMv8bit)
            {
                sb.AppendFormat("  VDD  (DRAM core)   : {0} mV ({1:F3} V) [OC 8-bit VID]\n", VddMv8bit, VddMv8bit / 1000.0);
                sb.AppendFormat("                       ({0} mV JEDEC 7-bit VID)\n", VddMv);
            }
            else
            {
                sb.AppendFormat("  VDD  (DRAM core)   : {0} mV ({1:F3} V)\n", VddMv, VddMv / 1000.0);
            }

            // VDDQ
            if (VddqMv != VddqMv8bit)
            {
                sb.AppendFormat("  VDDQ (I/O)         : {0} mV ({1:F3} V) [OC 8-bit VID]\n", VddqMv8bit, VddqMv8bit / 1000.0);
                sb.AppendFormat("                       ({0} mV JEDEC 7-bit VID)\n", VddqMv);
            }
            else
            {
                sb.AppendFormat("  VDDQ (I/O)         : {0} mV ({1:F3} V)\n",
                    VddqMv, VddqMv / 1000.0);
            }

            // VPP
            sb.AppendFormat("  VPP  (wordline)    : {0} mV ({1:F3} V)\n",
                VppMv, VppMv / 1000.0);

            if (VinBulkMv > 0)
                sb.AppendFormat("  VIN_Bulk (ADC)     : {0} mV ({1:F3} V)\n", VinBulkMv, VinBulkMv / 1000.0);
            if (SwaAdcMv > 0)
                sb.AppendFormat("  SWA ADC            : {0} mV ({1:F3} V)\n", SwaAdcMv, SwaAdcMv / 1000.0);
            if (SwbAdcMv > 0)
                sb.AppendFormat("  SWB ADC            : {0} mV ({1:F3} V)\n", SwbAdcMv, SwbAdcMv / 1000.0);
            if (SwcAdcMv > 0)
                sb.AppendFormat("  SWC ADC            : {0} mV ({1:F3} V)\n", SwcAdcMv, SwcAdcMv / 1000.0);
            if (Vout18AdcMv > 0)
                sb.AppendFormat("  VOUT_1.8V (ADC)    : {0} mV ({1:F3} V)\n", Vout18AdcMv, Vout18AdcMv / 1000.0);
            if (Vout10AdcMv > 0)
                sb.AppendFormat("  VOUT_1.0V (ADC)    : {0} mV ({1:F3} V)\n", Vout10AdcMv, Vout10AdcMv / 1000.0);

            if (Vout18SettingMv > 0)
                sb.AppendFormat("  VOUT_1.8V setting  : {0} mV ({1:F3} V)\n", Vout18SettingMv, Vout18SettingMv / 1000.0);
            if (Vout10SettingMv > 0)
                sb.AppendFormat("  VOUT_1.0V setting  : {0} mV ({1:F3} V)\n", Vout10SettingMv, Vout10SettingMv / 1000.0);
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
                    sb.AppendFormat("  SWB power          : {0:F2} W\n", SwbW);
                    sb.AppendFormat("  SWC power          : {0:F2} W\n", SwcW);
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
            sb.AppendFormat("  SWA current limit  : {0} mA\n", SwaCurrentLimitMa);
            sb.AppendFormat("  SWB current limit  : {0} mA\n", SwbCurrentLimitMa);
            sb.AppendFormat("  SWC current limit  : {0} mA\n", SwcCurrentLimitMa);
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

            return sb.ToString();
        }
    }
}

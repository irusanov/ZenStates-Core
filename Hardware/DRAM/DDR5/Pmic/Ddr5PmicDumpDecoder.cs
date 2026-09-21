using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ZenStates.Core.Hardware.DRAM.DDR5.Pmic
{
    /// <summary>
    /// Reverse of <see cref="Ddr5PmicData.ToString"/>: turns a textual PMIC dump - the
    /// "-- PMIC (Power Management IC) ---" block of a ZenTimings debug report - back into a
    /// <see cref="Ddr5PmicData"/> instance.
    /// <para>
    /// A dump carries decoded text rather than the raw register image, so
    /// <see cref="Ddr5PmicData.RawRegisters"/> stays null and the fields are filled straight from
    /// the printed values. Labels that are absent leave their field at its default, which keeps
    /// older reports readable.
    /// </para>
    /// </summary>
    public static partial class Ddr5PmicDecoder
    {
        /// <summary>Header that introduces the PMIC block inside a decoded SPD dump.</summary>
        public const string PmicDumpHeading = "-- PMIC (Power Management IC)";

        // "  SWA ADC            : 1545 mV (1.545 V)" -> label "SWA ADC", value "1545 mV (1.545 V)"
        private static readonly Regex DumpLabelRegex =
            new Regex(@"^[ \t]+(?<label>[^:]+?)[ \t]*:[ \t]*(?<value>.*?)[ \t]*$");

        // Continuation line of an OC rail: "                       (1170 mV JEDEC 7-bit VID)"
        private static readonly Regex JedecVidContinuationRegex =
            new Regex(@"^\(\s*(?<mv>\d+)\s*mV\s+JEDEC\s+7-bit\s+VID\s*\)$", RegexOptions.IgnoreCase);

        private static readonly Regex LeadingMillivoltsRegex = new Regex(@"^(?<mv>\d+)\s*mV", RegexOptions.IgnoreCase);
        // Watts are printed in the capturing machine's culture, so "1,13 W" as well as "1.13 W".
        private static readonly Regex LeadingWattsRegex = new Regex(@"^(?<w>\d+(?:[.,]\d+)?)\s*W", RegexOptions.IgnoreCase);
        private static readonly Regex LeadingMilliampsRegex = new Regex(@"^(?<ma>\d+)\s*mA", RegexOptions.IgnoreCase);
        private static readonly Regex HexByteRegex = new Regex(@"0x(?<hex>[0-9A-Fa-f]{1,2})");

        private static readonly Regex NvmLdoDefaultsRegex = new Regex(
            @"1\.8V\s*=\s*(?<v18>\d+)\s*mV\s*,\s*1\.0V\s*=\s*(?<v10>\d+)\s*mV", RegexOptions.IgnoreCase);

        private static readonly Regex RevisionRegex = new Regex(@"^(?<major>\d+)\.(?<minor>\d+)$");

        /// <summary>Which of the three rails the last "VDD/VDDQ/VPP" line described, for its continuation line.</summary>
        private enum DumpRail
        {
            None,
            Swa,
            Swb,
            Swc,
        }

        /// <summary>
        /// Decodes a printed PMIC dump. Returns null for null or empty input, and a
        /// <see cref="Ddr5PmicData"/> with <see cref="Ddr5PmicData.IsValid"/> false when the text
        /// carries no recognisable PMIC line (for example "PMIC: not detected").
        /// </summary>
        public static Ddr5PmicData DecodeFromDump(string dumpText)
        {
            if (string.IsNullOrEmpty(dumpText))
                return null;

            string normalized = dumpText.Replace("\r\n", "\n").Replace("\r", "\n");
            return DecodeFromDump(normalized.Split('\n'));
        }

        /// <summary>
        /// Decodes a printed PMIC dump that has already been split into lines, so a caller holding
        /// a whole report does not have to allocate the block as its own string first.
        /// </summary>
        public static Ddr5PmicData DecodeFromDump(string[] dumpLines)
        {
            if (dumpLines == null || dumpLines.Length == 0)
                return null;

            Ddr5PmicData pd = new Ddr5PmicData();
            int recognized = 0;
            DumpRail lastRail = DumpRail.None;

            for (int i = 0; i < dumpLines.Length; i++)
            {
                string line = dumpLines[i];
                if (line == null)
                    continue;

                string trimmed = line.Trim();
                if (trimmed.Length == 0)
                    continue;

                // A rail printed in OC mode is followed by its JEDEC 7-bit value on its own line.
                Match continuation = JedecVidContinuationRegex.Match(trimmed);
                if (continuation.Success)
                {
                    int jedecMv = ParseInt(continuation.Groups["mv"].Value);
                    switch (lastRail)
                    {
                        case DumpRail.Swa: pd.VddMv = jedecMv; break;
                        case DumpRail.Swb: pd.VddqMv = jedecMv; break;
                        case DumpRail.Swc: pd.VppMv = jedecMv; break;
                    }
                    continue;
                }

                Match match = DumpLabelRegex.Match(line);
                if (!match.Success)
                    continue;

                string label = match.Groups["label"].Value.Trim();
                string value = match.Groups["value"].Value.Trim();

                if (ApplyDumpLine(pd, label, value, ref lastRail))
                    recognized++;
            }

            // "  PMIC: not detected" and anything else without a single known label stays invalid,
            // so callers can tell "no PMIC in this report" from "PMIC reading all zeroes".
            pd.IsValid = recognized > 0;
            return pd;
        }

        /// <summary>Applies one "label : value" pair. Returns false for labels this decoder does not know.</summary>
        private static bool ApplyDumpLine(Ddr5PmicData pd, string label, string value, ref DumpRail lastRail)
        {
            switch (label)
            {
                // Identity and state
                case "Vendor":
                    pd.VendorName = value;
                    return true;
                case "Revision":
                    return ApplyRevision(pd, value);
                case "I2C Address":
                    {
                        byte address;
                        if (!TryParseHexByte(value, out address))
                            return false;

                        pd.I2cAddress = address;
                        pd.SpdHubAddress = unchecked((byte)(address + 0x08));
                        return true;
                    }
                case "VR Enabled":
                    pd.VrEnabled = IsYes(value);
                    return true;
                case "PMIC Temperature":
                    pd.PmicTemperature = value;
                    return true;
                case "Shutdown Temp":
                    pd.ShutdownTemperatureThreshold = value;
                    return true;
                case "High Voltage Mode":
                    pd.HighVoltageMode = value.Equals("Enabled", StringComparison.OrdinalIgnoreCase);
                    return true;
                case "Write Protect":
                    pd.WriteProtectFunctionControl = value;
                    return true;

                // Programmed rail voltages. The printed value is the 8-bit decode whenever the PMIC
                // runs in OC mode; the JEDEC 7-bit value then follows on the next line and is picked
                // up by the continuation handler, which is why both fields are seeded here.
                case "VDD  (DRAM core)":
                case "VDD (DRAM core)":
                    lastRail = DumpRail.Swa;
                    pd.VddMv8bit = ParseMillivolts(value);
                    pd.VddMv = pd.VddMv8bit;
                    return true;
                case "VDDQ (I/O)":
                    lastRail = DumpRail.Swb;
                    pd.VddqMv8bit = ParseMillivolts(value);
                    pd.VddqMv = pd.VddqMv8bit;
                    return true;
                case "VPP  (wordline)":
                case "VPP (wordline)":
                    lastRail = DumpRail.Swc;
                    pd.VppMv8bit = ParseMillivolts(value);
                    pd.VppMv = pd.VppMv8bit;
                    return true;

                // ADC-measured rails - the ones the main window shows.
                case "VIN_Bulk (ADC)":
                    pd.VinBulkMv = ParseMillivolts(value);
                    return true;
                case "SWA ADC":
                    pd.SwaAdcMv = ParseMillivolts(value);
                    return true;
                case "SWB ADC":
                    pd.SwbAdcMv = ParseMillivolts(value);
                    return true;
                case "SWC ADC":
                    pd.SwcAdcMv = ParseMillivolts(value);
                    return true;
                case "VOUT_1.8V (ADC)":
                    pd.Vout18AdcMv = ParseMillivolts(value);
                    return true;
                case "VOUT_1.0V (ADC)":
                    pd.Vout10AdcMv = ParseMillivolts(value);
                    return true;

                // LDO settings
                case "VOUT_1.8V setting":
                    pd.Vout18SettingMv = ParseMillivolts(value);
                    return true;
                case "VOUT_1.0V setting":
                    pd.Vout10SettingMv = ParseMillivolts(value);
                    return true;
                case "NVM LDO defaults":
                    {
                        Match m = NvmLdoDefaultsRegex.Match(value);
                        if (!m.Success)
                            return false;

                        pd.Vout18SettingNvmMv = ParseInt(m.Groups["v18"].Value);
                        pd.Vout10SettingNvmMv = ParseInt(m.Groups["v10"].Value);
                        return true;
                    }
                case "VOUT_1.0V PG":
                    pd.Vout10PowerGood = value.Equals("Good", StringComparison.OrdinalIgnoreCase);
                    return true;

                // ADC / telemetry configuration
                case "ADC enabled":
                    pd.AdcEnabled = IsYes(value);
                    return true;
                case "ADC selected input":
                    pd.AdcSelectedInput = value;
                    return true;
                case "ADC update freq":
                    pd.AdcUpdateFrequency = value;
                    return true;
                case "Telemetry mode":
                    pd.TelemetryReportsPower = value.Equals("Power", StringComparison.OrdinalIgnoreCase);
                    return true;
                case "Total power mode":
                    pd.TelemetryReportsTotalPower = IsYes(value);
                    return true;
                case "SWA telemetry raw":
                    pd.SwaTelemetryRaw = ParseHexInt(value);
                    return true;
                case "SWB telemetry raw":
                    pd.SwbTelemetryRaw = ParseHexInt(value);
                    return true;
                case "SWC telemetry raw":
                    pd.SwcTelemetryRaw = ParseHexInt(value);
                    return true;

                // Decoded power. Taken as printed rather than recomputed: in total power mode the
                // SWA register holds the combined total and SwaW is deliberately zero.
                case "SWA power":
                    pd.SwaW = ParseWatts(value);
                    return true;
                case "SWB power":
                    pd.SwbW = ParseWatts(value);
                    return true;
                case "SWC power":
                    pd.SwcW = ParseWatts(value);
                    return true;
                case "Total power":
                    pd.TotalW = ParseWatts(value);
                    return true;

                // Current limits and protection thresholds
                case "Current limit (raw)":
                    {
                        byte raw;
                        if (!TryParseHexByte(value, out raw))
                            return false;

                        pd.CurrentLimitRaw = raw;
                        return true;
                    }
                case "SWA current limit":
                    pd.SwaCurrentLimitMa = ParseMilliamps(value);
                    return true;
                case "SWA phase count":
                    pd.SwaPhaseCount = ParseInt(value);
                    return true;
                case "SWB current limit":
                    pd.SwbCurrentLimitMa = ParseMilliamps(value);
                    return true;
                case "SWC current limit":
                    pd.SwcCurrentLimitMa = ParseMilliamps(value);
                    return true;
                case "SWA OV / UV":
                    return SplitPair(value, out pd.VddOvThreshold, out pd.VddUvThreshold);
                case "SWB OV / UV":
                    return SplitPair(value, out pd.VddqOvThreshold, out pd.VddqUvThreshold);
                case "SWC OV / UV":
                    return SplitPair(value, out pd.VppOvThreshold, out pd.VppUvThreshold);

                // Regulator mode / switching frequency
                case "SWA mode / freq":
                    return SplitPair(value, out pd.SwaMode, out pd.SwaSwitchingFrequency);
                case "SWB mode / freq":
                    return SplitPair(value, out pd.SwbMode, out pd.SwbSwitchingFrequency);
                case "SWC mode / freq":
                    return SplitPair(value, out pd.SwcMode, out pd.SwcSwitchingFrequency);

                // Faults
                case "VIN_Bulk OV":
                    pd.VinBulkOverVoltage = IsYes(value);
                    return true;
                case "SWA PG fault":
                    pd.SwaPowerGoodFault = IsYes(value);
                    return true;
                case "SWB PG fault":
                    pd.SwbPowerGoodFault = IsYes(value);
                    return true;
                case "SWC PG fault":
                    pd.SwcPowerGoodFault = IsYes(value);
                    return true;
                case "High temp warn":
                    pd.HighTemperatureWarning = IsYes(value);
                    return true;
                case "Temp shutdown":
                    pd.CriticalTemperatureShutdown = IsYes(value);
                    return true;
                case "PEC error":
                    pd.PecError = IsYes(value);
                    return true;
                case "Parity error":
                    pd.ParityError = IsYes(value);
                    return true;

                default:
                    return false;
            }
        }

        private static bool ApplyRevision(Ddr5PmicData pd, string value)
        {
            Match m = RevisionRegex.Match(value);
            if (!m.Success)
                return false;

            pd.RevisionMajor = ParseInt(m.Groups["major"].Value);
            pd.RevisionMinor = ParseInt(m.Groups["minor"].Value);
            return true;
        }

        /// <summary>Splits "left / right" into its two halves on the first separating slash.</summary>
        private static bool SplitPair(string value, out string left, out string right)
        {
            int separator = value.IndexOf(" / ", StringComparison.Ordinal);
            if (separator < 0)
            {
                left = value;
                right = null;
                return value.Length > 0;
            }

            left = value.Substring(0, separator).Trim();
            right = value.Substring(separator + 3).Trim();
            return true;
        }

        private static bool IsYes(string value)
        {
            return value.Equals("Yes", StringComparison.OrdinalIgnoreCase);
        }

        private static int ParseMillivolts(string value)
        {
            Match m = LeadingMillivoltsRegex.Match(value);
            return m.Success ? ParseInt(m.Groups["mv"].Value) : 0;
        }

        private static int ParseMilliamps(string value)
        {
            Match m = LeadingMilliampsRegex.Match(value);
            return m.Success ? ParseInt(m.Groups["ma"].Value) : 0;
        }

        private static double ParseWatts(string value)
        {
            Match m = LeadingWattsRegex.Match(value);
            if (!m.Success)
                return 0;

            double watts;
            return double.TryParse(m.Groups["w"].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out watts)
                ? watts
                : 0;
        }

        private static int ParseHexInt(string value)
        {
            byte result;
            return TryParseHexByte(value, out result) ? result : 0;
        }

        private static bool TryParseHexByte(string value, out byte result)
        {
            result = 0;

            Match m = HexByteRegex.Match(value);
            if (!m.Success)
                return false;

            return byte.TryParse(m.Groups["hex"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);
        }

        private static int ParseInt(string value)
        {
            int result;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : 0;
        }
    }
}

using System.Text;
using static ZenStates.Core.Hardware.DRAM.DDR5.Spd.Ddr5SpdBytes;

namespace ZenStates.Core.Hardware.DRAM.DDR5.Spd
{
    /// <summary>
    /// The SPD bytes DDR5 (JESD400-5) and LPDDR5/5X (JESD406-5) share: the common module bytes 192~239 (Annex A.0) and
    /// the manufacturing information 512~554.
    /// </summary>
    internal static class Ddr5CommonDecoder
    {
        private const int SPD_MODULE_REVISION = 192;
        private const int SPD_SPD_DEVICE = 194;       // each device: 2 bytes manufacturer ID, type, revision
        private const int SPD_PMIC0_DEVICE = 198;
        private const int SPD_PMIC1_DEVICE = 202;
        private const int SPD_PMIC2_DEVICE = 206;
        private const int SPD_TS_DEVICE = 210;
        private const int SPD_MODULE_HEIGHT = 230;
        private const int SPD_MODULE_THICKNESS = 231;
        private const int SPD_RAW_CARD = 232;
        private const int SPD_DIMM_ATTRIBUTES = 233;  // [7:4] temperature range, [2] heat spreader, [3,1,0] DRAM rows
        private const int SPD_MODULE_ORGANISATION = 234;  // [6] asymmetrical, [5:3] package ranks per sub-channel - 1
        private const int SPD_BUS_WIDTH = 235;        // [7:5] sub-channels 1 << n, [4:3] extension, [2:0] width 8 << n

        private const int SPD_MOD_MFG_ID = 512;
        private const int SPD_MOD_MFG_LOCATION = 514;
        private const int SPD_MOD_MFG_YEAR = 515;
        private const int SPD_MOD_MFG_WEEK = 516;
        private const int SPD_MOD_SERIAL = 517;
        private const int SPD_MOD_PARTNO = 521;
        private const int SPD_MOD_PARTNO_LENGTH = 30;
        private const int SPD_MOD_REVISION = 551;
        private const int SPD_DRAM_MFG_ID = 552;
        private const int SPD_DRAM_STEPPING = 554;

        // The same fields in the 512-byte LPDDR4 layout of memory-down LPDDR5 (JESD21-C), 20-byte part number
        private const int MEMORY_DOWN_MANUFACTURING_SHIFT = 512 - 320;
        private const int MEMORY_DOWN_PARTNO_LENGTH = 20;
        private const int MEMORY_DOWN_MOD_REVISION = 349;
        private const int MEMORY_DOWN_DRAM_MFG_ID = 350;
        private const int MEMORY_DOWN_DRAM_STEPPING = 352;

        private static readonly string[] RawCardNames =
        {
            "A", "B", "C", "D", "E", "F", "G", "H", "J", "K", "L", "M", "N", "P", "R", "T",
            "U", "V", "W", "Y", "AA", "AB", "AC", "AD", "AE", "AF", "AG", "AH", "AJ", "AK",
        };

        public static void DecodeModuleCommon(byte[] spd, Ddr5SpdInfo info)
        {
            byte revision = B(spd, SPD_MODULE_REVISION);
            info.ModuleSpdRevision = string.Format("{0}.{1}", revision >> 4, revision & 0x0F);

            DecodeSupportDevices(spd, info);
            DecodeOrganisation(spd, info);

            int height = B(spd, SPD_MODULE_HEIGHT) & 0x1F;
            info.ModuleHeight = height == 0 ? "<= 15 mm"
                : height == 0x1F ? "> 45 mm"
                : string.Format("{0} < height <= {1} mm", 14 + height, 15 + height);

            byte thickness = B(spd, SPD_MODULE_THICKNESS);
            info.ModuleThickness = string.Format("front {0}, back {1}", Thickness(thickness & 0x0F), Thickness(thickness >> 4));

            byte rawCard = B(spd, SPD_RAW_CARD);
            int card = rawCard & 0x1F;
            info.ReferenceRawCard = card == 0x1F ? "ZZ (no JEDEC reference)"
                : string.Format("{0} rev {1}", card < RawCardNames.Length ? RawCardNames[card] : "Reserved", (rawCard >> 5) & 0x07);

            byte attributes = B(spd, SPD_DIMM_ATTRIBUTES);
            info.OperatingTemperatureRange = TemperatureRange(attributes >> 4);
            info.HeatSpreader = (attributes & 0x04) != 0;
            switch (((attributes >> 1) & 0x04) | (attributes & 0x03))
            {
                case 1: info.DramRows = 1; break;
                case 2: info.DramRows = 2; break;
                case 3: info.DramRows = 4; break;
                case 4: info.DramRows = 3; break;
                default: info.DramRows = 0; break;
            }
        }

        private static void DecodeOrganisation(byte[] spd, Ddr5SpdInfo info)
        {
            info.RanksPerChannel = ((B(spd, SPD_MODULE_ORGANISATION) >> 3) & 0x07) + 1;

            byte busWidth = B(spd, SPD_BUS_WIDTH);
            int subChannels = (busWidth >> 5) & 0x07;
            info.SubChannelsPerDimm = subChannels <= 3 ? 1 << subChannels : 0;

            int width = busWidth & 0x07;
            info.PrimaryBusWidthBits = width <= 3 ? 8 << width : 0;

            switch ((busWidth >> 3) & 0x03)
            {
                case 1: info.BusWidthExtensionBits = 4; break;
                case 2: info.BusWidthExtensionBits = 8; break;
                default: info.BusWidthExtensionBits = 0; break;
            }
        }

        private static void DecodeSupportDevices(byte[] spd, Ddr5SpdInfo info)
        {
            info.SpdDevice = ReadDevice(spd, "SPD", SPD_SPD_DEVICE);
            info.SpdDevice.TypeName = SpdDeviceName(info.SpdDevice.TypeCode);

            info.Pmic0 = ReadPmic(spd, "PMIC0", SPD_PMIC0_DEVICE);
            info.Pmic1 = ReadPmic(spd, "PMIC1", SPD_PMIC1_DEVICE);
            info.Pmic2 = ReadPmic(spd, "PMIC2", SPD_PMIC2_DEVICE);

            // The thermal sensor entry has one installed bit per sensor: [7] TS0, [6] TS1
            byte tsType = B(spd, SPD_TS_DEVICE + 2);
            info.ThermalSensor0Present = (tsType & 0x80) != 0;
            info.ThermalSensor1Present = (tsType & 0x40) != 0;
            info.ThermalSensors = ReadDevice(spd, "TS", SPD_TS_DEVICE);
            info.ThermalSensors.Installed = info.ThermalSensor0Present || info.ThermalSensor1Present;
            info.ThermalSensors.TypeName = ThermalSensorName(info.ThermalSensors.TypeCode);

            // The SPD5118 has a temperature sensor built in
            info.HasThermalSensor = info.ThermalSensors.Installed ||
                                    (info.SpdDevice.Installed && info.SpdDevice.TypeCode == 0);
        }

        private static Ddr5SpdDevice ReadPmic(byte[] spd, string role, int offset)
        {
            Ddr5SpdDevice device = ReadDevice(spd, role, offset);
            device.TypeName = device.TypeCode <= (int)Ddr5PmicType.PMIC5030
                ? ((Ddr5PmicType)device.TypeCode).ToString()
                : string.Format("Reserved (0x{0:X})", device.TypeCode);
            return device;
        }

        private static Ddr5SpdDevice ReadDevice(byte[] spd, string role, int offset)
        {
            Ddr5SpdDevice device = new Ddr5SpdDevice
            {
                Role = role,
                MfgIdBank = B(spd, offset),
                MfgIdCode = B(spd, offset + 1),
                Installed = (B(spd, offset + 2) & 0x80) != 0,
                TypeCode = B(spd, offset + 2) & 0x0F,
            };

            byte revision = B(spd, offset + 3);
            device.Revision = string.Format("{0}.{1}", Bcd((byte)(revision >> 4)), Bcd((byte)(revision & 0x0F)));

            if (device.MfgIdBank != 0 || device.MfgIdCode != 0)
                device.Manufacturer = ManufacturerMapping.Lookup(device.MfgIdBank, device.MfgIdCode);

            return device;
        }

        private static string SpdDeviceName(int code)
        {
            switch (code)
            {
                case 0: return "SPD5118";
                case 1: return "ESPD5216";
                default: return string.Format("Reserved (0x{0:X})", code);
            }
        }

        private static string ThermalSensorName(int code)
        {
            switch (code)
            {
                case 0: return "TS5111";
                case 1: return "TS5110";
                case 2: return "TS5211";
                case 3: return "TS5210";
                default: return string.Format("Reserved (0x{0:X})", code);
            }
        }

        private static string Thickness(int code)
        {
            if (code == 0)
                return "<= 1 mm";
            if (code == 0x0F)
                return "> 15 mm";
            return string.Format("{0} < thickness <= {1} mm", code, code + 1);
        }

        private static string TemperatureRange(int code)
        {
            switch (code & 0x0F)
            {
                case 0: return "A1T (-40 to +125 C)";
                case 1: return "A2T (-40 to +105 C)";
                case 2: return "A3T (-40 to +85 C)";
                case 3: return "IT (-40 to +95 C)";
                case 4: return "ST (-25 to +85 C)";
                case 5: return "ET (-25 to +105 C)";
                case 6: return "RT (0 to +45 C)";
                case 7: return "NT (0 to +85 C)";
                case 8: return "XT (0 to +95 C)";
                default: return "Reserved";
            }
        }

        /// <param name="memoryDown">The 512-byte LPDDR4 layout: the fields start at 320 and the part number is 20 bytes.</param>
        public static void DecodeManufacturing(byte[] spd, Ddr5SpdInfo info, bool memoryDown = false)
        {
            // Module ID, location, date and serial number keep their order, 192 bytes earlier
            int shift = memoryDown ? MEMORY_DOWN_MANUFACTURING_SHIFT : 0;

            info.ModuleMfgIdBank = B(spd, SPD_MOD_MFG_ID - shift);
            info.ModuleMfgIdMfr = B(spd, SPD_MOD_MFG_ID + 1 - shift);
            // Memory down has no module maker
            info.ModuleManufacturer = memoryDown && info.ModuleMfgIdBank == 0 && info.ModuleMfgIdMfr == 0
                ? "Not set"
                : ManufacturerMapping.Lookup(info.ModuleMfgIdBank, info.ModuleMfgIdMfr);
            info.ModuleMfgLocation = B(spd, SPD_MOD_MFG_LOCATION - shift);

            byte year = B(spd, SPD_MOD_MFG_YEAR - shift);
            byte week = B(spd, SPD_MOD_MFG_WEEK - shift);
            info.ModuleMfgYear = Bcd(year);
            info.ModuleMfgWeek = Bcd(week);
            info.ModuleMfgDate = year == 0 && week == 0
                ? "Not set"
                : string.Format("20{0:D2}, Week {1:D2}", info.ModuleMfgYear, info.ModuleMfgWeek);

            StringBuilder serial = new StringBuilder();
            for (int i = 0; i < 4; i++)
                serial.AppendFormat("{0:X2}", B(spd, SPD_MOD_SERIAL - shift + i));
            info.ModuleSerialNumber = serial.ToString();

            info.ModulePartNumber = memoryDown
                ? Ascii(spd, SPD_MOD_PARTNO - shift, MEMORY_DOWN_PARTNO_LENGTH)
                : Ascii(spd, SPD_MOD_PARTNO, SPD_MOD_PARTNO_LENGTH);
            info.ModuleRevisionCode = B(spd, memoryDown ? MEMORY_DOWN_MOD_REVISION : SPD_MOD_REVISION);

            int dramId = memoryDown ? MEMORY_DOWN_DRAM_MFG_ID : SPD_DRAM_MFG_ID;
            info.DramMfgIdBank = B(spd, dramId);
            info.DramMfgIdMfr = B(spd, dramId + 1);
            info.DramManufacturer = ManufacturerMapping.Lookup(info.DramMfgIdBank, info.DramMfgIdMfr);
            info.DramStepping = B(spd, memoryDown ? MEMORY_DOWN_DRAM_STEPPING : SPD_DRAM_STEPPING);
        }
    }
}

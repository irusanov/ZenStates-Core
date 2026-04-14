using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ZenStates.Core.Drivers;
using static ZenStates.Core.JedecPmicRegisters;

namespace ZenStates.Core
{
    internal static class Ddr5PmicReader
    {
        // SMBus helpers
        private static bool ReadRegNoLock(SmbusDriverBase smbus, byte addr, byte reg, out byte val)
        {
            return smbus.ReadByteDataNoLock(addr, reg, out val);
        }

        private static bool WriteRegNoLock(SmbusDriverBase smbus, byte addr, byte reg, byte val)
        {
            return smbus.WriteByteDataNoLock(addr, reg, val);
        }

        public static void ReadAdcVoltage(SmbusDriverBase smbus, byte pmicAddr, byte selectCode, out int mv)
        {
            if (Mutexes.WaitSmbus(5000))
            {
                try
                {
                    if (!ReadAdcVoltageNoLock(smbus, pmicAddr, selectCode, out mv))
                        throw new Exception("Failed to read ADC voltage.");
                }
                finally
                {
                    Mutexes.ReleaseSmbus();
                }
            }
            else
            {
                throw new TimeoutException("Timeout waiting for SMBus mutex.");
            }
        }

        private static bool ReadAdcVoltageNoLock(SmbusDriverBase smbus, byte pmicAddr, byte selectCode, out int mv)
        {
            mv = 0;

            byte reg30 = (byte)(0x80 | ((selectCode & 0x0F) << 3));
            byte raw;

            if (!WriteRegNoLock(smbus, pmicAddr, REG_TELEMETRY_SELECT, reg30))
                return false;

            // JESD301-2: The host shall wait minimum of 9 ms delay after the input selection for ADC readout and the actual readout from Table 137, "Register 0x31" to get the latest reading
            Thread.Sleep(9);

            // First read may still be previous/stale sample after mux switch.
            //if (!ReadRegNoLock(smbus, pmicAddr, REG_TELEMETRY_VALUE, out raw))
            //    return false;

            //Utils.DelayMicroseconds(200);

            if (!ReadRegNoLock(smbus, pmicAddr, REG_TELEMETRY_VALUE, out raw))
                return false;

            mv = Ddr5PmicDecoder.DecodeAdcMv(selectCode, raw);
            return true;
        }

        internal static void ReadAllAdcVoltagesNoLock(SmbusDriverBase smbus, byte pmicAddr, Ddr5PmicData pd)
        {

            ReadRegNoLock(smbus, pmicAddr, REG_TELEMETRY_SELECT, out byte originalReg30);

            try
            {
                ReadAdcVoltageNoLock(smbus, pmicAddr, 0x5, out pd.VinBulkMv);
                ReadAdcVoltageNoLock(smbus, pmicAddr, 0x0, out pd.SwaAdcMv);
                ReadAdcVoltageNoLock(smbus, pmicAddr, 0x2, out pd.SwbAdcMv);
                ReadAdcVoltageNoLock(smbus, pmicAddr, 0x3, out pd.SwcAdcMv);
                ReadAdcVoltageNoLock(smbus, pmicAddr, 0x8, out pd.Vout18AdcMv);
                ReadAdcVoltageNoLock(smbus, pmicAddr, 0x9, out pd.Vout10AdcMv);
            }
            finally
            {
                WriteRegNoLock(smbus, pmicAddr, REG_TELEMETRY_SELECT, originalReg30);
            }
        }

        // Detection

        /// <summary>Derive PMIC I2C address from SPD hub address.</summary>
        public static byte CalculatePmicAddrFromSpd(byte spdAddr)
        {
            return (byte)(spdAddr - SPD_PMIC_OFFSET);
        }

        /// <summary>Check if a PMIC responds at the given I2C address.</summary>
        internal static bool DetectNoLock(SmbusDriverBase smbus, byte pmicAddr)
        {
            try
            {
                if (!ReadRegNoLock(smbus, pmicAddr, REG_VENDOR_BANK, out byte bank)) return false;
                if (!ReadRegNoLock(smbus, pmicAddr, REG_VENDOR_CODE, out byte code)) return false;
                return code != 0x00 && code != 0xFF && bank != 0xFF;
            }
            catch
            {
                return false;
            }
        }


        internal static Ddr5PmicData ReadPmicNoLock(SmbusDriverBase smbus, byte pmicAddr)
        {
            byte[] rawRegisters = new byte[0x52];

            for (int i = 0; i < rawRegisters.Length; i++)
            {
                if (ReadRegNoLock(smbus, pmicAddr, (byte)i, out byte val))
                    rawRegisters[i] = val;
                else
                    rawRegisters[i] = 0xFF;
            }

            Ddr5PmicData pd = Ddr5PmicDecoder.Decode(pmicAddr, rawRegisters);
            ReadAllAdcVoltagesNoLock(smbus, pmicAddr, pd);

            return pd;
        }

        /// <summary>
        /// Read single Pmic
        /// </summary>
        /// <param name="smbus"></param>
        /// <param name="pmicAddr"></param>
        /// <returns></returns>
        public static Ddr5PmicData ReadPmic(SmbusDriverBase smbus, byte pmicAddr)
        {
            if (!Mutexes.WaitSmbus(5000))
            {
                Debug.WriteLine("Timeout waiting for SMBus mutex to read PMIC data.");
                return new Ddr5PmicData();
            }

            try
            {
                return ReadPmicNoLock(smbus, pmicAddr);
            }
            finally
            {
                Mutexes.ReleaseSmbus();
            }
        }

        /// <summary>Read PMIC data for all detected DIMMs by scanning 0x48-0x4F.</summary>
        internal static Dictionary<byte, Ddr5PmicData> ReadAllPmicsNoLock(SmbusDriverBase smbus)
        {
            Dictionary<byte, Ddr5PmicData> results = new Dictionary<byte, Ddr5PmicData>();

            for (byte addr = PMIC_ADDR_BASE; addr <= PMIC_ADDR_LAST; addr++)
            {
                if (DetectNoLock(smbus, addr))
                    results.Add(addr, ReadPmicNoLock(smbus, addr));
            }

            return results;
        }

        public static Dictionary<byte, Ddr5PmicData> ReadAllPmics(SmbusDriverBase smbus)
        {
            if (!Mutexes.WaitSmbus(5000))
            {
                Debug.WriteLine("Timeout waiting for SMBus mutex to read PMIC data.");
                return new Dictionary<byte, Ddr5PmicData>();
            }
            try
            {
                return ReadAllPmicsNoLock(smbus);
            }
            finally
            {
                Mutexes.ReleaseSmbus();
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using ZenStates.Core.Drivers;
using ZenStates.Core.Hardware.DRAM.DDR5.Hub;
using ZenStates.Core.Hardware.DRAM.DDR5.Thermal;
using ZenStates.Core.OHWM;

namespace ZenStates.Core.Hardware.DRAM.DDR5.Spd
{
    /// <summary>
    /// Reads the SPD of DDR5 and LPDDR5/5X modules from their SPD5118 hubs over SMBus, together with the hub registers
    /// and its temperature sensor. The bus access itself is in <see cref="Spd5118Hub"/>; every hub is put back on page 0
    /// and the previously selected SMBus port is restored afterwards.
    /// </summary>
    public static class Ddr5SpdReader
    {
        /// <summary>True when SMBIOS reports DDR5 or LPDDR5 memory; the hubs are only accessed then.</summary>
        internal static bool IsDdr5MemoryInstalled()
        {
            try
            {
                MemoryDevice[] devices = SMBiosSingleton.Instance.MemoryDevices;
                if (devices == null)
                    return false;

                for (int i = 0; i < devices.Length; i++)
                {
                    if (devices[i] == null || devices[i].Size == 0)
                        continue;

                    MemType type = MemoryConfig.SMBiosDramTypeToMemType(devices[i].Type);
                    return type == MemType.DDR5 || type == MemType.LPDDR5;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("IsDdr5MemoryInstalled: {0}", ex.Message));
            }

            return false;
        }

        /// <summary>
        /// Reads the SPD image of one hub: all 1024 bytes, or only the ranges a partial decode uses. Null when the base
        /// configuration (bytes 0~127) could not be read. The hub is left on page 0.
        /// </summary>
        private static byte[] ReadImageNoLock(SmbusDriverBase smbus, byte addr7, bool full, out bool complete)
        {
            byte[] image = new byte[Ddr5SpdDecoder.SPD_SIZE];
            complete = false;

            try
            {
                if (!Spd5118Hub.ReadNvmNoLock(smbus, addr7, image, 0, Ddr5SpdDecoder.BASE_LENGTH))
                    return null;

                if (full)
                {
                    complete = Spd5118Hub.ReadNvmNoLock(smbus, addr7, image, Ddr5SpdDecoder.BASE_LENGTH,
                        Ddr5SpdDecoder.SPD_SIZE - Ddr5SpdDecoder.BASE_LENGTH);
                }
                else
                {
                    bool common = Spd5118Hub.ReadNvmNoLock(smbus, addr7, image, Ddr5SpdDecoder.COMMON_FIRST, Ddr5SpdDecoder.COMMON_LENGTH);
                    bool manufacturing = Spd5118Hub.ReadNvmNoLock(smbus, addr7, image,
                        Ddr5SpdDecoder.MANUFACTURING_FIRST, Ddr5SpdDecoder.MANUFACTURING_LENGTH);
                    complete = common && manufacturing;
                }

                return image;
            }
            finally
            {
                Spd5118Hub.RestorePage0NoLock(smbus, addr7);
            }
        }

        /// <summary>Reads and decodes the SPD of one hub, with the hub registers and thermal sensor. Null when not readable.</summary>
        internal static Ddr5SpdInfo ReadModuleNoLock(SmbusDriverBase smbus, byte addr7, bool full)
        {
            Spd5118HubInfo hub = Spd5118Hub.ReadInfoNoLock(smbus, addr7);
            if (hub == null)
                return null;

            byte[] image = ReadImageNoLock(smbus, addr7, full, out bool complete);
            if (image == null)
                return null;

            if (full && !complete)
                Debug.WriteLine(string.Format("DDR5 SPD 0x{0:X2}: not all bytes could be read, the SPD is partial.", addr7));
            else if (!complete)
                Debug.WriteLine(string.Format("DDR5 SPD 0x{0:X2}: the common or manufacturing bytes could not be read.", addr7));

            Ddr5SpdInfo info = Ddr5SpdDecoder.Decode(image, !full || !complete);
            if (!info.IsValid)
                return null;

            info.HubInfo = hub;
            if (hub.TempSensorSupported)
                info.ThermalData = Ddr5ThermalSensor.ReadAllRegsNoLock(smbus, addr7);

            return info;
        }

        /// <summary>
        /// Reads and decodes the SPD of every module, keyed by hub address in module order. Restores the previously
        /// selected port.
        /// </summary>
        /// <param name="full">All 1024 bytes, or only the base configuration, common and manufacturing bytes.</param>
        internal static Dictionary<byte, Ddr5SpdInfo> ReadAllNoLock(SmbusDriverBase smbus, bool full)
        {
            Dictionary<byte, Ddr5SpdInfo> result = new Dictionary<byte, Ddr5SpdInfo>();

            if (smbus == null || !smbus.ChangePortNoLock(-1, out int savedPort))
                return result;

            try
            {
                List<byte> hubs = Spd5118Hub.ScanNoLock(smbus);
                for (int i = 0; i < hubs.Count; i++)
                {
                    Ddr5SpdInfo info = ReadModuleNoLock(smbus, hubs[i], full);
                    if (info != null)
                        result[hubs[i]] = info;
                }
            }
            finally
            {
                if (savedPort >= 0)
                    smbus.ChangePortNoLock(savedPort);
            }

            return result;
        }

        private static Dictionary<byte, Ddr5SpdInfo> ReadAllLocked(bool full)
        {
            Dictionary<byte, Ddr5SpdInfo> empty = new Dictionary<byte, Ddr5SpdInfo>();

            if (!IsDdr5MemoryInstalled())
                return empty;

            if (!Mutexes.WaitSmbus(5000))
            {
                Debug.WriteLine("DDR5 SPD: timeout waiting for the SMBus mutex.");
                return empty;
            }

            try
            {
                return ReadAllNoLock(SmbusProvider.Instance, full);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("DDR5 SPD: {0}", ex.Message));
                return empty;
            }
            finally
            {
                Mutexes.ReleaseSmbus();
            }
        }

        /// <summary>Reads and decodes the full SPD of all DDR5 / LPDDR5 modules, keyed by hub address in module order.</summary>
        public static Dictionary<byte, Ddr5SpdInfo> ReadAll()
        {
            return ReadAllLocked(true);
        }

        /// <summary>
        /// Reads only what is needed at startup (identity, organisation, JEDEC timings, manufacturer), a fraction of the
        /// SMBus traffic of <see cref="ReadAll"/>. The entries have <see cref="Ddr5SpdInfo.IsPartial"/> set.
        /// </summary>
        public static Dictionary<byte, Ddr5SpdInfo> ReadInitInfoAll()
        {
            return ReadAllLocked(false);
        }

        /// <summary>Writes the 1024-byte SPD of the module at <paramref name="addr7"/> to a file.</summary>
        public static bool DumpToFile(byte addr7, string filePath)
        {
            try
            {
                Dictionary<byte, Ddr5SpdInfo> all = ReadAll();
                if (!all.TryGetValue(addr7, out Ddr5SpdInfo info) || info == null)
                    throw new InvalidOperationException(string.Format("Could not read SPD from DIMM at address 0x{0:X2}.", addr7));
                if (info.IsPartial)
                    throw new InvalidOperationException(string.Format("Could not read the whole SPD of DIMM at address 0x{0:X2}.", addr7));

                string dir = System.IO.Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                System.IO.File.WriteAllBytes(filePath, info.RawSpd);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error dumping DDR5 SPD to file: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>Writes the SPD of every module to DIMM_0xNN.bin files in <paramref name="outputDirectory"/>.</summary>
        public static bool DumpToFiles(string outputDirectory)
        {
            try
            {
                Dictionary<byte, Ddr5SpdInfo> all = ReadAll();
                if (all.Count == 0)
                    throw new InvalidOperationException("No DDR5 modules found.");

                if (!System.IO.Directory.Exists(outputDirectory))
                    System.IO.Directory.CreateDirectory(outputDirectory);

                bool allOk = true;
                foreach (KeyValuePair<byte, Ddr5SpdInfo> entry in all)
                {
                    if (entry.Value.IsPartial)
                    {
                        allOk = false;
                        continue;
                    }

                    string path = System.IO.Path.Combine(outputDirectory, string.Format("DIMM_0x{0:X2}.bin", entry.Key));
                    System.IO.File.WriteAllBytes(path, entry.Value.RawSpd);
                }

                return allOk;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error dumping DDR5 SPD to files: {0}", ex.Message));
                return false;
            }
        }
    }
}

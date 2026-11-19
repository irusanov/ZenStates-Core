using System;
using System.Collections.Generic;
using System.Globalization;

namespace ZenStates.Core.Hardware.Apob
{
    /// <summary>
    /// Locates the APOB through physical memory reads (the InpOut driver), the same way the AMDAPOB PawnIO module
    /// does: the address recorded in the firmware image first, then the known addresses. Used when the module is not
    /// loaded, for example on a system with Secure Boot where the unsigned module cannot be loaded.
    /// </summary>
    internal static class ApobInpOutLocator
    {
        public const int SOURCE_FIRMWARE = 1;
        public const int SOURCE_KNOWN_ADDRESS = 2;

        private const uint APOB_SIGNATURE = 0x424F5041; // "APOB"

        private const uint APOB_HDR_VERSION = 0x04;
        private const uint APOB_HDR_TABLE_SIZE = 0x08;
        private const uint APOB_HDR_HEADER_SIZE = 0x0C;
        private const uint APOB_HDR_MIN_SIZE = 0x10;
        private const uint APOB_ENTRY_SIZE = 0x0C;
        private const uint APOB_ENTRY_MIN_SIZE = 0x10;

        // Sanity limit for the table size read from the header
        private const uint APOB_MAX_TABLE_SIZE = 0x100000; // 1 MiB

        private const uint PAGE_SIZE = 0x1000;

        /// <summary>TOP_MEM: below it is DRAM, from there to 4 GiB is MMIO.</summary>
        public const uint MSR_TOP_MEM = 0xC001001A;
        private const ulong TOP_MEM_ADDR_MASK = 0x0000FFFFFF800000;
        // Limit used when TOP_MEM cannot be read. The signed AMDFamily17 module does not allow
        // reading this MSR, so the InpOut path normally runs with this value. DRAM below the
        // PCI hole reaches past 2 GiB on any supported system, and the header checks still
        // guard each probe. The AMDAPOB module reads TOP_MEM itself and keeps 256 MiB.
        private const ulong TOP_MEM_FALLBACK = 0x80000000; // 2 GiB

        // The windows of the module: ROM2 below 4 GiB, ROM3 at its default base
        private static readonly ApobLocator.FlashWindow[] Windows =
        {
            new ApobLocator.FlashWindow("ROM2", ApobLocator.ROM2_DEFAULT_BASE, ApobLocator.ROM2_MAX_SIZE),
            new ApobLocator.FlashWindow("ROM3", ApobLocator.ROM3_DEFAULT_BASE, ApobLocator.ROM3_MAX_SIZE),
        };

        // The known addresses of the module, in the order they are probed
        private static readonly uint[] KnownAddresses =
        {
            0x0A200000, // AM4 and AM5 desktop firmware
            0x09F00000, // Mobile and Phoenix firmware; openSIL Phoenix Mayan CRB
            0x04000000, // openSIL Genoa Onyx CRB
            0x76B00000, // openSIL Turin Purico CRB
            0x75BA0000, // openSIL default (Kconfig.Plat)
        };

        internal sealed class Result
        {
            public uint Address;
            public uint Version;
            public uint TableSize;
            public uint HeaderSize;
            public int Source;
            public int FirmwareAddresses;
            public ulong EfsAddress;

            public string Describe()
            {
                string from = Source == SOURCE_FIRMWARE ? "firmware image" : "known address list";
                return string.Format(CultureInfo.InvariantCulture, "InpOut, {0}; firmware addresses: {1}, EFS: 0x{2:X}",
                    from, FirmwareAddresses, EfsAddress);
            }
        }

        /// <summary>The TOP_MEM limit from the raw MSR value; 0 when it is not known.</summary>
        public static ulong TopMemFromMsr(ulong msr)
        {
            return msr & TOP_MEM_ADDR_MASK;
        }

        // Data fabric (PCI 0:18h), read through ECAM. Sources: illumos amdzen/df.h,
        // coreboot soc/amd/*/data_fabric.h, Linux drivers/ras/amd/atl.
        private const uint DF_DEVICE = 0x18;
        private const uint AMD_VENDOR_ID = 0x1022;
        // DF::FabricBlockInstanceCount (D18F0x040): bits 27:24 hold the major revision from DF4 on, 0 before
        private const uint DF_FBICNT = 0x040;
        // DF::DramHoleControl: D18F0x104 on DF2 to DF3.5 (Zen 1 to Zen 3), D18F7x104 on DF4 and later
        // (Zen 4 and Zen 5). Bit 0 is the valid flag and bits 31:24 the base of the MMIO hole below 4 GiB,
        // which the firmware sets to the same address as TOP_MEM.
        private const uint DF_DRAM_HOLE_CTL = 0x104;
        private const uint DRAM_HOLE_VALID = 0x1;
        private const uint DRAM_HOLE_BASE_MASK = 0xFF000000;
        // ACPI MCFG: 36-byte table header and 8 reserved bytes, then 16-byte allocation entries
        // (base address, PCI segment, start bus, end bus)
        private const int MCFG_ENTRIES_OFFSET = 44;
        private const int MCFG_ENTRY_SIZE = 16;
        private const ulong ECAM_ALIGNMENT_MASK = 0xFFFFF; // 1 MiB
        // The lowest TOP_MEM taken from the data fabric; anything below is treated as a bad read
        private const ulong TOP_MEM_MIN = 0x10000000; // 256 MiB

        /// <summary>
        /// TOP_MEM from the data fabric's DRAM hole control register, read through the ECAM range
        /// from the ACPI MCFG table. 0 when it cannot be read.
        /// </summary>
        /// <param name="mcfg">The ACPI MCFG table, with its header; null when it is not available.</param>
        public static ulong TopMemFromDataFabric(ApobLocator.PhysicalDwordReader read, byte[] mcfg, List<string> trace)
        {
            ulong ecam = EcamBaseFromMcfg(mcfg, trace);
            if (ecam == 0)
                return 0;

            ulong df = ecam + (DF_DEVICE << 15);
            if (!read(df, out uint id) || (id & 0xFFFF) != AMD_VENDOR_ID)
            {
                Log(trace, "DF: D18F0x000 = 0x{0:X8}, not an AMD device", id);
                return 0;
            }

            if (!read(df + DF_FBICNT, out uint fbicnt))
            {
                Log(trace, "DF: D18F0x040 not readable");
                return 0;
            }

            uint major = (fbicnt >> 24) & 0xF;
            uint function = major >= 4 ? 7u : 0u;
            if (!read(df + (function << 12) + DF_DRAM_HOLE_CTL, out uint hole))
            {
                Log(trace, "DF: D18F{0}x104 not readable", function);
                return 0;
            }

            ulong holeBase = hole & DRAM_HOLE_BASE_MASK;
            Log(trace, "DF: major revision {0}, D18F{1}x104 = 0x{2:X8}", major, function, hole);

            if ((hole & DRAM_HOLE_VALID) == 0 || holeBase < TOP_MEM_MIN)
            {
                Log(trace, "DF: no valid DRAM hole");
                return 0;
            }

            return holeBase;
        }

        /// <summary>The ECAM base of PCI segment 0, bus 0 from the ACPI MCFG table; 0 when not found.</summary>
        private static ulong EcamBaseFromMcfg(byte[] mcfg, List<string> trace)
        {
            if (mcfg == null || mcfg.Length < MCFG_ENTRIES_OFFSET + MCFG_ENTRY_SIZE
                || mcfg[0] != (byte)'M' || mcfg[1] != (byte)'C' || mcfg[2] != (byte)'F' || mcfg[3] != (byte)'G')
            {
                Log(trace, "DF: no ACPI MCFG table");
                return 0;
            }

            int length = (int)Math.Min(BitConverter.ToUInt32(mcfg, 4), (uint)mcfg.Length);
            for (int offset = MCFG_ENTRIES_OFFSET; offset + MCFG_ENTRY_SIZE <= length; offset += MCFG_ENTRY_SIZE)
            {
                ulong address = BitConverter.ToUInt64(mcfg, offset);
                ushort segment = BitConverter.ToUInt16(mcfg, offset + 8);
                byte startBus = mcfg[offset + 10];

                // The base address is that of bus 0 even when the range starts at a later bus
                if (segment == 0 && startBus == 0 && address != 0 && (address & ECAM_ALIGNMENT_MASK) == 0)
                {
                    Log(trace, "DF: ECAM base 0x{0:X} (MCFG)", address);
                    return address;
                }
            }

            Log(trace, "DF: MCFG has no range for segment 0, bus 0");
            return 0;
        }

        /// <summary>Locates the table. Null when it is not found.</summary>
        /// <param name="topMem">TOP_MEM (<see cref="TopMemFromMsr"/>); only DRAM below it is probed. 0 uses the fallback.</param>
        public static Result Locate(ApobLocator.PhysicalDwordReader read, ulong topMem, List<string> trace)
        {
            if (topMem == 0)
            {
                topMem = TOP_MEM_FALLBACK;
                Log(trace, "TOP_MEM not readable, using fallback 0x{0:X}", topMem);
            }
            else
            {
                Log(trace, "TOP_MEM 0x{0:X}", topMem);
            }

            List<uint> firmware = ApobLocator.FindApobDestinations(read, Windows, out ulong efs, trace);
            var result = new Result { FirmwareAddresses = firmware.Count, EfsAddress = efs };

            foreach (uint address in firmware)
            {
                Log(trace, "address in the firmware image: 0x{0:X8}", address);
                if (Probe(read, address, topMem, result, trace))
                {
                    result.Source = SOURCE_FIRMWARE;
                    return result;
                }
            }

            foreach (uint address in KnownAddresses)
            {
                if (Probe(read, address, topMem, result, trace))
                {
                    result.Source = SOURCE_KNOWN_ADDRESS;
                    return result;
                }
            }

            Log(trace, "not found");
            return null;
        }

        /// <summary>Reads and checks the header, and the header of the first entry, at <paramref name="address"/>.</summary>
        public static bool ReadHeader(ApobLocator.PhysicalDwordReader read, ulong address, out uint version, out uint tableSize,
            out uint headerSize)
        {
            version = tableSize = headerSize = 0;

            if (!Read(read, address, out uint signature) || signature != APOB_SIGNATURE)
                return false;

            if (!Read(read, address + APOB_HDR_VERSION, out version) ||
                !Read(read, address + APOB_HDR_TABLE_SIZE, out tableSize) ||
                !Read(read, address + APOB_HDR_HEADER_SIZE, out headerSize))
                return false;

            // The header and the header of the first entry are within the first page
            if (headerSize < APOB_HDR_MIN_SIZE || headerSize > PAGE_SIZE - APOB_ENTRY_MIN_SIZE || (headerSize & 0x3) != 0)
                return false;

            if (tableSize < headerSize + APOB_ENTRY_MIN_SIZE || tableSize > APOB_MAX_TABLE_SIZE)
                return false;

            // The first entry starts right after the header and has to fit in the table
            if (!Read(read, address + headerSize + APOB_ENTRY_SIZE, out uint entrySize))
                return false;

            return entrySize >= APOB_ENTRY_MIN_SIZE && entrySize <= tableSize - headerSize;
        }

        private static bool Probe(ApobLocator.PhysicalDwordReader read, uint address, ulong topMem, Result result, List<string> trace)
        {
            // Only DRAM is probed
            if ((ulong)address + PAGE_SIZE > topMem)
            {
                Log(trace, "0x{0:X8}: above TOP_MEM", address);
                return false;
            }

            if (!ReadHeader(read, address, out uint version, out uint tableSize, out uint headerSize))
            {
                Log(trace, "0x{0:X8}: no APOB header", address);
                return false;
            }

            // The whole table has to be in DRAM
            if ((ulong)address + tableSize > topMem)
            {
                Log(trace, "0x{0:X8}: table of 0x{1:X} bytes ends above TOP_MEM", address, tableSize);
                return false;
            }

            Log(trace, "found at 0x{0:X8}, version 0x{1:X}, size 0x{2:X}, header size 0x{3:X}", address, version, tableSize, headerSize);
            result.Address = address;
            result.Version = version;
            result.TableSize = tableSize;
            result.HeaderSize = headerSize;
            return true;
        }

        private static bool Read(ApobLocator.PhysicalDwordReader read, ulong address, out uint value)
        {
            value = 0;
            try
            {
                return read(address, out value);
            }
            catch
            {
                return false;
            }
        }

        private static void Log(List<string> trace, string format, params object[] args)
        {
            if (trace != null && trace.Count < 250)
                trace.Add(string.Format(CultureInfo.InvariantCulture, format, args));
        }
    }
}

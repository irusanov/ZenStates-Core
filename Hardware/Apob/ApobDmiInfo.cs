using System.Collections.Generic;

namespace ZenStates.Core.Hardware.Apob
{
    /// <summary>A physical DIMM record of <see cref="ApobDmiInfo"/> (SMBIOS type 17 source data).</summary>
    public sealed class ApobDmiPhysicalDimm
    {
        public int Socket { get; internal set; }
        public int Channel { get; internal set; }
        public int Dimm { get; internal set; }
        public bool Present { get; internal set; }
        public byte SpdAddress { get; internal set; }
        public ushort Handle { get; internal set; }

        /// <summary>Configured memory speed as written to SMBIOS type 17, MEMCLK in MHz on DDR5.</summary>
        public ushort ConfiguredSpeed { get; internal set; }

        /// <summary>Configured voltage in mV.</summary>
        public ushort ConfiguredVoltage { get; internal set; }

        public string SlotName
        {
            get { return string.Format("{0}{1}", (char)('A' + Channel), Dimm + 1); }
        }
    }

    /// <summary>A logical DIMM record of <see cref="ApobDmiInfo"/> (SMBIOS type 20 source data).</summary>
    public sealed class ApobDmiLogicalDimm
    {
        public int Socket { get; internal set; }
        public int Channel { get; internal set; }
        public int Dimm { get; internal set; }
        public bool Present { get; internal set; }
        public bool Interleaved { get; internal set; }
        public ushort Handle { get; internal set; }

        /// <summary>Starting address in KB (0xFFFFFFFF when the extended address is used).</summary>
        public uint StartingAddressKb { get; internal set; }

        /// <summary>Ending address in KB (0xFFFFFFFF when the extended address is used).</summary>
        public uint EndingAddressKb { get; internal set; }

        public ulong ExtStartingAddress { get; internal set; }
        public ulong ExtEndingAddress { get; internal set; }

        public string SlotName
        {
            get { return string.Format("{0}{1}", (char)('A' + Channel), Dimm + 1); }
        }

        /// <summary>The size of the range in MB, 0 when it is empty.</summary>
        public ulong SizeMB
        {
            get
            {
                if (StartingAddressKb == 0xFFFFFFFF || EndingAddressKb == 0xFFFFFFFF)
                    return ExtEndingAddress > ExtStartingAddress ? (ExtEndingAddress - ExtStartingAddress + 1) >> 20 : 0;

                return EndingAddressKb > StartingAddressKb ? ((ulong)EndingAddressKb - StartingAddressKb + 1) >> 10 : 0;
            }
        }
    }

    /// <summary>
    /// APOB_MEM_DMI_INFO (SMBIOS group, type 8): the memory type and the per DIMM data the BIOS builds the SMBIOS
    /// memory tables from.
    /// </summary>
    /// <remarks>
    /// Data layout (verified on a Granite Ridge dump): MemoryType:7 EccCapable:1 byte, MaxPhysicalDimms byte,
    /// MaxLogicalDimms byte, a reserved byte, MaxPhysicalDimms 8-byte records, then MaxLogicalDimms 32-byte records.
    /// The first byte of a record is Socket:2 Channel:3 Dimm:2 DimmPresent:1 on client parts (this does not match
    /// the Channel:4 Dimm:1 of the openSIL server header).
    /// </remarks>
    public sealed class ApobDmiInfo
    {
        private const uint PHYSICAL_SIZE = 8;
        private const uint LOGICAL_SIZE = 32;

        /// <summary>SMBIOS type 17 memory type (0x1A DDR4, 0x22 DDR5, 0x23 LPDDR5).</summary>
        public byte MemoryType { get; private set; }
        public bool EccCapable { get; private set; }
        public int MaxPhysicalDimms { get; private set; }
        public int MaxLogicalDimms { get; private set; }

        public List<ApobDmiPhysicalDimm> PhysicalDimms { get; private set; }
        public List<ApobDmiLogicalDimm> LogicalDimms { get; private set; }

        public string MemoryTypeName
        {
            get
            {
                switch (MemoryType)
                {
                    case 0x18: return "DDR3";
                    case 0x1A: return "DDR4";
                    case 0x1D: return "LPDDR3";
                    case 0x1E: return "LPDDR4";
                    case 0x22: return "DDR5";
                    case 0x23: return "LPDDR5";
                    default: return string.Format("0x{0:X2}", MemoryType);
                }
            }
        }

        public static ApobDmiInfo Decode(byte[] buffer, uint entryOffset)
        {
            if (!ApobBytes.TryGetData(buffer, entryOffset, out uint data, out uint size) || size < 4)
                return null;

            byte typeByte = buffer[data];
            int physical = buffer[data + 1];
            int logical = buffer[data + 2];

            if (4 + (ulong)physical * PHYSICAL_SIZE + (ulong)logical * LOGICAL_SIZE > size)
                return null;

            var info = new ApobDmiInfo
            {
                MemoryType = (byte)(typeByte & 0x7F),
                EccCapable = (typeByte & 0x80) != 0,
                MaxPhysicalDimms = physical,
                MaxLogicalDimms = logical,
                PhysicalDimms = new List<ApobDmiPhysicalDimm>(),
                LogicalDimms = new List<ApobDmiLogicalDimm>(),
            };

            uint o = data + 4;
            for (int i = 0; i < physical; i++, o += PHYSICAL_SIZE)
            {
                byte bits = buffer[o];
                info.PhysicalDimms.Add(new ApobDmiPhysicalDimm
                {
                    Socket = bits & 0x3,
                    Channel = (bits >> 2) & 0x7,
                    Dimm = (bits >> 5) & 0x3,
                    Present = (bits & 0x80) != 0,
                    SpdAddress = buffer[o + 1],
                    Handle = ApobBytes.U16(buffer, o + 2),
                    ConfiguredSpeed = ApobBytes.U16(buffer, o + 4),
                    ConfiguredVoltage = ApobBytes.U16(buffer, o + 6),
                });
            }

            for (int i = 0; i < logical; i++, o += LOGICAL_SIZE)
            {
                byte bits = buffer[o];
                info.LogicalDimms.Add(new ApobDmiLogicalDimm
                {
                    Socket = bits & 0x3,
                    Channel = (bits >> 2) & 0x7,
                    Dimm = (bits >> 5) & 0x3,
                    Present = (bits & 0x80) != 0,
                    Interleaved = (buffer[o + 1] & 0x1) != 0,
                    Handle = ApobBytes.U16(buffer, o + 2),
                    StartingAddressKb = ApobBytes.U32(buffer, o + 4),
                    EndingAddressKb = ApobBytes.U32(buffer, o + 8),
                    ExtStartingAddress = ApobBytes.U64(buffer, o + 16),
                    ExtEndingAddress = ApobBytes.U64(buffer, o + 24),
                });
            }

            return info;
        }
    }
}

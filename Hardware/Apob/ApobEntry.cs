using System.Collections.Generic;

namespace ZenStates.Core.Hardware.Apob
{
    /// <summary>
    /// An entry (APOB_TYPE_HEADER and its data) of the APOB table.
    /// </summary>
    /// <remarks>
    /// Some entries carry 0xFFFF in the upper half of the group and type IDs. Their data is not readable (it looks
    /// encrypted or obfuscated), and the group and type are in the lower half, see <see cref="IsEncrypted"/>.
    /// </remarks>
    public sealed class ApobEntry
    {
        /// <summary>APOB_TYPE_HEADER: GroupID, DataTypeID, InstanceID, TypeSize, then a 32-byte HMAC.</summary>
        public const uint HEADER_SIZE = 48;

        /// <summary>Offset of the entry (its header) in the APOB table.</summary>
        public uint Offset { get; internal set; }

        /// <summary>The GroupID field as stored, upper half included.</summary>
        public uint RawGroupId { get; internal set; }

        /// <summary>The DataTypeID field as stored, upper half included.</summary>
        public uint RawDataTypeId { get; internal set; }

        public uint InstanceId { get; internal set; }

        /// <summary>Size of the entry, header included.</summary>
        public uint Size { get; internal set; }

        public uint GroupId { get { return RawGroupId & 0xFFFF; } }
        public uint DataTypeId { get { return RawDataTypeId & 0xFFFF; } }

        /// <summary>The data of the entry is not readable (0xFFFF in the upper half of the group and type IDs).</summary>
        public bool IsEncrypted { get { return (RawGroupId >> 16) == 0xFFFF || (RawDataTypeId >> 16) == 0xFFFF; } }

        /// <summary>Offset of the data, right after the header.</summary>
        public uint DataOffset { get { return Offset + HEADER_SIZE; } }

        public uint DataSize { get { return Size > HEADER_SIZE ? Size - HEADER_SIZE : 0; } }

        public string GroupName { get { return ApobEntryNames.GetGroupName(GroupId); } }

        /// <summary>A description of the data type, "Unknown" when it is not known.</summary>
        public string Name { get { return ApobEntryNames.GetTypeName(GroupId, DataTypeId); } }

        public bool Is(uint groupId, uint dataTypeId)
        {
            return GroupId == groupId && DataTypeId == dataTypeId;
        }

        /// <summary>A copy of the entry, header included, or null when it does not fit in <paramref name="table"/>.</summary>
        public byte[] GetRawEntry(byte[] table)
        {
            if (table == null || (ulong)Offset + Size > (ulong)table.Length)
                return null;

            byte[] buffer = new byte[Size];
            System.Buffer.BlockCopy(table, (int)Offset, buffer, 0, (int)Size);
            return buffer;
        }

        /// <summary>Number of non-zero bytes in the data of the entry (the header is not counted).</summary>
        public int CountNonZeroDataBytes(byte[] table)
        {
            if (table == null || (ulong)Offset + Size > (ulong)table.Length)
                return 0;

            return ApobBytes.CountNonZero(table, DataOffset, DataSize);
        }

        public override string ToString()
        {
            return string.Format("{0}/{1} {2} {3}{4}", GroupId, DataTypeId, GroupName, Name, IsEncrypted ? " (encrypted)" : "");
        }

        /// <summary>The entries of a raw APOB table, in table order.</summary>
        internal static List<ApobEntry> Enumerate(byte[] table, uint firstEntry, uint tableSize, IList<uint> extraStarts)
        {
            var result = new List<ApobEntry>();
            List<ApobCoreMapParser.Entry> entries = ApobCoreMapParser.EnumerateEntries(table, firstEntry, tableSize, extraStarts);

            for (int i = 0; i < entries.Count; i++)
            {
                ApobCoreMapParser.Entry e = entries[i];
                result.Add(new ApobEntry
                {
                    Offset = e.Offset,
                    RawGroupId = e.GroupId,
                    RawDataTypeId = e.DataTypeId,
                    InstanceId = e.InstanceId,
                    Size = e.Size,
                });
            }

            return result;
        }
    }

    /// <summary>Names of the APOB groups and data types (AGESA APOBCMN.h, openSIL ApobCmn.h).</summary>
    public static class ApobEntryNames
    {
        public const uint GROUP_MEM = 1;
        public const uint GROUP_DF = 2;
        public const uint GROUP_CCX = 3;
        public const uint GROUP_GNB = 4;
        public const uint GROUP_FCH = 5;
        public const uint GROUP_PSP = 6;
        public const uint GROUP_GEN = 7;
        public const uint GROUP_SMBIOS = 8;
        public const uint GROUP_FABRIC = 9;

        public static string GetGroupName(uint groupId)
        {
            switch (groupId)
            {
                case GROUP_MEM: return "MEM";
                case GROUP_DF: return "DF";
                case GROUP_CCX: return "CCX";
                case GROUP_GNB: return "GNB";
                case GROUP_FCH: return "FCH";
                case GROUP_PSP: return "PSP";
                case GROUP_GEN: return "GEN";
                case GROUP_SMBIOS: return "SMBIOS";
                case GROUP_FABRIC: return "FABRIC";
                default: return "Group " + groupId;
            }
        }

        public static string GetTypeName(uint groupId, uint dataTypeId)
        {
            switch (groupId)
            {
                case GROUP_MEM:
                    if (dataTypeId >= 30 && dataTypeId < 40)
                        return "S3 DDR PHY replay phase " + (dataTypeId - 30);
                    // The number of MOP entries is the channel count of the program; Phoenix uses 50..58 for something else
                    if (dataTypeId >= 40 && dataTypeId < 48)
                        return "S3 MOP array replay channel " + (dataTypeId - 40);

                    switch (dataTypeId)
                    {
                        case 1: return "General errors";
                        case 2: return "General configuration info";
                        case 5: return "PMU SMB (message block)";
                        case 7: return "DIMM SMBus info";
                        case 15: return "NVDIMM info";
                        case 16: return "APCB boot info";
                        case 17: return "DIMM SPD data";
                        case 18: return "MBIST result info";
                        case 22: return "PMU training failure info";
                        case 25: return "System configuration info";
                        case 27: return "SoC init config";
                        case 28: return "RMP info (memory profile)";
                    }
                    break;

                case GROUP_CCX:
                    switch (dataTypeId)
                    {
                        case 1: return "CCX logical to physical map";
                        case 2: return "CCX EDC throttle threshold";
                        case 3: return "CCD logical to physical map";
                    }
                    break;

                case GROUP_GEN:
                    switch (dataTypeId)
                    {
                        case 3: return "Configuration info";
                        case 4: return "S3 replay buffer info";
                        case 6: return "Event log";
                        case 23: return "Environment flags";
                        case 26: return "Configuration data";
                    }
                    break;

                case GROUP_SMBIOS:
                    if (dataTypeId == 8)
                        return "Memory SMBIOS info";
                    break;

                case GROUP_FABRIC:
                    switch (dataTypeId)
                    {
                        case 9: return "System memory map";
                        case 19: return "NPS info";
                        case 20: return "S-Link info";
                        case 21: return "DXIO PHY FW override info";
                        case 24: return "CXL info";
                    }
                    break;
            }

            return "Unknown";
        }
    }

    /// <summary>Bounds checked little-endian reads of APOB entry data.</summary>
    internal static class ApobBytes
    {
        /// <summary>The data of the entry at <paramref name="entryOffset"/> of <paramref name="buffer"/>, from its TypeSize.</summary>
        internal static bool TryGetData(byte[] buffer, uint entryOffset, out uint dataOffset, out uint dataSize)
        {
            dataOffset = 0;
            dataSize = 0;

            if (buffer == null || (ulong)entryOffset + ApobEntry.HEADER_SIZE > (ulong)buffer.Length)
                return false;

            uint size = U32(buffer, entryOffset + 12);
            if (size < ApobEntry.HEADER_SIZE || (ulong)entryOffset + size > (ulong)buffer.Length)
                return false;

            dataOffset = entryOffset + ApobEntry.HEADER_SIZE;
            dataSize = size - ApobEntry.HEADER_SIZE;
            return true;
        }

        internal static bool IsEntry(byte[] buffer, uint entryOffset, uint groupId, uint dataTypeId)
        {
            if (buffer == null || (ulong)entryOffset + 16 > (ulong)buffer.Length)
                return false;

            return (U32(buffer, entryOffset) & 0xFFFF) == groupId && (U32(buffer, entryOffset + 4) & 0xFFFF) == dataTypeId;
        }

        internal static byte U8(byte[] b, uint offset)
        {
            return b[offset];
        }

        internal static ushort U16(byte[] b, uint offset)
        {
            return unchecked((ushort)(b[offset] | (b[offset + 1] << 8)));
        }

        internal static uint U32(byte[] b, uint offset)
        {
            return unchecked((uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24)));
        }

        internal static ulong U64(byte[] b, uint offset)
        {
            return U32(b, offset) | ((ulong)U32(b, offset + 4) << 32);
        }

        internal static int CountNonZero(byte[] b, uint offset, uint length)
        {
            int count = 0;
            ulong end = (ulong)offset + length;
            if (b == null || end > (ulong)b.Length)
                return 0;

            for (uint i = offset; i < end; i++)
            {
                if (b[i] != 0)
                    count++;
            }

            return count;
        }
    }
}

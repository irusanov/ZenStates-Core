using System;
using System.Collections.Generic;
using System.Globalization;

namespace ZenStates.Core.Hardware.Apob
{
    /// <summary>A configuration token of an APCB (one BIOS / CBS option, identified by a 32-bit hash).</summary>
    public sealed class ApcbToken
    {
        /// <summary>The token id (a hash of the option name).</summary>
        public uint Id { get; internal set; }

        /// <summary>Token type: 0 = bool, 1 = u8, 2 = u16, 4 = u32.</summary>
        public ushort TypeId { get; internal set; }

        /// <summary>APCB type instance the token was listed under.</summary>
        public ushort Instance { get; internal set; }

        /// <summary>Priority mask of the type the token was listed under.</summary>
        public byte PriorityMask { get; internal set; }

        public uint Value { get; internal set; }

        public string TypeName
        {
            get
            {
                switch (TypeId)
                {
                    case 0: return "bool";
                    case 1: return "u8";
                    case 2: return "u16";
                    case 4: return "u32";
                    default: return "t" + TypeId.ToString(CultureInfo.InvariantCulture);
                }
            }
        }

        /// <summary>The name of the token (<see cref="ApcbTokenNames"/>), or null when it is not known.</summary>
        public ApcbTokenName Name
        {
            get { return ApcbTokenNames.Get(Id); }
        }

        /// <summary>The value formatted to the width of its type.</summary>
        public string ValueText
        {
            get
            {
                switch (TypeId)
                {
                    case 0: return Value != 0 ? "true" : "false";
                    case 1: return "0x" + (Value & 0xFF).ToString("X2", CultureInfo.InvariantCulture);
                    case 2: return "0x" + (Value & 0xFFFF).ToString("X4", CultureInfo.InvariantCulture);
                    default: return "0x" + Value.ToString("X8", CultureInfo.InvariantCulture);
                }
            }
        }
    }

    /// <summary>A group of an APCB (TOKN, PSPG, MEMG, ...).</summary>
    public sealed class ApcbGroup
    {
        public string Signature { get; internal set; }
        public ushort GroupId { get; internal set; }
        public uint Size { get; internal set; }
        public int TypeCount { get; internal set; }
    }

    /// <summary>
    /// An AMD PSP Customization Block read from the SPI flash. The AGESA boot loader reads the BIOS memory options
    /// from it before DRAM exists; the BIOS stores the options changed from their defaults as tokens.
    /// </summary>
    public sealed class ApcbBlob
    {
        /// <summary>BIOS directory entry type: 0x60 = APCB (writable copy), 0x68 = APCB backup / defaults.</summary>
        public uint EntryType { get; internal set; }

        /// <summary>Instance of the BIOS directory entry (entry byte 2, bits 7:4).</summary>
        public int EntryInstance { get; internal set; }

        public ulong Address { get; internal set; }

        /// <summary>Flash offset of the BIOS directory that lists the APCB (tells the processor families apart).</summary>
        public uint Directory { get; internal set; }

        /// <summary>Size of the BIOS directory entry (the space reserved for the APCB).</summary>
        public uint SlotSize { get; internal set; }

        /// <summary>Size of the APCB from its header.</summary>
        public uint Size { get; internal set; }

        public ushort Version { get; internal set; }
        public uint UniqueInstance { get; internal set; }
        public byte Checksum { get; internal set; }

        /// <summary>True when all the bytes of the APCB add up to 0, which also means the flash read was intact.</summary>
        public bool ChecksumValid { get; internal set; }

        public List<ApcbGroup> Groups { get; } = new List<ApcbGroup>();
        public List<ApcbToken> Tokens { get; } = new List<ApcbToken>();

        /// <summary>Other flash addresses holding an identical copy (same size, instance and checksum).</summary>
        public List<ulong> AlsoAt { get; } = new List<ulong>();

        /// <summary>Why the blob could not be (fully) parsed, or null.</summary>
        public string Error { get; internal set; }
    }

    /// <summary>
    /// Reads the APCBs listed in the BIOS directories (types 0x60 and 0x68) from the memory-mapped SPI flash and
    /// decodes their token groups. Only version 3 APCBs (version 0x30, Zen 4 / Zen 5) have their tokens decoded.
    /// </summary>
    /// <remarks>
    /// APCB v3 layout: an 0x80-byte header ("APCB", header size, version, total size, unique instance, checksum byte
    /// at 0x10) followed by groups. A group has a 16-byte header (signature, group id, header size, version, total
    /// size) and holds types; a type has a 16-byte header (group id, type id, size, instance, context type and
    /// format, unit size, priority mask, key size and position, board mask). In the TOKN group (0x3000) a type with
    /// context type 2 holds {u32 token id, u32 value} pairs; the type id gives the token type (0 bool, 1 u8, 2 u16,
    /// 4 u32). The byte sum of a valid APCB is 0. All reads and walks are bounded.
    /// </remarks>
    internal static class ApcbReader
    {
        internal delegate byte[] PhysicalBlockReader(ulong address, int size);

        public const uint BIOS_TYPE_APCB = 0x60;
        public const uint BIOS_TYPE_APCB_BACKUP = 0x68;

        private const uint APCB_SIGNATURE = 0x42435041;   // "APCB"
        private const ushort APCB_VERSION_3 = 0x30;
        private const int MIN_HEADER_SIZE = 0x20;
        private const int MAX_APCB_SIZE = 0x20000;
        private const ushort TOKEN_GROUP_ID = 0x3000;
        private const int GROUP_HEADER_SIZE = 16;
        private const int TYPE_HEADER_SIZE = 16;
        private const byte CONTEXT_TYPE_TOKENS = 2;
        private const byte TOKEN_UNIT_SIZE = 8;
        private const int MAX_GROUPS = 64;
        private const int MAX_TYPES = 4096;
        private const int MAX_TOKENS = 16384;

        /// <summary>
        /// Reads and decodes every APCB of the BIOS directories. Identical copies (same size, unique instance and
        /// checksum) are read once and listed in <see cref="ApcbBlob.AlsoAt"/>.
        /// </summary>
        /// <param name="readDword">Reads a dword of physical memory.</param>
        /// <param name="readBlock">Optional bulk reader, null or a null result falls back to dword reads.</param>
        /// <param name="windows">Flash windows to search.</param>
        /// <param name="trace">Optional; receives the directory walk and the read steps.</param>
        public static List<ApcbBlob> ReadAll(ApobLocator.PhysicalDwordReader readDword, PhysicalBlockReader readBlock,
            IList<ApobLocator.FlashWindow> windows, List<string> trace)
        {
            var blobs = new List<ApcbBlob>();
            List<ApobLocator.BiosEntry> entries = ApobLocator.FindBiosEntries(readDword, windows,
                new[] { BIOS_TYPE_APCB, BIOS_TYPE_APCB_BACKUP }, trace);

            foreach (ApobLocator.BiosEntry entry in entries)
            {
                byte[] header = ReadDwords(readDword, entry.Address, MIN_HEADER_SIZE);
                if (header == null || ReadUInt32(header, 0) != APCB_SIGNATURE)
                {
                    Add(trace, "0x{0:X}: no APCB signature", entry.Address);
                    continue;
                }

                uint size = ReadUInt32(header, 8);
                uint unique = ReadUInt32(header, 12);
                byte checksum = header[0x10];

                ApcbBlob copy = FindCopy(blobs, entry.Type, size, unique, checksum);
                if (copy != null)
                {
                    copy.AlsoAt.Add(entry.Address);
                    continue;
                }

                var blob = new ApcbBlob
                {
                    EntryType = entry.Type,
                    EntryInstance = entry.Flags >> 4,
                    Address = entry.Address,
                    Directory = entry.Directory,
                    SlotSize = entry.Size,
                    Size = size,
                    Version = ReadUInt16(header, 6),
                    UniqueInstance = unique,
                    Checksum = checksum,
                };
                blobs.Add(blob);

                if (size < MIN_HEADER_SIZE || size > entry.Size || size > MAX_APCB_SIZE)
                {
                    blob.Error = string.Format(CultureInfo.InvariantCulture, "APCB size 0x{0:X} does not fit its slot of 0x{1:X}", size, entry.Size);
                    continue;
                }

                byte[] data = null;
                if (readBlock != null)
                {
                    try
                    {
                        data = readBlock(entry.Address, (int)size);
                    }
                    catch
                    {
                        data = null;
                    }

                    if (data != null && (data.Length < size || ReadUInt32(data, 0) != APCB_SIGNATURE))
                        data = null;
                }

                if (data == null)
                    data = ReadDwords(readDword, entry.Address, (int)size);

                if (data == null)
                {
                    blob.Error = "flash read failed";
                    continue;
                }

                Parse(blob, data, (int)size);
            }

            Add(trace, "{0} APCB(s) read", blobs.Count);
            return blobs;
        }

        /// <summary>Decodes the groups and tokens of an APCB image (from its signature on) into <paramref name="blob"/>.</summary>
        public static void Parse(ApcbBlob blob, byte[] data, int size)
        {
            if (data == null || size < MIN_HEADER_SIZE || size > data.Length || ReadUInt32(data, 0) != APCB_SIGNATURE)
            {
                blob.Error = "not an APCB";
                return;
            }

            int sum = 0;
            for (int i = 0; i < size; i++)
                sum += data[i];
            blob.ChecksumValid = (sum & 0xFF) == 0;

            if (blob.Version != APCB_VERSION_3)
            {
                blob.Error = string.Format(CultureInfo.InvariantCulture, "APCB version 0x{0:X} is not decoded", blob.Version);
                return;
            }

            int headerSize = ReadUInt16(data, 4);
            if (headerSize < MIN_HEADER_SIZE || headerSize > size)
            {
                blob.Error = "invalid header size";
                return;
            }

            int pos = headerSize;
            int types = 0;
            while (pos + GROUP_HEADER_SIZE <= size && blob.Groups.Count < MAX_GROUPS)
            {
                uint signature = ReadUInt32(data, pos);
                ushort groupId = ReadUInt16(data, pos + 4);
                int groupHeaderSize = ReadUInt16(data, pos + 6);
                uint groupSize = ReadUInt32(data, pos + 12);

                if (groupHeaderSize < GROUP_HEADER_SIZE || groupSize < (uint)groupHeaderSize || pos + (long)groupSize > size)
                {
                    blob.Error = string.Format(CultureInfo.InvariantCulture, "invalid group header at +0x{0:X}", pos);
                    return;
                }

                var group = new ApcbGroup { Signature = SignatureText(signature), GroupId = groupId, Size = groupSize };
                blob.Groups.Add(group);

                int end = pos + (int)groupSize;
                int tpos = pos + groupHeaderSize;
                while (tpos + TYPE_HEADER_SIZE <= end && types < MAX_TYPES)
                {
                    ushort typeId = ReadUInt16(data, tpos + 2);
                    int typeSize = ReadUInt16(data, tpos + 4);
                    ushort instance = ReadUInt16(data, tpos + 6);
                    byte contextType = data[tpos + 8];
                    byte unitSize = data[tpos + 10];
                    byte priority = data[tpos + 11];

                    if (typeSize < TYPE_HEADER_SIZE || tpos + typeSize > end)
                    {
                        blob.Error = string.Format(CultureInfo.InvariantCulture, "invalid type header at +0x{0:X}", tpos);
                        break;
                    }

                    group.TypeCount++;
                    types++;

                    if (groupId == TOKEN_GROUP_ID && contextType == CONTEXT_TYPE_TOKENS && unitSize == TOKEN_UNIT_SIZE)
                    {
                        for (int t = tpos + TYPE_HEADER_SIZE; t + TOKEN_UNIT_SIZE <= tpos + typeSize && blob.Tokens.Count < MAX_TOKENS; t += TOKEN_UNIT_SIZE)
                        {
                            blob.Tokens.Add(new ApcbToken
                            {
                                Id = ReadUInt32(data, t),
                                TypeId = typeId,
                                Instance = instance,
                                PriorityMask = priority,
                                Value = ReadUInt32(data, t + 4),
                            });
                        }
                    }

                    tpos += (typeSize + 3) & ~3;
                }

                pos = end;
            }
        }

        private static ApcbBlob FindCopy(List<ApcbBlob> blobs, uint type, uint size, uint unique, byte checksum)
        {
            for (int i = 0; i < blobs.Count; i++)
            {
                ApcbBlob b = blobs[i];
                if (b.EntryType == type && b.Size == size && b.UniqueInstance == unique && b.Checksum == checksum)
                    return b;
            }

            return null;
        }

        private static byte[] ReadDwords(ApobLocator.PhysicalDwordReader read, ulong address, int size)
        {
            byte[] data = new byte[(size + 3) & ~3];
            for (int i = 0; i < data.Length; i += 4)
            {
                uint value;
                try
                {
                    if (!read(address + (ulong)i, out value))
                        return null;
                }
                catch
                {
                    return null;
                }

                // Masked: the project builds with checked arithmetic, a plain narrowing cast would throw
                data[i] = (byte)(value & 0xFF);
                data[i + 1] = (byte)((value >> 8) & 0xFF);
                data[i + 2] = (byte)((value >> 16) & 0xFF);
                data[i + 3] = (byte)((value >> 24) & 0xFF);
            }

            return data;
        }

        // Built from unsigned parts: an int with the top bit set would overflow the cast in checked arithmetic
        private static uint ReadUInt32(byte[] data, int offset)
        {
            return data[offset] | ((uint)data[offset + 1] << 8) | ((uint)data[offset + 2] << 16) | ((uint)data[offset + 3] << 24);
        }

        private static ushort ReadUInt16(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static string SignatureText(uint signature)
        {
            char[] chars = new char[4];
            for (int i = 0; i < 4; i++)
            {
                char c = (char)((signature >> (i * 8)) & 0xFF);
                chars[i] = c >= 0x20 && c < 0x7F ? c : '.';
            }

            return new string(chars);
        }

        private static void Add(List<string> trace, string format, params object[] args)
        {
            if (trace != null)
                trace.Add(string.Format(CultureInfo.InvariantCulture, format, args));
        }
    }
}

using System.Collections.Generic;
using System.Globalization;

namespace ZenStates.Core.Hardware.Apob
{
    /// <summary>
    /// Reads the AMD firmware directories in the memory-mapped SPI flash and returns the BIOS directory entries of
    /// the requested types (the APCBs, types 0x60 and 0x68).
    /// </summary>
    /// <remarks>
    /// The walk follows what PSPTool and coreboot's amdfwtool do:
    /// <list type="bullet">
    /// <item>The Embedded Firmware Structure (signature 0x55AA55AA) sits at one of a few fixed offsets of a 16 MiB
    /// image. Its dwords point to PSP and BIOS directories ($PSP, $BHD) or combo directories (2PSP, 2BHD) that list
    /// one directory per processor family.</item>
    /// <item>BIOS directories link to their level 2 ($BL2) through entry 0x70. PSP directories link to a PSP level 2
    /// ($PL2) through 0x40, to a BIOS level 2 through 0x49 and, with A/B recovery, to the A and B level 2 directories
    /// through 0x48 and 0x4A (directly or through an image slot header).</item>
    /// </list>
    /// The flash is reached through the FCH ROM decode windows (coreboot, soc/amd/common/block/spi/mmap_boot_rom3.c):
    /// ROM2, at most 16 MiB just below 4 GiB, and ROM3, which maps up to 64 MiB of flash linearly in high MMIO
    /// (0xFD_0000_0000 by default). On boards with more than 16 MiB of flash ROM2 may show a part of the flash without
    /// the firmware directories, so every 16 MiB page of the windows is searched for an EFS. All reads are bounded, so
    /// a corrupt or unexpected layout ends the walk instead of looping.
    /// </remarks>
    internal static class ApobLocator
    {
        internal delegate bool PhysicalDwordReader(ulong address, out uint value);

        /// <summary>A range of physical address space that maps SPI flash.</summary>
        internal struct FlashWindow
        {
            public string Name;
            public ulong Base;
            public uint Size;

            public FlashWindow(string name, ulong baseAddress, uint size)
            {
                Name = name;
                Base = baseAddress;
                Size = size;
            }
        }

        /// <summary>A BIOS directory entry found in the flash, with the physical address of its data.</summary>
        internal struct BiosEntry
        {
            public uint Type;
            public byte Flags;        // entry byte 2: reset/copy/read-only/compressed, instance in bits 7:4
            public byte SubProgram;   // entry byte 3
            public ulong Address;     // physical address of the data (window base + offset)
            public uint Size;
            public uint Directory;    // window offset of the directory that lists the entry
            public string Window;
        }

        public const ulong ROM2_DEFAULT_BASE = 0xFF000000;
        public const ulong ROM3_DEFAULT_BASE = 0xFD00000000;
        public const uint ROM2_MAX_SIZE = 0x01000000;
        public const uint ROM3_MAX_SIZE = 0x04000000;

        private const uint IMAGE_SIZE = 0x01000000;       // 16 MiB: pointers inside an image are relative to it
        private const uint X86_IMAGE_BASE = 0xFF000000;   // pointers written as x86 addresses of the image

        private const uint EFS_SIGNATURE = 0x55AA55AA;
        private const uint EFS_POINTER_FIRST = 0x04;
        private const uint EFS_POINTER_LAST = 0x3C;

        private const uint COOKIE_PSP = 0x50535024;       // "$PSP"
        private const uint COOKIE_PSP_L2 = 0x324C5024;    // "$PL2"
        private const uint COOKIE_BHD = 0x44484224;       // "$BHD"
        private const uint COOKIE_BHD_L2 = 0x324C4224;    // "$BL2"
        private const uint COOKIE_COMBO_PSP = 0x50535032; // "2PSP"
        private const uint COOKIE_COMBO_BHD = 0x44484232; // "2BHD"

        private const uint DIR_HEADER_SIZE = 0x10;
        private const uint PSP_ENTRY_SIZE = 0x10;
        private const uint BIOS_ENTRY_SIZE = 0x18;
        private const uint COMBO_HEADER_SIZE = 0x20;
        private const uint COMBO_ENTRY_SIZE = 0x10;
        private const uint ISH_PL2_LOCATION = 0x10;

        private const uint PSP_TYPE_L2 = 0x40;
        private const uint PSP_TYPE_L2A = 0x48;
        private const uint PSP_TYPE_BIOS_L2 = 0x49;
        private const uint PSP_TYPE_L2B = 0x4A;
        private const uint BIOS_TYPE_L2 = 0x70;
        private const uint BIOS_TYPE_APOB = 0x61;

        // BIOS directory entry: destination (64-bit) of the data in DRAM
        private const uint BIOS_ENTRY_DESTINATION = 0x10;
        private const uint PAGE_SIZE = 0x1000;
        private const int MAX_APOB_DESTINATIONS = 4;

        private const int ADDR_MODE_REL_TAB = 2;

        private const uint MAX_ENTRIES = 128;
        private const int MAX_DIRECTORIES = 32;
        private const int MAX_DEPTH = 6;

        // Offsets of the Embedded Firmware Structure inside a 16 MiB image, in the order they are tried.
        private static readonly uint[] EfsOffsets = { 0x020000, 0x120000, 0x820000, 0xC20000, 0xE20000, 0xF20000, 0xFA0000 };

        /// <summary>
        /// Returns the BIOS directory entries of the given types (for example 0x60 / 0x68 = APCB), with the physical
        /// address of their data, without duplicates. The windows are tried in order; the first window whose
        /// directories list any matching entry wins. Empty when nothing could be read.
        /// </summary>
        public static List<BiosEntry> FindBiosEntries(PhysicalDwordReader read, IList<FlashWindow> windows, uint[] types, List<string> trace = null)
        {
            var entries = new List<BiosEntry>();

            foreach (FlashWindow window in windows)
            {
                if (window.Size < IMAGE_SIZE)
                {
                    Log(trace, "{0} 0x{1:X}: window of 0x{2:X} bytes skipped", window.Name, window.Base, window.Size);
                    continue;
                }

                var walker = new Walker(read, window, trace, types);
                WalkWindow(walker, window);
                walker.Log("{0}: {1} matching BIOS entr(y/ies)", window.Name, walker.Entries.Count);

                if (walker.Entries.Count > 0)
                {
                    entries = walker.Entries;
                    break;
                }
            }

            return entries;
        }

        /// <summary>
        /// Returns the APOB addresses recorded in the firmware: the destinations of the APOB entries (type 0x61) of
        /// the BIOS directories, page aligned and below 4 GiB, without duplicates (at most 4). The windows are tried
        /// in order; the first window that gives an address wins. This is the walk of the AMDAPOB PawnIO module.
        /// </summary>
        /// <param name="efsAddress">Physical address of the last EFS found, 0 when none.</param>
        public static List<uint> FindApobDestinations(PhysicalDwordReader read, IList<FlashWindow> windows, out ulong efsAddress,
            List<string> trace = null)
        {
            var addresses = new List<uint>();
            efsAddress = 0;

            foreach (FlashWindow window in windows)
            {
                if (window.Size < IMAGE_SIZE)
                {
                    Log(trace, "{0} 0x{1:X}: window of 0x{2:X} bytes skipped", window.Name, window.Base, window.Size);
                    continue;
                }

                var walker = new Walker(read, window, trace, null) { CollectApob = true };
                WalkWindow(walker, window);
                walker.Log("{0}: {1} APOB address(es)", window.Name, walker.ApobDestinations.Count);

                if (walker.EfsAddress != 0)
                    efsAddress = walker.EfsAddress;

                if (walker.ApobDestinations.Count > 0)
                {
                    addresses = walker.ApobDestinations;
                    break;
                }
            }

            return addresses;
        }

        // Searches every 16 MiB page of the window for an EFS and walks the directories it points to.
        private static void WalkWindow(Walker walker, FlashWindow window)
        {
            walker.Log("{0} window 0x{1:X}, 0x{2:X} bytes", window.Name, window.Base, window.Size);

            bool efsFound = false;
            for (uint image = 0; image + IMAGE_SIZE <= window.Size; image += IMAGE_SIZE)
            {
                foreach (uint efsOffset in EfsOffsets)
                {
                    uint efs = image + efsOffset;
                    if (!walker.Read(efs, out uint signature) || signature != EFS_SIGNATURE)
                        continue;

                    efsFound = true;
                    walker.EfsAddress = window.Base + efs;
                    walker.Log("EFS at 0x{0:X7}", efs);
                    walker.WalkEfs(efs, image);
                }
            }

            if (!efsFound)
                walker.LogWindowSample();
        }

        private static void Log(List<string> trace, string format, params object[] args)
        {
            if (trace != null && trace.Count < Walker.MAX_TRACE_LINES)
                trace.Add(string.Format(CultureInfo.InvariantCulture, format, args));
        }

        private sealed class Walker
        {
            public const int MAX_TRACE_LINES = 250;

            private readonly PhysicalDwordReader _read;
            private readonly FlashWindow _window;
            private readonly List<string> _trace;
            private readonly List<uint> _visited = new List<uint>();
            private readonly uint[] _collectTypes;

            public readonly List<BiosEntry> Entries = new List<BiosEntry>();

            /// <summary>Collect the destinations of the APOB entries into <see cref="ApobDestinations"/>.</summary>
            public bool CollectApob;
            public readonly List<uint> ApobDestinations = new List<uint>();

            /// <summary>Physical address of the last EFS found in the window, 0 when none.</summary>
            public ulong EfsAddress;

            public Walker(PhysicalDwordReader read, FlashWindow window, List<string> trace, uint[] collectTypes)
            {
                _read = read;
                _window = window;
                _trace = trace;
                _collectTypes = collectTypes;
            }

            public void Log(string format, params object[] args) => ApobLocator.Log(_trace, format, args);

            /// <summary>Reads a dword at a window offset.</summary>
            public bool Read(uint offset, out uint value)
            {
                value = 0;
                if (offset > _window.Size - 4 || (offset & 0x3) != 0)
                    return false;

                try
                {
                    return _read(_window.Base + offset, out value);
                }
                catch
                {
                    return false;
                }
            }

            private bool Read64(uint offset, out ulong value)
            {
                value = 0;
                if (!Read(offset, out uint low) || !Read(offset + 4, out uint high))
                    return false;

                value = ((ulong)high << 32) | low;
                return true;
            }

            /// <summary>
            /// Converts a flash pointer of the image at <paramref name="image"/> (an x86 address of the image or an
            /// offset from its start) to a window offset.
            /// </summary>
            private bool TryImageOffset(uint image, ulong pointer, out uint offset)
            {
                offset = 0;
                ulong relative;
                if (pointer >= X86_IMAGE_BASE && pointer <= 0xFFFFFFFF)
                    relative = pointer - X86_IMAGE_BASE;
                else if (pointer > 0 && pointer < _window.Size)
                    relative = pointer;
                else
                    return false;

                ulong target = image + relative;
                offset = (uint)target;
                return relative != 0 && target <= _window.Size - 4 && (offset & 0x3) == 0;
            }

            public void WalkEfs(uint efs, uint image)
            {
                for (uint field = EFS_POINTER_FIRST; field <= EFS_POINTER_LAST; field += 4)
                {
                    if (!Read(efs + field, out uint pointer) || pointer == 0 || pointer >= 0xFFFFFFFE)
                        continue;

                    if (TryImageOffset(image, pointer, out uint offset))
                    {
                        Log("EFS+0x{0:X2} = 0x{1:X8} -> 0x{2:X7}", field, pointer, offset);
                        Walk(offset, image, 0);
                    }
                    else
                    {
                        Log("EFS+0x{0:X2} = 0x{1:X8} outside the window", field, pointer);
                    }
                }
            }

            // No EFS anywhere: show what the window reads like (all 0xFF or 0 means it is not readable this way).
            public void LogWindowSample()
            {
                var offsets = new List<uint> { 0x000000, 0x020000 };
                for (uint image = IMAGE_SIZE; image < _window.Size; image += IMAGE_SIZE)
                    offsets.Add(image + 0x020000);
                offsets.Add(_window.Size - 0x10);

                foreach (uint offset in offsets)
                {
                    var values = new List<string>();
                    for (uint i = 0; i < 16; i += 4)
                        values.Add(Read(offset + i, out uint value) ? value.ToString("X8", CultureInfo.InvariantCulture) : "--------");

                    Log("  0x{0:X7}: {1}", offset, string.Join(" ", values.ToArray()));
                }
            }

            private static string CookieText(uint cookie)
            {
                var chars = new char[4];
                for (int i = 0; i < 4; i++)
                {
                    char c = (char)((cookie >> (i * 8)) & 0xFF);
                    chars[i] = c >= 0x20 && c < 0x7F ? c : '.';
                }
                return new string(chars);
            }

            private void Walk(uint offset, uint image, int depth)
            {
                if (depth > MAX_DEPTH || _visited.Count >= MAX_DIRECTORIES || _visited.Contains(offset))
                    return;

                if (!Read(offset, out uint cookie))
                {
                    Log("  0x{0:X7}: read failed", offset);
                    return;
                }

                switch (cookie)
                {
                    case COOKIE_COMBO_PSP:
                    case COOKIE_COMBO_BHD:
                        _visited.Add(offset);
                        WalkCombo(offset, image, depth);
                        break;
                    case COOKIE_PSP:
                    case COOKIE_PSP_L2:
                        _visited.Add(offset);
                        WalkDirectory(offset, image, depth, false);
                        break;
                    case COOKIE_BHD:
                    case COOKIE_BHD_L2:
                        _visited.Add(offset);
                        WalkDirectory(offset, image, depth, true);
                        break;
                    default:
                        Log("  0x{0:X7}: not a directory (0x{1:X8} '{2}')", offset, cookie, CookieText(cookie));
                        break;
                }
            }

            // Combo directory: one directory per processor family. All are followed.
            private void WalkCombo(uint offset, uint image, int depth)
            {
                if (!Read(offset + 8, out uint count))
                    return;

                Log("  0x{0:X7}: combo directory, {1} entries", offset, count);

                count = System.Math.Min(count, MAX_ENTRIES);
                for (uint i = 0; i < count; i++)
                {
                    uint entry = offset + COMBO_HEADER_SIZE + i * COMBO_ENTRY_SIZE;
                    if (!Read(entry + 4, out uint id) || !Read(entry + 8, out uint pointer))
                        continue;

                    if (TryImageOffset(image, pointer, out uint target))
                    {
                        Log("    id 0x{0:X8} -> 0x{1:X8}", id, pointer);
                        Walk(target, image, depth + 1);
                    }
                    else
                    {
                        Log("    id 0x{0:X8} -> 0x{1:X8} outside the window", id, pointer);
                    }
                }
            }

            private void WalkDirectory(uint offset, uint image, int depth, bool bios)
            {
                if (!Read(offset, out uint cookie) || !Read(offset + 8, out uint count) || !Read(offset + 12, out uint info))
                    return;

                // Directory address mode: bits 30:29, or 25:24 in version 1 headers (bit 31 set).
                int dirMode = (info >> 31) != 0 ? (int)((info >> 24) & 3) : (int)((info >> 29) & 3);
                uint entrySize = bios ? BIOS_ENTRY_SIZE : PSP_ENTRY_SIZE;
                var types = new List<string>();

                Log("  0x{0:X7}: '{1}' {2} entries, address mode {3}", offset, CookieText(cookie), count, dirMode);

                count = System.Math.Min(count, MAX_ENTRIES);
                for (uint i = 0; i < count; i++)
                {
                    uint entry = offset + DIR_HEADER_SIZE + i * entrySize;
                    if (!Read(entry, out uint typeField) || !Read64(entry + 8, out ulong location))
                        break;

                    uint type = typeField & 0xFF;
                    types.Add(type.ToString("X2", CultureInfo.InvariantCulture));

                    // Entries have their own address mode (bits 63:62) only in directories relative to the table
                    // or slot; otherwise the directory mode applies.
                    int mode = dirMode < ADDR_MODE_REL_TAB ? dirMode : (int)(location >> 62);
                    location &= 0x3FFFFFFFFFFFFFFFUL;

                    if (bios)
                    {
                        if (_collectTypes != null && System.Array.IndexOf(_collectTypes, type) >= 0)
                            CollectEntry(offset, image, entry, typeField, location, mode);

                        if (CollectApob && type == BIOS_TYPE_APOB)
                            CollectApobDestination(entry);

                        if (type == BIOS_TYPE_L2)
                        {
                            if (TryEntryOffset(offset, image, location, mode, out uint target))
                                Walk(target, image, depth + 1);
                            else
                                Log("    L2 link 0x{0:X} (mode {1}) outside the window", location, mode);
                        }
                    }
                    else if (type == PSP_TYPE_L2 || type == PSP_TYPE_BIOS_L2)
                    {
                        if (TryEntryOffset(offset, image, location, mode, out uint target))
                            Walk(target, image, depth + 1);
                        else
                            Log("    0x{0:X2} link 0x{1:X} (mode {2}) outside the window", type, location, mode);
                    }
                    else if (type == PSP_TYPE_L2A || type == PSP_TYPE_L2B)
                    {
                        WalkRecoverySlot(image, location, depth);
                    }
                }

                Log("  0x{0:X7}: types {1}", offset, string.Join(" ", types.ToArray()));
            }

            // A/B recovery entries are always relative to the start of the image and point either to the level 2
            // directory or to an image slot header that holds its location.
            private void WalkRecoverySlot(uint image, ulong location, int depth)
            {
                if (!TryImageOffset(image, location, out uint target) || !Read(target, out uint cookie))
                {
                    Log("    recovery slot 0x{0:X} outside the window", location);
                    return;
                }

                if (cookie == COOKIE_PSP_L2 || cookie == COOKIE_PSP)
                {
                    Walk(target, image, depth + 1);
                }
                else if (Read(target + ISH_PL2_LOCATION, out uint pl2) && TryImageOffset(image, pl2, out uint l2))
                {
                    Walk(l2, image, depth + 1);
                }
            }

            private bool TryEntryOffset(uint directory, uint image, ulong location, int mode, out uint offset)
            {
                if (mode >= ADDR_MODE_REL_TAB)
                {
                    // Relative to the directory (mode 2) or its slot (mode 3, treated the same, as PSPTool does)
                    ulong target = directory + location;
                    offset = (uint)target;
                    return target <= _window.Size - 4 && (offset & 0x3) == 0;
                }

                // Physical address or offset from the start of the image; images mix both in either mode.
                return TryImageOffset(image, location, out offset);
            }

            // The APOB entry has no data in the flash; its destination is where the PSP puts the table in DRAM.
            private void CollectApobDestination(uint entry)
            {
                if (!Read64(entry + BIOS_ENTRY_DESTINATION, out ulong destination))
                    return;

                // A page aligned address below 4 GiB
                if (destination == 0 || destination > uint.MaxValue || (destination & (PAGE_SIZE - 1)) != 0)
                {
                    Log("    entry 0x61: destination 0x{0:X} not used", destination);
                    return;
                }

                uint address = (uint)destination;
                if (ApobDestinations.Contains(address) || ApobDestinations.Count >= MAX_APOB_DESTINATIONS)
                    return;

                Log("    entry 0x61: APOB destination 0x{0:X8}", address);
                ApobDestinations.Add(address);
            }

            private void CollectEntry(uint directory, uint image, uint entry, uint typeField, ulong location, int mode)
            {
                uint type = typeField & 0xFF;
                if (!Read(entry + 4, out uint size))
                    return;

                if (!TryEntryOffset(directory, image, location, mode, out uint target) || size == 0 || target + (ulong)size > _window.Size)
                {
                    Log("    entry 0x{0:X2}: 0x{1:X} bytes at 0x{2:X} (mode {3}) outside the window", type, size, location, mode);
                    return;
                }

                ulong address = _window.Base + target;
                for (int i = 0; i < Entries.Count; i++)
                {
                    if (Entries[i].Address == address)
                        return;
                }

                Log("    entry 0x{0:X2}: 0x{1:X} bytes at 0x{2:X7}", type, size, target);
                Entries.Add(new BiosEntry
                {
                    Type = type,
                    Flags = (byte)((typeField >> 16) & 0xFF),     // masked: the project uses checked arithmetic
                    SubProgram = (byte)((typeField >> 24) & 0xFF),
                    Address = address,
                    Size = size,
                    Directory = directory,
                    Window = _window.Name,
                });
            }
        }
    }
}

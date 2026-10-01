using System;
using System.Collections.Generic;
using System.Text;

namespace ZenStates.Core.Hardware.Apob
{
    /// <summary>A core of the APOB logical to physical map.</summary>
    public sealed class ApobCoreMapCore
    {
        /// <summary>Position in the logical order of the whole map (the order the OS enumerates cores in).</summary>
        public int LogicalIndex { get; internal set; }

        public int LogicalCcd { get; internal set; }
        public int LogicalCcx { get; internal set; }
        public int LogicalCore { get; internal set; }

        public int PhysicalCcd { get; internal set; }
        public int PhysicalCcx { get; internal set; }
        public int PhysicalCore { get; internal set; }

        /// <summary>Enabled state of each thread (SMT) of the core.</summary>
        public bool[] ThreadEnabled { get; internal set; }

        public int EnabledThreads
        {
            get
            {
                int count = 0;
                for (int i = 0; i < ThreadEnabled.Length; i++)
                {
                    if (ThreadEnabled[i])
                        count++;
                }
                return count;
            }
        }

        public override string ToString()
        {
            return string.Format("Core {0}: CCD {1} CCX {2} Core {3} ({4} thread{5})",
                LogicalIndex, PhysicalCcd, PhysicalCcx, PhysicalCore, EnabledThreads, EnabledThreads == 1 ? "" : "s");
        }
    }

    /// <summary>
    /// The CCD / CCX / core logical to physical map the ABL leaves in the APOB (group APOB_CCX):
    /// APOB_CCD_LOGICAL_TO_PHYSICAL_MAP_TYPE (Zen 4 and later) or APOB_CCX_LOGICAL_TO_PHYSICAL_MAP_TYPE (a die
    /// of complexes, older programs).
    ///
    /// The array sizes of the structure are program dependent (openSIL Apob.h: MAX_CCDS_PER_DIE,
    /// MAX_COMPLEXES_PER_CCD, MAX_CORES_PER_COMPLEX, MAX_THREADS_PER_CORE), so they are worked out from the entry
    /// size and checked against the content, see <see cref="ApobCoreMapParser"/>.
    /// </summary>
    public sealed class ApobCoreMap
    {
        internal ApobCoreMap()
        {
            Cores = new List<ApobCoreMapCore>();
        }

        /// <summary>APOB data type: 3 for the CCD map, 1 for the CCX (complex) map.</summary>
        public uint DataTypeId { get; internal set; }

        /// <summary>APOB instance (socket / die).</summary>
        public uint InstanceId { get; internal set; }

        /// <summary>Offset of the entry in the APOB table.</summary>
        public uint EntryOffset { get; internal set; }

        public uint EntrySize { get; internal set; }

        // Structure dimensions (array sizes of the program, not the populated CCDs / CCXs, see PhysicalCcdCount)
        public int CcdSlots { get; internal set; }
        public int ComplexSlotsPerCcd { get; internal set; }
        public int CoreSlotsPerComplex { get; internal set; }
        public int ThreadSlotsPerCore { get; internal set; }

        /// <summary>Enabled cores in logical order.</summary>
        public List<ApobCoreMapCore> Cores { get; private set; }

        /// <summary>The raw entry, header included.</summary>
        public byte[] RawEntry { get; internal set; }

        public int EnabledThreads
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Cores.Count; i++)
                    count += Cores[i].EnabledThreads;
                return count;
            }
        }

        /// <summary>Bit mask of the physical CCDs that have enabled cores.</summary>
        public uint PhysicalCcdMask
        {
            get
            {
                uint mask = 0;
                for (int i = 0; i < Cores.Count; i++)
                    mask |= 1u << Cores[i].PhysicalCcd;
                return mask;
            }
        }

        /// <summary>Number of physical CCDs with enabled cores.</summary>
        public int PhysicalCcdCount
        {
            get
            {
                int count = 0;
                for (uint mask = PhysicalCcdMask; mask != 0; mask &= mask - 1)
                    count++;
                return count;
            }
        }

        /// <summary>Number of physical CCXs (complexes) with enabled cores, over all CCDs.</summary>
        public int PhysicalCcxCount
        {
            get
            {
                var seen = new Dictionary<int, bool>();
                for (int i = 0; i < Cores.Count; i++)
                    seen[Cores[i].PhysicalCcd * 256 + Cores[i].PhysicalCcx] = true;
                return seen.Count;
            }
        }

        /// <summary>The array sizes of the structure, e.g. "8 CCD x 2 CCX x 8 cores x 2 threads".</summary>
        public string SlotLayout
        {
            get
            {
                return string.Format("{0} CCD x {1} CCX x {2} cores x {3} threads",
                    CcdSlots, ComplexSlotsPerCcd, CoreSlotsPerComplex, ThreadSlotsPerCore);
            }
        }

        /// <summary>Bit mask of the enabled physical cores of a physical CCD (bit = CCX * cores per complex + core).</summary>
        public uint GetPhysicalCoreMask(int physicalCcd)
        {
            uint mask = 0;
            for (int i = 0; i < Cores.Count; i++)
            {
                ApobCoreMapCore core = Cores[i];
                if (core.PhysicalCcd == physicalCcd)
                    mask |= 1u << (core.PhysicalCcx * CoreSlotsPerComplex + core.PhysicalCore);
            }
            return mask;
        }

        /// <summary>The core with the given logical index (0-based, logical processor / threads per core), or null.</summary>
        public ApobCoreMapCore GetLogicalCore(int logicalIndex)
        {
            return logicalIndex >= 0 && logicalIndex < Cores.Count ? Cores[logicalIndex] : null;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("{0} map, instance {1}: {2} CCD{3}, {4} CCX{5}, {6} cores, {7} threads",
                DataTypeId == ApobCoreMapParser.CCD_MAP_TYPE ? "CCD" : "CCX", InstanceId,
                PhysicalCcdCount, PhysicalCcdCount == 1 ? "" : "s", PhysicalCcxCount, PhysicalCcxCount == 1 ? "" : "s",
                Cores.Count, EnabledThreads);
            return sb.ToString();
        }
    }

    /// <summary>Finds and decodes the logical to physical core map in a raw APOB table.</summary>
    public static class ApobCoreMapParser
    {
        // APOB group and type IDs (APOBCMN.h)
        public const uint APOB_CCX_GROUP = 3;
        public const uint CCX_MAP_TYPE = 1;     // APOB_CCX_LOGICAL_TO_PHYSICAL_MAP_TYPE
        public const uint CCD_MAP_TYPE = 3;     // APOB_CCD_LOGICAL_TO_PHYSICAL_MAP_TYPE

        public const byte NOT_PRESENT = 0xFF;   // CCX_NOT_PRESENT

        // APOB_TYPE_HEADER: GroupID, DataTypeID, InstanceID, TypeSize, then a 32-byte HMAC
        internal const int TYPE_HEADER_SIZE = 16 + 32;

        private const int MAX_ENTRIES = 4096;

        // Program dependent array sizes to try, the likely ones first
        private static readonly int[] ThreadCandidates = { 2, 1 };
        private static readonly int[] CoreCandidates = { 8, 16, 4 };
        private static readonly int[] ComplexCandidates = { 1, 2 };
        private const int MAX_CCD_SLOTS = 16;

        /// <summary>An entry of the APOB table.</summary>
        public struct Entry
        {
            public uint Offset;
            public uint GroupId;
            public uint DataTypeId;
            public uint InstanceId;
            public uint Size;
        }

        /// <summary>
        /// The entries of the table: from the first entry (header size) on, each followed by the next at its TypeSize,
        /// like AmdGetApobEntry does. When the chain breaks, the chains starting at <paramref name="extraStarts"/>
        /// (the offsets listed in the header) are walked too.
        /// </summary>
        public static List<Entry> EnumerateEntries(byte[] table, uint firstEntry, uint tableSize, IList<uint> extraStarts = null)
        {
            var entries = new List<Entry>();
            var seen = new Dictionary<uint, bool>();

            if (table == null)
                return entries;

            uint end = tableSize == 0 || tableSize > (uint)table.Length ? (uint)table.Length : tableSize;

            WalkChain(table, firstEntry, end, entries, seen);

            if (extraStarts != null)
            {
                for (int i = 0; i < extraStarts.Count; i++)
                    WalkChain(table, extraStarts[i], end, entries, seen);
            }

            entries.Sort((a, b) => a.Offset.CompareTo(b.Offset));
            return entries;
        }

        private static void WalkChain(byte[] table, uint offset, uint end, List<Entry> entries, Dictionary<uint, bool> seen)
        {
            for (int count = 0; count < MAX_ENTRIES && offset != 0 && (ulong)offset + 16 <= end; count++)
            {
                if (seen.ContainsKey(offset))
                    return;

                uint size = Utils.ReadUInt32(table, offset + 12);
                if (size < 16 || (ulong)offset + size > end)
                    return;

                seen[offset] = true;
                entries.Add(new Entry
                {
                    Offset = offset,
                    GroupId = Utils.ReadUInt32(table, offset),
                    DataTypeId = Utils.ReadUInt32(table, offset + 4),
                    InstanceId = Utils.ReadUInt32(table, offset + 8),
                    Size = size,
                });

                offset += size;
            }
        }

        /// <summary>
        /// Decodes all the core map entries of the table (one per instance), the CCD map in preference to the CCX map.
        /// </summary>
        /// <param name="expectedThreads">Logical processors of the system (0 when unknown), to choose between layouts.</param>
        public static List<ApobCoreMap> Parse(byte[] table, List<Entry> entries, int expectedThreads)
        {
            var maps = new List<ApobCoreMap>();
            if (table == null || entries == null)
                return maps;

            foreach (uint type in new[] { CCD_MAP_TYPE, CCX_MAP_TYPE })
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    Entry entry = entries[i];
                    if (entry.GroupId != APOB_CCX_GROUP || entry.DataTypeId != type)
                        continue;

                    ApobCoreMap map = Decode(table, entry, expectedThreads);
                    if (map != null)
                        maps.Add(map);
                }

                if (maps.Count > 0)
                    break;
            }

            maps.Sort((a, b) => a.InstanceId.CompareTo(b.InstanceId));
            return maps;
        }

        /// <summary>Decodes a raw core map entry (header included), e.g. one saved in a debug report.</summary>
        public static ApobCoreMap Decode(byte[] rawEntry, int expectedThreads)
        {
            if (rawEntry == null || rawEntry.Length < TYPE_HEADER_SIZE)
                return null;

            var entry = new Entry
            {
                Offset = 0,
                GroupId = Utils.ReadUInt32(rawEntry, 0),
                DataTypeId = Utils.ReadUInt32(rawEntry, 4),
                InstanceId = Utils.ReadUInt32(rawEntry, 8),
                Size = Math.Min(Utils.ReadUInt32(rawEntry, 12), (uint)rawEntry.Length),
            };

            if (entry.GroupId != APOB_CCX_GROUP || (entry.DataTypeId != CCD_MAP_TYPE && entry.DataTypeId != CCX_MAP_TYPE))
                return null;

            return Decode(rawEntry, entry, expectedThreads);
        }

        private struct Layout
        {
            public int Ccds;
            public int Complexes;
            public int Cores;
            public int Threads;
            public int Size;
            public int Order;

            public int CoreSize { get { return 1 + Threads; } }
            public int ComplexSize { get { return 1 + Cores * CoreSize; } }
            public int CcdSize { get { return 1 + Complexes * ComplexSize; } }
        }

        private static ApobCoreMap Decode(byte[] table, Entry entry, int expectedThreads)
        {
            if (entry.Size <= TYPE_HEADER_SIZE)
                return null;

            int payloadOffset = (int)entry.Offset + TYPE_HEADER_SIZE;
            int payloadSize = (int)entry.Size - TYPE_HEADER_SIZE;
            bool ccdMap = entry.DataTypeId == CCD_MAP_TYPE;

            ApobCoreMap best = null;
            int bestScore = int.MinValue;

            foreach (Layout layout in CandidateLayouts(payloadSize, ccdMap))
            {
                ApobCoreMap map = TryDecode(table, payloadOffset, layout, ccdMap);
                if (map == null)
                    continue;

                // The structure is sized by its arrays, rounded up to 4 bytes (the header has 32-bit fields):
                // a layout that needs padding is less likely, and one that matches the thread count is much more
                int score = -(payloadSize - layout.Size) * 16 - layout.Order;
                if (expectedThreads > 0 && map.EnabledThreads == expectedThreads)
                    score += 1 << 20;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = map;
                    best.CcdSlots = layout.Ccds;
                    best.ComplexSlotsPerCcd = layout.Complexes;
                    best.CoreSlotsPerComplex = layout.Cores;
                    best.ThreadSlotsPerCore = layout.Threads;
                }
            }

            if (best == null)
                return null;

            best.DataTypeId = entry.DataTypeId;
            best.InstanceId = entry.InstanceId;
            best.EntryOffset = entry.Offset;
            best.EntrySize = entry.Size;
            best.RawEntry = new byte[entry.Size];
            Buffer.BlockCopy(table, (int)entry.Offset, best.RawEntry, 0, (int)entry.Size);
            return best;
        }

        private static IEnumerable<Layout> CandidateLayouts(int payloadSize, bool ccdMap)
        {
            int order = 0;
            foreach (int threads in ThreadCandidates)
            {
                foreach (int cores in CoreCandidates)
                {
                    foreach (int complexes in ComplexCandidates)
                    {
                        int maxCcds = ccdMap ? MAX_CCD_SLOTS : 1;
                        for (int ccds = 1; ccds <= maxCcds; ccds++)
                        {
                            var layout = new Layout { Ccds = ccds, Complexes = complexes, Cores = cores, Threads = threads, Order = order++ };
                            layout.Size = ccdMap ? ccds * layout.CcdSize : complexes * layout.ComplexSize;

                            // At most 3 bytes of padding up to the 4-byte alignment of the structure
                            if (layout.Size <= payloadSize && payloadSize - layout.Size < 4)
                                yield return layout;
                        }
                    }
                }
            }
        }

        // Decodes the payload with one layout; null when the content does not fit it
        private static ApobCoreMap TryDecode(byte[] table, int offset, Layout layout, bool ccdMap)
        {
            var map = new ApobCoreMap();
            var physicalCcdSeen = new bool[256];
            int logicalIndex = 0;

            for (int ccd = 0; ccd < layout.Ccds; ccd++)
            {
                int ccdOffset = offset + ccd * layout.CcdSize;
                int physicalCcd = 0;
                int complexOffset = ccdOffset;

                if (ccdMap)
                {
                    byte value = table[ccdOffset];
                    complexOffset = ccdOffset + 1;

                    if (value == NOT_PRESENT)
                    {
                        if (!IsEmptyCcd(table, complexOffset, layout))
                            return null;
                        continue;
                    }

                    if (value >= MAX_CCD_SLOTS || physicalCcdSeen[value])
                        return null;

                    physicalCcdSeen[value] = true;
                    physicalCcd = value;
                }

                var physicalComplexSeen = new bool[256];
                for (int ccx = 0; ccx < layout.Complexes; ccx++)
                {
                    int ccxOffset = complexOffset + ccx * layout.ComplexSize;
                    byte physicalCcx = table[ccxOffset];

                    if (physicalCcx == NOT_PRESENT)
                    {
                        if (!AreAbsentCores(table, ccxOffset + 1, layout))
                            return null;
                        continue;
                    }

                    if (physicalCcx >= 4 || physicalComplexSeen[physicalCcx])
                        return null;
                    physicalComplexSeen[physicalCcx] = true;

                    var physicalCoreSeen = new bool[256];
                    for (int core = 0; core < layout.Cores; core++)
                    {
                        int coreOffset = ccxOffset + 1 + core * layout.CoreSize;
                        byte physicalCore = table[coreOffset];

                        if (physicalCore == NOT_PRESENT)
                        {
                            if (!IsAbsentCore(table, coreOffset, layout))
                                return null;
                            continue;
                        }

                        if (physicalCore >= layout.Cores || physicalCoreSeen[physicalCore])
                            return null;
                        physicalCoreSeen[physicalCore] = true;

                        var threads = new bool[layout.Threads];
                        for (int t = 0; t < layout.Threads; t++)
                        {
                            byte enabled = table[coreOffset + 1 + t];
                            if (enabled > 1)
                                return null;
                            threads[t] = enabled == 1;
                        }

                        map.Cores.Add(new ApobCoreMapCore
                        {
                            LogicalIndex = logicalIndex++,
                            LogicalCcd = ccd,
                            LogicalCcx = ccx,
                            LogicalCore = core,
                            PhysicalCcd = physicalCcd,
                            PhysicalCcx = physicalCcx,
                            PhysicalCore = physicalCore,
                            ThreadEnabled = threads,
                        });
                    }
                }
            }

            return map.Cores.Count > 0 && map.EnabledThreads > 0 ? map : null;
        }

        // An absent core: 0xFF, thread flags 0 or 0xFF (the whole structure may have been filled with 0xFF)
        private static bool IsAbsentCore(byte[] table, int coreOffset, Layout layout)
        {
            for (int t = 0; t < layout.Threads; t++)
            {
                byte value = table[coreOffset + 1 + t];
                if (value != 0 && value != NOT_PRESENT)
                    return false;
            }
            return true;
        }

        // The cores of an absent complex: no thread flag set
        private static bool AreAbsentCores(byte[] table, int firstCoreOffset, Layout layout)
        {
            for (int core = 0; core < layout.Cores; core++)
            {
                if (!IsAbsentCore(table, firstCoreOffset + core * layout.CoreSize, layout))
                    return false;
            }
            return true;
        }

        // The complexes of an absent CCD hold no enabled core
        private static bool IsEmptyCcd(byte[] table, int complexOffset, Layout layout)
        {
            for (int ccx = 0; ccx < layout.Complexes; ccx++)
            {
                int ccxOffset = complexOffset + ccx * layout.ComplexSize;
                for (int core = 0; core < layout.Cores; core++)
                {
                    int coreOffset = ccxOffset + 1 + core * layout.CoreSize;
                    for (int t = 0; t < layout.Threads; t++)
                    {
                        if (table[coreOffset + 1 + t] == 1)
                            return false;
                    }
                }
            }
            return true;
        }
    }
}

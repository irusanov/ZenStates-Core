using System;
using System.Collections.Generic;

namespace ZenStates.Core.Hardware.Apob
{
    /// <summary>
    /// A timing of <see cref="ApobChannelTimingLayout"/>, relative to the start of the block, or to the clock bytes
    /// of a DDR4 block (<see cref="FromClocks"/>).
    /// </summary>
    public sealed class ApobTimingField
    {
        public ApobTimingField(string name, int offset, bool tentative = false, ApobValueWidth? width = null, bool fromClocks = false)
        {
            Name = name;
            Offset = offset;
            Tentative = tentative;
            Width = width;
            FromClocks = fromClocks;
        }

        public string Name { get; private set; }
        public int Offset { get; private set; }

        /// <summary>Width of the field, null for the width of the layout.</summary>
        public ApobValueWidth? Width { get; private set; }

        /// <summary>The offset is relative to the clock bytes found by the DDR4 search, not to the block.</summary>
        public bool FromClocks { get; private set; }

        /// <summary>
        /// The field is placed by elimination (another field had the same value on the dumps it was worked out
        /// from), it can be swapped with its neighbour.
        /// </summary>
        public bool Tentative { get; private set; }
    }

    /// <summary>
    /// Where the per channel timing blocks of the GEN configuration info entry are and what is in them. A block is
    /// found by its clocks: MEMCLK in range, the field after it at half MEMCLK (when the block has one), the data
    /// rate (when the block has one) at twice MEMCLK, and a sane CL.
    /// <para>
    /// DDR4 blocks keep the timings in clocks as bytes a few bytes after MEMCLK, the distance differs between
    /// programs: <see cref="ClockBytesSearchEnd"/> above 0 makes the search look for them between
    /// <see cref="ClockBytesSearchStart"/> and <see cref="ClockBytesSearchEnd"/> bytes after MEMCLK, in the DDR4
    /// order CL, CWL, RCD, RP, RTP, RAS, RC, WR, RRDS, WTRS, FAW, RRDL, WTRL.
    /// </para>
    /// </summary>
    public sealed class ApobChannelTimingLayout
    {
        public ApobChannelTimingLayout(string name, uint sourceGroupId, uint sourceDataTypeId, ApobValueWidth valueWidth,
            int dataRateOffset, int memClkOffset, int halfMemClkOffset, int minMemClk, int maxMemClk,
            int clOffset, int minCl, int maxCl, ApobTimingField[] fields,
            int extendedRecordOffset = -1, int extendedRecordStride = 0, int pStateBlockStride = 0,
            int clockBytesSearchStart = 0, int clockBytesSearchEnd = 0, int wckOffset = -1,
            int lpddr5ModeRegisterOffset = -1, int lpddr5ModeRegisterCopies = 0,
            int ddr4ModeRegisterSearchStart = 0, int ddr4ModeRegisterSearchEnd = 0)
        {
            Ddr4ModeRegisterSearchStart = ddr4ModeRegisterSearchStart;
            Ddr4ModeRegisterSearchEnd = ddr4ModeRegisterSearchEnd;
            Lpddr5ModeRegisterOffset = lpddr5ModeRegisterOffset;
            Lpddr5ModeRegisterCopies = lpddr5ModeRegisterOffset >= 0 ? lpddr5ModeRegisterCopies : 0;
            WckOffset = wckOffset;
            ClockBytesSearchStart = clockBytesSearchStart;
            ClockBytesSearchEnd = clockBytesSearchEnd;
            ExtendedRecordOffset = extendedRecordOffset;
            ExtendedRecordStride = extendedRecordStride;
            PStateBlockStride = pStateBlockStride;
            Name = name;
            SourceGroupId = sourceGroupId;
            SourceDataTypeId = sourceDataTypeId;
            ValueWidth = valueWidth;
            DataRateOffset = dataRateOffset;
            MemClkOffset = memClkOffset;
            HalfMemClkOffset = halfMemClkOffset;
            MinMemClk = minMemClk;
            MaxMemClk = maxMemClk;
            ClOffset = clOffset;
            MinCl = minCl;
            MaxCl = maxCl;
            Fields = fields;

            int width = ValueBytes;
            int span = Math.Max(Math.Max(Math.Max(clOffset, memClkOffset), Math.Max(dataRateOffset, halfMemClkOffset)), wckOffset) + width;
            if (clockBytesSearchEnd > 0)
                span = Math.Max(span, clockBytesSearchEnd + DDR4_CLOCK_BYTES);
            if (Lpddr5ModeRegisterCopies > 0)
                span = Math.Max(span, lpddr5ModeRegisterOffset + Lpddr5ModeRegisterCopies * ApobLpddr5ModeRegisters.Count);

            for (int i = 0; i < fields.Length; i++)
            {
                int end = (fields[i].FromClocks ? clockBytesSearchEnd : 0) + fields[i].Offset + WidthBytes(fields[i].Width ?? valueWidth);
                if (end > span)
                    span = end;
            }
            Span = span;
        }

        public string Name { get; private set; }
        public uint SourceGroupId { get; private set; }
        public uint SourceDataTypeId { get; private set; }

        /// <summary>Width of every field of the block.</summary>
        public ApobValueWidth ValueWidth { get; private set; }

        /// <summary>Offset of the data rate (MT/s), -1 when the block has none.</summary>
        public int DataRateOffset { get; private set; }

        public int MemClkOffset { get; private set; }

        /// <summary>
        /// Offset of the LPDDR5 WCK (MHz, 2 or 4 x MEMCLK), -1 when the block has none. The data rate is twice WCK; it
        /// replaces the data rate check.
        /// </summary>
        public int WckOffset { get; private set; }

        /// <summary>Offset of the field that holds half MEMCLK, used to find the block.</summary>
        public int HalfMemClkOffset { get; private set; }

        public int MinMemClk { get; private set; }
        public int MaxMemClk { get; private set; }
        public int ClOffset { get; private set; }
        public int MinCl { get; private set; }
        public int MaxCl { get; private set; }
        public ApobTimingField[] Fields { get; private set; }

        /// <summary>
        /// Offset of the extended copy of the ODT / drive strength record from the first timing block of the
        /// channel, -1 when not known. The record is read with the extended layout of the profile.
        /// </summary>
        public int ExtendedRecordOffset { get; private set; }

        /// <summary>Distance between the extended records of the memory P-states of a channel.</summary>
        public int ExtendedRecordStride { get; private set; }

        /// <summary>
        /// Distance between the timing blocks of the memory P-states of a channel, 0 when there is one block per
        /// channel. A block at another distance from the previous one starts the next channel.
        /// </summary>
        public int PStateBlockStride { get; private set; }

        /// <summary>
        /// First and last distance after MEMCLK to look for the DDR4 clock bytes at, 0 when the block has the
        /// fields at fixed offsets.
        /// </summary>
        public int ClockBytesSearchStart { get; private set; }
        public int ClockBytesSearchEnd { get; private set; }

        /// <summary>
        /// Offset of the LPDDR5 mode registers (<see cref="ApobLpddr5ModeRegisters"/>) from the start of the block, -1
        /// when the block has none. <see cref="Lpddr5ModeRegisterCopies"/> sets follow each other there.
        /// </summary>
        public int Lpddr5ModeRegisterOffset { get; private set; }
        public int Lpddr5ModeRegisterCopies { get; private set; }

        /// <summary>
        /// First and last distance after the DDR4 clock bytes to look for the DDR4 mode registers
        /// (<see cref="ApobDdr4ModeRegisters"/>) at, 0 when the blocks have none.
        /// </summary>
        public int Ddr4ModeRegisterSearchStart { get; private set; }
        public int Ddr4ModeRegisterSearchEnd { get; private set; }

        public int ValueBytes
        {
            get { return WidthBytes(ValueWidth); }
        }

        /// <summary>Bytes from the start of the block to the end of the last field.</summary>
        public int Span { get; private set; }

        // CL, CWL, RCD, RP, RTP, RAS, RC, WR, RRDS, WTRS, FAW, RRDL, WTRL, CCDL
        internal const int DDR4_CLOCK_BYTES = 14;

        private static int WidthBytes(ApobValueWidth width)
        {
            switch (width)
            {
                case ApobValueWidth.UInt8: return 1;
                case ApobValueWidth.UInt32: return 4;
                default: return 2;
            }
        }

        internal uint Read(byte[] buffer, uint blockOffset, int fieldOffset)
        {
            return Read(buffer, blockOffset + (uint)fieldOffset, ValueWidth);
        }

        internal uint ReadField(byte[] buffer, uint blockOffset, uint clockBytesOffset, ApobTimingField field)
        {
            uint o = (field.FromClocks ? clockBytesOffset : blockOffset) + (uint)field.Offset;
            return Read(buffer, o, field.Width ?? ValueWidth);
        }

        private static uint Read(byte[] buffer, uint offset, ApobValueWidth width)
        {
            switch (width)
            {
                case ApobValueWidth.UInt8: return buffer[offset];
                case ApobValueWidth.UInt32: return ApobBytes.U32(buffer, offset);
                default: return ApobBytes.U16(buffer, offset);
            }
        }

        /// <summary>
        /// The DDR4 clock bytes at <paramref name="offset"/> are plausible: CL in range, CWL not above CL, RAS above
        /// RCD, RC above RAS, and the S / L pairs in order.
        /// </summary>
        internal bool IsDdr4ClockBytes(byte[] buffer, uint offset, ulong end)
        {
            if ((ulong)offset + DDR4_CLOCK_BYTES > end)
                return false;

            int cl = buffer[offset], cwl = buffer[offset + 1], rcd = buffer[offset + 2], rp = buffer[offset + 3];
            int rtp = buffer[offset + 4], ras = buffer[offset + 5], rc = buffer[offset + 6], wr = buffer[offset + 7];
            int rrds = buffer[offset + 8], wtrs = buffer[offset + 9], faw = buffer[offset + 10];
            int rrdl = buffer[offset + 11], wtrl = buffer[offset + 12];

            return cl >= MinCl && cl <= MaxCl && cwl >= 5 && cwl <= cl &&
                rcd >= 5 && rcd <= MaxCl && rp >= 5 && rp <= MaxCl && rtp >= 2 && rtp <= 32 &&
                ras > rcd && rc > ras && wr >= 5 && wr <= 64 &&
                rrds >= 2 && rrds <= 16 && wtrs >= 1 && wtrs <= 16 && faw >= rrds && faw <= 96 &&
                rrdl >= rrds && wtrl >= wtrs;
        }
    }

    /// <summary>A decoded value of <see cref="ApobChannelTimings"/>.</summary>
    public sealed class ApobTimingValue
    {
        public ApobTimingField Field { get; internal set; }
        public uint Value { get; internal set; }

        public override string ToString()
        {
            return Field.Name + (Field.Tentative ? "?" : "") + "=" + Value;
        }
    }

    /// <summary>
    /// The timings the ABL worked out for a memory channel, from the GEN configuration info entry (GEN group,
    /// type 3). The values are clocks except the clock fields.
    /// </summary>
    /// <remarks>
    /// There is a block per channel, and on APUs with memory P-states a block per P-state of each channel (Phoenix:
    /// MEMCLK 2400, 2400 and 1000 for each of its 2 channels). On Phoenix the RFC1 of the block (708) is not what
    /// the UMC is programmed with (384, the RFC2 value).
    /// </remarks>
    public sealed class ApobChannelTimings
    {
        internal ApobChannelTimings()
        {
            Values = new List<ApobTimingValue>();
        }

        /// <summary>Index of the block in the entry: channel order, P-states of a channel next to each other.</summary>
        public int Index { get; internal set; }

        /// <summary>The channel of the block.</summary>
        public int Channel { get; internal set; }

        /// <summary>The memory P-state of the block, 0 on CPUs with one block per channel.</summary>
        public int PState { get; internal set; }

        /// <summary>
        /// The block is for the MEMCLK the memory runs at (<see cref="Apob.ActiveMemClk"/>), false when that is
        /// not known.
        /// </summary>
        public bool IsActive { get; internal set; }

        /// <summary>
        /// The extended copy of the ODT / drive strength record of this channel and P-state, null when the layout
        /// does not place it.
        /// </summary>
        public ApobData ExtendedData { get; internal set; }

        /// <summary>Offset of <see cref="ExtendedData"/> relative to the start of the entry.</summary>
        public uint ExtendedDataOffset { get; internal set; }

        /// <summary>Offset of the block relative to the start of the entry.</summary>
        public uint EntryRelativeOffset { get; internal set; }

        /// <summary>Data rate in MT/s.</summary>
        public uint DataRate { get; internal set; }

        /// <summary>MEMCLK in MHz.</summary>
        public uint MemClk { get; internal set; }

        public List<ApobTimingValue> Values { get; private set; }

        /// <summary>
        /// The LPDDR5 mode registers of the block, one set per copy the layout has (two on Rembrandt, equal on the
        /// dumps seen so far). Empty when the layout has none or they do not decode.
        /// </summary>
        public List<ApobLpddr5ModeRegisters> Lpddr5ModeRegisterSets { get; private set; } = new List<ApobLpddr5ModeRegisters>();

        /// <summary>DDR4: the mode registers MR0 to MR6 of the block, null when the layout has none or they are not found.</summary>
        public ApobDdr4ModeRegisters Ddr4ModeRegisters { get; internal set; }

        /// <summary>The first set of <see cref="Lpddr5ModeRegisterSets"/>, null when there is none.</summary>
        public ApobLpddr5ModeRegisters Lpddr5ModeRegisters
        {
            get { return Lpddr5ModeRegisterSets.Count > 0 ? Lpddr5ModeRegisterSets[0] : null; }
        }

        /// <summary>The value of the named field, null when the layout has no such field.</summary>
        public uint? Get(string name)
        {
            for (int i = 0; i < Values.Count; i++)
            {
                if (string.Equals(Values[i].Field.Name, name, StringComparison.OrdinalIgnoreCase))
                    return Values[i].Value;
            }
            return null;
        }

        public uint? Tcas { get { return Get("Tcas"); } }
        /// <summary>DDR4 has one tRCD, DDR5 a read and a write one.</summary>
        public uint? Trcd { get { return Get("Trcd"); } }
        public uint? Trcdrd { get { return Get("Trcdrd"); } }
        public uint? Trcdwr { get { return Get("Trcdwr"); } }
        public uint? Trp { get { return Get("Trp"); } }
        public uint? Tras { get { return Get("Tras"); } }
        public uint? Trc { get { return Get("Trc"); } }
        public uint? Twr { get { return Get("Twr"); } }
        public uint? Trtp { get { return Get("Trtp"); } }
        public uint? Trefi { get { return Get("Trefi"); } }
        public uint? Trfc { get { return Get("Trfc"); } }
        public uint? Trfc2 { get { return Get("Trfc2"); } }
        public uint? Trfcsb { get { return Get("Trfcsb"); } }
        public uint? Tccdl { get { return Get("Tccdl"); } }
        public uint? Tccdlwr { get { return Get("Tccdlwr"); } }
        public uint? Tccdlwr2 { get { return Get("Tccdlwr2"); } }

        public override string ToString()
        {
            return string.Format("Block {0}: {1} MT/s, CAS {2}", Index, DataRate, Tcas);
        }

        /// <summary>
        /// Finds the timing blocks in the entry at <paramref name="entryOffset"/> of <paramref name="buffer"/>, in
        /// order. Empty when there are none or the entry is not complete.
        /// </summary>
        internal static List<ApobChannelTimings> Read(byte[] buffer, uint entryOffset, ApobChannelTimingLayout layout, int maxChannels = 16)
        {
            var result = new List<ApobChannelTimings>();
            if (layout == null || !ApobBytes.TryGetData(buffer, entryOffset, out uint data, out uint size))
                return result;

            ulong end = (ulong)data + size;
            // Blocks are at least 2-byte aligned relative to the entry
            uint o = data;
            while ((ulong)o + (ulong)layout.Span <= end && result.Count < maxChannels)
            {
                // ulong: the fields are arbitrary data until the block is found
                ulong clk = layout.Read(buffer, o, layout.MemClkOffset);
                ulong half = layout.HalfMemClkOffset >= 0 ? layout.Read(buffer, o, layout.HalfMemClkOffset) : clk / 2;
                ulong rate = layout.DataRateOffset >= 0 ? layout.Read(buffer, o, layout.DataRateOffset) : clk * 2;
                bool rateOk = rate == clk * 2;
                if (layout.WckOffset >= 0)
                {
                    ulong wck = layout.Read(buffer, o, layout.WckOffset);
                    rateOk = clk > 0 && (wck == clk * 2 || wck == clk * 4);
                    rate = wck * 2;
                }

                uint clocks = o;
                bool found = false;

                if (clk >= (ulong)layout.MinMemClk && clk <= (ulong)layout.MaxMemClk && half * 2 == clk - (clk & 1) && rateOk)
                {
                    if (layout.ClockBytesSearchEnd > 0)
                    {
                        // DDR4: the clock bytes follow MEMCLK at a program dependent distance
                        for (int gap = layout.ClockBytesSearchStart; gap <= layout.ClockBytesSearchEnd && !found; gap++)
                        {
                            uint candidate = o + (uint)layout.MemClkOffset + (uint)gap;
                            if (layout.IsDdr4ClockBytes(buffer, candidate, end))
                            {
                                clocks = candidate;
                                found = true;
                            }
                        }
                    }
                    else
                    {
                        ulong cas = layout.Read(buffer, o, layout.ClOffset);
                        found = cas >= (ulong)layout.MinCl && cas <= (ulong)layout.MaxCl;
                    }
                }

                if (found)
                {
                    var timings = new ApobChannelTimings
                    {
                        Index = result.Count,
                        Channel = 0,
                        PState = 0,
                        EntryRelativeOffset = o - entryOffset,
                        DataRate = (uint)rate,
                        MemClk = (uint)clk,
                    };

                    for (int i = 0; i < layout.Fields.Length; i++)
                    {
                        ApobTimingField field = layout.Fields[i];
                        timings.Values.Add(new ApobTimingValue
                        {
                            Field = field,
                            Value = layout.ReadField(buffer, o, clocks, field),
                        });
                    }

                    // LPDDR5: the mode registers, checked against the WCK ratio of the block
                    for (int copy = 0; copy < layout.Lpddr5ModeRegisterCopies && clk > 0; copy++)
                    {
                        uint mrOffset = o + (uint)layout.Lpddr5ModeRegisterOffset + (uint)(copy * ApobLpddr5ModeRegisters.Count);
                        ApobLpddr5ModeRegisters registers = ApobLpddr5ModeRegisters.TryRead(buffer, mrOffset, end, (int)(rate / 2 / clk));
                        if (registers == null)
                            break;
                        timings.Lpddr5ModeRegisterSets.Add(registers);
                    }

                    // DDR4: the mode registers, found by the timings they encode
                    if (layout.Ddr4ModeRegisterSearchEnd > 0 && layout.ClockBytesSearchEnd > 0)
                    {
                        timings.Ddr4ModeRegisters = ApobDdr4ModeRegisters.Find(buffer,
                            clocks + (uint)layout.Ddr4ModeRegisterSearchStart, clocks + (uint)layout.Ddr4ModeRegisterSearchEnd, end,
                            timings.Tcas, timings.Get("Tcwl"), timings.Twr, timings.Tccdl);
                    }

                    if (result.Count > 0)
                    {
                        ApobChannelTimings previous = result[result.Count - 1];
                        bool samechannel = layout.PStateBlockStride > 0 &&
                            timings.EntryRelativeOffset - previous.EntryRelativeOffset == (uint)layout.PStateBlockStride;
                        timings.Channel = samechannel ? previous.Channel : previous.Channel + 1;
                        timings.PState = samechannel ? previous.PState + 1 : 0;
                    }

                    result.Add(timings);
                    o = layout.ClockBytesSearchEnd > 0 ? clocks + ApobChannelTimingLayout.DDR4_CLOCK_BYTES : o + (uint)layout.Span;
                    if ((o & 1) != (entryOffset & 1))
                        o++;
                    continue;
                }

                o += 2;
            }

            return result;
        }
    }
}

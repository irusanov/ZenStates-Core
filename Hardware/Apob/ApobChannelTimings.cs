using System;
using System.Collections.Generic;

namespace ZenStates.Core.Hardware.Apob
{
    /// <summary>A timing of <see cref="ApobChannelTimingLayout"/>, relative to the start of the block.</summary>
    public sealed class ApobTimingField
    {
        public ApobTimingField(string name, int offset, bool tentative = false)
        {
            Name = name;
            Offset = offset;
            Tentative = tentative;
        }

        public string Name { get; private set; }
        public int Offset { get; private set; }

        /// <summary>
        /// The field is placed by elimination (another field had the same value on the dumps it was worked out
        /// from), it can be swapped with its neighbour.
        /// </summary>
        public bool Tentative { get; private set; }
    }

    /// <summary>
    /// Where the per channel timing blocks of the GEN configuration info entry are and what is in them. A block is
    /// found by its clocks: MEMCLK in range, the field after it at half MEMCLK, the data rate (when the block has
    /// one) at twice MEMCLK, and a sane CL.
    /// </summary>
    public sealed class ApobChannelTimingLayout
    {
        public ApobChannelTimingLayout(string name, uint sourceGroupId, uint sourceDataTypeId, ApobValueWidth valueWidth,
            int dataRateOffset, int memClkOffset, int halfMemClkOffset, int minMemClk, int maxMemClk,
            int clOffset, int minCl, int maxCl, ApobTimingField[] fields,
            int extendedRecordOffset = -1, int extendedRecordStride = 0, int pStateBlockStride = 0)
        {
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
            int span = Math.Max(Math.Max(clOffset, memClkOffset), Math.Max(dataRateOffset, halfMemClkOffset)) + width;
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].Offset + width > span)
                    span = fields[i].Offset + width;
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

        public int ValueBytes
        {
            get { return ValueWidth == ApobValueWidth.UInt32 ? 4 : 2; }
        }

        /// <summary>Bytes from the start of the block to the end of the last field.</summary>
        public int Span { get; private set; }

        internal uint Read(byte[] buffer, uint blockOffset, int fieldOffset)
        {
            uint o = blockOffset + (uint)fieldOffset;
            return ValueWidth == ApobValueWidth.UInt32 ? ApobBytes.U32(buffer, o) : ApobBytes.U16(buffer, o);
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

        public uint? Cas { get { return Get("Cas"); } }
        public uint? RcdRd { get { return Get("RcdRd"); } }
        public uint? RcdWr { get { return Get("RcdWr"); } }
        public uint? Rp { get { return Get("Rp"); } }
        public uint? Ras { get { return Get("Ras"); } }
        public uint? Rc { get { return Get("Rc"); } }
        public uint? Wr { get { return Get("Wr"); } }
        public uint? Rtp { get { return Get("Rtp"); } }
        public uint? Refi { get { return Get("Refi"); } }
        public uint? Rfc1 { get { return Get("Rfc1"); } }
        public uint? Rfc2 { get { return Get("Rfc2"); } }
        public uint? RfcSb { get { return Get("RfcSb"); } }
        public uint? Ccdl { get { return Get("Ccdl"); } }
        public uint? CcdlWr { get { return Get("CcdlWr"); } }
        public uint? CcdlWr2 { get { return Get("CcdlWr2"); } }

        public override string ToString()
        {
            return string.Format("Block {0}: {1} MT/s, CAS {2}", Index, DataRate, Cas);
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
                ulong half = layout.Read(buffer, o, layout.HalfMemClkOffset);
                ulong rate = layout.DataRateOffset >= 0 ? layout.Read(buffer, o, layout.DataRateOffset) : clk * 2;
                ulong cas = layout.Read(buffer, o, layout.ClOffset);

                if (clk >= (ulong)layout.MinMemClk && clk <= (ulong)layout.MaxMemClk && half * 2 == clk && rate == clk * 2 &&
                    cas >= (ulong)layout.MinCl && cas <= (ulong)layout.MaxCl)
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
                            Value = layout.Read(buffer, o, field.Offset),
                        });
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
                    o += (uint)layout.Span;
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

using System;
using System.Collections.Generic;

namespace ZenStates.Core.Hardware.Apob
{
    /// <summary>
    /// The SPD of one memory slot as the ABL used it, from the DIMM SPD data entry (MEM group, type 17). On boards with
    /// soldered LPDDR5 this is the only copy of the SPD: it comes from the BIOS image, there is no SPD device.
    /// </summary>
    public sealed class ApobDimmSpd
    {
        public int Socket { get; internal set; }
        public int Channel { get; internal set; }
        public int Dimm { get; internal set; }

        /// <summary>SMBus address the ABL read the SPD from, 0 for memory down.</summary>
        public uint SmbusAddress { get; internal set; }

        /// <summary>The SPD bytes: 512 for DDR4 / LPDDR4 / memory-down LPDDR5, 1024 for DDR5.</summary>
        public byte[] Data { get; internal set; }

        /// <summary>The SPD key byte (byte 2): 0x0C DDR4, 0x12 DDR5, 0x13 LPDDR5, 0x15 LPDDR5X.</summary>
        public byte DeviceType
        {
            get { return Data != null && Data.Length > 2 ? Data[2] : (byte)0; }
        }

        /// <summary>The slot name, e.g. "A1".</summary>
        public string SlotName
        {
            get { return string.Format("{0}{1}", (char)('A' + Channel), Dimm + 1); }
        }

        public override string ToString()
        {
            return string.Format("{0} (socket {1}, channel {2}, DIMM {3}): type 0x{4:X2}, {5} bytes", SlotName, Socket, Channel, Dimm,
                DeviceType, Data != null ? Data.Length : 0);
        }
    }

    /// <summary>
    /// Decodes the DIMM SPD data entry (MEM group, type 17): MaxDimmsPerChannel and MaxChannelsPerSocket bytes, then a
    /// record per slot, in one of two layouts (openSIL ApobCmn.h):
    /// <list type="bullet">
    /// <item>APOB_SPD_STRUCT, 532 bytes: socket, channel, DIMM, page, present, mux present, mux address, mux channel
    /// bytes, SMBus address, serial number and flags (u32 each), 512 SPD bytes. DDR4 (Summit Ridge, Matisse) and the
    /// LPDDR5 of Rembrandt, whose entry is sized for the DDR5 records but filled with these.</item>
    /// <item>APOB_D5_SPD_STRUCT, 1036 bytes: DRAM down valid, present, 2 padding bytes, SMBus address (u32), socket,
    /// channel, DIMM, shadow valid, 1024 SPD bytes. DDR5 (Phoenix).</item>
    /// </list>
    /// The layout is the one whose present records hold a known SPD key byte.
    /// </summary>
    public static class ApobDimmSpdParser
    {
        private const uint RECORDS_OFFSET = 4;
        private const uint SPD_STRUCT_SIZE = 532;
        private const uint SPD_STRUCT_DATA = 20;
        private const uint D5_SPD_STRUCT_SIZE = 1036;
        private const uint D5_SPD_STRUCT_DATA = 12;

        /// <summary>The present slots of the entry at <paramref name="entryOffset"/>; empty when none or not decodable.</summary>
        public static List<ApobDimmSpd> Decode(byte[] buffer, uint entryOffset)
        {
            var empty = new List<ApobDimmSpd>();
            if (!ApobBytes.TryGetData(buffer, entryOffset, out uint data, out uint size) || size < RECORDS_OFFSET)
                return empty;

            int slots = buffer[data] * buffer[data + 1];
            if (slots <= 0)
                return empty;

            List<ApobDimmSpd> ddr4 = DecodeRecords(buffer, data, size, slots, false);
            List<ApobDimmSpd> ddr5 = DecodeRecords(buffer, data, size, slots, true);
            return ddr5.Count > ddr4.Count ? ddr5 : ddr4;
        }

        private static List<ApobDimmSpd> DecodeRecords(byte[] buffer, uint data, uint size, int slots, bool d5)
        {
            var result = new List<ApobDimmSpd>();
            uint recordSize = d5 ? D5_SPD_STRUCT_SIZE : SPD_STRUCT_SIZE;
            uint spdOffset = d5 ? D5_SPD_STRUCT_DATA : SPD_STRUCT_DATA;
            uint spdSize = recordSize - spdOffset;

            for (int i = 0; i < slots; i++)
            {
                uint record = data + RECORDS_OFFSET + (uint)i * recordSize;
                if ((ulong)record + recordSize > (ulong)data + size)
                    break;

                byte present = d5 ? buffer[record + 1] : buffer[record + 4];
                if (present > 1)
                    return new List<ApobDimmSpd>();     // not this layout
                if (present == 0)
                    continue;

                byte[] spd = new byte[spdSize];
                Buffer.BlockCopy(buffer, (int)(record + spdOffset), spd, 0, (int)spdSize);
                if (!IsKnownDeviceType(spd[2]))
                    return new List<ApobDimmSpd>();

                result.Add(new ApobDimmSpd
                {
                    Socket = d5 ? buffer[record + 8] : buffer[record],
                    Channel = d5 ? buffer[record + 9] : buffer[record + 1],
                    Dimm = d5 ? buffer[record + 10] : buffer[record + 2],
                    SmbusAddress = ApobBytes.U32(buffer, record + (d5 ? 4u : 8u)),
                    Data = spd,
                });
            }

            return result;
        }

        // DDR4, LPDDR4, LPDDR4X, DDR5, LPDDR5, DDR5 NVDIMM-P, LPDDR5X
        private static bool IsKnownDeviceType(byte keyByte)
        {
            return keyByte == 0x0C || (keyByte >= 0x10 && keyByte <= 0x15);
        }

        /// <summary>
        /// Clears the module serial numbers of a raw copy of the entry: the record field (DDR4 layout) and the SPD bytes
        /// 325~328 (512-byte SPD) or 517~520 (1024-byte SPD).
        /// </summary>
        public static void MaskSerialNumbers(byte[] rawEntry)
        {
            if (!ApobBytes.TryGetData(rawEntry, 0, out uint data, out uint size) || size < RECORDS_OFFSET)
                return;

            int slots = rawEntry[data] * rawEntry[data + 1];
            bool d5 = DecodeRecords(rawEntry, data, size, slots, true).Count > DecodeRecords(rawEntry, data, size, slots, false).Count;
            uint recordSize = d5 ? D5_SPD_STRUCT_SIZE : SPD_STRUCT_SIZE;
            uint spd = d5 ? D5_SPD_STRUCT_DATA : SPD_STRUCT_DATA;
            uint serial = d5 ? 517u : 325u;

            for (int i = 0; i < slots; i++)
            {
                uint record = data + RECORDS_OFFSET + (uint)i * recordSize;
                if ((ulong)record + recordSize > (ulong)data + size)
                    break;

                if (!d5)
                    Array.Clear(rawEntry, (int)(record + 12), 4);
                Array.Clear(rawEntry, (int)(record + spd + serial), 4);
            }
        }
    }
}

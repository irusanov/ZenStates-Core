using System;
using System.Collections.Generic;
using ZenStates.Core.Hardware.DRAM;

namespace ZenStates.Core.Hardware.Apob
{
    /// <summary>A DIMM slot of <see cref="ApobBootInfo"/>: the module the ABL saw at the last training.</summary>
    public sealed class ApobBootDimm
    {
        /// <summary>Index of the slot in the table (channel * 2 + DIMM on a 2 DIMM per channel board).</summary>
        public int Slot { get; internal set; }

        /// <summary>JEDEC ID of the DRAM manufacturer: continuation byte low, code high (SPD order).</summary>
        public ushort DramManufacturerId { get; internal set; }

        /// <summary>JEDEC ID of the module manufacturer: continuation byte low, code high (SPD order).</summary>
        public ushort ModuleManufacturerId { get; internal set; }

        public uint SerialNumber { get; internal set; }

        public bool Present { get; internal set; }

        public string DramManufacturer
        {
            get { return LookupManufacturer(DramManufacturerId); }
        }

        public string ModuleManufacturer
        {
            get { return LookupManufacturer(ModuleManufacturerId); }
        }

        /// <summary>The slot name, e.g. "A2", for 2 DIMMs per channel.</summary>
        public string SlotName
        {
            get { return string.Format("{0}{1}", (char)('A' + Slot / 2), Slot % 2 + 1); }
        }

        private static string LookupManufacturer(ushort id)
        {
            if (id == 0)
                return "N/A";

            try
            {
                return ManufacturerMapping.Lookup(unchecked((byte)id), unchecked((byte)(id >> 8)));
            }
            catch
            {
                return string.Format("0x{0:X4}", id);
            }
        }
    }

    /// <summary>
    /// APOB_APCB_BOOT_INFO (MEM group, type 16): the APCB instance, the fingerprint of the DIMMs and the date of
    /// the last memory training.
    /// </summary>
    /// <remarks>
    /// Data layout (verified on a Granite Ridge dump): ApcbActiveInstance u32, then a 12-byte record per slot
    /// (DRAM manufacturer u16, module manufacturer u16, serial number u32, present u8, 3 reserved), then the
    /// DimmConfigurationUpdated, ApcbRecoveryFlag, ActionOnBistFailure and WorkloadProfile bytes, the
    /// LastPmuTrainTime (day, month, year, century bytes) and 12 bytes of unknown use.
    /// </remarks>
    public sealed class ApobBootInfo
    {
        private const uint SLOT_SIZE = 12;
        private const uint TRAILER_SIZE = 20;

        public uint ApcbActiveInstance { get; private set; }
        public List<ApobBootDimm> Dimms { get; private set; }

        public byte DimmConfigurationUpdated { get; private set; }
        public byte ApcbRecoveryFlag { get; private set; }
        public byte ActionOnBistFailure { get; private set; }
        public byte WorkloadProfile { get; private set; }

        /// <summary>
        /// LastPmuTrainTime as stored: day, month, year and century bytes, low to high. The century byte is 0 on
        /// Phoenix.
        /// </summary>
        public uint LastTrainingTimeRaw { get; private set; }

        /// <summary>The date of the last full memory training, null when not set or not a date.</summary>
        public DateTime? LastTrainingDate
        {
            get
            {
                // Binary on Zen 4 / Zen 5, BCD on Zen 1 to Zen 3 (0x24 0x09 0x26 = 2026-09-24)
                DateTime? binary = ToDate(LastTrainingTimeRaw, false);
                if (binary.HasValue && binary.Value.Year <= DateTime.Now.Year + 1)
                    return binary;

                DateTime? bcd = ToDate(LastTrainingTimeRaw, true);
                return bcd ?? binary;
            }
        }

        private static DateTime? ToDate(uint raw, bool bcd)
        {
            int[] parts = new int[4];
            for (int i = 0; i < 4; i++)
            {
                int b = (int)((raw >> (8 * i)) & 0xFF);
                if (bcd)
                {
                    if ((b & 0xF) > 9 || (b >> 4) > 9)
                        return null;
                    b = (b >> 4) * 10 + (b & 0xF);
                }
                parts[i] = b;
            }

            int day = parts[0], month = parts[1];
            int year = (parts[3] == 0 ? 20 : parts[3]) * 100 + parts[2];

            if (year < 2000 || year > 2200 || month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
                return null;

            return new DateTime(year, month, day);
        }

        /// <summary>The 12 bytes after LastPmuTrainTime, of unknown use (LastPartSerialNum / reserved in openSIL).</summary>
        public uint Unknown0 { get; private set; }
        public uint Unknown1 { get; private set; }
        public uint Unknown2 { get; private set; }

        /// <summary>Decodes the entry at <paramref name="entryOffset"/>, null when the size does not fit the layout.</summary>
        public static ApobBootInfo Decode(byte[] buffer, uint entryOffset)
        {
            if (!ApobBytes.TryGetData(buffer, entryOffset, out uint data, out uint size))
                return null;

            if (size < 4 + SLOT_SIZE + TRAILER_SIZE || (size - 4 - TRAILER_SIZE) % SLOT_SIZE != 0)
                return null;

            uint slots = (size - 4 - TRAILER_SIZE) / SLOT_SIZE;
            var info = new ApobBootInfo
            {
                ApcbActiveInstance = ApobBytes.U32(buffer, data),
                Dimms = new List<ApobBootDimm>(),
            };

            for (uint i = 0; i < slots; i++)
            {
                uint o = data + 4 + i * SLOT_SIZE;
                info.Dimms.Add(new ApobBootDimm
                {
                    Slot = (int)i,
                    DramManufacturerId = ApobBytes.U16(buffer, o),
                    ModuleManufacturerId = ApobBytes.U16(buffer, o + 2),
                    SerialNumber = ApobBytes.U32(buffer, o + 4),
                    Present = buffer[o + 8] != 0,
                });
            }

            // Zen 2 / Zen 3 have something else in front of the trailer: no DIMMs when a slot does not look like one
            for (int i = 0; i < info.Dimms.Count; i++)
            {
                uint o = data + 4 + (uint)i * SLOT_SIZE;
                ApobBootDimm dimm = info.Dimms[i];
                // The bytes after the present flag are not always 0 (Summit Ridge)
                bool valid = buffer[o + 8] <= 1 &&
                    (!dimm.Present || (dimm.DramManufacturerId != 0 && dimm.ModuleManufacturerId != 0));
                if (!valid)
                {
                    info.Dimms.Clear();
                    break;
                }
            }

            uint t = data + 4 + slots * SLOT_SIZE;
            info.DimmConfigurationUpdated = buffer[t];
            info.ApcbRecoveryFlag = buffer[t + 1];
            info.ActionOnBistFailure = buffer[t + 2];
            info.WorkloadProfile = buffer[t + 3];
            info.LastTrainingTimeRaw = ApobBytes.U32(buffer, t + 4);
            info.Unknown0 = ApobBytes.U32(buffer, t + 8);
            info.Unknown1 = ApobBytes.U32(buffer, t + 12);
            info.Unknown2 = ApobBytes.U32(buffer, t + 16);
            return info;
        }

        /// <summary>
        /// Zeroes the serial numbers in a raw copy of the entry (header included), for reports that do not print
        /// serial numbers.
        /// </summary>
        internal static void MaskSerialNumbers(byte[] rawEntry)
        {
            if (!ApobBytes.TryGetData(rawEntry, 0, out uint data, out uint size))
                return;
            if (size < 4 + SLOT_SIZE + TRAILER_SIZE || (size - 4 - TRAILER_SIZE) % SLOT_SIZE != 0)
                return;

            // Nothing to hide when the slots are not DIMM records (Zen 2 / Zen 3)
            ApobBootInfo info = Decode(rawEntry, 0);
            if (info == null || info.Dimms.Count == 0)
                return;

            uint slots = (size - 4 - TRAILER_SIZE) / SLOT_SIZE;
            for (uint i = 0; i < slots; i++)
            {
                uint o = data + 4 + i * SLOT_SIZE + 4;
                for (uint b = 0; b < 4; b++)
                    rawEntry[o + b] = 0;
            }
        }
    }
}

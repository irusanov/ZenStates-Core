namespace ZenStates.Core.Hardware.Apob
{
    /// <summary>
    /// MEM group, type 28 (APOB_MEM_RMP_INFO in the AGESA type list): on Granite Ridge it holds the memory
    /// profile (EXPO) the ABL picked up, with a copy of the EXPO block of the SPD.
    /// </summary>
    /// <remarks>
    /// Tentative, decoded from a single dump: u16 values 1, 1, 1, then MEMCLK 3200, VDD 1450 mV and CL 28 (the
    /// EXPO profile 1 of the modules: tCK 312 ps, tAA 8736 ps, 1.45 V), then from data offset 0x1C a copy of the
    /// SPD EXPO block (bytes 832 and on: "EXPO", revision, profile bits, then the profiles).
    /// </remarks>
    public sealed class ApobMemoryProfileInfo
    {
        private const uint EXPO_OFFSET = 0x1C;
        private const uint EXPO_SIGNATURE = 0x4F505845; // "EXPO"
        private const uint EXPO_PROFILE1_OFFSET = 10;

        /// <summary>The three u16 values at the start of the data (1, 1, 1 with EXPO enabled), meaning unknown.</summary>
        public ushort Flags0 { get; private set; }
        public ushort Flags1 { get; private set; }
        public ushort Flags2 { get; private set; }

        /// <summary>MEMCLK of the profile in MHz.</summary>
        public ushort MemClk { get; private set; }

        /// <summary>VDD of the profile in mV.</summary>
        public ushort VddMv { get; private set; }

        /// <summary>CAS latency of the profile.</summary>
        public ushort Cas { get; private set; }

        public bool HasExpoBlock { get; private set; }

        /// <summary>EXPO revision byte (0x10 = 1.0).</summary>
        public byte ExpoRevision { get; private set; }

        /// <summary>EXPO profile enable bits (bit 0 = profile 1).</summary>
        public byte ExpoProfileBits { get; private set; }

        /// <summary>tCKAVGmin of EXPO profile 1 in ps.</summary>
        public ushort ExpoProfile1TckPs { get; private set; }

        /// <summary>tAAmin of EXPO profile 1 in ps.</summary>
        public ushort ExpoProfile1TaaPs { get; private set; }

        public static ApobMemoryProfileInfo Decode(byte[] buffer, uint entryOffset)
        {
            if (!ApobBytes.TryGetData(buffer, entryOffset, out uint data, out uint size) || size < 12)
                return null;

            var info = new ApobMemoryProfileInfo
            {
                Flags0 = ApobBytes.U16(buffer, data),
                Flags1 = ApobBytes.U16(buffer, data + 2),
                Flags2 = ApobBytes.U16(buffer, data + 4),
                MemClk = ApobBytes.U16(buffer, data + 6),
                VddMv = ApobBytes.U16(buffer, data + 8),
                Cas = ApobBytes.U16(buffer, data + 10),
            };

            if (size >= EXPO_OFFSET + EXPO_PROFILE1_OFFSET + 8 && ApobBytes.U32(buffer, data + EXPO_OFFSET) == EXPO_SIGNATURE)
            {
                uint expo = data + EXPO_OFFSET;
                info.HasExpoBlock = true;
                info.ExpoRevision = buffer[expo + 4];
                info.ExpoProfileBits = buffer[expo + 5];
                info.ExpoProfile1TckPs = ApobBytes.U16(buffer, expo + EXPO_PROFILE1_OFFSET + 4);
                info.ExpoProfile1TaaPs = ApobBytes.U16(buffer, expo + EXPO_PROFILE1_OFFSET + 6);
            }

            return info;
        }
    }
}

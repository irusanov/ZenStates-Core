using System;
using ZenStates.Core.Hardware.DRAM;

namespace ZenStates.Core.Hardware.Mock
{
    /// <summary>
    /// The pieces the DDR4 and DDR5 mock timings share: the captured registers they decode and the
    /// memory frequency taken from the report.
    /// </summary>
    internal sealed class MockTimingsSource
    {
        private readonly IRegisterSource registers;
        private readonly float reportedFrequency;

        internal MockTimingsSource(IRegisterSource registers, float reportedFrequency)
        {
            this.registers = registers ?? throw new ArgumentNullException(nameof(registers));
            this.reportedFrequency = reportedFrequency;
        }

        internal bool TryReadRegister(uint address, out uint value)
        {
            return registers.TryRead(address, out value);
        }

        /// <summary>
        /// Frequency for a captured system: the value the report stated, or the ratio decoded from
        /// the captured registers against a default reference clock when it did not state one.
        /// </summary>
        internal float ResolveFrequency(float ratio, double defaultBclk)
        {
            if (reportedFrequency > 0)
                return reportedFrequency;

            return ratio * (float)defaultBclk * 2;
        }
    }

    [Serializable]
    public sealed class MockDdr4Timings : Ddr4Timings
    {
        [NonSerialized]
        private readonly MockTimingsSource source;

        /// <param name="registers">Captured UMC registers covering this channel.</param>
        /// <param name="reportedFrequency">Data rate in MT/s from the report, or 0 if unknown.</param>
        public MockDdr4Timings(IRegisterSource registers, float reportedFrequency = 0f) : base(null)
        {
            source = new MockTimingsSource(registers, reportedFrequency);
        }

        protected override bool TryReadRegister(uint address, out uint value)
        {
            return source.TryReadRegister(address, out value);
        }

        public override float Frequency => source.ResolveFrequency(Ratio, DefaultBclk);
    }

    [Serializable]
    public sealed class MockDdr5Timings : Ddr5Timings
    {
        [NonSerialized]
        private readonly MockTimingsSource source;

        /// <param name="registers">Captured UMC registers covering this channel.</param>
        /// <param name="reportedFrequency">Data rate in MT/s from the report, or 0 if unknown.</param>
        public MockDdr5Timings(IRegisterSource registers, float reportedFrequency = 0f) : base(null)
        {
            source = new MockTimingsSource(registers, reportedFrequency);
        }

        protected override bool TryReadRegister(uint address, out uint value)
        {
            return source.TryReadRegister(address, out value);
        }

        public override float Frequency => source.ResolveFrequency(Ratio, DefaultBclk);
    }

    /// <summary>
    /// Creates the mock timings class matching a DRAM type.
    /// </summary>
    public static class MockDramTimings
    {
        /// <summary>
        /// UMC register carrying the memory clock ratio. Its presence at a channel's DCT offset is
        /// what marks that channel as captured, as opposed to merely mentioned in a report.
        /// </summary>
        public const uint ChannelRatioRegister = 0x50200;

        /// <summary>
        /// True when <paramref name="registers"/> actually holds the channel at
        /// <paramref name="dctOffset"/>, rather than only some other channel's block.
        /// </summary>
        public static bool HasChannel(IRegisterSource registers, uint dctOffset)
        {
            return registers != null && registers.TryRead(dctOffset | ChannelRatioRegister, out _);
        }

        /// <summary>
        /// Builds mock timings for <paramref name="memType"/> and decodes the channel at
        /// <paramref name="dctOffset"/> from <paramref name="registers"/>.
        /// </summary>
        /// <returns>The populated timings, or null when the DRAM type has no mock implementation.</returns>
        public static BaseDramTimings CreateAndRead(
            MemType memType,
            IRegisterSource registers,
            uint dctOffset,
            float reportedFrequency = 0f)
        {
            BaseDramTimings timings = Create(memType, registers, reportedFrequency);
            timings?.Read(dctOffset);
            return timings;
        }

        /// <summary>Builds mock timings for <paramref name="memType"/> without reading them.</summary>
        public static BaseDramTimings Create(
            MemType memType,
            IRegisterSource registers,
            float reportedFrequency = 0f)
        {
            switch (memType)
            {
                case MemType.DDR4:
                case MemType.LPDDR4:
                    return new MockDdr4Timings(registers, reportedFrequency);

                case MemType.DDR5:
                case MemType.LPDDR5:
                    return new MockDdr5Timings(registers, reportedFrequency);

                default:
                    return null;
            }
        }
    }
}

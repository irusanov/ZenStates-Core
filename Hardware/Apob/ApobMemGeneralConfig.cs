using System.Collections.Generic;
using ZenStates.Core.Common;

namespace ZenStates.Core.Hardware.Apob
{
    /// <summary>
    /// The DDR4 impedance and setup settings of a channel from <see cref="ApobMemGeneralConfig"/>: the "current
    /// value set by APCB" arrays. The values use the encoding of the BIOS memory controller table.
    /// </summary>
    public sealed class ApobDdr4ChannelConfig
    {
        public int Channel { get; internal set; }

        public Ddr4ProcOdt ProcOdt { get; internal set; }
        public Ddr4Rtt RttNom { get; internal set; }
        public Ddr4RttWr RttWr { get; internal set; }
        public Ddr4Rtt RttPark { get; internal set; }
        public Ddr4Setup AddrCmdSetup { get; internal set; }
        public Ddr4Setup CsOdtSetup { get; internal set; }
        public Ddr4Setup CkeSetup { get; internal set; }
        public Ddr4DrvStren ClkDrvStren { get; internal set; }
        public Ddr4DrvStren AddrCmdDrvStren { get; internal set; }
        public Ddr4DrvStren CsOdtCmdDrvStren { get; internal set; }
        public Ddr4DrvStren CkeDrvStren { get; internal set; }

        /// <summary>The channel has settings (ProcODT and the drive strengths are 0 on unpopulated channels).</summary>
        public bool IsPopulated { get; internal set; }
    }

    /// <summary>A memory setting of <see cref="ApobMemGeneralConfig"/> (APOB_MEM_CFG_INFO).</summary>
    public sealed class ApobMemSetting
    {
        public string Name { get; internal set; }

        /// <summary>The configured value (TRUE / FALSE for the enables).</summary>
        public ushort Value { get; internal set; }

        public ushort StatusCode { get; internal set; }

        public override string ToString()
        {
            return string.Format("{0}: {1} (status 0x{2:X4})", Name, Value, StatusCode);
        }
    }

    /// Two sizes are known: 496 bytes (Zen 2 / Zen 3, 8 channel arrays at data offset 0xA4, then the interleave
    /// mode, the DIMM sizes and 30 settings) and 256 bytes (Zen 1, 4 channel arrays at data offset 0x98, then the
    /// interleave mode). Zen 4 / Zen 5 (DDR5) use another layout and are not decoded.
    /// </remarks>
    public sealed class ApobMemGeneralConfig
    {
        private const uint ZEN2_DATA_SIZE = 496 - ApobEntry.HEADER_SIZE;
        private const uint ZEN1_DATA_SIZE = 256 - ApobEntry.HEADER_SIZE;

        public ushort MemClkFreq { get; private set; }
        public ushort DdrMaxRate { get; private set; }
        public bool[] EccEnable { get; private set; }
        public bool ChannelInterleave { get; private set; }

        public uint InterleaveCurrentMode { get; private set; }
        public uint InterleaveCapability { get; private set; }
        public uint InterleaveSize { get; private set; }

        /// <summary>The settings of each channel slot of the arrays, populated or not.</summary>
        public List<ApobDdr4ChannelConfig> Channels { get; private set; }

        /// <summary>Chip select interleave of each channel and the memory options, empty on Zen 1.</summary>
        public List<ApobMemSetting> Settings { get; private set; }

        public static ApobMemGeneralConfig Decode(byte[] buffer, uint entryOffset)
        {
            if (!ApobBytes.TryGetData(buffer, entryOffset, out uint data, out uint size))
                return null;

            int channels;
            uint arrays;
            if (size == ZEN2_DATA_SIZE)
            {
                channels = 8;
                arrays = data + 0xA4;
            }
            else if (size == ZEN1_DATA_SIZE)
            {
                channels = 4;
                arrays = data + 0x98;
            }
            else
            {
                return null;
            }

            var info = new ApobMemGeneralConfig
            {
                MemClkFreq = ApobBytes.U16(buffer, data + 4),
                DdrMaxRate = ApobBytes.U16(buffer, data + 6),
                EccEnable = new bool[channels],
                Channels = new List<ApobDdr4ChannelConfig>(),
                Settings = new List<ApobMemSetting>(),
            };

            for (int i = 0; i < channels; i++)
                info.EccEnable[i] = buffer[data + 8 + i] != 0;

            if (channels == 8)
                info.ChannelInterleave = buffer[data + 24] != 0;

            for (int c = 0; c < channels; c++)
            {
                uint o = arrays + (uint)c;
                uint n = (uint)channels;
                byte procOdt = buffer[o];
                byte clk = buffer[o + 7 * n], addrCmd = buffer[o + 8 * n], csOdt = buffer[o + 9 * n], cke = buffer[o + 10 * n];
                info.Channels.Add(new ApobDdr4ChannelConfig
                {
                    Channel = c,
                    ProcOdt = new Ddr4ProcOdt(procOdt),
                    RttNom = new Ddr4Rtt(buffer[o + n]),
                    RttWr = new Ddr4RttWr(buffer[o + 2 * n]),
                    RttPark = new Ddr4Rtt(buffer[o + 3 * n]),
                    AddrCmdSetup = new Ddr4Setup(buffer[o + 4 * n]),
                    CsOdtSetup = new Ddr4Setup(buffer[o + 5 * n]),
                    CkeSetup = new Ddr4Setup(buffer[o + 6 * n]),
                    ClkDrvStren = new Ddr4DrvStren(clk),
                    AddrCmdDrvStren = new Ddr4DrvStren(addrCmd),
                    CsOdtCmdDrvStren = new Ddr4DrvStren(csOdt),
                    CkeDrvStren = new Ddr4DrvStren(cke),
                    IsPopulated = procOdt != 0 || clk != 0 || addrCmd != 0 || csOdt != 0 || cke != 0,
                });
            }

            uint interleave = arrays + 11 * (uint)channels;
            info.InterleaveCurrentMode = ApobBytes.U32(buffer, interleave);
            info.InterleaveCapability = ApobBytes.U32(buffer, interleave + 4);
            info.InterleaveSize = ApobBytes.U32(buffer, interleave + 8);

            return info;
        }
    }
}

using OpenHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;

namespace ZenStates.Core.DRAM
{
    public class MemoryConfig
    {
        private const int DRAM_TYPE_BIT_MASK = 0x3;

        private const uint DRAM_TYPE_REG_ADDR = 0x50100;

        private const int MAX_CHANNELS = 12;

        private readonly uint ChannelsPerDimm;

        private readonly Cpu cpu;

        public struct Channel
        {
            public bool Enabled;

            public uint Offset;
        }

        public struct TimingDef
        {
            public string Name;

            public int HiBit;

            public int LoBit;
        }

        public enum CapacityUnit
        {
            B = 0,
            KB = 1,
            MB = 2,
            GB = 3,
        }

        public MemType Type { get; protected set; } = MemType.UNKNOWN;

        public Capacity TotalCapacity { get; protected set; }

        public List<KeyValuePair<uint, BaseDramTimings>> Timings { get; protected set; }

        public List<Channel> Channels { get; protected set; }

        public List<MemoryModule> Modules { get; protected set; }

        public MemoryConfig(Cpu cpuInstance)
        {
            cpu = cpuInstance;
            ChannelsPerDimm = 1; // Type == MemType.DDR5 ? 2u : 1u;
            Channels = new List<Channel>();
            Modules = new List<MemoryModule>();
            Timings = new List<KeyValuePair<uint, BaseDramTimings>>();

            ReadModulesInfo();

            if (!Mutexes.WaitPciBus(5000))
            {
                throw new TimeoutException("MemoryConfig: Timeout waiting for PCI bus mutex.");
            }

            //var offset = Modules.Count > 0 ? Modules[0].DctOffset : 0;
            //Type = (MemType)(cpu.ReadDword(offset | DRAM_TYPE_REG_ADDR) & DRAM_TYPE_BIT_MASK);

            try
            {
                ReadChannels();

                foreach (MemoryModule module in Modules)
                {
                    if (Type == MemType.DDR4 || Type == MemType.LPDDR4)
                        Timings.Add(new KeyValuePair<uint, BaseDramTimings>(module.DctOffset, new Ddr4Timings(cpu)));
                    else if (Type == MemType.DDR5 || Type == MemType.LPDDR5)
                        Timings.Add(new KeyValuePair<uint, BaseDramTimings>(module.DctOffset, new Ddr5Timings(cpu)));

                    ReadTimingsInternal(module.DctOffset);
                }
            }
            finally
            {
                Mutexes.ReleasePciBus();
            }
        }

        internal static MemType SMBiosDramTypeToMemType(MemoryType type)
        {
            switch (type)
            {
                case MemoryType.DDR4:
                    return MemType.DDR4;
                case MemoryType.LPDDR4:
                    return MemType.LPDDR4;
                case MemoryType.DDR5:
                    return MemType.DDR5;
                case MemoryType.LPDDR5:
                    return MemType.LPDDR5;
                default:
                    return MemType.UNKNOWN;
            }
        }

        internal void ReadTimingsInternal(uint offset = 0)
        {
            foreach (var item in Timings)
            {
                if (item.Key == offset)
                {
                    item.Value.Read(offset);
                    break;
                }
            }
        }

        public void ReadTimings(uint offset = 0)
        {
            if (!Mutexes.WaitPciBus(5000))
            {
                throw new TimeoutException("Timeout waiting for PCI bus mutex.");
            }

            try
            {
                ReadTimingsInternal(offset);
            }
            finally
            {
                Mutexes.ReleasePciBus();
            }
        }

        private void ReadModulesInfo()
        {
            foreach (var module in SMBiosSingleton.Instance.MemoryDevices)
            {
                if (module.Size > 0)
                {
                    ulong sizeInBytes = (ulong)module.Size * 1024 * 1024;
                    var type = SMBiosDramTypeToMemType(module.Type);
                    Modules.Add(new MemoryModule(module.PartNumber.Trim(), module.BankLocator.Trim(),
                        module.ManufacturerName.Trim(), module.DeviceLocator.Trim(),
                        sizeInBytes, module.Speed, type));
                }
            }

            if (Modules?.Count > 0)
            {
                ulong totalCapacity = 0UL;
                foreach (MemoryModule module in Modules)
                {
                    totalCapacity += module.Capacity.SizeInBytes;
                }
                TotalCapacity = new Capacity(totalCapacity);
                Type = Modules[0].Type;
            }
        }

        private MemRank GetRank(uint address)
        {
            if (Type == MemType.DDR4 || Type == MemType.LPDDR4)
            {
                return (MemRank)Utils.GetBits(cpu.ReadDword(address), 0, 1);
            }
            else if (Type == MemType.DDR5 || Type == MemType.LPDDR5)
            {
                var value = cpu.ReadDword(address);
                if (value != 0 && (value == 0x07FFFBFE || Utils.GetBits(value, 9, 2) < 3))
                    return MemRank.DR;
                value = cpu.ReadDword(address + 4);
                if (value != 0 && (value == 0x07FFFBFE || Utils.GetBits(value, 9, 2) < 3))
                    return MemRank.DR;
            }

            return MemRank.SR;
        }

        private DramAddressConfig GetAddressConfig(uint address)
        {
            var value = cpu.ReadDword(address);
            var config = new DramAddressConfig();
            if (value != 0)
            {
                config.NumBanks = Utils.GetBits(value, 20, 2);
                config.NumCol = 5 + Utils.GetBits(value, 16, 4);
                config.NumRow = 10 + Utils.GetBits(value, 8, 4);
                config.NumRM = Utils.GetBits(value, 4, 3);
                config.NumBankGroups = Utils.GetBits(value, 2, 2);
                config.Rank = GetRank(address - 0x20);
            }
            return config;
        }

        private void ReadChannels()
        {
            int dimmIndex = 0;
            uint dimmsPerChannel = 1;

            // Get the offset by probing the UMC0 to UMC7
            // It appears that offsets 0x80 and 0x84 are DIMM config registers
            // When a DIMM is DR, bit 0 is set to 1
            // 0x50000
            // offset 0, bit 0 when set to 1 means DIMM1 is installed
            // offset 8, bit 0 when set to 1 means DIMM2 is installed
            for (uint i = 0; i < MAX_CHANNELS * ChannelsPerDimm; i += ChannelsPerDimm)
            {
                try
                {
                    uint offset = i << 20;
                    bool channel = Utils.GetBits(cpu.ReadDword(offset | 0x50DF0), 19, 1) == 0;
                    bool dimm1 = Utils.GetBits(cpu.ReadDword(offset | 0x50000), 0, 1) == 1;
                    bool dimm2 = Utils.GetBits(cpu.ReadDword(offset | 0x50008), 0, 1) == 1;
                    bool enabled = channel && (dimm1 || dimm2);

                    Channels.Add(new Channel()
                    {
                        Enabled = enabled,
                        Offset = offset,
                    });

                    if (enabled)
                    {
                        if (dimm1)
                        {
                            MemoryModule module = Modules[dimmIndex++];
                            module.Slot = $"{Convert.ToChar(i / ChannelsPerDimm + 65)}1";
                            module.DctOffset = offset;
                            module.Rank = (Type == MemType.DDR4 || Type == MemType.LPDDR4) ? GetRank(offset | 0x50080) : GetRank(offset | 0x50020);
                            module.AddressConfig = GetAddressConfig(offset | 0x50040);
                        }

                        if (dimm2)
                        {
                            MemoryModule module = Modules[dimmIndex++];
                            module.Slot = $"{Convert.ToChar(i / ChannelsPerDimm + 65)}2";
                            module.DctOffset = offset;
                            module.Rank = (Type == MemType.DDR4 || Type == MemType.LPDDR4) ? GetRank(offset | 0x50084) : GetRank(offset | 0x50028);
                            module.AddressConfig = GetAddressConfig(offset | 0x50048);
                        }
                    }
                }
                catch
                {
                    // do nothing
                }
            }
        }
    }
}
/*
0x0025060c
0x25060c    0010 0101 0000 0110 0000 1100
    numBanks[21:20]  32 banks(2h)
    numCol[19:16] 10 + 5
    NumRow[11:8] 10 + 6
    numRM[6:4] 0
    bank groups 8 (3h)

> 0x00150508
0x150508    0001 0101 0000 0101 0000 1000
    numBanks [21:20]  32 banks (2h)
    numCol [19:16] 10 + 5
    NumRow [11:8] 10 + 5
    numRM [6:4] 0
    bank groups [3:2] 4 (2h)

*/
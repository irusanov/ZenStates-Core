using System.Collections;

namespace ZenStates.Core.DRAM
{
    public enum MemType
    {
        UNKNOWN = -1,
        DDR4,
        DDR5,
        LPDDR4,
        LPDDR5,
    }

    public enum MemRank
    {
        SR = 0,
        DR = 1,
        QR = 2,
    }

    public struct DramAddressConfig
    {
        public MemRank Rank { get; set; }
        public uint NumBanks { get; set; }
        public uint NumCol { get; set; }
        public uint NumRow { get; set; }
        public uint NumRM { get; set; }
        public uint NumBankGroups { get; set; }
        private static string GetBankDescription(uint value)
        {
            switch (value)
            {
                case 0x0:
                    return "8 Banks (3 bit)";
                case 0x1:
                    return "16 Banks (4 bit)";
                case 0x2:
                    return "32 Banks (5 bit)";
                case 0x3:
                    return "64 Banks (6 bit)";
                default:
                    return "Unknown Bank Configuration";
            }
        }

        private static string GetBankGroupDescription(uint value)
        {
            switch (value)
            {
                case 0x0:
                    return "No Bank Groups (0 bit)";
                case 0x1:
                    return "2 Bank Groups (1 bit)";
                case 0x2:
                    return "4 Bank Groups (2 bit)";
                case 0x3:
                    return "8 Bank Groups (3 bit)";
                default:
                    return "Unknown Bank Group Configuration";
            }
        }

        private static string GetRmDescription(uint value)
        {
            switch (value)
            {
                case 0x0:
                    return "No RM (0 bit)";
                case 0x1:
                    return "2x RM (1 bit)";
                case 0x2:
                    return "4x RM (2 bit)";
                case 0x3:
                    return "8x RM (3 bit)";
                default:
                    return "Unknown RM Configuration";
            }
        }


        public override string ToString()
        {
            return $"{GetBankDescription(NumBanks)}, Col: {NumCol}, Row: {NumRow}, {GetRmDescription(NumRM)}, {GetBankGroupDescription(NumBankGroups)}";
        }
    }


    public readonly struct DimmConfiguration
    {
        public bool PkgRnkTimingAlign { get; }
        public bool DimmRefDis { get; }
        public bool DqMapSwapDis { get; }
        public bool X16Dram { get; }
        public bool X4Dram { get; }
        public bool LRDIMM { get; }
        public bool RDIMM { get; }
        public bool CIsCS { get; }
        public bool Dram3DS { get; }
        public bool OutputInvert { get; }
        public bool OnDimmMirror { get; }

        public DimmConfiguration(int value)
        {
            PkgRnkTimingAlign = (value & (1 << 10)) != 0;
            DimmRefDis = (value & (1 << 9)) != 0;
            DqMapSwapDis = (value & (1 << 8)) != 0;
            X16Dram = (value & (1 << 7)) != 0;
            X4Dram = (value & (1 << 6)) != 0;
            LRDIMM = (value & (1 << 5)) != 0;
            RDIMM = (value & (1 << 4)) != 0;
            CIsCS = (value & (1 << 3)) != 0;
            Dram3DS = (value & (1 << 2)) != 0;
            OutputInvert = (value & (1 << 1)) != 0;
            OnDimmMirror = (value & (1 << 0)) != 0;
        }

        public override string ToString()
        {
            return
                $"PkgRnkTimingAlign: {(PkgRnkTimingAlign ? "Enabled" : "Disabled")}\n" +
                $"DimmRefDis: {(DimmRefDis ? "Disabled" : "Enabled")}\n" +
                $"DqMapSwapDis: {(DqMapSwapDis ? "Disabled" : "Enabled")}\n" +
                $"X16Dram: {(X16Dram ? "Enabled" : "Disabled")}\n" +
                $"X4Dram: {(X4Dram ? "Enabled" : "Disabled")}\n" +
                $"LRDIMM: {(LRDIMM ? "Enabled" : "Disabled")}\n" +
                $"RDIMM: {(RDIMM ? "Enabled" : "Disabled")}\n" +
                $"CIsCS: {(CIsCS ? "Enabled" : "Disabled")}\n" +
                $"Dram3DS: {(Dram3DS ? "Enabled" : "Disabled")}\n" +
                $"OutputInvert: {(OutputInvert ? "Enabled" : "Disabled")}\n" +
                $"OnDimmMirror: {(OnDimmMirror ? "Enabled" : "Disabled")}";
        }
    }

    public class MemoryModule : IEnumerable
    {
        public string BankLabel { get; set; }
        public string PartNumber { get; set; }
        public string Manufacturer { get; set; }
        public string DeviceLocator { get; set; }
        public Capacity Capacity { get; set; }
        public uint ClockSpeed { get; set; }
        public MemRank Rank { get; set; }
        public MemType Type { get; set; } = MemType.UNKNOWN;
        public string Slot { get; set; } = "";
        public uint DctOffset { get; set; } = 0;
        public DramAddressConfig AddressConfig { get; set; }

        public MemoryModule()
        {
            Capacity = new Capacity();
        }

        public MemoryModule(string partNumber, string bankLabel, string manufacturer,
            string deviceLocator, ulong capacity, uint clockSpeed, MemType type)
        {
            PartNumber = partNumber;
            Capacity = new Capacity(capacity);
            ClockSpeed = clockSpeed;
            BankLabel = bankLabel;
            Manufacturer = manufacturer;
            DeviceLocator = deviceLocator;
            Type = type;
        }

        public IEnumerator GetEnumerator()
        {
            return ((IEnumerable)PartNumber).GetEnumerator();
        }

        public override string ToString()
        {
            return $"{Slot}: {PartNumber} ({Capacity}, {Rank})";
        }
    }
}

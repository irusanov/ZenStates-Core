namespace ZenStates.Core.Hardware.DRAM
{
    public interface IDramTimings
    {
        void ReadRatio(uint offset = 0);
        void ReadBankGroupSwap(uint offset = 0);
        void Read(uint offset = 0);
    }
}


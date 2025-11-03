namespace ZenStates.Core.DRAM
{
    public interface IDramTimings
    {
        void ReadBankGroupSwap(uint offset = 0);
        void Read(uint offset = 0);
    }
}


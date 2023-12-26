using static ZenStates.Core.DRAM.MemoryConfig;

namespace ZenStates.Core.DRAM
{
    public interface IDramTimings
    {
        // Properties
        MemType Type { get; }
        float Frequency { get; }
        float Ratio { get; }
        uint BGS { get; }
        uint BGSAlt { get; }
        uint GDM { get; }
        uint PowerDown { get; }
        uint Cmd2T { get; }
        uint CL { get; }
        uint RCDWR { get; }
        uint RCDRD { get; }
        uint RP { get; }
        uint RAS { get; }
        uint RC { get; }
        uint RRDS { get; }
        uint RRDL { get; }
        uint FAW { get; }
        uint WTRS { get; }
        uint WTRL { get; }
        uint WR { get; }
        uint RDRDSCL { get; }
        uint WRWRSCL { get; }
        uint CWL { get; }
        uint RTP { get; }
        uint RDWR { get; }
        uint WRRD { get; }
        uint RDRDSC { get; }
        uint RDRDSD { get; }
        uint RDRDDD { get; }
        uint WRWRSC { get; }
        uint WRWRSD { get; }
        uint WRWRDD { get; }
        uint TRCPAGE { get; }
        uint CKE { get; }
        uint STAG { get; }
        uint MOD { get; }
        uint MODPDA { get; }
        uint MRD { get; }
        uint MRDPDA { get; }

        // Methods
        void ReadBankGroupSwap(uint offset = 0);
        void Read(uint offset = 0);
    }
}


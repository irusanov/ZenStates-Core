namespace ZenStates.Core
{
    public class GpuMailbox : Mailbox
    {
        // Subsystem frequencies

        public uint SMU_MSG_SetMaxGfxClkFreq { get; set; } = 0x0;

        public uint SMU_MSG_SetMinGfxClkFreq { get; set; } = 0x0;

        public uint SMU_MSG_SetMaxSocClkFreq { get; set; } = 0x0;

        public uint SMU_MSG_SetMinSocClkFreq { get; set; } = 0x0;

        public uint SMU_MSG_SetMaxFclkFreq { get; set; } = 0x0;

        public uint SMU_MSG_SetMinFclkFreq { get; set; } = 0x0;

        public uint SMU_MSG_SetMaxVcn { get; set; } = 0x0;

        public uint SMU_MSG_SetMinVcn { get; set; } = 0x0;

        public uint SMU_MSG_SetMaxLclk { get; set; } = 0x0;

        public uint SMU_MSG_SetMinLclk { get; set; } = 0x0;
    }
}
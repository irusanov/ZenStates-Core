namespace ZenStates.Core
{
    public sealed class MP1Mailbox : Mailbox
    {
        // Configurable commands
        public uint SMU_MSG_EnableOcMode { get; set; } = 0x0;
        public uint SMU_MSG_DisableOcMode { get; set; } = 0x0;
        public uint SMU_MSG_SetOverclockFrequencyAllCores { get; set; } = 0x0;
        public uint SMU_MSG_SetOverclockFrequencyPerCore { get; set; } = 0x0;
        public uint SMU_MSG_SetBoostLimitFrequencyAllCores { get; set; } = 0x0;
        public uint SMU_MSG_SetBoostLimitFrequency { get; set; } = 0x0;
        public uint SMU_MSG_SetOverclockCpuVid { get; set; } = 0x0;
        public uint SMU_MSG_SetDldoPsmMargin { get; set; } = 0x0;
        public uint SMU_MSG_SetAllDldoPsmMargin { get; set; } = 0x0;
        public uint SMU_MSG_GetDldoPsmMargin { get; set; } = 0x0;
        public uint SMU_MSG_SetPBOScalar { get; set; } = 0x0;
        public uint SMU_MSG_SetEDCVDDLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetTDCVDDLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetPPTLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetHTCLimit { get; set; } = 0x0;
    }
}
namespace ZenStates.Core
{
    public sealed class RSMUMailbox : Mailbox
    {
        // Configurable commands
        public uint SMU_MSG_GetTableVersion { get; set; } = 0x0;
        public uint SMU_MSG_GetBiosIfVersion { get; set; } = 0x0;
        public uint SMU_MSG_TransferTableToDram { get; set; } = 0x0;
        public uint SMU_MSG_GetDramBaseAddress { get; set; } = 0x0;
        public uint SMU_MSG_EnableSmuFeatures { get; set; } = 0x0;
        public uint SMU_MSG_DisableSmuFeatures { get; set; } = 0x0;
        public uint SMU_MSG_SetOverclockFrequencyAllCores { get; set; } = 0x0;
        public uint SMU_MSG_SetOverclockFrequencyPerCore { get; set; } = 0x0;
        public uint SMU_MSG_SetBoostLimitFrequencyAllCores { get; set; } = 0x0;
        public uint SMU_MSG_SetBoostLimitFrequency { get; set; } = 0x0;
        public uint SMU_MSG_SetOverclockCpuVid { get; set; } = 0x0;
        public uint SMU_MSG_EnableOcMode { get; set; } = 0x0;
        public uint SMU_MSG_DisableOcMode { get; set; } = 0x0;
        public uint SMU_MSG_GetPBOScalar { get; set; } = 0x0;
        public uint SMU_MSG_SetPBOScalar { get; set; } = 0x0;
        public uint SMU_MSG_SetPPTLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetTDCVDDLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetTDCSOCLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetEDCVDDLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetEDCSOCLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetHTCLimit { get; set; } = 0x0;
        public uint SMU_MSG_GetTjMax { get; set; } = 0x0;
        public uint SMU_MSG_SetTjMax { get; set; } = 0x0;
        public uint SMU_MSG_PBO_EN { get; set; } = 0x0;
    }
}
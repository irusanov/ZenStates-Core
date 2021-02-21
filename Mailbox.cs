
namespace ZenStates.Core
{
    public sealed class Mailbox
    {
        // Configurable registers
        public uint SMU_ADDR_MSG { get; set; } = 0x0;
        public uint SMU_ADDR_RSP { get; set; } = 0x0;
        public uint SMU_ADDR_ARG { get; set; } = 0x0;

        // SMU Messages (command IDs)
        // 0x1 and 0x2 seem to be common for all mailboxes
        public uint SMU_MSG_TestMessage { get; } = 0x1;
        public uint SMU_MSG_GetSmuVersion { get; } = 0x2;

        // Configurable commands
        public uint SMU_MSG_GetTableVersion { get; set; } = 0x0;
        public uint SMU_MSG_TransferTableToDram { get; set; } = 0x0;
        public uint SMU_MSG_GetDramBaseAddress { get; set; } = 0x0;
        public uint SMU_MSG_SetOverclockFrequencyAllCores { get; set; } = 0x0;
        public uint SMU_MSG_SetOverclockFrequencyPerCore { get; set; } = 0x0;
        public uint SMU_MSG_SetOverclockCpuVid { get; set; } = 0x0;
        public uint SMU_MSG_EnableOcMode { get; set; } = 0x0;
        public uint SMU_MSG_DisableOcMode { get; set; } = 0x0;
        public uint SMU_MSG_GetPBOScalar { get; set; } = 0x0;
        public uint SMU_MSG_SetPBOScalar { get; set; } = 0x0;
        public uint SMU_MSG_SetPPTLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetTDCLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetEDCLimit { get; set; } = 0x0;
    }
}

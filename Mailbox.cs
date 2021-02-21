using System;
using System.Collections.Generic;
using System.Text;

namespace ZenStates.Core
{
    public sealed class Mailbox
    {
        public uint SMU_ADDR_MSG { get; set; } = 0x0;
        public uint SMU_ADDR_RSP { get; set; } = 0x0;
        public uint SMU_ADDR_ARG { get; set; } = 0x0;

        // SMU Messages
        // 0x1 and 0x2 seem to be common for all mailboxes
        public uint SMU_MSG_TestMessage { get; set; } = 0x1;
        public uint SMU_MSG_GetSmuVersion { get; set; } = 0x2;

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

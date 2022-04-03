namespace ZenStates.Core
{
    public class Mailbox
    {
        // Configurable registers
        public uint SMU_ADDR_MSG { get; set; } = 0x0;
        public uint SMU_ADDR_RSP { get; set; } = 0x0;
        public uint SMU_ADDR_ARG { get; set; } = 0x0;

        // SMU Messages (command IDs)
        // 0x1 and 0x2 seem to be common for all mailboxes
        public uint SMU_MSG_TestMessage { get; } = 0x1;
        public uint SMU_MSG_GetSmuVersion { get; } = 0x2;
    }
}

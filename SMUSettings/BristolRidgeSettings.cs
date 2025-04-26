namespace ZenStates.Core.SMUSettings
{
    public class BristolRidgeSettings : SMU
    {
        public BristolRidgeSettings()
        {
            SMU_TYPE = SmuType.TYPE_CPU9;

            SMU_OFFSET_ADDR = 0xB8;
            SMU_OFFSET_DATA = 0xBC;

            Rsmu.SMU_ADDR_MSG = 0x13000000;
            Rsmu.SMU_ADDR_RSP = 0x13000010;
            Rsmu.SMU_ADDR_ARG = 0x13000020;
        }
    }
}

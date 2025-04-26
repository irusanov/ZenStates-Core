namespace ZenStates.Core.SMUSettings
{
    public class Zen5Settings : Zen4Settings
    {
        public Zen5Settings()
        {
            // HSMP
            Hsmp.SMU_ADDR_MSG = 0x3B10934;
            Hsmp.SMU_ADDR_RSP = 0x3B10980;
            Hsmp.SMU_ADDR_ARG = 0x3B109E0;
        }
    }
}

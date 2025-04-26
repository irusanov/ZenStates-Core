namespace ZenStates.Core.SMUSettings
{
    public class UnsupportedSettings : SMU
    {
        public UnsupportedSettings()
        {
            SMU_TYPE = SmuType.TYPE_UNSUPPORTED;
        }
    }
}

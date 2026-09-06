namespace ZenStates.Core.Hardware.Smu.Settings
{
    public class UnsupportedSettings : SMU
    {
        public UnsupportedSettings()
        {
            SMU_TYPE = SmuType.TYPE_UNSUPPORTED;
        }
    }
}

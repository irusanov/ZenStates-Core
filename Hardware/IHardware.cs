namespace ZenStates.Core.Hardware
{
    public interface IHardware
    {
        HardwareType HardwareType { get; }

        string GetReport();
    }
}

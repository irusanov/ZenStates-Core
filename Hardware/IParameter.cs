namespace ZenStates.Core.Hardware
{
    public interface IParameter
    {
        float Value { get; }

        string Description { get; }

        string Name { get; }
    }
}

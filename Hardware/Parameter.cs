namespace ZenStates.Core.Hardware
{
    public class Parameter : IParameter
    {
        public Parameter(string name, string description, float value)
        {
            Name = name;
            Description = description;
            Value = value;
        }
        public string Name { get; }
        public string Description { get; }
        public float Value { get; }
    }
}

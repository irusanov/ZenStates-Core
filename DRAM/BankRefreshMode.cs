namespace ZenStates.Core.DRAM
{
    public sealed class BankRefreshMode
    {
        public int Value { get; }
        public string Name { get; }

        private BankRefreshMode(int value, string name)
        {
            Value = value;
            Name = name;
        }

        public static readonly BankRefreshMode UNKNOWN = new BankRefreshMode(-1, nameof(UNKNOWN));
        public static readonly BankRefreshMode NORMAL = new BankRefreshMode(0, nameof(NORMAL));
        public static readonly BankRefreshMode FGR = new BankRefreshMode(1, nameof(FGR));
        public static readonly BankRefreshMode MIXED = new BankRefreshMode(2, nameof(MIXED));
        public static readonly BankRefreshMode PBONLY = new BankRefreshMode(2, nameof(PBONLY));

        public override string ToString()
        {
            switch (Name)
            {
                case "UNKNOWN": return "Unknown";
                case "NORMAL": return "Normal";
                case "FGR": return "FGR";
                case "MIXED": return "Mixed";
                case "PBONLY": return "Per-Bank Only";
                default: return Name;
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj is BankRefreshMode other) return Value == other.Value;
            return false;
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public static bool operator ==(BankRefreshMode a, BankRefreshMode b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            return a.Value == b.Value;
        }

        public static bool operator !=(BankRefreshMode a, BankRefreshMode b) => !(a == b);

        public static BankRefreshMode FromInt(int value)
        {
            switch (value)
            {
                case -1: return UNKNOWN;
                case 0: return NORMAL;
                case 1: return FGR;
                case 2: return MIXED;
                default: return new BankRefreshMode(value, value.ToString());
            }
        }

        public static implicit operator int(BankRefreshMode mode) => mode?.Value ?? -1;
    }
}

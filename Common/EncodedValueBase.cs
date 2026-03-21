using System.Collections.Generic;

namespace ZenStates.Core
{
    public abstract class EncodedValueBase
    {
        protected int RawValue { get; }

        protected EncodedValueBase(int value)
        {
            RawValue = value;
        }

        protected abstract Dictionary<int, string> Lookup { get; }

        protected virtual string FormatValue(string resolvedValue) => resolvedValue;

        public override string ToString()
        {
            string resolvedValue = Lookup.TryGetValue(RawValue, out var output)
                ? output
                : "N/A";

            return FormatValue(resolvedValue);
        }
    }
}

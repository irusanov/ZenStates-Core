using System.Collections.Generic;
using System.Data.SqlTypes;

namespace ZenStates.Core
{
    public abstract class EncodedValueBase : INullable
    {
        private readonly int? _rawValue;

        public int? RawValue
        {
            get { return _rawValue; }
        }

        public bool IsNull
        {
            get { return _rawValue == null; }
        }

        protected EncodedValueBase(int? value)
        {
            _rawValue = value;
        }

        protected abstract Dictionary<int, string> Lookup { get; }

        protected virtual string FormatValue(string resolvedValue)
        {
            return resolvedValue;
        }

        public override string ToString()
        {
            if (IsNull)
                return "N/A";

            if (!Lookup.TryGetValue(_rawValue.Value, out string resolvedValue))
                resolvedValue = "N/A";

            return FormatValue(resolvedValue);
        }
    }
}

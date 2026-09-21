using System;
using System.Collections;
using System.Collections.Generic;

namespace ZenStates.Core.Hardware
{
    public interface IRegisterSource
    {
        bool TryRead(uint address, out uint value);
    }

    [Serializable]
    public sealed class VirtualRegisters : IRegisterSource, IEnumerable<VirtualRegister>
    {
        private readonly Dictionary<uint, uint> values;

        public VirtualRegisters(string name = null)
        {
            Name = name;
            values = new Dictionary<uint, uint>();
        }

        public VirtualRegisters(IDictionary<uint, uint> registers, string name = null)
        {
            Name = name;
            values = registers == null
                ? new Dictionary<uint, uint>()
                : new Dictionary<uint, uint>(registers);
        }

        /// <summary>Which register space these came from, e.g. a debug report section name. May be null.</summary>
        public string Name { get; }

        public int Count => values.Count;

        public bool IsEmpty => values.Count == 0;

        public void Set(uint address, uint value)
        {
            values[address] = value;
        }

        public void Set(VirtualRegister register)
        {
            values[register.Address] = register.Value;
        }

        public bool TryRead(uint address, out uint value)
        {
            return values.TryGetValue(address, out value);
        }

        /// <summary>Reads a register together with its address, for callers that want the bit helpers.</summary>
        public bool TryGet(uint address, out VirtualRegister register)
        {
            if (values.TryGetValue(address, out uint value))
            {
                register = new VirtualRegister(address, value);
                return true;
            }

            register = default(VirtualRegister);
            return false;
        }

        public bool Contains(uint address) => values.ContainsKey(address);

        /// <summary>
        /// True when every one of <paramref name="addresses"/> is present. Useful for deciding
        /// whether a block worth decoding was actually captured.
        /// </summary>
        public bool ContainsAll(params uint[] addresses)
        {
            if (addresses == null)
                return false;

            foreach (uint address in addresses)
            {
                if (!values.ContainsKey(address))
                    return false;
            }

            return true;
        }

        public bool TryReadOffset(uint baseAddress, uint offset, out uint value)
        {
            return TryRead(baseAddress | offset, out value);
        }

        public IEnumerator<VirtualRegister> GetEnumerator()
        {
            foreach (KeyValuePair<uint, uint> entry in values)
                yield return new VirtualRegister(entry.Key, entry.Value);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString()
        {
            return string.IsNullOrEmpty(Name)
                ? $"{Count} registers"
                : $"{Name}: {Count} registers";
        }
    }
}

using System.Collections.Generic;

namespace ZenStates.Core.Hardware.Apob
{
    /// <summary>A hole of <see cref="ApobMemoryMap"/>: a range of the address space that is not usable DRAM.</summary>
    public sealed class ApobMemoryHole
    {
        public ulong Base { get; internal set; }
        public ulong Size { get; internal set; }
        public uint Type { get; internal set; }

        public ulong End
        {
            get { return Size > 0 ? Base + Size - 1 : Base; }
        }

        public string TypeName
        {
            get { return ApobMemoryMap.GetHoleTypeName(Type); }
        }
    }

    /// <summary>
    /// APOB_SYSTEM_MEMORY_MAP (FABRIC group, type 9): the top of system memory and the holes the ABL reserved
    /// (MMIO, UMA, firmware reserved ranges).
    /// </summary>
    /// <remarks>
    /// Data layout (verified on a Granite Ridge dump): TopOfSystemMemory u64, NumberOfHoles u32, a reserved u32,
    /// then the hole descriptors: Base u64, Size u64, Type u32, reserved u32. The type names follow
    /// MEMORY_HOLE_TYPES of openSIL; client parts have types beyond that list.
    /// </remarks>
    public sealed class ApobMemoryMap
    {
        private const uint DESCRIPTOR_SIZE = 24;

        private static readonly string[] HoleTypeNames =
        {
            "UMA",
            "MMIO",
            "Privileged DRAM",
            "Reserved 1TB remap",
            "Reserved S-Link",
            "Reserved S-Link alignment",
            "Reserved DRTM",
            "Reserved CVIP",
            "Reserved SMU features",
            "Reserved fTPM",
            "Reserved MPIO C20",
            "Reserved NBIF",
            "Reserved CXL",
            "Reserved CXL alignment",
            "Reserved CPU TMR",
            "Reserved RAS EINJ",
        };

        public ulong TopOfSystemMemory { get; private set; }
        public uint NumberOfHoles { get; private set; }
        public List<ApobMemoryHole> Holes { get; private set; }

        /// <summary>Total of the hole sizes, in bytes.</summary>
        public ulong HoleBytes
        {
            get
            {
                ulong total = 0;
                for (int i = 0; i < Holes.Count; i++)
                    total += Holes[i].Size;
                return total;
            }
        }

        public static string GetHoleTypeName(uint type)
        {
            return type < HoleTypeNames.Length ? HoleTypeNames[type] : "Unknown (" + type + ")";
        }

        public static ApobMemoryMap Decode(byte[] buffer, uint entryOffset)
        {
            if (!ApobBytes.TryGetData(buffer, entryOffset, out uint data, out uint size) || size < 16)
                return null;

            uint holes = ApobBytes.U32(buffer, data + 8);
            uint capacity = (size - 16) / DESCRIPTOR_SIZE;
            if (holes > capacity)
                return null;

            var map = new ApobMemoryMap
            {
                TopOfSystemMemory = ApobBytes.U64(buffer, data),
                NumberOfHoles = holes,
                Holes = new List<ApobMemoryHole>(),
            };

            for (uint i = 0; i < holes; i++)
            {
                uint o = data + 16 + i * DESCRIPTOR_SIZE;
                map.Holes.Add(new ApobMemoryHole
                {
                    Base = ApobBytes.U64(buffer, o),
                    Size = ApobBytes.U64(buffer, o + 8),
                    Type = ApobBytes.U32(buffer, o + 16),
                });
            }

            return map;
        }
    }
}

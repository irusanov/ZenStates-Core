using System;
using System.Collections.Generic;
using ZenStates.Core.Hardware.Motherboard;
using ZenStates.Core.Hardware.Motherboard.Lpc;
using ZenStates.Core.OHWM;
using static ZenStates.Core.Cpu;

namespace ZenStates.Core.Hardware
{
    public struct SmuVersionNumber
    {
        private readonly uint _value;

        public SmuVersionNumber(uint value)
        {
            _value = value;
        }

        public uint Value => _value;

        public override string ToString()
        {
            if (_value == 0)
                return "Unknown";

            if ((_value & 0xFF000000) > 0)
                return $"{(_value >> 24) & 0xff}.{(_value >> 16) & 0xff}.{(_value >> 8) & 0xff}.{_value & 0xff}";

            return $"{(_value >> 16) & 0xff}.{(_value >> 8) & 0xff}.{_value & 0xff}";
        }

        public static implicit operator SmuVersionNumber(uint value)
        {
            return new SmuVersionNumber(value);
        }

        public static implicit operator uint(SmuVersionNumber value)
        {
            return value._value;
        }
    }

    public struct CpuId
    {
        private readonly uint _value;
        public CpuId(uint value)
        {
            _value = value;
        }
        public uint Value => _value;
        public override string ToString()
        {
            return _value.ToString("X8").TrimStart('0');
        }
        public static implicit operator CpuId(uint value)
        {
            return new CpuId(value);
        }
        public static implicit operator uint(CpuId value)
        {
            return value._value;
        }
    }

    public readonly struct SuperIoSensorGroup
    {
        public string ChipName { get; }
        public Chip Chip { get; }

        public static HardwareType HardwareType => HardwareType.SuperIO;
        public IEnumerable<Sensor> Sensors { get; }

        public SuperIoSensorGroup(string chipName, Chip chip, IEnumerable<Sensor> sensors)
        {
            ChipName = chipName;
            Chip = chip;
            Sensors = sensors;
        }
    }

    [Serializable]
    public class SystemInfo : IDisposable
    {
        private readonly CPUInfo _cpuInfo;
        private readonly LpcIO _lpcIO;
        private readonly List<IHardware> _hardware;
        private bool disposedValue;

        public SystemInfo(CPUInfo info, SMU smu, string agesaVersion)
        {
            _cpuInfo = info;
            SmuVersion = smu.Version;
            SmuTableVersion = smu.TableVersion;
            SmuType = smu.SMU_TYPE.ToString();
            AgesaVersion = agesaVersion;
            _hardware = new List<IHardware>();

            SMBios smbios = SMBiosSingleton.Instance;
            MbVendor = smbios?.Board?.ManufacturerName ?? "N/A";
            MbName = smbios?.Board?.ProductName ?? "N/A";
            BiosVersion = smbios?.Bios?.Version ?? "N/A";
            _lpcIO = new LpcIO(smbios);
            for (var i = 0; i < _lpcIO.SuperIO.Length; i++)
            {
                _hardware.Add(new SuperIOHardware(_lpcIO.SuperIO[i], smbios, i));
            }
        }

        // CPU identity
        public string CpuName => _cpuInfo.cpuName ?? "N/A";
        public string Vendor => _cpuInfo.vendor ?? "N/A";
        public string CodeName => _cpuInfo.codeName.ToString();
        public CpuId CpuId => _cpuInfo.cpuid;

        public Family Family => _cpuInfo.family;
        public uint BaseModel => _cpuInfo.baseModel;
        public uint ExtendedModel => _cpuInfo.extModel;
        public uint Model => _cpuInfo.model;
        public uint Stepping => _cpuInfo.stepping;
        public uint PatchLevel => _cpuInfo.patchLevel;

        // This is not working correctly, it needs mappings for each generation
         //public string PackageType => $"{_cpuInfo.packageType} ({(int)_cpuInfo.packageType})";
         public int PackageType => (int)_cpuInfo.packageType;

        // Topology
        public int FusedCoreCount => (int)_cpuInfo.topology.cores;
        public int PhysicalCoreCount => (int)_cpuInfo.topology.physicalCores;
        public int NodesPerProcessor => (int)_cpuInfo.topology.cpuNodes;
        public int Threads => (int)_cpuInfo.topology.logicalCores;
        public bool SMT => _cpuInfo.topology.threadsPerCore > 1;
        public int CCDCount => (int)_cpuInfo.topology.ccds;
        public int CCXCount => (int)_cpuInfo.topology.ccxs;
        public int CoresPerCCX => (int)_cpuInfo.topology.coresPerCcx;

        // Board / BIOS
        public string MbVendor { get; private set; }
        public string MbName { get; private set; }
        public string BiosVersion { get; private set; }
        public string AgesaVersion { get; set; }
        //public IEnumerable<Sensor> Sensors
        //{
        //    get
        //    {
        //        foreach (SuperIOHardware hardware in _superIoHardware)
        //        {
        //            foreach (Sensor sensor in hardware.Sensors)
        //                yield return sensor;
        //        }
        //    }
        //}

        // Sensors grouped by the SuperIO chip they were read from.
        // Useful when a board has multiple SuperIO chips and sensors need
        // to be displayed/labeled per-chip.

        public List<IHardware> Hardware => _hardware;

        public IEnumerable<SuperIoSensorGroup> SensorGroups
        {
            get
            {
                foreach (IHardware hardware in _hardware)
                {
                    if (hardware.HardwareType == HardwareType.SuperIO)
                    {
                        yield return new SuperIoSensorGroup(((SuperIOHardware)hardware).ChipName, ((SuperIOHardware)hardware).Chip, ((SuperIOHardware)hardware)?.Sensors);
                    }
                }
            }
        }

        // Update SuperIOs only for now
        public void UpdateSensors()
        {
            foreach (IHardware hardware in _hardware)
            {
                if (hardware.HardwareType == HardwareType.SuperIO)
                {
                    ((SuperIOHardware)hardware).Update();
                }
            }
        }

        // SMU
        public SmuVersionNumber SmuVersion { get; private set; }
        public uint SmuTableVersion { get; private set; }
        public string SmuType { get; private set; }

        // Static access to SMBios
        public static SMBios SMBios => SMBiosSingleton.Instance;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _lpcIO?.Close();
                }

                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~SystemInfo()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}

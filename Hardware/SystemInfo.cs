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

    /// <summary>
    /// Sensors read from one source: a SuperIO chip, the SVI3 telemetry in the power table, the CPU's
    /// measured reference clocks, or the ASUS WMI interface.
    /// </summary>
    public readonly struct SensorGroup
    {
        public string Name { get; }
        public HardwareType HardwareType { get; }

        /// <summary>The SuperIO chip the sensors were read from; <see cref="Chip.Unknown"/> for other groups.</summary>
        public Chip Chip { get; }
        public IEnumerable<Sensor> Sensors { get; }

        public SensorGroup(string name, HardwareType hardwareType, Chip chip, IEnumerable<Sensor> sensors)
        {
            Name = name;
            HardwareType = hardwareType;
            Chip = chip;
            Sensors = sensors;
        }

        internal static SensorGroup FromSuperIo(SuperIOHardware hardware)
        {
            return new SensorGroup(hardware.ChipName, HardwareType.SuperIO, hardware.Chip, hardware.Sensors);
        }

        internal static SensorGroup FromSvi3(Svi3Hardware hardware)
        {
            return new SensorGroup(hardware.Name, HardwareType.Svi3, Chip.Unknown, hardware.Sensors);
        }

        internal static SensorGroup FromBclk(BclkHardware hardware)
        {
            return new SensorGroup(hardware.Name, HardwareType.Cpu, Chip.Unknown, hardware.Sensors);
        }

        internal static SensorGroup FromAsusWmi(AsusWmiHardware hardware)
        {
            return new SensorGroup(hardware.Name, HardwareType.AsusWmi, Chip.Unknown, hardware.Sensors);
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

        // Sensors grouped by where they were read from: the SVI3 telemetry first when the CPU reports
        // it (a handful of values), then the CPU's measured reference clocks (named after the CPU),
        // then one group per SuperIO chip, so boards with several chips can label them per chip, then
        // the ASUS WMI sensors on boards that have them.

        public List<IHardware> Hardware => _hardware;

        public IEnumerable<SensorGroup> SensorGroups
        {
            get
            {
                foreach (IHardware hardware in _hardware)
                {
                    if (hardware is Svi3Hardware svi3 && svi3.HasSensors)
                        yield return SensorGroup.FromSvi3(svi3);
                }

                foreach (IHardware hardware in _hardware)
                {
                    if (hardware is BclkHardware bclk && bclk.HasSensors)
                        yield return SensorGroup.FromBclk(bclk);
                }

                foreach (IHardware hardware in _hardware)
                {
                    if (hardware is SuperIOHardware superIo)
                        yield return SensorGroup.FromSuperIo(superIo);
                }

                foreach (IHardware hardware in _hardware)
                {
                    if (hardware is AsusWmiHardware asusWmi && asusWmi.HasSensors)
                        yield return SensorGroup.FromAsusWmi(asusWmi);
                }
            }
        }

        /// <summary>
        /// The ASUS WMI sensors; null when the board doesn't have them. A method rather than a property, so
        /// the reports that list this class's properties leave it out.
        /// </summary>
        public AsusWmiHardware GetAsusWmi()
        {
            foreach (IHardware hardware in _hardware)
            {
                if (hardware is AsusWmiHardware asusWmi)
                    return asusWmi;
            }

            return null;
        }

        /// <summary>
        /// Reads the SuperIO chips and copies the power table's SVI3 values into their sensors, so
        /// refresh the power table first. The ASUS WMI sensors are left out, as each of them is a WMI
        /// call: update them with <see cref="AsusWmiHardware.Update()"/> while they are shown.
        /// </summary>
        public void UpdateSensors()
        {
            foreach (IHardware hardware in _hardware)
            {
                if (hardware is SuperIOHardware superIo)
                    superIo.Update();
                else if (hardware is Svi3Hardware svi3)
                    svi3.Update();
                else if (hardware is BclkHardware bclk)
                    bclk.Update();
            }
        }

        internal void AddHardware(IHardware hardware)
        {
            if (hardware != null)
                _hardware.Add(hardware);
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

                    GetAsusWmi()?.Dispose();
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

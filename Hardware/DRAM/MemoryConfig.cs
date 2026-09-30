using System;
using System.Collections.Generic;
using System.Diagnostics;
using ZenStates.Core.Drivers;
using ZenStates.Core.Hardware.DRAM.DDR4.Spd;
using ZenStates.Core.Hardware.DRAM.DDR4.Thermal;
using ZenStates.Core.Hardware.DRAM.DDR5.Pmic;
using ZenStates.Core.Hardware.DRAM.DDR5.Spd;
using ZenStates.Core.Hardware.DRAM.DDR5.Thermal;
using ZenStates.Core.Hardware.Smu.Commands;
using ZenStates.Core.OHWM;

namespace ZenStates.Core.Hardware.DRAM
{
    public class MemoryConfig
    {
        private readonly SmbusDriverBase smbusDriver = SmbusProvider.Instance;

        private const int DRAM_TYPE_BIT_MASK = 0x3;

        private const uint DRAM_TYPE_REG_ADDR = 0x50100;

        private const int MAX_CHANNELS = 12;

        private readonly uint ChannelsPerDimm;

        private readonly Cpu cpu;

        public struct Channel
        {
            public bool Enabled;

            public uint Offset;
        }

        public struct TimingDef
        {
            public string Name;

            public int HiBit;

            public int LoBit;
        }

        public enum CapacityUnit
        {
            B = 0,
            KB = 1,
            MB = 2,
            GB = 3,
        }

        public MemType Type { get; protected set; } = MemType.UNKNOWN;

        public Capacity TotalCapacity { get; protected set; }

        public bool IsExpoProfileActive { get; protected set; } = false;

        public List<KeyValuePair<uint, BaseDramTimings>> Timings { get; protected set; }

        public List<Channel> Channels { get; protected set; }

        public List<MemoryModule> Modules { get; protected set; }

        // SpdInfo is replaced as a whole (copy-on-write) and never mutated after it has been
        // published, so readers such as RefreshTelemetry can iterate a snapshot safely while
        // RefreshSpdInfo runs on another thread.
        private volatile Dictionary<byte, Ddr5SpdInfo> spdInfo;

        // Serializes writers of spdInfo (RefreshSpdInfo) so concurrent merges don't lose updates.
        private readonly object spdInfoLock = new object();

        public Dictionary<byte, Ddr5SpdInfo> SpdInfo
        {
            get { return spdInfo; }
            protected set { spdInfo = value; }
        }

        // Same copy-on-write rule as spdInfo, guarded by the same lock.
        private volatile Dictionary<byte, Ddr4SpdInfo> ddr4Spd;

        /// <summary>
        /// DDR4 SPD of each module, keyed by the SPD address in module order. Null when the memory is not DDR4.
        /// The entries read at startup are partial (<see cref="Ddr4SpdInfo.IsPartial"/>); <see cref="RefreshSpdInfo"/>
        /// reads the whole SPD.
        /// </summary>
        public Dictionary<byte, Ddr4SpdInfo> Ddr4Spd
        {
            get { return ddr4Spd; }
            protected set { ddr4Spd = value; }
        }

        private bool IsSpdSupported
        {
            get { return Type == MemType.DDR5 || Type == MemType.LPDDR5; }
        }

        /// <summary>
        /// DDR4 module thermal sensors: one entry per module SPD found, keyed by the SPD address and in the order
        /// of the modules. The value is null for a module without a sensor. Null when the memory is not DDR4.
        /// The entries are read at startup and updated in place by <see cref="RefreshTelemetry"/>.
        /// </summary>
        public Dictionary<byte, Ddr4ThermalData> Ddr4ThermalSensors { get; protected set; }

        /// <summary>Whether any module reports live data (DDR5 SPD hub and PMIC, or a DDR4 thermal sensor).</summary>
        public bool HasDimmTelemetry
        {
            get
            {
                Dictionary<byte, Ddr5SpdInfo> spd = SpdInfo;
                if (spd != null && spd.Count > 0)
                    return true;

                Dictionary<byte, Ddr4ThermalData> ddr4 = Ddr4ThermalSensors;
                if (ddr4 != null)
                {
                    foreach (Ddr4ThermalData td in ddr4.Values)
                    {
                        if (td != null && td.IsValid)
                            return true;
                    }
                }

                return false;
            }
        }

        private long LastTelemetryRefreshTick;
        private readonly object telemetryThrottleLock = new object();

        public MemoryConfig(Cpu cpuInstance)
        {
            cpu = cpuInstance;
            ChannelsPerDimm = 1; // Type == MemType.DDR5 ? 2u : 1u;
            Channels = new List<Channel>();
            Modules = new List<MemoryModule>();
            Timings = new List<KeyValuePair<uint, BaseDramTimings>>();

            ReadModulesInfo();

            if (!Mutexes.WaitPciBus(5000))
            {
                throw new TimeoutException("MemoryConfig: Timeout waiting for PCI bus mutex.");
            }

            //var offset = Modules.Count > 0 ? Modules[0].DctOffset : 0;
            //Type = (MemType)(cpu.ReadDword(offset | DRAM_TYPE_REG_ADDR) & DRAM_TYPE_BIT_MASK);

            try
            {
                ReadChannelsNoLock();

                foreach (MemoryModule module in Modules)
                {
                    if (Type == MemType.DDR4 || Type == MemType.LPDDR4)
                        Timings.Add(new KeyValuePair<uint, BaseDramTimings>(module.DctOffset, new Ddr4Timings(cpu)));
                    else if (Type == MemType.DDR5 || Type == MemType.LPDDR5)
                        Timings.Add(new KeyValuePair<uint, BaseDramTimings>(module.DctOffset, new Ddr5Timings(cpu)));

                    ReadTimingsInternal(module.DctOffset);
                }
            }
            finally
            {
                Mutexes.ReleasePciBus();
            }

            if (Type == MemType.DDR4)
            {
                // Module thermal sensors (TSOD); read-only, modules without one are simply listed without data.
                Ddr4ThermalSensors = Ddr4ThermalSensor.ReadAll(smbusDriver);

                // Only the SPD bytes needed at startup; the SPD window reads the rest
                Dictionary<byte, Ddr4SpdInfo> ddr4Info = Ddr4SpdReader.ReadInitInfoAll();
                LinkDdr4ThermalSensors(ddr4Info);
                Ddr4Spd = ddr4Info;

                var names = new List<string>();
                foreach (Ddr4SpdInfo entry in ddr4Info.Values)
                    names.Add(entry != null && entry.IsValid ? entry.ModuleManufacturer : null);
                UpdateModuleManufacturers(names);
                return;
            }

            if (Type != MemType.DDR5 && Type != MemType.LPDDR5)
                return;

            // Only read partial info needed for initialization as reading whole SPD data is expensive
            SpdInfo = Ddr5SpdReader.ReadDdr5SpdInitInfoAll();

            var ddr5Names = new List<string>();
            foreach (var spdEntry in SpdInfo.Values)
            {
                if (spdEntry != null)
                    ddr5Names.Add(spdEntry.ModuleManufacturer);
            }
            UpdateModuleManufacturers(ddr5Names);

            // Should not need a try/catch here, but just in case
            // The command is largely untested and may not return a valid result for all platforms
            try
            {
                using (var cmd = new GetEXPOProfileActive(cpu.smu))
                {
                    cmd.Execute();
                    IsExpoProfileActive = cmd.IsEXPOProfileActive;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MemoryConfig: Failed to get EXPO profile status: {ex.Message}");
            }
            //Ddr5SpdReader.DumpDdr5SpdToFiles(Directory.GetCurrentDirectory());

            // Populate PMIC data for telemetry
            //RefreshTelemetry();
        }

        // The module manufacturers from SPD, in module order, replace the SMBIOS names unless SPD doesn't know the vendor.
        private void UpdateModuleManufacturers(List<string> spdNames)
        {
            try
            {
                for (int i = 0; i < spdNames.Count && i < Modules.Count; i++)
                {
                    string name = spdNames[i];
                    if (string.IsNullOrEmpty(name))
                        continue;

                    if (string.IsNullOrEmpty(Modules[i].Manufacturer) ||
                        Modules[i].Manufacturer.StartsWith("Unknown") ||
                        !name.StartsWith("Unknown"))
                    {
                        Modules[i].Manufacturer = name;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MemoryConfig: Failed to update manufacturer info from SPD: {ex.Message}");
            }
        }

        // Attaches the thermal sensor of each module (found at startup) to its SPD entry.
        private void LinkDdr4ThermalSensors(Dictionary<byte, Ddr4SpdInfo> spd)
        {
            Dictionary<byte, Ddr4ThermalData> sensors = Ddr4ThermalSensors;
            if (spd == null || sensors == null)
                return;

            foreach (KeyValuePair<byte, Ddr4SpdInfo> entry in spd)
            {
                if (entry.Value != null && sensors.TryGetValue(entry.Key, out Ddr4ThermalData sensor))
                    entry.Value.ThermalData = sensor;
            }
        }

        /// <summary>Reads and decodes the whole SPD of all DDR4 modules (not cached; see <see cref="RefreshSpdInfo"/>).</summary>
        public Dictionary<byte, Ddr4SpdInfo> ReadAndDecodeAllDdr4()
        {
            if (Type != MemType.DDR4)
                return null;

            Dictionary<byte, Ddr4SpdInfo> info = Ddr4SpdReader.ReadAll();
            LinkDdr4ThermalSensors(info);
            return info;
        }

        internal static MemType SMBiosDramTypeToMemType(MemoryType type)
        {
            switch (type)
            {
                case MemoryType.DDR4:
                    return MemType.DDR4;
                case MemoryType.LPDDR4:
                    return MemType.LPDDR4;
                case MemoryType.DDR5:
                    return MemType.DDR5;
                case MemoryType.LPDDR5:
                    return MemType.LPDDR5;
                default:
                    return MemType.UNKNOWN;
            }
        }

        internal void ReadTimingsInternal(uint offset = 0)
        {
            foreach (var item in Timings)
            {
                if (item.Key == offset)
                {
                    item.Value.Read(offset);
                    break;
                }
            }
        }

        public BaseDramTimings ReadTimings(uint offset = 0)
        {
            if (!Mutexes.WaitPciBus(5000))
            {
                throw new TimeoutException("ReadTimings: Timeout waiting for PCI bus mutex.");
            }

            try
            {
                ReadTimingsInternal(offset);
                return Timings.Find(x => x.Key == offset).Value;
            }
            finally
            {
                Mutexes.ReleasePciBus();
            }
        }

        public Dictionary<byte, Ddr5SpdInfo> ReadAndDecodeAll()
        {
            if (!IsSpdSupported)
                return null;

            return Ddr5SpdDecoder.ReadAndDecodeAll(smbusDriver);
        }

        public bool RefreshSpdInfo()
        {
            if (Type == MemType.DDR4)
                return RefreshDdr4SpdInfo();

            if (!IsSpdSupported)
                return false;

            try
            {
                Dictionary<byte, Ddr5SpdInfo> info = ReadAndDecodeAll();
                if (info == null)
                    return false;

                lock (spdInfoLock)
                {
                    // Build a new dictionary and publish it with a single reference swap, so
                    // readers iterating the previous snapshot never see it mutated.
                    Dictionary<byte, Ddr5SpdInfo> current = SpdInfo;
                    Dictionary<byte, Ddr5SpdInfo> merged = current != null
                        ? new Dictionary<byte, Ddr5SpdInfo>(current)
                        : new Dictionary<byte, Ddr5SpdInfo>();

                    foreach (var entry in info)
                    {
                        if (entry.Value != null)
                            merged[entry.Key] = entry.Value;
                    }

                    SpdInfo = merged;
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshSpdInfo: {ex.Message}");
                return false;
            }
        }

        private bool RefreshDdr4SpdInfo()
        {
            try
            {
                Dictionary<byte, Ddr4SpdInfo> info = ReadAndDecodeAllDdr4();
                if (info == null || info.Count == 0)
                    return false;

                lock (spdInfoLock)
                {
                    Dictionary<byte, Ddr4SpdInfo> current = Ddr4Spd;
                    Dictionary<byte, Ddr4SpdInfo> merged = current != null
                        ? new Dictionary<byte, Ddr4SpdInfo>(current)
                        : new Dictionary<byte, Ddr4SpdInfo>();

                    foreach (var entry in info)
                    {
                        // Keep what was read at startup if this read came back worse
                        if (entry.Value != null && entry.Value.IsValid)
                            merged[entry.Key] = entry.Value;
                    }

                    Ddr4Spd = merged;
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshDdr4SpdInfo: {ex.Message}");
                return false;
            }
        }

        public bool RefreshTelemetry(int uiRefreshIntervalMs = 2000)
        {
            // Work on snapshots; RefreshSpdInfo may swap SpdInfo concurrently.
            Dictionary<byte, Ddr5SpdInfo> snapshot = null;
            Dictionary<byte, Ddr4ThermalData> ddr4Sensors = null;

            if (Type == MemType.DDR4)
            {
                ddr4Sensors = Ddr4ThermalSensors;
                if (ddr4Sensors == null || ddr4Sensors.Count == 0)
                    return false;
            }
            else
            {
                if (!IsSpdSupported)
                    return false;

                snapshot = SpdInfo;
                if (snapshot == null || snapshot.Count == 0)
                    return false;
            }

            // Mask off the sign bit to handle TickCount wrap-around safely.
            long now = Environment.TickCount & int.MaxValue;
            long previousTick;

            // Reserve this refresh before any I/O: a second caller that arrives while this one waits for
            // or holds the SMBus sees the new tick and skips, instead of queueing the same reads again.
            lock (telemetryThrottleLock)
            {
                previousTick = LastTelemetryRefreshTick;
                long elapsed = now - previousTick;

                if (elapsed >= 0 && elapsed < uiRefreshIntervalMs)
                    return false;

                LastTelemetryRefreshTick = now;
            }

            bool updated = false;

            if (!Mutexes.WaitSmbus(5000))
            {
                Debug.WriteLine("RefreshTelemetry: Timeout waiting for SMBus bus mutex.");
                ReleaseTelemetryReservation(now, previousTick);
                return false;
            }

            try
            {
                updated = ddr4Sensors != null
                    ? Ddr4ThermalSensor.RefreshAllNoLock(smbusDriver, ddr4Sensors)
                    : RefreshDdr5TelemetryNoLock(snapshot);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshTelemetry: {ex.Message}");
            }
            finally
            {
                Mutexes.ReleaseSmbus();
            }

            // Nothing was read: let the next call try again right away, as before.
            if (!updated)
                ReleaseTelemetryReservation(now, previousTick);

            return updated;
        }

        // Reads the PMIC and SPD hub temperature of every DDR5 module; the SMBus mutex must be held.
        private bool RefreshDdr5TelemetryNoLock(Dictionary<byte, Ddr5SpdInfo> snapshot)
        {
            bool updated = false;

            smbusDriver.ChangePortNoLock(-1, out int savedPort);

            try
            {
                if (!Ddr5SpdReader.SelectHubPortNoLock(false))
                    return false;

                foreach (var info in snapshot)
                {
                    Ddr5PmicData pd = info.Value?.PmicData;
                    if (pd != null && pd.IsValid)
                    {
                        // Set points first: they can be changed at runtime, and the voltage mode is decided
                        // from them together with the measured rails.
                        Ddr5PmicReader.ReadVoltageSettingsNoLock(smbusDriver, pd.I2cAddress, pd);
                        Ddr5PmicReader.ReadAllAdcVoltagesNoLock(smbusDriver, pd.I2cAddress, pd);
                        Ddr5PmicReader.ReadPmicTemperatureNoLock(smbusDriver, pd.I2cAddress, pd);
                        Ddr5PmicReader.ReadPmicTelemetryNoLock(smbusDriver, pd.I2cAddress, pd);
                        updated = true;
                    }

                    // The SPD hub sensor is read even when the module's PMIC is not readable. The key is the hub address.
                    Ddr5ThermalData td = info.Value?.ThermalData;
                    if (td != null && td.IsValid && td.TempSensorEnabled)
                    {
                        // Merge updated temperature and status into the existing thermal data
                        // instead of replacing the whole object, preserving other cached fields.
                        if (Ddr5ThermalSensor.RefreshTemperatureAndStatusNoLock(smbusDriver, info.Key, td))
                            updated = true;
                    }
                }
            }
            finally
            {
                if (savedPort >= 0)
                    smbusDriver.ChangePortNoLock(savedPort);
            }

            return updated;
        }

        // Undoes a reservation that read nothing, unless a later refresh has reserved since.
        private void ReleaseTelemetryReservation(long reservedTick, long previousTick)
        {
            lock (telemetryThrottleLock)
            {
                if (LastTelemetryRefreshTick == reservedTick)
                    LastTelemetryRefreshTick = previousTick;
            }
        }

        private void ReadModulesInfo()
        {
            foreach (var module in SMBiosSingleton.Instance.MemoryDevices)
            {
                if (module.Size > 0)
                {
                    ulong sizeInBytes = (ulong)module.Size * 1024 * 1024;
                    var type = SMBiosDramTypeToMemType(module.Type);
                    Modules.Add(new MemoryModule(module.PartNumber.Trim(), module.BankLocator.Trim(),
                        module.ManufacturerName.Trim(), module.DeviceLocator.Trim(),
                        sizeInBytes, module.Speed, type));
                }
            }

            if (Modules.Count > 0)
            {
                ulong totalCapacity = 0UL;
                foreach (MemoryModule module in Modules)
                {
                    totalCapacity += module.Capacity.SizeInBytes;
                }
                TotalCapacity = new Capacity(totalCapacity);
                Type = Modules[0].Type;
            }
        }

        // TODO: Use rank from spd with priority over register value
        private MemRank GetRank(uint address)
        {
            if (Type == MemType.DDR4 || Type == MemType.LPDDR4)
            {
                return (MemRank)Utils.GetBits(cpu.ReadDwordNoLock(address), 0, 1);
            }
            if (Type == MemType.DDR5 || Type == MemType.LPDDR5)
            {
                var value = cpu.ReadDwordNoLock(address);
                if (value != 0 && (value == 0x07FFFBFE || Utils.GetBits(value, 9, 2) < 3))
                    return MemRank.DR;
                value = cpu.ReadDwordNoLock(address + 4);
                if (value != 0 && (value == 0x07FFFBFE || Utils.GetBits(value, 9, 2) < 3))
                    return MemRank.DR;
            }

            return MemRank.SR;
        }

        private DramAddressConfig GetAddressConfig(uint address)
        {
            var value = cpu.ReadDwordNoLock(address);
            var config = new DramAddressConfig();
            if (value != 0)
            {
                config.NumBanks = Utils.GetBits(value, 20, 2);
                config.NumCol = 5 + Utils.GetBits(value, 16, 4);
                config.NumRow = 10 + Utils.GetBits(value, 8, 4);
                config.NumRM = Utils.GetBits(value, 4, 3);
                config.NumBankGroups = Utils.GetBits(value, 2, 2);
                config.Rank = GetRank(address - 0x20);
            }
            return config;
        }

        private void ReadChannelsNoLock()
        {
            int dimmIndex = 0;
            //uint dimmsPerChannel = 1;

            // Get the offset by probing the UMC0 to UMC7
            // It appears that offsets 0x80 and 0x84 are DIMM config registers
            // When a DIMM is DR, bit 0 is set to 1
            // 0x50000
            // offset 0, bit 0 when set to 1 means DIMM1 is installed
            // offset 8, bit 0 when set to 1 means DIMM2 is installed
            for (uint i = 0; i < MAX_CHANNELS * ChannelsPerDimm; i += ChannelsPerDimm)
            {
                try
                {
                    if (dimmIndex >= Modules.Count)
                        break;

                    uint offset = i << 20;
                    bool channel = Utils.GetBits(cpu.ReadDwordNoLock(offset | 0x50DF0), 19, 1) == 0;
                    bool dimm1 = Utils.GetBits(cpu.ReadDwordNoLock(offset | 0x50000), 0, 1) == 1;
                    bool dimm2 = Utils.GetBits(cpu.ReadDwordNoLock(offset | 0x50008), 0, 1) == 1;
                    bool enabled = channel && (dimm1 || dimm2);

                    Channels.Add(new Channel()
                    {
                        Enabled = enabled,
                        Offset = offset,
                    });

                    if (enabled)
                    {
                        if (dimm1)
                        {
                            var address = offset | ((Type == MemType.DDR4 || Type == MemType.LPDDR4) ? 0x50080u : 0x50020u);
                            MemoryModule module = Modules[dimmIndex];
                            module.Slot = $"{Convert.ToChar(i / ChannelsPerDimm + 65)}1";
                            module.DctOffset = offset;
                            module.Rank = GetRank(address);
                            module.AddressConfig = GetAddressConfig(offset | 0x50040);
                            dimmIndex += 1;
                        }

                        if (dimm2)
                        {
                            var address = offset | ((Type == MemType.DDR4 || Type == MemType.LPDDR4) ? 0x50084u : 0x50028u);
                            MemoryModule module = Modules[dimmIndex];
                            module.Slot = $"{Convert.ToChar(i / ChannelsPerDimm + 65)}2";
                            module.DctOffset = offset;
                            module.Rank = GetRank(address);
                            module.AddressConfig = GetAddressConfig(offset | 0x50048);
                            dimmIndex += 1;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ReadChannelsNoLock: channel {i} skipped ({ex.GetType().Name}): {ex.Message}");
                }
            }
        }
    }
}
/*
0x0025060c
0x25060c    0010 0101 0000 0110 0000 1100
    numBanks[21:20]  32 banks(2h)
    numCol[19:16] 10 + 5
    NumRow[11:8] 10 + 6
    numRM[6:4] 0
    bank groups 8 (3h)

> 0x00150508
0x150508    0001 0101 0000 0101 0000 1000
    numBanks [21:20]  32 banks (2h)
    numCol [19:16] 10 + 5
    NumRow [11:8] 10 + 5
    numRM [6:4] 0
    bank groups [3:2] 4 (2h)

*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ZenStates.Core
{
    /// <summary>
    /// Provides access to AMD Ryzen SMU (System Management Unit) functionality
    /// for reading PM table sensors and system information.
    /// Adopted from LibreHardwareMonitor
    /// </summary>
    public class RyzenSmu : IDisposable
    {
        #region Command Constants
        private const string IOCTL_GET_SMU_VERSION = "ioctl_get_smu_version";
        private const string IOCTL_SEND_SMU_COMMAND = "ioctl_send_smu_command";
        private const string IOCTL_READ_SMU_REGISTER = "ioctl_read_smu_register";
        private const string IOCTL_WRITE_SMU_REGISTER = "ioctl_write_smu_register";
        private const string IOCTL_RESOLVE_PM_TABLE = "ioctl_resolve_pm_table";
        private const string IOCTL_READ_PM_TABLE = "ioctl_read_pm_table";
        private const string IOCTL_UPDATE_PM_TABLE = "ioctl_update_pm_table";
        private const string IOCTL_GET_CODE_NAME = "ioctl_get_code_name";
        #endregion

        #region Private Fields

        private readonly PawnIo _pawnIo;
        private readonly CpuCodeName _cpuCodeName;
        private readonly bool _isSupported;
        private readonly Exception _initializationException;
        private readonly uint _pmTableVersion;
        private readonly long _dramBaseAddress;
        private uint _pmTableSize;
        private uint _pmTableSizeAlt;
        private uint _detectedPmTableSize;
        private volatile bool _disposed;
        private readonly object _disposeLock = new object();

        #endregion

        #region Static Data

        /// <summary>
        /// Dictionary mapping PM table versions to their supported sensor definitions.
        /// Each version corresponds to different CPU architectures (Zen, Zen 2, Zen 3, Zen 4).
        /// </summary>
        private static readonly Dictionary<uint, Dictionary<uint, SmuSensorDefinition>> SupportedPmTableVersions =
            new Dictionary<uint, Dictionary<uint, SmuSensorDefinition>>()
            {
                {
                    // Zen Raven Ridge APU
                    0x001E0004, new Dictionary<uint, SmuSensorDefinition>
                    {
                        { 7, new SmuSensorDefinition("TDC", SensorType.Current, 1.0f) },
                        { 11, new SmuSensorDefinition("EDC", SensorType.Current, 1.0f) },
                        { 66, new SmuSensorDefinition("SoC", SensorType.Current, 1.0f) },
                        { 67, new SmuSensorDefinition("SoC", SensorType.Power, 1.0f) },
                        { 108, new SmuSensorDefinition("Core #1", SensorType.Temperature, 1.0f) },
                        { 109, new SmuSensorDefinition("Core #2", SensorType.Temperature, 1.0f) },
                        { 110, new SmuSensorDefinition("Core #3", SensorType.Temperature, 1.0f) },
                        { 111, new SmuSensorDefinition("Core #4", SensorType.Temperature, 1.0f) },
                        { 150, new SmuSensorDefinition("GFX", SensorType.Voltage, 1.0f) },
                        { 151, new SmuSensorDefinition("GFX", SensorType.Temperature, 1.0f) },
                        { 154, new SmuSensorDefinition("GFX", SensorType.Clock, 1.0f) },
                        { 156, new SmuSensorDefinition("GFX", SensorType.Load, 1.0f) },
                        { 166, new SmuSensorDefinition("Fabric", SensorType.Clock, 1.0f) },
                        { 177, new SmuSensorDefinition("Uncore", SensorType.Clock, 1.0f) },
                        { 178, new SmuSensorDefinition("Memory", SensorType.Clock, 1.0f) },
                        { 342, new SmuSensorDefinition("Displays", SensorType.Factor, 1.0f) }
                    }
                },
                {
                    // Zen 2
                    0x00240903, new Dictionary<uint, SmuSensorDefinition>
                    {
                        { 15, new SmuSensorDefinition("TDC", SensorType.Current, 1.0f) },
                        { 21, new SmuSensorDefinition("EDC", SensorType.Current, 1.0f) },
                        { 48, new SmuSensorDefinition("Fabric", SensorType.Clock, 1.0f) },
                        { 50, new SmuSensorDefinition("Uncore", SensorType.Clock, 1.0f) },
                        { 51, new SmuSensorDefinition("Memory", SensorType.Clock, 1.0f) },
                        { 115, new SmuSensorDefinition("SoC", SensorType.Temperature, 1.0f) }
                    }
                },
                {
                    // Zen 3
                    0x00380805, new Dictionary<uint, SmuSensorDefinition>
                    {
                        { 3, new SmuSensorDefinition("TDC", SensorType.Current, 1.0f) },
                        // Note: EDC requires post-processing for this version
                        { 48, new SmuSensorDefinition("Fabric", SensorType.Clock, 1.0f) },
                        { 50, new SmuSensorDefinition("Uncore", SensorType.Clock, 1.0f) },
                        { 51, new SmuSensorDefinition("Memory", SensorType.Clock, 1.0f) },
                        { 127, new SmuSensorDefinition("SoC", SensorType.Temperature, 1.0f) }
                    }
                },
                {
                    // Zen 4
                    0x00540004, new Dictionary<uint, SmuSensorDefinition>
                    {
                        { 3, new SmuSensorDefinition("CPU PPT", SensorType.Power, 1.0f) },
                        { 11, new SmuSensorDefinition("Package", SensorType.Temperature, 1.0f) },
                        { 20, new SmuSensorDefinition("Core Power", SensorType.Power, 1.0f) },
                        { 21, new SmuSensorDefinition("SOC Power", SensorType.Power, 1.0f) },
                        { 22, new SmuSensorDefinition("Misc Power", SensorType.Power, 1.0f) },
                        { 26, new SmuSensorDefinition("Total Power", SensorType.Power, 1.0f) },
                        { 47, new SmuSensorDefinition("VDDCR", SensorType.Voltage, 1.0f) },
                        { 48, new SmuSensorDefinition("TDC", SensorType.Current, 1.0f) },
                        { 49, new SmuSensorDefinition("EDC", SensorType.Current, 1.0f) },
                        { 52, new SmuSensorDefinition("VDDCR SoC", SensorType.Voltage, 1.0f) },
                        { 57, new SmuSensorDefinition("VDD Misc", SensorType.Voltage, 1.0f) },
                        { 70, new SmuSensorDefinition("Fabric", SensorType.Clock, 1.0f) },
                        { 74, new SmuSensorDefinition("Uncore", SensorType.Clock, 1.0f) },
                        { 78, new SmuSensorDefinition("Memory", SensorType.Clock, 1.0f) },
                        { 211, new SmuSensorDefinition("IOD Hotspot", SensorType.Temperature, 1.0f) },
                        { 268, new SmuSensorDefinition("LDO VDD", SensorType.Voltage, 1.0f) },
                        { 539, new SmuSensorDefinition("L3 (CCD1)", SensorType.Temperature, 1.0f) },
                        { 540, new SmuSensorDefinition("L3 (CCD2)", SensorType.Temperature, 1.0f) }
                    }
                }
            };

        #endregion

        #region Constructor and Initialization

        /// <summary>
        /// Initializes a new instance of the RyzenSmu class.
        /// Automatically detects CPU and initializes PM table access.
        /// </summary>
        public RyzenSmu()
        {
            try
            {
                // Load the PawnIO module from embedded resource
                string resourceName = "ZenStates.Core.Resources.PawnIo.RyzenSMU.bin";
                _pawnIo = PawnIo.LoadModuleFromResource(typeof(RyzenSmu).Assembly, resourceName);
                //_pawnIo = PawnIo.LoadModuleFromFile("RyzenSMU.amx");

                // Get CPU information
                _cpuCodeName = (CpuCodeName)GetCodeName();

                // Resolve PM table information
                ResolvePmTable(out _pmTableVersion, out _dramBaseAddress);

                // Configure PM table size based on CPU and version
                ConfigurePmTableSize();

                _isSupported = true;
            }
            catch (Exception ex)
            {
                _isSupported = false;
                _initializationException = ex;
                Debug.WriteLine("RyzenSmu initialization failed: " + ex.Message);
            }
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets a value indicating whether the PawnIO module is loaded.
        /// </summary>
        public bool IsLoaded => _pawnIo.IsLoaded;

        /// <summary>
        /// Gets a value indicating whether the current CPU is supported.
        /// </summary>
        public bool IsSupported => _isSupported;

        /// <summary>
        /// Gets the CPU code name.
        /// </summary>
        public CpuCodeName CpuCodeName => _cpuCodeName;

        /// <summary>
        /// Gets the PM table version.
        /// </summary>
        public uint PmTableVersion => _pmTableVersion;

        /// <summary>
        /// Gets the PM table szie.
        /// Temporary add setter to update from legacy PowerTable if needed
        /// </summary>
        public uint PmTableSize
        {
            get => _pmTableSize;
            set => _pmTableSize = value;
        }

        /// <summary>
        /// Gets the DRAM base address for the PM table.
        /// </summary>
        public long DramBaseAddress => _dramBaseAddress;

        /// <summary>
        /// Gets a value indicating whether the PM table layout is defined for this CPU.
        /// </summary>
        public bool IsPmTableLayoutDefined => SupportedPmTableVersions.ContainsKey(_pmTableVersion);

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the SMU version.
        /// </summary>
        /// <returns>The SMU version as a 32-bit unsigned integer.</returns>
        public uint GetSmuVersion()
        {
            ThrowIfDisposed();

            if (!Mutexes.WaitPciBus(5000))
                return (int)SMU.Status.TIMEOUT_MUTEX_LOCK;

            try
            {
                long[] result = _pawnIo.Execute(IOCTL_GET_SMU_VERSION, new long[0], 1);
                return Convert.ToUInt32(result[0] & 0xffffffff);
            }
            finally
            {
                Mutexes.ReleasePciBus();
            }
        }

        /// <summary>
        /// Send the SMU Command to RSMU. PawnIO module does not support other mailboxes.
        /// </summary>
        /// <returns>The SMU status converted to HR result.</returns>
        public int SendSmuCommand(uint command, ref uint[] args)
        {
            ThrowIfDisposed();

            if (!Mutexes.WaitPciBus(5000))
                return (int)SMU.Status.TIMEOUT_MUTEX_LOCK;

            try
            {
                long[] inputBuffer = new long[7];
                inputBuffer[0] = command;
                for (int i = 0; i < 6; i++)
                {
                    inputBuffer[i + 1] = args[i];
                }
                long[] outBuffer = new long[6];
                int result = _pawnIo.ExecuteHr(IOCTL_SEND_SMU_COMMAND, inputBuffer, 7, outBuffer, 6, out uint returnSize);
                for (int i = 0; i < 6; i++)
                {
                    args[i] = (uint)outBuffer[i];
                }
                return result;
            }
            finally
            {
                Mutexes.ReleasePciBus();
            }
        }

        /// <summary>
        /// Read the SMU Register.
        /// </summary>
        /// <returns>Reading status: true - success, false - failed.</returns>
        public bool SmuReadReg(uint register, out uint value)
        {
            ThrowIfDisposed();

            if (!Mutexes.WaitPciBus(5000))
            {
                value = 0;
                return false;
            }

            try
            {
                return SmuReadRegInternal(register, out value);
            }
            finally
            {
                Mutexes.ReleasePciBus();
            }
        }

        internal bool SmuReadRegInternal(uint register, out uint value)
        {
            ThrowIfDisposed();

            try
            {
                long[] inputBuffer = new long[1];
                inputBuffer[0] = register;
                long[] outBuffer = new long[1];
                int result = _pawnIo.ExecuteHr(IOCTL_READ_SMU_REGISTER, inputBuffer, 1, outBuffer, 1, out uint returnSize);
                value = (uint)outBuffer[0];
                return result == 0;
            }
            catch
            {
                value = 0;
                return false;
            }
        }

        /// <summary>
        /// Write the SMU Register.
        /// </summary>
        /// <returns>Reading status: true - success, false - failed.</returns>
        internal bool SmuWriteRegInternal(uint register, uint value)
        {
            ThrowIfDisposed();

            try
            {
                long[] inputBuffer = new long[2];
                inputBuffer[0] = register;
                inputBuffer[1] = value;
                long[] outBuffer = new long[0];
                int result = _pawnIo.ExecuteHr(IOCTL_WRITE_SMU_REGISTER, inputBuffer, 2, outBuffer, 0, out uint returnSize);
                return result == 0;
            }
            catch
            {
                return false;
            }
        }

        public bool SmuWriteReg(uint register, uint value)
        {
            ThrowIfDisposed();

            if (!Mutexes.WaitPciBus(5000))
            {
                return false;
            }

            try
            {
                return SmuWriteRegInternal(register, value);
            }
            finally
            {
                Mutexes.ReleasePciBus();
            }
        }

        /// <summary>
        /// Gets the PM table structure definition for the current CPU.
        /// </summary>
        /// <returns>A dictionary mapping table indices to sensor definitions.</returns>
        public Dictionary<uint, SmuSensorDefinition> GetPmTableStructure()
        {
            ThrowIfDisposed();

            if (!IsPmTableLayoutDefined)
                return new Dictionary<uint, SmuSensorDefinition>();

            return new Dictionary<uint, SmuSensorDefinition>(SupportedPmTableVersions[_pmTableVersion]);
        }

        /// <summary>
        /// Reads the current PM table values.
        /// </summary>
        /// <returns>An array of float values representing the PM table contents.</returns>
        public float[] GetPmTable()
        {
            ThrowIfDisposed();

            if (!_isSupported)
                return new float[] { 0 };

            float[] table = null;

            // Retry mechanism for PM table reading
            for (int retriesLeft = 2; retriesLeft > 0; retriesLeft--)
            {
                try
                {
                    table = UpdateAndReadPmTable();

                    // Check if table is valid (not empty and first value is not zero)
                    if (table.Length > 0 && table[0] != 0)
                        return table;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("GetPmTable attempt failed: " + ex.Message);
                }
            }

            return table?.Length == 0 ? new float[] { 0 } : table;
        }

        /// <summary>
        /// Generates a detailed report of the SMU status and PM table contents.
        /// </summary>
        /// <returns>A formatted string containing the SMU report.</returns>
        public string GetReport()
        {
            ThrowIfDisposed();

            StringBuilder report = new StringBuilder();

            report.AppendLine("Ryzen SMU Report");
            report.AppendLine(new string('=', 50));
            report.AppendLine();
            report.AppendFormat("CPU Code Name: {0}\n", _cpuCodeName);
            report.AppendFormat("PM Table Version: 0x{0:X8}\n", _pmTableVersion);
            report.AppendFormat("CPU Supported: {0}\n", _isSupported);
            report.AppendFormat("PM Table Layout Defined: {0}\n", IsPmTableLayoutDefined);

            if (_isSupported)
            {
                report.AppendFormat("PM Table Size: 0x{0:X}\n", _pmTableSize);
                report.AppendFormat("PM Table Size (detected): 0x{0:X}\n", _detectedPmTableSize);
                report.AppendFormat("PM Table Base Address: 0x{0:X16}\n", _dramBaseAddress);
                report.AppendLine();

                AppendPmTableDump(report);
            }
            else
            {
                report.AppendLine();
                if (_initializationException != null)
                    report.AppendFormat("Initialization Error: {0}\n", _initializationException.Message);
                else
                    report.AppendFormat("Initialization Error: {0}\n", "Unknown error");
            }

            return report.ToString();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Gets the CPU code name from the SMU.
        /// </summary>
        /// <returns>The CPU code name as a int value.</returns>
        private int GetCodeName()
        {
            long[] input = new long[0];
            long[] output = new long[1];

            int status = _pawnIo.ExecuteHr(IOCTL_GET_CODE_NAME, input, 0, output, 1, out uint returnSize);
            if (status != 0 || returnSize == 0 || output.Length == 0)
            {
                Debug.WriteLine($"ioctl_get_code_name failed with status: 0x{status:X8}, output length: {output.Length}, returnSize: {returnSize}");
                return (int)CpuCodeName.Undefined;
            }
            return Convert.ToInt32(output[0] & 0xffffffff);
        }

        /// <summary>
        /// Resolves PM table information including version and base address.
        /// </summary>
        /// <param name="version">The PM table version.</param>
        /// <param name="baseAddress">The PM table base address.</param>
        private void ResolvePmTable(out uint version, out long baseAddress)
        {
            if (!Mutexes.WaitPciBus(5000))
                throw new TimeoutException("Timeout waiting for PCI bus mutex");

            try
            {
                long[] result = _pawnIo.Execute(IOCTL_RESOLVE_PM_TABLE, new long[2], 2);
                version = Convert.ToUInt32(result[0] & 0xffffffff);
                baseAddress = result[1];
            }
            finally
            {
                Mutexes.ReleasePciBus();
            }
        }

        public long[] ReadPmTable(int size)
        {
            long[] outArray = _pawnIo.Execute(IOCTL_READ_PM_TABLE, new long[size], size);
            return outArray;
        }

        public void UpdatePmTable()
        {
            if (!Mutexes.WaitPciBus(5000))
                throw new TimeoutException("Timeout waiting for PCI bus mutex");

            try
            {
                _pawnIo.Execute(IOCTL_UPDATE_PM_TABLE, new long[0] { }, 0);
            }
            finally
            {
                Mutexes.ReleasePciBus();
            }
        }

        /// <summary>
        /// Updates and reads the PM table from DRAM.
        /// </summary>
        /// <returns>An array of float values from the PM table.</returns>
        private float[] UpdateAndReadPmTable()
        {
            float[] table = new float[_pmTableSize / 4];

            // Update the PM table
            UpdatePmTable();
            // Read the PM table
            long[] rawData = ReadPmTable((int)((_pmTableSize + 7) / 8));
            Buffer.BlockCopy(rawData, 0, table, 0, (int)_pmTableSize);

            return table;
        }

        /// <summary>
        /// Appends PM table dump information to the report.
        /// </summary>
        /// <param name="report">The StringBuilder to append to.</param>
        private void AppendPmTableDump(StringBuilder report)
        {
            report.AppendLine("PM Table Dump:");
            report.AppendLine("  Index  Offset   Value      Sensor");
            report.AppendLine(new string('-', 60));

            try
            {
                float[] pmValues = UpdateAndReadPmTable();
                Dictionary<uint, SmuSensorDefinition> sensorMap = GetPmTableStructure();

                for (int i = 0; i < pmValues.Length; i++)
                {
                    uint index = (uint)i;
                    string sensorInfo = sensorMap.ContainsKey(index)
                        ? $"{sensorMap[index].Name} ({sensorMap[index].Type})"
                        : "Unknown";

                    report.AppendFormat("  {0,5}  0x{1:X3}   {2,8:F2}   {3}\n",
                        i, i * 4, pmValues[i], sensorInfo);
                }
            }
            catch (Exception ex)
            {
                report.AppendFormat("  Error reading PM table: {0}\n", ex.Message);
            }
        }

        /// <summary>
        /// Throws an ObjectDisposedException if the object has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RyzenSmu));
        }

        #endregion

        #region PM Table Size Configuration Methods

        /// <summary>
        /// Configures the PM table size based on the CPU code name and PM table version.
        /// </summary>
        private void ConfigurePmTableSize()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.Matisse:
                    ConfigureMatissePmTableSize();
                    break;

                case CpuCodeName.Vermeer:
                    ConfigureVermeerPmTableSize();
                    break;

                case CpuCodeName.Renoir:
                case CpuCodeName.Rembrandt:
                    ConfigureRenoirPmTableSize();
                    break;

                case CpuCodeName.Cezanne:
                    ConfigureCezannePmTableSize();
                    break;

                case CpuCodeName.Picasso:
                case CpuCodeName.RavenRidge:
                case CpuCodeName.RavenRidge2:
                    ConfigureRavenRidgePmTableSize();
                    break;

                case CpuCodeName.Raphael:
                case CpuCodeName.GraniteRidge:
                    ConfigureRaphaelPmTableSize();
                    break;

                // TODO: Implement all codenames and known table sizes; add fallback
                // Temporary use big enough default table size
                // The actual table refresh does not care about the size and the handling of supported codenames are done in the driver module
                default:
                    _pmTableSize = 0x994; // Default size for unsupported CPUs
                    break;
                    //throw new NotSupportedException($"CPU code name {_cpuCodeName} is not supported");
            }

            _detectedPmTableSize = _pmTableSize;
        }

        private void ConfigureMatissePmTableSize()
        {
            switch (_pmTableVersion)
            {
                case 0x240902: _pmTableSize = 0x514; break;
                case 0x240903: _pmTableSize = 0x518; break;
                case 0x240802: _pmTableSize = 0x7E0; break;
                case 0x240803: _pmTableSize = 0x7E4; break;
                    //default:
                    //    throw new NotSupportedException($"Matisse PM table version 0x{_pmTableVersion:X8} is not supported");
            }
        }

        private void ConfigureVermeerPmTableSize()
        {
            switch (_pmTableVersion)
            {
                case 0x2D0903: _pmTableSize = 0x594; break;
                case 0x380904: _pmTableSize = 0x5A4; break;
                case 0x380905: _pmTableSize = 0x5D0; break;
                case 0x2D0803: _pmTableSize = 0x894; break;
                case 0x380804: _pmTableSize = 0x8A4; break;
                case 0x380805: _pmTableSize = 0x8F0; break;
                    //default:
                    //    throw new NotSupportedException($"Vermeer PM table version 0x{_pmTableVersion:X8} is not supported");
            }
        }

        private void ConfigureRenoirPmTableSize()
        {
            switch (_pmTableVersion)
            {
                case 0x370000: _pmTableSize = 0x794; break;
                case 0x370001: _pmTableSize = 0x884; break;
                case 0x370002:
                case 0x370003: _pmTableSize = 0x88C; break;
                case 0x370004: _pmTableSize = 0x8AC; break;
                case 0x370005: _pmTableSize = 0x8C8; break;
                case 0x450005: _pmTableSize = 0xAA4; break;
                    //default:
                    //    throw new NotSupportedException($"Renoir PM table version 0x{_pmTableVersion:X8} is not supported");
            }
        }

        private void ConfigureCezannePmTableSize()
        {
            switch (_pmTableVersion)
            {
                case 0x400005: _pmTableSize = 0x944; break;
                    //default:
                    //    throw new NotSupportedException($"Cezanne PM table version 0x{_pmTableVersion:X8} is not supported");
            }
        }

        private void ConfigureRavenRidgePmTableSize()
        {
            _pmTableSizeAlt = 0xA4;
            _pmTableSize = 0x608 + _pmTableSizeAlt;
        }

        private void ConfigureRaphaelPmTableSize()
        {
            switch (_pmTableVersion)
            {
                case 0x00540004: _pmTableSize = 0x948; break;
                case 0x00540104: _pmTableSize = 0x950; break;
                case 0x00620205: _pmTableSize = 0x994; break;
                    //default:
                    //    throw new NotSupportedException($"Raphael/GraniteRidge PM table version 0x{_pmTableVersion:X8} is not supported");
            }
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the RyzenSmu and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            lock (_disposeLock)
            {
                if (_disposed)
                    return;

                if (disposing)
                {
                    _pawnIo?.Close();
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer for RyzenSmu class.
        /// </summary>
        ~RyzenSmu()
        {
            Dispose(false);
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Represents a sensor definition in the PM table.
    /// </summary>
    public struct SmuSensorDefinition
    {
        /// <summary>
        /// The name of the sensor.
        /// </summary>
        public string Name
        {
            get;
        }

        /// <summary>
        /// The type of sensor measurement.
        /// </summary>
        public SensorType Type
        {
            get;
        }

        /// <summary>
        /// The scale factor to apply to the raw sensor value.
        /// </summary>
        public float Scale
        {
            get;
        }

        /// <summary>
        /// Initializes a new instance of the SmuSensorDefinition struct.
        /// </summary>
        /// <param name="name">The sensor name.</param>
        /// <param name="type">The sensor type.</param>
        /// <param name="scale">The scale factor.</param>
        public SmuSensorDefinition(string name, SensorType type, float scale)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type;
            Scale = scale;
        }
    }

    /// <summary>
    /// Defines the types of sensors available.
    /// </summary>
    public enum SensorType
    {
        /// <summary>Voltage measurement in volts (V)</summary>
        Voltage,
        /// <summary>Current measurement in amperes (A)</summary>
        Current,
        /// <summary>Power measurement in watts (W)</summary>
        Power,
        /// <summary>Clock frequency in MHz</summary>
        Clock,
        /// <summary>Temperature in degrees Celsius (°C)</summary>
        Temperature,
        /// <summary>Load percentage (%)</summary>
        Load,
        /// <summary>Frequency in Hz</summary>
        Frequency,
        /// <summary>Fan speed in RPM</summary>
        Fan,
        /// <summary>Flow rate in L/h</summary>
        Flow,
        /// <summary>Control percentage (%)</summary>
        Control,
        /// <summary>Level percentage (%)</summary>
        Level,
        /// <summary>Unitless factor (1)</summary>
        Factor,
        /// <summary>Data in GB (2^30 Bytes)</summary>
        Data,
        /// <summary>Small data in MB (2^20 Bytes)</summary>
        SmallData,
        /// <summary>Throughput in B/s</summary>
        Throughput,
        /// <summary>Time span in seconds</summary>
        TimeSpan,
        /// <summary>Timing in nanoseconds (ns)</summary>
        Timing,
        /// <summary>Energy in milliwatt-hours (mWh)</summary>
        Energy,
        /// <summary>Noise in dBA</summary>
        Noise,
        /// <summary>Conductivity in µS/cm</summary>
        Conductivity,
        /// <summary>Humidity percentage (%)</summary>
        Humidity
    }

    /// <summary>
    /// Defines the CPU code names for different AMD processor families.
    /// This enum should match the enum in RyzenSmu PawnIO module.
    /// https://github.com/namazso/PawnIO.Modules/blob/5628d05dd7045d3fdf69fd2ed2dc8086e90f238c/RyzenSMU.p#L22
    /// </summary>
    public enum CpuCodeName
    {
        Undefined = -1,
        Colfax,
        Renoir,
        Picasso,
        Matisse,
        Threadripper,
        CastlePeak,
        RavenRidge,
        RavenRidge2,
        SummitRidge,
        PinnacleRidge,
        Rembrandt,
        Vermeer,
        Vangogh,
        Cezanne,
        Milan,
        Dali,
        Raphael,
        GraniteRidge,
        Naples,
        FireFlight,
        Rome,
        Chagall,
        Lucienne,
        Phoenix,
        Phoenix2,
        Mendocino,
        Genoa,
        StormPeak,
        DragonRange,
        Mero,
        HawkPoint,
        StrixPoint,
        StrixHalo,
        KrackanPoint,
        KrackanPoint2,
        Turin,
        TurinD,
        Bergamo,
        ShimadaPeak,
    }

    #endregion
}

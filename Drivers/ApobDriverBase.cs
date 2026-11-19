using System;
using System.Collections.Generic;

namespace ZenStates.Core.Drivers
{
    /// <summary>Which driver locates and reads the APOB table (<see cref="CoreOptions.ApobDriver"/>).</summary>
    public enum ApobDriverType
    {
        /// <summary>The PawnIO AMDAPOB module when it loads, otherwise InpOut.</summary>
        Auto = 0,
        /// <summary>The PawnIO AMDAPOB module only.</summary>
        PawnIo = 1,
        /// <summary>Physical memory reads through the InpOut driver only.</summary>
        InpOut = 2,
    }

    /// <summary>Where a driver found the APOB table, from its header.</summary>
    public sealed class ApobLocation
    {
        /// <summary>Physical address of the table.</summary>
        public uint Address { get; internal set; }
        /// <summary>APOB header version.</summary>
        public uint Version { get; internal set; }
        /// <summary>Size of the whole table in bytes, header included.</summary>
        public uint TableSize { get; internal set; }
        /// <summary>Header size in bytes, also the offset of the first entry.</summary>
        public uint HeaderSize { get; internal set; }
        /// <summary>How the address was found, for the debug report; null when not known.</summary>
        public string Description { get; internal set; }
    }

    /// <summary>
    /// Locates the APOB (AGESA PSP Output Block) of the running system and reads its bytes; parsing is left to
    /// <see cref="Hardware.Apob.Apob"/>. Pick one with <see cref="ApobProvider.Create"/>, by type or from
    /// <see cref="CoreOptions.ApobDriver"/>, or construct one directly.
    /// </summary>
    public abstract class ApobDriverBase : IDisposable
    {
        private const uint APOB_SIGNATURE = 0x424F5041; // "APOB"
        private const int APOB_MIN_SIZE = 0x10;

        private readonly object _sync = new object();
        private ApobLocation _location;
        private bool _located;
        private bool _disposed;

        public abstract ApobDriverType Type { get; }

        /// <summary>Name of the driver, shown with the location in the debug report.</summary>
        public abstract string Name { get; }

        /// <summary>The driver can be used: its module is loaded or its device is open.</summary>
        public abstract bool IsAvailable { get; }

        /// <summary>The steps of the lookup, for the debug report; empty when the driver keeps none.</summary>
        public List<string> Trace { get; } = new List<string>();

        /// <summary>
        /// Locates the table, once: later calls return the first result. Null when the driver is not available or
        /// the table was not found.
        /// </summary>
        public ApobLocation Locate()
        {
            lock (_sync)
            {
                if (_located)
                    return _location;

                _located = true;
                if (_disposed || !IsAvailable)
                    return null;

                try
                {
                    _location = LocateTable();
                }
                catch (Exception ex)
                {
                    Trace.Add("failed: " + ex.Message);
                    _location = null;
                }

                return _location;
            }
        }

        /// <summary>
        /// Reads the whole table at <paramref name="location"/>. Null when the read fails or the bytes do not start
        /// with the APOB signature.
        /// </summary>
        public byte[] Read(ApobLocation location)
        {
            if (location == null || location.TableSize < APOB_MIN_SIZE)
                return null;

            lock (_sync)
            {
                if (_disposed || !IsAvailable)
                    return null;

                byte[] table;
                try
                {
                    table = ReadTable(location);
                }
                catch (Exception ex)
                {
                    Trace.Add("read failed: " + ex.Message);
                    return null;
                }

                if (table == null || table.Length < location.TableSize || BitConverter.ToUInt32(table, 0) != APOB_SIGNATURE)
                    return null;

                return table;
            }
        }

        /// <summary>Locates the table; null when it was not found.</summary>
        protected abstract ApobLocation LocateTable();

        /// <summary>Reads <see cref="ApobLocation.TableSize"/> bytes of the table found by <see cref="LocateTable"/>.</summary>
        protected abstract byte[] ReadTable(ApobLocation location);

        protected virtual void Dispose(bool disposing)
        {
            _disposed = true;
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;

                Dispose(disposing: true);
            }

            GC.SuppressFinalize(this);
        }
    }
}

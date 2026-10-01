using System.Collections.Generic;

namespace ZenStates.Core.Hardware.Apob
{
    /// <summary>An event of <see cref="ApobEventLog"/> (APOB_ERROR_LOG).</summary>
    public sealed class ApobEvent
    {
        /// <summary>The severity, an AGESA_STATUS value.</summary>
        public uint EventClass { get; internal set; }

        /// <summary>The event code, e.g. 0x04120400 MEM_ERROR_PMU_TRAINING (MiscMemDefines.h).</summary>
        public uint EventInfo { get; internal set; }

        public uint DataA { get; internal set; }
        public uint DataB { get; internal set; }

        public string EventClassName
        {
            get { return ApobEventLog.GetEventClassName(EventClass); }
        }

        public override string ToString()
        {
            return string.Format("{0}: 0x{1:X8} (0x{2:X8}, 0x{3:X8})", EventClassName, EventInfo, DataA, DataB);
        }
    }

    /// <summary>
    /// EVENT_LOG_STRUCT (GEN group, type 6): the events (alerts, warnings, errors) the ABL logged during boot.
    /// </summary>
    /// <remarks>Data layout: Count u16, 2 bytes of padding, then up to 64 events of 16 bytes.</remarks>
    public sealed class ApobEventLog
    {
        private const uint EVENT_SIZE = 16;

        // AGESA_STATUS
        private static readonly string[] EventClassNames =
        {
            "Success",
            "Unsupported",
            "Bounds check",
            "Sync more data",
            "Sync slave assert",
            "Alert",
            "Warning",
            "Error",
            "Critical",
            "Fatal",
        };

        /// <summary>The number of events as stored, may be larger than <see cref="Events"/> when it overflowed.</summary>
        public uint Count { get; private set; }

        public List<ApobEvent> Events { get; private set; }

        public static string GetEventClassName(uint eventClass)
        {
            return eventClass < EventClassNames.Length ? EventClassNames[eventClass] : "Class " + eventClass;
        }

        public static ApobEventLog Decode(byte[] buffer, uint entryOffset)
        {
            if (!ApobBytes.TryGetData(buffer, entryOffset, out uint data, out uint size) || size < 4)
                return null;

            uint count = ApobBytes.U16(buffer, data);
            uint capacity = (size - 4) / EVENT_SIZE;

            var log = new ApobEventLog
            {
                Count = count,
                Events = new List<ApobEvent>(),
            };

            uint stored = count < capacity ? count : capacity;
            for (uint i = 0; i < stored; i++)
            {
                uint o = data + 4 + i * EVENT_SIZE;
                log.Events.Add(new ApobEvent
                {
                    EventClass = ApobBytes.U32(buffer, o),
                    EventInfo = ApobBytes.U32(buffer, o + 4),
                    DataA = ApobBytes.U32(buffer, o + 8),
                    DataB = ApobBytes.U32(buffer, o + 12),
                });
            }

            return log;
        }
    }
}

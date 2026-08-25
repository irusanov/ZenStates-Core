namespace ZenStates.Core.Hardware
{
    public enum SensorType
    {
        Voltage, // V
        Current, // A
        Power, // W
        Clock, // MHz
        Temperature, // °C
        Load, // %
        Frequency, // Hz
        Fan, // RPM
        Flow, // L/h
        Control, // %
        Level, // %
        Factor, // 1
        Data, // GB = 2^30 Bytes
        SmallData, // MB = 2^20 Bytes
        Throughput, // B/s
        TimeSpan, // Seconds
        Timing, // ns
        Energy, // milliwatt-hour (mWh)
        Noise, // dBA
        Conductivity, // µS/cm
        Humidity // %
    }

    public interface ISensor
    {
        int Index { get; }
        string Name { get; }
        SensorType SensorType { get; }
        float? Min { get; }
        float? Max { get; }
        float? Value { get; }
        void ResetMin();
        void ResetMax();
    }
}

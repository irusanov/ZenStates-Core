using System.Collections.Generic;

namespace ZenStates.Core.Hardware
{
    public class Sensor : ISensor
    {
        private float? _currentValue;

        public Sensor(string name, int index, SensorType sensorType, Parameter[] parameters = null)
        {
            Index = index;
            Name = name;
            SensorType = sensorType;
            Parameters = parameters;
        }

        public int Index { get; }

        public string Name { get; }

        public SensorType SensorType { get; }

        public Parameter[] Parameters { get; }

        public float? Min { get; private set; }

        public float? Max { get; private set; }

        public float? Value
        {
            get => _currentValue;
            set
            {
                _currentValue = value;
                if (value.HasValue && !float.IsNaN(value.Value) && !float.IsInfinity(value.Value))
                {
                    if (!Min.HasValue || Min > value)
                        Min = value;

                    if (!Max.HasValue || Max < value)
                        Max = value;
                }
            }
        }

        public void ResetMax()
        {
            Max = null;
        }

        public void ResetMin()
        {
            Min = null;
        }
    }
}

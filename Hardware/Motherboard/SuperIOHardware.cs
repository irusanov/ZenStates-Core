using System;
using System.Collections.Generic;
using System.Text;

namespace ZenStates.Core.Hardware.Motherboard
{
    internal sealed class SuperIOHardware
    {
        private readonly List<Voltage> _voltages;
        private readonly List<Temperature> _temperatures;
        private readonly List<Fan> _fans;
        private readonly List<Control> _controls;

        public SuperIOHardware()
        {

        }
    }
}

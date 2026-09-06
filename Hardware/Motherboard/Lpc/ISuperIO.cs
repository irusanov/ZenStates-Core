// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.
// Adаpted from LibreHardwareMonitor (https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)

namespace ZenStates.Core.Hardware.Motherboard.Lpc
{
    public interface ISuperIO
    {
        Chip Chip { get; }

        float?[] Voltages { get; }
        float?[] Temperatures { get; }
        float?[] Fans { get; }
        float?[] Controls { get; }

        void SetControl(int index, byte? value);

        byte? ReadGpio(int index);
        void WriteGpio(int index, byte value);

        string GetReport();
        void Update();
        void Close();
    }
}

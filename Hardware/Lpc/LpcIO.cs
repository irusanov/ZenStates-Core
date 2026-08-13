// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.
// Adаpted from LibreHardwareMonitor (https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace ZenStates.Core.Hardware.Lpc
{
    internal class LpcIO
    {
        // ReSharper disable InconsistentNaming
        private const byte BASE_ADDRESS_REGISTER = 0x60;
        private const byte ALTERNATE_BASE_ADDRESS_REGISTER = 0x64;
        private const byte CHIP_ID_REGISTER = 0x20;
        private const byte CHIP_REVISION_REGISTER = 0x21;
        private const byte IT87_ENVIRONMENT_CONTROLLER_LDN = 0x04;
        private const byte IT8705_GPIO_LDN = 0x05;
        private const byte IT87XX_GPIO_LDN = 0x07;
        private const byte IT87_CHIP_VERSION_REGISTER = 0x22;
        private const byte LOGICAL_DEVICE_ACTIVATE_REGISTER = 0x30;
        private const byte LOGICAL_DEVICE_ACTIVATE_ENABLED = 0x01;
        private const byte WINBOND_NUVOTON_HARDWARE_MONITOR_LDN = 0x0B;
        // ReSharper restore InconsistentNaming

        private static readonly ushort[] RegisterPorts = { 0x2E, 0x4E };
        private static readonly ushort[] ValuePorts = { 0x2F, 0x4F };

        private readonly StringBuilder _report = new StringBuilder();
        private readonly List<ISuperIO> _superIOs = new List<ISuperIO>();

        public LpcIO()
        {
            if (!Mutexes.WaitIsaBus(100))
                return;

            Detect();

            Mutexes.ReleaseIsaBus();
        }

        public ISuperIO[] SuperIO
        {
            get { return _superIOs.ToArray(); }
        }

        public string GetReport()
        {
            if (_report.Length > 0)
                return "LpcIO" + Environment.NewLine + Environment.NewLine + _report;

            return null;
        }

        public void Close()
        {
            foreach (ISuperIO superIO in _superIOs)
                superIO.Close();
        }

        private void Detect()
        {
            for (int i = 0; i < RegisterPorts.Length; i++)
            {
                LpcPort port = new LpcPort(RegisterPorts[i], ValuePorts[i]);

                if (DetectWinbondFintek(port))
                    continue;

                if (DetectIT87(port))
                    continue;

                port.Close();
            }
        }

        private void ReportUnknownChip(LpcPort port, string type, int chip)
        {
            _report.Append("Chip ID: Unknown ");
            _report.Append(type);
            _report.Append(" with ID 0x");
            _report.Append(chip.ToString("X", CultureInfo.InvariantCulture));
            _report.Append(" at 0x");
            _report.Append(port.RegisterPort.ToString("X", CultureInfo.InvariantCulture));
            _report.Append("/0x");
            _report.AppendLine(port.ValuePort.ToString("X", CultureInfo.InvariantCulture));
            _report.AppendLine();
        }

        private static bool IsInvalidRuntimeBase(ushort address)
        {
            return address == 0 || (address & 0xF007) != 0 || address < 0x100;
        }

        // Nuvoton NCT6xxx / NCT5585D
        private bool DetectWinbondFintek(LpcPort port)
        {
            port.WinbondNuvotonFintekEnter();

            byte id = port.ReadByte(CHIP_ID_REGISTER);
            byte revision = port.ReadByte(CHIP_REVISION_REGISTER);
            Chip chip = Chip.Unknown;
            byte logicalDeviceNumber = 0;

            switch (id)
            {
                case 0xB4:
                    switch (revision & 0xF0)
                    {
                        case 0x70:
                            chip = Chip.NCT6771F;
                            logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                            break;
                    }
                    break;

                case 0xC3:
                    switch (revision & 0xF0)
                    {
                        case 0x30:
                            chip = Chip.NCT6776F;
                            logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                            break;
                    }
                    break;

                case 0xC4:
                    switch (revision & 0xF0)
                    {
                        case 0x50:
                            chip = Chip.NCT610XD;
                            logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                            break;
                    }
                    break;

                case 0xC5:
                    switch (revision & 0xF0)
                    {
                        case 0x60:
                            chip = Chip.NCT6779D;
                            logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                            break;
                    }
                    break;

                case 0xC7:
                    switch (revision)
                    {
                        case 0x32:
                            chip = Chip.NCT6683D;
                            logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                            break;
                    }
                    break;

                case 0xC8:
                    switch (revision)
                    {
                        case 0x03:
                            chip = Chip.NCT6791D;
                            logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                            break;
                    }
                    break;

                case 0xC9:
                    switch (revision)
                    {
                        case 0x11:
                            chip = Chip.NCT6792D;
                            logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                            break;
                        case 0x13:
                            chip = Chip.NCT6792DA;
                            logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                            break;
                    }
                    break;

                case 0xD1:
                    switch (revision)
                    {
                        case 0x21:
                            chip = Chip.NCT6793D;
                            logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                            break;
                    }
                    break;

                case 0xD3:
                    switch (revision)
                    {
                        case 0x52:
                            chip = Chip.NCT6795D;
                            logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                            break;
                    }
                    break;

                case 0xD4:
                    switch (revision)
                    {
                        case 0x23:
                            chip = Chip.NCT6796D;
                            logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                            break;
                        case 0x2A:
                            chip = Chip.NCT6796DR;
                            logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                            break;
                        case 0x51:
                            chip = Chip.NCT6797D;
                            logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                            break;
                        case 0x2B:
                            chip = Chip.NCT6798D;
                            logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                            break;
                        case 0x40:
                        case 0x41:
                            chip = Chip.NCT6686D;
                            logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                            break;
                        case 0x2E:
                            chip = Chip.NCT6796DS;
                            logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                            break;
                    }
                    break;

                case 0xD5:
                    switch (revision)
                    {
                        case 0x92:
                            chip = Chip.NCT6687D;
                            logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                            break;
                        case 0xB2:
                            chip = Chip.NCT6687DR;
                            logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                            break;
                    }
                    break;

                case 0xD8:
                    switch (revision)
                    {
                        case 0x02:
                            chip = Chip.NCT6799D;
                            logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                            break;
                        case 0x06:
                            chip = Chip.NCT6701D;
                            logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                            break;
                        case 0x2C:
                            chip = Chip.NCT6701D;
                            logicalDeviceNumber = WINBOND_NUVOTON_HARDWARE_MONITOR_LDN;
                            break;
                    }
                    break;
            }

            if (chip == Chip.Unknown)
            {
                if (id != 0 && id != 0xff)
                {
                    port.WinbondNuvotonFintekExit();
                    ReportUnknownChip(port, "Nuvoton", (id << 8) | revision);
                }
                else
                {
                    port.WinbondNuvotonFintekExit();
                }

                return false;
            }

            port.FindBars();
            port.Select(logicalDeviceNumber);

            // Some NCT6701D firmware leaves the hardware-monitor logical device disabled.
            if (chip == Chip.NCT6701D && port.ReadByte(LOGICAL_DEVICE_ACTIVATE_REGISTER) == 0)
                port.WriteByte(LOGICAL_DEVICE_ACTIVATE_REGISTER, LOGICAL_DEVICE_ACTIVATE_ENABLED);

            ushort address = port.ReadWord(BASE_ADDRESS_REGISTER);
            Thread.Sleep(1);
            ushort verify = port.ReadWord(BASE_ADDRESS_REGISTER);

            // The D8 family can expose the runtime base through the alternate 0x64/0x65 pair.
            if (chip == Chip.NCT6701D && address == verify && IsInvalidRuntimeBase(address))
            {
                address = port.ReadWord(ALTERNATE_BASE_ADDRESS_REGISTER);
                Thread.Sleep(1);
                verify = port.ReadWord(ALTERNATE_BASE_ADDRESS_REGISTER);
            }

            // Disable the hardware monitor I/O space lock on NCT679xD chips.
            if (address == verify &&
                (chip == Chip.NCT6791D || chip == Chip.NCT6792D || chip == Chip.NCT6792DA ||
                 chip == Chip.NCT6793D || chip == Chip.NCT6795D || chip == Chip.NCT6796D ||
                 chip == Chip.NCT6796DR || chip == Chip.NCT6796DS || chip == Chip.NCT6797D ||
                 chip == Chip.NCT6798D || chip == Chip.NCT6799D || chip == Chip.NCT6701D))
            {
                port.NuvotonDisableIOSpaceLock();
            }

            port.WinbondNuvotonFintekExit();

            if (address != verify)
            {
                _report.Append("Chip ID: 0x");
                _report.AppendLine(chip.ToString("X"));
                _report.Append("Chip revision: 0x");
                _report.AppendLine(revision.ToString("X", CultureInfo.InvariantCulture));
                _report.AppendLine("Error: Address verification failed");
                _report.AppendLine();
                return false;
            }

            if (IsInvalidRuntimeBase(address))
            {
                _report.Append("Chip ID: 0x");
                _report.AppendLine(chip.ToString("X"));
                _report.Append("Chip revision: 0x");
                _report.AppendLine(revision.ToString("X", CultureInfo.InvariantCulture));
                _report.Append("Error: Invalid address 0x");
                _report.AppendLine(address.ToString("X", CultureInfo.InvariantCulture));
                _report.AppendLine();
                return false;
            }

            _superIOs.Add(new Nct677X(port, chip, revision, address));
            return true;
        }

        // ITE IT87xx
        private bool DetectIT87(LpcPort port)
        {
            // IT87XX can enter only on port 0x2E or 0x4E.
            if (port.RegisterPort != 0x2E && port.RegisterPort != 0x4E)
                return false;

            // Read chip ID before entering: if already entered on 0x4E, leave it alone.
            ushort chipId;
            if (port.RegisterPort != 0x4E || !port.TryReadWord(CHIP_ID_REGISTER, out chipId))
            {
                port.IT87Enter();
                chipId = port.ReadWord(CHIP_ID_REGISTER);
            }

            Chip chip;
            switch (chipId)
            {
                case 0x8613: chip = Chip.IT8613E; break;
                case 0x8620: chip = Chip.IT8620E; break;
                case 0x8625: chip = Chip.IT8625E; break;
                case 0x8628: chip = Chip.IT8628E; break;
                case 0x8631: chip = Chip.IT8631E; break;
                case 0x8638: chip = Chip.IT8638E; break;
                case 0x8655: chip = Chip.IT8655E; break;
                case 0x8665: chip = Chip.IT8665E; break;
                case 0x8686: chip = Chip.IT8686E; break;
                case 0x8688: chip = Chip.IT8688E; break;
                case 0x8689: chip = Chip.IT8689E; break;
                case 0x8696: chip = Chip.IT8696E; break;
                case 0x8705: chip = Chip.IT8705F; break;
                case 0x8712: chip = Chip.IT8712F; break;
                case 0x8716: chip = Chip.IT8716F; break;
                case 0x8718: chip = Chip.IT8718F; break;
                case 0x8720: chip = Chip.IT8720F; break;
                case 0x8721: chip = Chip.IT8721F; break;
                case 0x8726: chip = Chip.IT8726F; break;
                case 0x8728: chip = Chip.IT8728F; break;
                case 0x8771: chip = Chip.IT8771E; break;
                case 0x8772: chip = Chip.IT8772E; break;
                case 0x8733: chip = Chip.IT8792E; break;
                case 0x8695: chip = Chip.IT87952E; break;
                default: chip = Chip.Unknown; break;
            }

            if (chip == Chip.Unknown)
            {
                if (chipId != 0 && chipId != 0xffff)
                {
                    port.IT87Exit();
                    ReportUnknownChip(port, "ITE", chipId);
                }
                else
                {
                    port.IT87Exit();
                }

                return false;
            }

            port.FindBars();
            port.Select(IT87_ENVIRONMENT_CONTROLLER_LDN);

            ushort address = port.ReadWord(BASE_ADDRESS_REGISTER);
            Thread.Sleep(1);
            ushort verify = port.ReadWord(BASE_ADDRESS_REGISTER);

            byte version = (byte)(port.ReadByte(IT87_CHIP_VERSION_REGISTER) & 0x0F);

            ushort gpioAddress;
            ushort gpioVerify;

            if (chip == Chip.IT8705F)
            {
                port.Select(IT8705_GPIO_LDN);
                gpioAddress = port.ReadWord(BASE_ADDRESS_REGISTER);
                Thread.Sleep(1);
                gpioVerify = port.ReadWord(BASE_ADDRESS_REGISTER);
            }
            else
            {
                port.Select(IT87XX_GPIO_LDN);
                gpioAddress = port.ReadWord((byte)(BASE_ADDRESS_REGISTER + 2));
                Thread.Sleep(1);
                gpioVerify = port.ReadWord((byte)(BASE_ADDRESS_REGISTER + 2));
            }

            port.IT87Exit();

            if (address != verify || address < 0x100 || (address & 0xF007) != 0)
            {
                _report.Append("Chip ID: 0x");
                _report.AppendLine(chip.ToString("X"));
                _report.Append("Error: Invalid address 0x");
                _report.AppendLine(address.ToString("X", CultureInfo.InvariantCulture));
                _report.AppendLine();
                return false;
            }

            if (gpioAddress != gpioVerify || gpioAddress < 0x100 || (gpioAddress & 0xF007) != 0)
            {
                _report.Append("Chip ID: 0x");
                _report.AppendLine(chip.ToString("X"));
                _report.Append("Error: Invalid GPIO address 0x");
                _report.AppendLine(gpioAddress.ToString("X", CultureInfo.InvariantCulture));
                _report.AppendLine();
                return false;
            }

            _superIOs.Add(new IT87XX(port, chip, address, gpioAddress, version, null));
            return true;
        }
    }
}

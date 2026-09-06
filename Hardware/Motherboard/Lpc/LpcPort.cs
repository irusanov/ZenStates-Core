// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.
// Adаpted from LibreHardwareMonitor (https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)

using System;

namespace ZenStates.Core.Hardware.Motherboard.Lpc
{
    /// <summary>
    /// Adapted from LibreHardwareMonitor LpcPort
    /// commonly found on AMD-platform motherboards (ITE IT87xx and Nuvoton NCT67xx).
    /// </summary>
    internal sealed class LpcPort : IDisposable
    {
        private const byte DEVICE_SELECT_REGISTER = 0x07;
        private const byte CHIP_ID_REGISTER = 0x20;
        private const byte CHIP_REVISION_REGISTER = 0x21;
        private const byte BASE_ADDRESS_REGISTER = 0x60;

        // Nuvoton: IO-space lock bit is in global register 0x28
        private const byte NUVOTON_IO_SPACE_LOCK_REGISTER = 0x28;

        // ITE: config-mode exit command register
        private const byte IT87_CONFIGURATION_CONTROL_REGISTER = 0x02;

        public static readonly ushort[] RegisterPorts = { 0x2E, 0x4E };
        public static readonly ushort[] ValuePorts = { 0x2F, 0x4F };

        private readonly PawnIo.LpcIO _pawnModule;
        private bool _disposed;

        public ushort RegisterPort { get; }

        public ushort ValuePort { get; }

        public LpcPort(ushort registerPort, ushort valuePort)
        {
            RegisterPort = registerPort;
            ValuePort = valuePort;

            _pawnModule = new PawnIo.LpcIO();
            _pawnModule.SelectSlot(registerPort == 0x2E ? 0 : 1);
        }

        public byte ReadIoPort(ushort port)
        {
            _pawnModule.ReadPort(port, out byte value);
            return value;
        }

        public bool ReadIoPort(ushort port, out byte value)
            => _pawnModule.ReadPort(port, out value);

        public void WriteIoPort(ushort port, byte value)
            => _pawnModule.WritePort(port, value);

        public bool ReadByte(byte register, out byte value)
            => _pawnModule.ReadByte(register, out value);

        public byte ReadByte(byte register)
        {
            _pawnModule.ReadByte(register, out byte value);
            return value;
        }

        public bool WriteByte(byte register, byte value)
            => _pawnModule.WriteByte(register, value);

        public bool ReadWord(byte register, out ushort value)
            => _pawnModule.ReadWord(register, out value);

        public ushort ReadWord(byte register)
        {
            _pawnModule.ReadWord(register, out ushort value);
            return value;
        }

        public bool TryReadWord(byte register, out ushort value)
            => _pawnModule.ReadWord(register, out value);

        public void Select(byte logicalDeviceNumber)
            => WriteByte(DEVICE_SELECT_REGISTER, logicalDeviceNumber);

        public bool ReadChipId(out byte id, out byte revision)
        {
            if (ReadByte(CHIP_ID_REGISTER, out id) && ReadByte(CHIP_REVISION_REGISTER, out revision))
                return true;

            id = revision = 0;
            return false;
        }

        public bool ReadBaseAddress(out ushort address)
        {
            if (ReadWord(BASE_ADDRESS_REGISTER, out address))
                return true;

            address = 0;
            return false;
        }

        public void FindBars() => _pawnModule.FindBars();

        public void WinbondNuvotonFintekEnter()
        {
            _pawnModule.WritePort(RegisterPort, 0x87);
            _pawnModule.WritePort(RegisterPort, 0x87);
        }

        public void WinbondNuvotonFintekExit()
        {
            _pawnModule.WritePort(RegisterPort, 0xAA);
        }

        public void NuvotonDisableIOSpaceLock()
        {
            if (!ReadByte(NUVOTON_IO_SPACE_LOCK_REGISTER, out byte options))
                return;

            if ((options & 0x10) != 0)
                WriteByte(NUVOTON_IO_SPACE_LOCK_REGISTER, (byte)(options & ~0x10));
        }

        public void IT87Enter()
        {
            _pawnModule.WritePort(RegisterPort, 0x87);
            _pawnModule.WritePort(RegisterPort, 0x01);
            _pawnModule.WritePort(RegisterPort, 0x55);
            _pawnModule.WritePort(RegisterPort, RegisterPort == 0x4E ? (byte)0xAA : (byte)0x55);
        }

        public void IT87Exit()
        {
            // Do not exit config mode for the secondary super-IO port.
            if (RegisterPort != 0x4E)
            {
                _pawnModule.WritePort(RegisterPort, IT87_CONFIGURATION_CONTROL_REGISTER);
                _pawnModule.WritePort(ValuePort, 0x02);
            }
        }

        public void SmscEnter()
        {
            _pawnModule.WritePort(RegisterPort, 0x55);
        }

        public void SmscExit()
        {
            _pawnModule.WritePort(RegisterPort, 0xAA);
        }

        public void Close()
        {
            if (_disposed)
                return;

            _disposed = true;
            _pawnModule.Dispose();
        }

        public void Dispose() => Close();
    }
}

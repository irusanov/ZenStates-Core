// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.
// Adаpted from LibreHardwareMonitor (https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)

using System;
using System.Globalization;
using System.Text;

// ReSharper disable once InconsistentNaming

namespace ZenStates.Core.Hardware.Lpc
{
    internal class W836XX : ISuperIO
    {
        // ReSharper disable InconsistentNaming
        private const byte ADDRESS_REGISTER_OFFSET = 0x05;
        private const byte BANK_SELECT_REGISTER = 0x4E;
        private const byte DATA_REGISTER_OFFSET = 0x06;
        private const byte HIGH_BYTE = 0x80;
        private const byte TEMPERATURE_SOURCE_SELECT_REG = 0x49;
        private const byte VENDOR_ID_REGISTER = 0x4F;
        private const byte VOLTAGE_VBAT_REG = 0x51;

        private const ushort WINBOND_VENDOR_ID = 0x5CA3;

        private readonly byte[] FAN_BIT_REG = { 0x47, 0x4B, 0x4C, 0x59, 0x5D };
        private readonly byte[] FAN_DIV_BIT0 = { 36, 38, 30, 8, 10 };
        private readonly byte[] FAN_DIV_BIT1 = { 37, 39, 31, 9, 11 };
        private readonly byte[] FAN_DIV_BIT2 = { 5, 6, 7, 23, 15 };
        private readonly byte[] FAN_TACHO_BANK = { 0, 0, 0, 0, 5 };
        private readonly byte[] FAN_TACHO_REG = { 0x28, 0x29, 0x2A, 0x3F, 0x53 };
        private readonly byte[] TEMPERATURE_BANK = { 1, 2, 0 };
        private readonly byte[] TEMPERATURE_REG = { 0x50, 0x50, 0x27 };
        // ReSharper restore InconsistentNaming

        private readonly LpcPort _port;
        private readonly ushort _address;
        private readonly byte _revision;

        private readonly bool[] _peciTemperature;
        private readonly byte[] _voltageBank;
        private readonly float _voltageGain = 0.008f;
        private readonly byte[] _voltageRegister;

        private readonly byte[] _fanPwmRegister;
        private readonly byte[] _fanPrimaryControlModeRegister;
        private readonly byte[] _fanPrimaryControlValue;
        private readonly byte[] _fanSecondaryControlModeRegister;
        private readonly byte[] _fanSecondaryControlValue;
        private readonly byte[] _fanTertiaryControlModeRegister;
        private readonly byte[] _fanTertiaryControlValue;

        private readonly byte[] _initialFanControlValue;
        private readonly byte[] _initialFanSecondaryControlValue;
        private readonly byte[] _initialFanTertiaryControlValue;
        private readonly bool[] _restoreDefaultFanPwmControlRequired;

        public W836XX(LpcPort port, Chip chip, byte revision, ushort address)
        {
            _port = port;
            _address = address;
            _revision = revision;
            Chip = chip;

            // initialise to empty arrays so the fields are never null
            _peciTemperature = new bool[0];
            _voltageBank = new byte[0];
            _voltageRegister = new byte[0];
            _fanPwmRegister = new byte[0];
            _fanPrimaryControlModeRegister = new byte[0];
            _fanPrimaryControlValue = new byte[0];
            _fanSecondaryControlModeRegister = new byte[0];
            _fanSecondaryControlValue = new byte[0];
            _fanTertiaryControlModeRegister = new byte[0];
            _fanTertiaryControlValue = new byte[0];
            _initialFanControlValue = new byte[0];
            _initialFanSecondaryControlValue = new byte[0];
            _initialFanTertiaryControlValue = new byte[0];
            _restoreDefaultFanPwmControlRequired = new bool[0];

            Temperatures = new float?[0];
            Voltages = new float?[0];
            Fans = new float?[0];
            Controls = new float?[0];

            if (!IsWinbondVendor())
                return;

            Temperatures = new float?[3];
            _peciTemperature = new bool[3];

            switch (chip)
            {
                case Chip.W83667HG:
                case Chip.W83667HGB:
                    {
                        byte flag = ReadByte(0, TEMPERATURE_SOURCE_SELECT_REG);
                        _peciTemperature[0] = (flag & 0x04) != 0;
                        _peciTemperature[1] = (flag & 0x40) != 0;
                        _peciTemperature[2] = false;
                        break;
                    }

                case Chip.W83627DHG:
                case Chip.W83627DHGP:
                    {
                        byte sel = ReadByte(0, TEMPERATURE_SOURCE_SELECT_REG);
                        _peciTemperature[0] = (sel & 0x07) != 0;
                        _peciTemperature[1] = (sel & 0x70) != 0;
                        _peciTemperature[2] = false;
                        break;
                    }

                default:
                    _peciTemperature[0] = false;
                    _peciTemperature[1] = false;
                    _peciTemperature[2] = false;
                    break;
            }

            switch (chip)
            {
                case Chip.W83627EHF:
                    Voltages = new float?[10];
                    _voltageRegister = new byte[] { 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x50, 0x51, 0x52 };
                    _voltageBank = new byte[] { 0, 0, 0, 0, 0, 0, 0, 5, 5, 5 };
                    _voltageGain = 0.008f;

                    Fans = new float?[5];
                    _fanPwmRegister = new byte[] { 0x01, 0x03, 0x11 };
                    _fanPrimaryControlModeRegister = new byte[] { 0x04, 0x04, 0x12 };
                    _fanPrimaryControlValue = new byte[] { 0xF3, 0xCF, 0xF9 };
                    _initialFanControlValue = new byte[3];
                    _initialFanSecondaryControlValue = new byte[3];
                    Controls = new float?[3];
                    _restoreDefaultFanPwmControlRequired = new bool[3];
                    break;

                case Chip.W83627DHG:
                case Chip.W83627DHGP:
                    Voltages = new float?[9];
                    _voltageRegister = new byte[] { 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x50, 0x51 };
                    _voltageBank = new byte[] { 0, 0, 0, 0, 0, 0, 0, 5, 5 };
                    _voltageGain = 0.008f;

                    Fans = new float?[5];
                    _fanPwmRegister = new byte[] { 0x01, 0x03, 0x11 };
                    _fanPrimaryControlModeRegister = new byte[] { 0x04, 0x04, 0x12 };
                    _fanPrimaryControlValue = new byte[] { 0xF3, 0xCF, 0xF9 };
                    _initialFanControlValue = new byte[3];
                    _initialFanSecondaryControlValue = new byte[3];
                    Controls = new float?[3];
                    _restoreDefaultFanPwmControlRequired = new bool[3];
                    break;

                case Chip.W83667HG:
                case Chip.W83667HGB:
                    Voltages = new float?[9];
                    _voltageRegister = new byte[] { 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x50, 0x51 };
                    _voltageBank = new byte[] { 0, 0, 0, 0, 0, 0, 0, 5, 5 };
                    _voltageGain = 0.008f;

                    Fans = new float?[5];
                    _fanPwmRegister = new byte[] { 0x01, 0x03, 0x11 };
                    _fanPrimaryControlModeRegister = new byte[] { 0x04, 0x04, 0x12 };
                    _fanPrimaryControlValue = new byte[] { 0xF3, 0xCF, 0xF9 };
                    _fanSecondaryControlModeRegister = new byte[] { 0x7C, 0x7C, 0x7C };
                    _fanSecondaryControlValue = new byte[] { 0xEF, 0xDF, 0xBF };
                    _fanTertiaryControlModeRegister = new byte[] { 0x62, 0x7C, 0x62 };
                    _fanTertiaryControlValue = new byte[] { 0xEF, 0xDF, 0xDF };
                    _initialFanControlValue = new byte[3];
                    _initialFanSecondaryControlValue = new byte[3];
                    _initialFanTertiaryControlValue = new byte[3];
                    Controls = new float?[3];
                    _restoreDefaultFanPwmControlRequired = new bool[3];
                    break;

                case Chip.W83627HF:
                    Voltages = new float?[7];
                    _voltageRegister = new byte[] { 0x20, 0x21, 0x22, 0x23, 0x24, 0x50, 0x51 };
                    _voltageBank = new byte[] { 0, 0, 0, 0, 0, 5, 5 };
                    _voltageGain = 0.016f;

                    Fans = new float?[3];
                    _fanPwmRegister = new byte[] { 0x5A, 0x5B };
                    Controls = new float?[2];
                    _restoreDefaultFanPwmControlRequired = new bool[2];
                    break;

                case Chip.W83627THF:
                    Voltages = new float?[7];
                    _voltageRegister = new byte[] { 0x20, 0x21, 0x22, 0x23, 0x24, 0x50, 0x51 };
                    _voltageBank = new byte[] { 0, 0, 0, 0, 0, 5, 5 };
                    _voltageGain = 0.016f;

                    Fans = new float?[3];
                    _fanPwmRegister = new byte[] { 0x01, 0x03, 0x11 };
                    _fanPrimaryControlModeRegister = new byte[] { 0x04, 0x04, 0x12 };
                    _fanPrimaryControlValue = new byte[] { 0xF3, 0xCF, 0xF9 };
                    _initialFanControlValue = new byte[3];
                    Controls = new float?[3];
                    _restoreDefaultFanPwmControlRequired = new bool[3];
                    break;

                case Chip.W83687THF:
                    Voltages = new float?[7];
                    _voltageRegister = new byte[] { 0x20, 0x21, 0x22, 0x23, 0x24, 0x50, 0x51 };
                    _voltageBank = new byte[] { 0, 0, 0, 0, 0, 5, 5 };
                    _voltageGain = 0.016f;

                    Fans = new float?[3];
                    break;
            }
        }

        public Chip Chip { get; }

        public float?[] Controls { get; private set; }
        public float?[] Fans { get; private set; }
        public float?[] Temperatures { get; private set; }
        public float?[] Voltages { get; private set; }

        public byte? ReadGpio(int index)
        {
            return null;
        }

        public void WriteGpio(int index, byte value)
        {
        }

        public void SetControl(int index, byte? value)
        {
            if (index < 0 || index >= Controls.Length)
                throw new ArgumentOutOfRangeException("index");

            if (!Mutexes.WaitIsaBus(10))
                return;

            if (value.HasValue)
            {
                SaveDefaultFanPwmControl(index);

                if (_fanPrimaryControlModeRegister.Length > 0)
                {
                    WriteByte(0,
                              _fanPrimaryControlModeRegister[index],
                              (byte)(_fanPrimaryControlValue[index] & ReadByte(0, _fanPrimaryControlModeRegister[index])));

                    if (_fanSecondaryControlModeRegister.Length > 0)
                    {
                        if (_fanSecondaryControlModeRegister[index] != _fanPrimaryControlModeRegister[index])
                        {
                            WriteByte(0,
                                      _fanSecondaryControlModeRegister[index],
                                      (byte)(_fanSecondaryControlValue[index] & ReadByte(0, _fanSecondaryControlModeRegister[index])));
                        }

                        if (_fanTertiaryControlModeRegister.Length > 0 &&
                            _fanTertiaryControlModeRegister[index] != _fanSecondaryControlModeRegister[index])
                        {
                            WriteByte(0,
                                      _fanTertiaryControlModeRegister[index],
                                      (byte)(_fanTertiaryControlValue[index] & ReadByte(0, _fanTertiaryControlModeRegister[index])));
                        }
                    }
                }

                WriteByte(0, _fanPwmRegister[index], value.Value);
            }
            else
            {
                RestoreDefaultFanPwmControl(index);
            }

            Mutexes.ReleaseIsaBus();
        }

        public void Update()
        {
            if (!Mutexes.WaitIsaBus(10))
                return;

            for (int i = 0; i < Voltages.Length; i++)
            {
                if (_voltageRegister[i] != VOLTAGE_VBAT_REG)
                {
                    float fValue;
                    if ((Chip == Chip.W83627HF || Chip == Chip.W83627THF || Chip == Chip.W83687THF) && i == 0)
                    {
                        byte vrmConfiguration = ReadByte(0, 0x18);
                        int v = ReadByte(_voltageBank[i], _voltageRegister[i]);
                        fValue = (vrmConfiguration & 0x01) == 0
                            ? 0.016f * v
                            : (0.00488f * v) + 0.69f;
                    }
                    else
                    {
                        int v = ReadByte(_voltageBank[i], _voltageRegister[i]);
                        fValue = _voltageGain * v;
                    }

                    Voltages[i] = fValue > 0 ? fValue : (float?)null;
                }
                else
                {
                    bool valid = (ReadByte(0, 0x5D) & 0x01) > 0;
                    Voltages[i] = valid ? _voltageGain * ReadByte(5, VOLTAGE_VBAT_REG) : (float?)null;
                }
            }

            for (int i = 0; i < Temperatures.Length; i++)
            {
                int tval = (sbyte)ReadByte(TEMPERATURE_BANK[i], TEMPERATURE_REG[i]) << 1;
                if (TEMPERATURE_BANK[i] > 0)
                    tval |= ReadByte(TEMPERATURE_BANK[i], (byte)(TEMPERATURE_REG[i] + 1)) >> 7;

                float temperature = tval / 2.0f;
                Temperatures[i] = (temperature <= 125 && temperature >= -55 && !_peciTemperature[i])
                    ? temperature
                    : (float?)null;
            }

            ulong bits = 0;
            foreach (byte t in FAN_BIT_REG)
                bits = (bits << 8) | ReadByte(0, t);

            ulong newBits = bits;
            for (int i = 0; i < Fans.Length; i++)
            {
                int count = ReadByte(FAN_TACHO_BANK[i], FAN_TACHO_REG[i]);

                int divisorBits = (int)(
                    (((bits >> FAN_DIV_BIT2[i]) & 1) << 2) |
                    (((bits >> FAN_DIV_BIT1[i]) & 1) << 1) |
                    ((bits >> FAN_DIV_BIT0[i]) & 1));

                int divisor = 1 << divisorBits;

                Fans[i] = count < 0xff ? 1.35e6f / (count * divisor) : 0;

                if (count > 192 && divisorBits < 7)
                    divisorBits++;
                else if (count < 96 && divisorBits > 0)
                    divisorBits--;

                newBits = SetBit(newBits, FAN_DIV_BIT2[i], (divisorBits >> 2) & 1);
                newBits = SetBit(newBits, FAN_DIV_BIT1[i], (divisorBits >> 1) & 1);
                newBits = SetBit(newBits, FAN_DIV_BIT0[i], divisorBits & 1);
            }

            for (int i = 0; i < Controls.Length; i++)
            {
                byte pwm = ReadByte(0, _fanPwmRegister[i]);
                Controls[i] = (float)Math.Round(pwm * 100.0f / 0xFF);
            }

            // Write back updated fan divisor bits if they changed.
            if (newBits != bits)
            {
                byte[] newBytes = new byte[FAN_BIT_REG.Length];
                for (int i = 0; i < FAN_BIT_REG.Length; i++)
                {
                    newBytes[i] = (byte)((newBits >> ((FAN_BIT_REG.Length - 1 - i) * 8)) & 0xFF);
                }
                for (int i = 0; i < FAN_BIT_REG.Length; i++)
                {
                    WriteByte(0, FAN_BIT_REG[i], newBytes[i]);
                }
            }

            Mutexes.ReleaseIsaBus();
        }

        public void Close()
        {
            _port.Close();
        }

        public string GetReport()
        {
            StringBuilder r = new StringBuilder();

            r.AppendLine("LPC " + GetType().Name);
            r.AppendLine();
            r.Append("Chip Id: 0x");
            r.AppendLine(Chip.ToString("X"));
            r.Append("Chip Revision: 0x");
            r.AppendLine(_revision.ToString("X", CultureInfo.InvariantCulture));
            r.Append("Base Address: 0x");
            r.AppendLine(_address.ToString("X4", CultureInfo.InvariantCulture));
            r.AppendLine();

            if (!Mutexes.WaitIsaBus(100))
                return r.ToString();

            r.AppendLine("Hardware Monitor Registers");
            r.AppendLine();
            r.AppendLine("      00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F");
            r.AppendLine();

            for (int i = 0; i <= 0x7; i++)
            {
                r.Append(" ");
                r.Append((i << 4).ToString("X2", CultureInfo.InvariantCulture));
                r.Append("  ");
                for (int j = 0; j <= 0xF; j++)
                {
                    r.Append(" ");
                    r.Append(ReadByte(0, (byte)((i << 4) | j)).ToString("X2", CultureInfo.InvariantCulture));
                }
                r.AppendLine();
            }

            for (int k = 1; k <= 15; k++)
            {
                r.AppendLine("Bank " + k);
                for (int i = 0x5; i < 0x6; i++)
                {
                    r.Append(" ");
                    r.Append((i << 4).ToString("X2", CultureInfo.InvariantCulture));
                    r.Append("  ");
                    for (int j = 0; j <= 0xF; j++)
                    {
                        r.Append(" ");
                        r.Append(ReadByte((byte)k, (byte)((i << 4) | j)).ToString("X2", CultureInfo.InvariantCulture));
                    }
                    r.AppendLine();
                }
            }

            r.AppendLine();
            Mutexes.ReleaseIsaBus();
            return r.ToString();
        }

        private byte ReadByte(byte bank, byte register)
        {
            _port.WriteIoPort((ushort)(_address + ADDRESS_REGISTER_OFFSET), BANK_SELECT_REGISTER);
            _port.WriteIoPort((ushort)(_address + DATA_REGISTER_OFFSET), bank);
            _port.WriteIoPort((ushort)(_address + ADDRESS_REGISTER_OFFSET), register);
            return _port.ReadIoPort((ushort)(_address + DATA_REGISTER_OFFSET));
        }

        private void WriteByte(byte bank, byte register, byte value)
        {
            _port.WriteIoPort((ushort)(_address + ADDRESS_REGISTER_OFFSET), BANK_SELECT_REGISTER);
            _port.WriteIoPort((ushort)(_address + DATA_REGISTER_OFFSET), bank);
            _port.WriteIoPort((ushort)(_address + ADDRESS_REGISTER_OFFSET), register);
            _port.WriteIoPort((ushort)(_address + DATA_REGISTER_OFFSET), value);
        }

        private bool IsWinbondVendor()
        {
            ushort vendorId = (ushort)((ReadByte(HIGH_BYTE, VENDOR_ID_REGISTER) << 8) | ReadByte(0, VENDOR_ID_REGISTER));
            return vendorId == WINBOND_VENDOR_ID;
        }

        private static ulong SetBit(ulong target, int bit, int value)
        {
            if ((value & 1) != value)
                throw new ArgumentException("Value must be one bit only.");

            if (bit < 0 || bit > 63)
                throw new ArgumentException("Bit out of range.");

            ulong mask = (ulong)1 << bit;
            return value > 0 ? target | mask : target & ~mask;
        }

        private void SaveDefaultFanPwmControl(int index)
        {
            if (_fanPrimaryControlModeRegister.Length > 0 &&
                _initialFanControlValue.Length > 0 &&
                _fanPrimaryControlValue.Length > 0 &&
                _restoreDefaultFanPwmControlRequired.Length > 0 &&
                !_restoreDefaultFanPwmControlRequired[index])
            {
                _initialFanControlValue[index] = ReadByte(0, _fanPrimaryControlModeRegister[index]);

                if (_fanSecondaryControlModeRegister.Length > 0 &&
                    _initialFanSecondaryControlValue.Length > 0 &&
                    _fanSecondaryControlValue.Length > 0)
                {
                    if (_fanSecondaryControlModeRegister[index] != _fanPrimaryControlModeRegister[index])
                    {
                        _initialFanSecondaryControlValue[index] = ReadByte(0, _fanSecondaryControlModeRegister[index]);
                    }

                    if (_fanTertiaryControlModeRegister.Length > 0 &&
                        _initialFanTertiaryControlValue.Length > 0 &&
                        _fanTertiaryControlValue.Length > 0 &&
                        _fanTertiaryControlModeRegister[index] != _fanSecondaryControlModeRegister[index])
                    {
                        _initialFanTertiaryControlValue[index] = ReadByte(0, _fanTertiaryControlModeRegister[index]);
                    }
                }

                _restoreDefaultFanPwmControlRequired[index] = true;
            }
        }

        private void RestoreDefaultFanPwmControl(int index)
        {
            if (_fanPrimaryControlModeRegister.Length > 0 &&
                _initialFanControlValue.Length > 0 &&
                _fanPrimaryControlValue.Length > 0 &&
                _restoreDefaultFanPwmControlRequired.Length > 0 &&
                _restoreDefaultFanPwmControlRequired[index])
            {
                WriteByte(0,
                          _fanPrimaryControlModeRegister[index],
                          (byte)((_initialFanControlValue[index] & ~_fanPrimaryControlValue[index]) |
                                 ReadByte(0, _fanPrimaryControlModeRegister[index])));

                if (_fanSecondaryControlModeRegister.Length > 0 &&
                    _initialFanSecondaryControlValue.Length > 0 &&
                    _fanSecondaryControlValue.Length > 0)
                {
                    if (_fanSecondaryControlModeRegister[index] != _fanPrimaryControlModeRegister[index])
                    {
                        WriteByte(0,
                                  _fanSecondaryControlModeRegister[index],
                                  (byte)((_initialFanSecondaryControlValue[index] & ~_fanSecondaryControlValue[index]) |
                                         ReadByte(0, _fanSecondaryControlModeRegister[index])));
                    }

                    if (_fanTertiaryControlModeRegister.Length > 0 &&
                        _initialFanTertiaryControlValue.Length > 0 &&
                        _fanTertiaryControlValue.Length > 0 &&
                        _fanTertiaryControlModeRegister[index] != _fanSecondaryControlModeRegister[index])
                    {
                        WriteByte(0,
                                  _fanTertiaryControlModeRegister[index],
                                  (byte)((_initialFanTertiaryControlValue[index] & ~_fanTertiaryControlValue[index]) |
                                         ReadByte(0, _fanTertiaryControlModeRegister[index])));
                    }
                }

                _restoreDefaultFanPwmControlRequired[index] = false;
            }
        }
    }
}

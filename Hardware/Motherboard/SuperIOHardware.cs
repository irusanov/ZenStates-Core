using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ZenStates.Core.Hardware.Motherboard.Lpc;
using ZenStates.Core.OHWM;

namespace ZenStates.Core.Hardware.Motherboard
{
    internal sealed class SuperIOHardware : IHardware
    {
        private readonly List<Sensor> _voltages = new List<Sensor>();
        private readonly List<Sensor> _temperatures = new List<Sensor>();
        private readonly List<Sensor> _fans = new List<Sensor>();
        //private readonly List<Sensor> _controls = new List<Sensor>();

        private readonly ISuperIO _superIO;
        private readonly Model _motherboardName;
        private readonly Manufacturer _motherboardVendor;

        public SuperIOHardware(ISuperIO superIO, SMBios smbios, int index)
        {
            _superIO = superIO;
            _motherboardName = Identification.GetModel(smbios.Board.ProductName.ToString());
            _motherboardVendor = Identification.GetManufacturer(smbios.Board.ManufacturerName.ToString());

            GetBoardSpecificConfiguration(
                superIO,
                _motherboardName,
                _motherboardVendor,
                index,
                0,
                out IList<Voltage> v,
                out IList<Temperature> t,
                out IList<Fan> f,
                out IList<Control> c);

            CreateVoltageSensors(superIO, v);
            CreateTemperatureSensors(superIO, t);
            CreateFanSensors(superIO, f);
        }

        public IList<Sensor> Voltages => _voltages;

        public IList<Sensor> Temperatures => _temperatures;

        public IList<Sensor> Fans => _fans;

        public Chip Chip => _superIO.Chip;

        public string ChipName => Lpc.ChipName.GetName(Chip);

        public HardwareType HardwareType => HardwareType.SuperIO;

        public IEnumerable<Sensor> Sensors
        {
            get
            {
                foreach (Sensor sensor in _voltages)
                    yield return sensor;
                foreach (Sensor sensor in _temperatures)
                    yield return sensor;
                foreach (Sensor sensor in _fans)
                    yield return sensor;
            }
        }

        private void CreateFanSensors(ISuperIO superIO, IList<Fan> f)
        {
            foreach (Fan fan in f)
            {
                if (fan.Index < superIO.Fans.Length)
                {
                    Sensor sensor = new Sensor(fan.Name, fan.Index, SensorType.Fan);
                    _fans.Add(sensor);
                }
            }
        }

        private void CreateTemperatureSensors(ISuperIO superIO, IList<Temperature> t)
        {
            foreach (Temperature temperature in t)
            {
                if (temperature.Index < superIO.Temperatures.Length)
                {
                    Sensor sensor = new Sensor(
                        temperature.Name,
                        temperature.Index,
                        SensorType.Temperature,
                        new Parameter[]
                        {
                            new Parameter("Offset [°C]", "Temperature offset.", 0)
                        }
                        );
                    _temperatures.Add(sensor);
                }
            }
        }

        private void CreateVoltageSensors(ISuperIO superIO, IList<Voltage> v)
        {
            const string formula = "Voltage = value + (value - Vf) * Ri / Rf.";
            foreach (Voltage voltage in v)
            {
                if (voltage.Index < superIO.Voltages.Length)
                {
                    Sensor sensor = new Sensor(voltage.Name,
                                        voltage.Index,
                                        SensorType.Voltage,
                                        new Parameter[]
                                        {
                                            new Parameter("Ri [kΩ]", "Input resistance.\n" + formula, voltage.Ri),
                                            new Parameter("Rf [kΩ]", "Reference resistance.\n" + formula, voltage.Rf),
                                            new Parameter("Vf [V]", "Reference voltage.\n" + formula, voltage.Vf)
                                        });

                    _voltages.Add(sensor);
                }
            }
        }


        private static void GetBoardSpecificConfiguration(
            ISuperIO superIO,
            Model model,
            Manufacturer manufacturer,
            int index,
            int superIOIndex,
            out IList<Voltage> v,
            out IList<Temperature> t,
            out IList<Fan> f,
            out IList<Control> c)
        {
            v = new List<Voltage>();
            t = new List<Temperature>();
            f = new List<Fan>();
            c = new List<Control>();

            switch (superIO.Chip)
            {
                case Chip.IT8705F:
                case Chip.IT8712F:
                case Chip.IT8716F:
                case Chip.IT8718F:
                case Chip.IT8720F:
                case Chip.IT8726F:
                    GetIteConfigurationsA(superIO, manufacturer, model, v, t, f, c);
                    break;

                case Chip.IT8613E:
                case Chip.IT8620E:
                case Chip.IT8625E:
                case Chip.IT8628E:
                case Chip.IT8631E:
                case Chip.IT8638E:
                case Chip.IT8655E:
                case Chip.IT8665E:
                case Chip.IT8686E:
                case Chip.IT8688E:
                case Chip.IT8689E:
                case Chip.IT8721F:
                case Chip.IT8728F:
                case Chip.IT8771E:
                case Chip.IT8772E:
                case Chip.IT8696E:
                    GetIteConfigurationsB(superIO, manufacturer, model, v, t, f, c);
                    break;

                case Chip.IT87952E:
                case Chip.IT8792E:
                case Chip.IT8790E:
                    GetIteConfigurationsC(superIO, manufacturer, model, v, t, f, c);
                    break;

                case Chip.F71858:
                    v.Add(new Voltage("VCC3V", 0, 150, 150));
                    v.Add(new Voltage("VSB3V", 1, 150, 150));
                    v.Add(new Voltage("Battery", 2, 150, 150));

                    for (int i = 0; i < superIO.Temperatures.Length; i++)
                        t.Add(new Temperature("Temperature #" + (i + 1), i));

                    for (int i = 0; i < superIO.Fans.Length; i++)
                        f.Add(new Fan("Fan #" + (i + 1), i));

                    break;

                case Chip.F71808E:
                case Chip.F71862:
                case Chip.F71869:
                case Chip.F71869A:
                case Chip.F71882:
                case Chip.F71889AD:
                case Chip.F71889ED:
                case Chip.F71889F:
                    GetFintekConfiguration(superIO, manufacturer, model, v, t, f, c);
                    break;

                case Chip.W83627EHF:
                    GetWinbondConfigurationEhf(manufacturer, model, v, t, f, c);
                    break;

                case Chip.W83627DHG:
                case Chip.W83627DHGP:
                case Chip.W83667HG:
                case Chip.W83667HGB:
                    GetWinbondConfigurationHg(manufacturer, model, v, t, f, c);
                    break;

                case Chip.W83627HF:
                    v.Add(new Voltage("Vcore", 0));
                    v.Add(new Voltage("Voltage #2", 1, true));
                    v.Add(new Voltage("Voltage #3", 2, true));
                    v.Add(new Voltage("AVCC", 3, 34, 51));
                    v.Add(new Voltage("Voltage #5", 4, true));
                    v.Add(new Voltage("+5VSB", 5, 34, 51));
                    v.Add(new Voltage("CMOS Battery", 6));
                    t.Add(new Temperature("CPU", 0));
                    t.Add(new Temperature("Auxiliary", 1));
                    t.Add(new Temperature("System", 2));
                    f.Add(new Fan("System Fan", 0));
                    f.Add(new Fan("CPU Fan", 1));
                    f.Add(new Fan("Auxiliary Fan", 2));
                    c.Add(new Control("Fan 1", 0));
                    c.Add(new Control("Fan 2", 1));
                    break;

                case Chip.W83627THF:
                case Chip.W83687THF:
                    v.Add(new Voltage("Vcore", 0));
                    v.Add(new Voltage("Voltage #2", 1, true));
                    v.Add(new Voltage("Voltage #3", 2, true));
                    v.Add(new Voltage("AVCC", 3, 34, 51));
                    v.Add(new Voltage("Voltage #5", 4, true));
                    v.Add(new Voltage("+5VSB", 5, 34, 51));
                    v.Add(new Voltage("CMOS Battery", 6));
                    t.Add(new Temperature("CPU", 0));
                    t.Add(new Temperature("Auxiliary", 1));
                    t.Add(new Temperature("System", 2));
                    f.Add(new Fan("System Fan", 0));
                    f.Add(new Fan("CPU Fan", 1));
                    f.Add(new Fan("Auxiliary Fan", 2));
                    c.Add(new Control("System Fan", 0));
                    c.Add(new Control("CPU Fan", 1));
                    c.Add(new Control("Auxiliary Fan", 2));
                    break;

                case Chip.NCT5585D:
                    switch (manufacturer)
                    {
                        case Manufacturer.ASRock when model == Model.X870E_NOVA_WIFI:
                            // Voltages
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("CPU VDDIO", 1));
                            v.Add(new Voltage("+3.3V (AVCC)", 2, 34, 34));
                            v.Add(new Voltage("+3.3V (3VCC)", 3, 34, 34));
                            v.Add(new Voltage("VDD MISC", 4));
                            v.Add(new Voltage("3VSB", 7, 34, 34));
                            v.Add(new Voltage("VBat", 8, 34, 34));
                            v.Add(new Voltage("Voltage #2", 12));
                            v.Add(new Voltage("Voltage #3", 13));
                            v.Add(new Voltage("Voltage #7", 14));
                            v.Add(new Voltage("Voltage #9", 15));

                            // Temperatures
                            t.Add(new Temperature("MOS", 1));
                            t.Add(new Temperature("CPU (PECI)", 2));
                            t.Add(new Temperature("Auxiliary 3", 3)); // AUXTIN3

                            // Fans
                            f.Add(new Fan("MOS Fan", 1));

                            // Controls
                            c.Add(new Control("MOS Fan", 1));
                            break;
                    }
                    break;

                case Chip.NCT6771F:
                case Chip.NCT6776F:
                    GetNuvotonConfigurationF(superIO, manufacturer, model, v, t, f, c);
                    break;

                case Chip.NCT610XD:
                    v.Add(new Voltage("Vcore", 0));
                    v.Add(new Voltage("Voltage #0", 1, true));
                    v.Add(new Voltage("AVCC", 2, 34, 34));
                    v.Add(new Voltage("+3.3V", 3, 34, 34));
                    v.Add(new Voltage("Voltage #1", 4, true));
                    v.Add(new Voltage("Voltage #2", 5, true));
                    v.Add(new Voltage("Reserved", 6, true));
                    v.Add(new Voltage("+3V Standby", 7, 34, 34));
                    v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                    v.Add(new Voltage("Voltage #10", 9, true));
                    t.Add(new Temperature("CPU Core", 0));
                    t.Add(new Temperature("Auxiliary", 1));
                    t.Add(new Temperature("System0", 2));
                    t.Add(new Temperature("System1", 3));
                    t.Add(new Temperature("System2", 4));
                    t.Add(new Temperature("System3", 5));

                    for (int i = 0; i < superIO.Fans.Length; i++)
                        f.Add(new Fan("Fan #" + (i + 1), i));

                    for (int i = 0; i < superIO.Controls.Length; i++)
                        c.Add(new Control("Fan #" + (i + 1), i));

                    break;

                case Chip.NCT6779D:
                case Chip.NCT6791D:
                case Chip.NCT6792D:
                case Chip.NCT6792DA:
                case Chip.NCT6793D:
                case Chip.NCT6795D:
                case Chip.NCT6796D:
                case Chip.NCT6796DR:
                case Chip.NCT6796DS:
                case Chip.NCT6797D:
                case Chip.NCT6798D:
                case Chip.NCT6799D:
                case Chip.NCT6701D:
                case Chip.NCT6683D:
                    GetNuvotonConfigurationD(superIO, manufacturer, model, superIOIndex, v, t, f, c);
                    break;

                case Chip.NCT6686D:
                case Chip.NCT6687D:
                    switch (manufacturer)
                    {
                        case Manufacturer.ASRock when model == Model.X870E_TAICHI:
                            t.Add(new Temperature("CPU", 0));
                            t.Add(new Temperature("VRM MOS", 2));

                            f.Add(new Fan("Water Pump", 0)); // W_PUMP
                            f.Add(new Fan("Chassis Fan #3", 1)); // CHA_FAN3
                            f.Add(new Fan("Chassis Fan #4", 2)); // CHA_FAN4

                            c.Add(new Control("Water Pump", 0)); // W_PUMP
                            c.Add(new Control("Chassis Fan #3", 1)); // CHA_FAN3
                            c.Add(new Control("Chassis Fan #4", 2)); // CHA_FAN4
                            break;

                        case Manufacturer.ASRock when model == Model.B850I_LIGHTNING_WIFI:
                            v.Add(new Voltage("+12V", 0));
                            v.Add(new Voltage("+5V", 1));
                            v.Add(new Voltage("VCore", 2));
                            v.Add(new Voltage("Super I/O", 3));
                            v.Add(new Voltage("DRAM", 4));
                            v.Add(new Voltage("+3.3V", 8));
                            v.Add(new Voltage("VTT", 9));
                            v.Add(new Voltage("+3.3V Standby", 11));
                            v.Add(new Voltage("Battery", 13));

                            t.Add(new Temperature("Motherboard", 1));
                            t.Add(new Temperature("T_Sensor", 2));
                            t.Add(new Temperature("CPU", 3));

                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("Pump Fan", 1));
                            f.Add(new Fan("Chassis Fan", 3));

                            c.Add(new Control("CPU Fan", 0)); // CPU_FAN
                            c.Add(new Control("AIO Pump", 1)); // AIO_PUMP
                            c.Add(new Control("Chassis Fan", 3)); // CHA_FAN1
                            break;

                        case Manufacturer.MSI when model == Model.B550A_PRO:
                            v.Add(new Voltage("+12V", 0));
                            v.Add(new Voltage("+5V", 1));
                            v.Add(new Voltage("CPU NB/SoC", 2));
                            v.Add(new Voltage("VDIMM", 3, 1, 1));
                            v.Add(new Voltage("Vcore", 4, -1, 2));
                            v.Add(new Voltage("Chipset", 5));
                            v.Add(new Voltage("CPU SA", 6));
                            v.Add(new Voltage("+3.3V", 8));
                            v.Add(new Voltage("+1.8V", 9));
                            v.Add(new Voltage("CPU VDDP", 10));
                            v.Add(new Voltage("+3V Standby", 11));
                            v.Add(new Voltage("AVSB", 12));

                            t.Add(new Temperature("CPU Core", 0));
                            t.Add(new Temperature("System", 1));
                            t.Add(new Temperature("VRM MOS", 2));
                            t.Add(new Temperature("Chipset", 3));
                            t.Add(new Temperature("CPU Socket", 4));
                            t.Add(new Temperature("PCIe x1", 5));

                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("Pump Fan", 1));
                            f.Add(new Fan("System Fan #1", 2));
                            f.Add(new Fan("System Fan #2", 3));
                            f.Add(new Fan("System Fan #3", 4));
                            f.Add(new Fan("System Fan #4", 5));
                            f.Add(new Fan("System Fan #5", 6));
                            f.Add(new Fan("System Fan #6", 7));

                            c.Add(new Control("CPU Fan", 0));
                            c.Add(new Control("Pump Fan", 1));
                            c.Add(new Control("System Fan #1", 2));
                            c.Add(new Control("System Fan #2", 3));
                            c.Add(new Control("System Fan #3", 4));
                            c.Add(new Control("System Fan #4", 5));
                            c.Add(new Control("System Fan #5", 6));
                            c.Add(new Control("System Fan #6", 7));

                            break;

                        case Manufacturer.MSI when model == Model.B650M_Gaming_Plus_Wifi: // NCT6687D
                            v.Add(new Voltage("+12V", 0));
                            v.Add(new Voltage("+5V", 1));
                            v.Add(new Voltage("CPU NB/SoC", 2));
                            v.Add(new Voltage("CPU VDDIO", 3, 1, 1));
                            v.Add(new Voltage("Vcore", 4, -1, 2));
                            v.Add(new Voltage("+3.3V", 8));
                            v.Add(new Voltage("+3V Standby", 11));
                            v.Add(new Voltage("AVSB", 12));
                            v.Add(new Voltage("CPU Termination", 9));
                            v.Add(new Voltage("CMOS Battery", 13));

                            t.Add(new Temperature("CPU", 0));
                            t.Add(new Temperature("System", 1));
                            t.Add(new Temperature("VRM MOS", 2));
                            t.Add(new Temperature("Chipset", 3));
                            t.Add(new Temperature("CPU Socket", 4));

                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("CPU Pump Fan", 1));
                            f.Add(new Fan("System Fan #1", 2));
                            f.Add(new Fan("System Fan #2", 3));
                            f.Add(new Fan("System Fan #3", 4));

                            c.Add(new Control("CPU Fan", 0));
                            c.Add(new Control("CPU Pump Fan", 1));
                            c.Add(new Control("System Fan #1", 2));
                            c.Add(new Control("System Fan #2", 3));
                            c.Add(new Control("System Fan #3", 4));

                            break;

                        default:
                            v.Add(new Voltage("+12V", 0));
                            v.Add(new Voltage("+5V", 1));
                            v.Add(new Voltage("Vcore", 2));
                            v.Add(new Voltage("Voltage #1", 3));
                            v.Add(new Voltage("VDIMM", 4));
                            v.Add(new Voltage("CPU I/O", 5));
                            v.Add(new Voltage("CPU SA", 6));
                            v.Add(new Voltage("Voltage #2", 7));
                            v.Add(new Voltage("AVCC3", 8));
                            v.Add(new Voltage("CPU Termination", 9));
                            v.Add(new Voltage("VRef", 10));
                            v.Add(new Voltage("VSB", 11));
                            v.Add(new Voltage("AVSB", 12));
                            v.Add(new Voltage("CMOS Battery", 13));

                            t.Add(new Temperature("CPU", 0));
                            t.Add(new Temperature("System", 1));
                            t.Add(new Temperature("VRM MOS", 2));
                            t.Add(new Temperature("PCH", 3));
                            t.Add(new Temperature("CPU Socket", 4));
                            t.Add(new Temperature("PCIe x1", 5));
                            t.Add(new Temperature("M2 #1", 6));

                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("Pump Fan", 1));
                            f.Add(new Fan("System Fan #1", 2));
                            f.Add(new Fan("System Fan #2", 3));
                            f.Add(new Fan("System Fan #3", 4));
                            f.Add(new Fan("System Fan #4", 5));
                            f.Add(new Fan("System Fan #5", 6));
                            f.Add(new Fan("System Fan #6", 7));

                            c.Add(new Control("CPU Fan", 0));
                            c.Add(new Control("Pump Fan", 1));
                            c.Add(new Control("System Fan #1", 2));
                            c.Add(new Control("System Fan #2", 3));
                            c.Add(new Control("System Fan #3", 4));
                            c.Add(new Control("System Fan #4", 5));
                            c.Add(new Control("System Fan #5", 6));
                            c.Add(new Control("System Fan #6", 7));

                            break;
                    }

                    break;

                case Chip.NCT6687DR:

                    // Universal Sensor and Control defaults
                    t.Add(new Temperature("CPU Core", 0));
                    t.Add(new Temperature("System", 1));
                    t.Add(new Temperature("VRM MOS", 2));
                    t.Add(new Temperature("Chipset", 3));

                    f.Add(new Fan("CPU Fan", 0));
                    f.Add(new Fan("Pump Fan #1", 1));
                    f.Add(new Fan("Chipset Fan", 2));
                    f.Add(new Fan("System Fan #1", 10));
                    f.Add(new Fan("System Fan #2", 11));
                    f.Add(new Fan("System Fan #3", 12));
                    f.Add(new Fan("System Fan #4", 13));
                    f.Add(new Fan("System Fan #5", 14));
                    f.Add(new Fan("System Fan #6", 15));
                    f.Add(new Fan("EZ-Connect Fan", 3));

                    c.Add(new Control("CPU Fan", 0));
                    c.Add(new Control("Pump Fan", 1));
                    c.Add(new Control("Chipset Fan", 2));
                    c.Add(new Control("System Fan #1", 10));
                    c.Add(new Control("System Fan #2", 11));
                    c.Add(new Control("System Fan #3", 12));
                    c.Add(new Control("System Fan #4", 13));
                    c.Add(new Control("System Fan #5", 14));
                    c.Add(new Control("System Fan #6", 15));
                    c.Add(new Control("EZ-Connect Fan", 3));

                    switch (model)
                    {
                        case Model.X870E_TOMAHAWK_WIFI:
                        case Model.X870E_TOMAHAWK_MAX_WIFI_PZ:
                        case Model.X870E_EDGE_TI_WIFI:
                            v.Add(new Voltage("+12V", 0));
                            v.Add(new Voltage("+5V", 1));
                            v.Add(new Voltage("CPU NB/SoC", 2));
                            v.Add(new Voltage("CPU VDDIO", 3, 1, 1));
                            v.Add(new Voltage("Vcore", 4, -1, 2));
                            v.Add(new Voltage("Chipset", 5));
                            v.Add(new Voltage("CPU SA", 6));
                            //v.Add(new Voltage("Unknown_7", 7)); //"Voltage #7"
                            v.Add(new Voltage("+3.3V", 8));
                            v.Add(new Voltage("VREF", 9));
                            v.Add(new Voltage("+1.8V", 10));
                            v.Add(new Voltage("+3V Standby", 11));
                            v.Add(new Voltage("AVSB", 12));
                            v.Add(new Voltage("CMOS Battery", 13));

                            t.Add(new Temperature("CPU Socket", 4));

                            break;

                        case Model.X870E_ACE_MAX:
                        case Model.X870E_UNIFY_X_MAX:
                            v.Add(new Voltage("+12V", 0));
                            v.Add(new Voltage("+5V", 1));
                            v.Add(new Voltage("CPU NB/SoC", 2));
                            v.Add(new Voltage("CPU VDDIO", 3, 1, 1));
                            v.Add(new Voltage("Vcore", 4, -1, 2));
                            v.Add(new Voltage("Chipset #1", 5));
                            v.Add(new Voltage("CPU SA", 6));
                            //v.Add(new Voltage("Unknown_7", 7)); //"Voltage #7"
                            v.Add(new Voltage("+3.3V", 8));
                            v.Add(new Voltage("VREF", 9));
                            v.Add(new Voltage("+1.8V", 10));
                            v.Add(new Voltage("+3V Standby", 11));
                            v.Add(new Voltage("AVSB", 12));
                            v.Add(new Voltage("CMOS Battery", 13));

                            t.Add(new Temperature("Chipset #2", 5));
                            t.Add(new Temperature("T_SEN #1", 6));
                            t.Add(new Temperature("T_SEN #2", 4));

                            break;

                        case Model.X870E_CARBON_WIFI:
                        case Model.X870E_GODLIKE:
                            f.Add(new Fan("System Fan #7", 9));
                            c.Add(new Control("System Fan #7", 9));

                            v.Add(new Voltage("+12V", 0));
                            v.Add(new Voltage("+5V", 1));
                            v.Add(new Voltage("CPU NB/SoC", 2));
                            v.Add(new Voltage("CPU VDDIO", 3, 1, 1));
                            v.Add(new Voltage("Vcore", 4, -1, 2));
                            v.Add(new Voltage("Chipset", 5));
                            v.Add(new Voltage("CPU SA", 6));
                            //v.Add(new Voltage("Unknown_4", 7)); //"Voltage #2"
                            v.Add(new Voltage("+3.3V", 8));
                            v.Add(new Voltage("VREF", 9));
                            v.Add(new Voltage("+1.8V", 10));
                            v.Add(new Voltage("+3V Standby", 11));
                            v.Add(new Voltage("AVSB", 12));
                            v.Add(new Voltage("CMOS Battery", 13));

                            t.Add(new Temperature("PCIe x1", 5));
                            t.Add(new Temperature("T_SEN #1", 6));
                            t.Add(new Temperature("T_SEN #2", 4));

                            break;

                        default:
                            v.Add(new Voltage("+12V", 0));
                            v.Add(new Voltage("+5V", 1));
                            v.Add(new Voltage("CPU NB/SoC", 2));
                            v.Add(new Voltage("VDIMM", 3, 1, 1));
                            v.Add(new Voltage("Vcore", 4, -1, 2));
                            v.Add(new Voltage("Chipset", 5));
                            v.Add(new Voltage("CPU SA", 6));
                            //v.Add(new Voltage("Unknown_4", 7)); //"Voltage #2"
                            v.Add(new Voltage("+3.3V", 8));
                            v.Add(new Voltage("VREF", 9));
                            v.Add(new Voltage("+1.8V", 10));
                            v.Add(new Voltage("+3V Standby", 11));
                            v.Add(new Voltage("AVSB", 12));
                            v.Add(new Voltage("CMOS Battery", 13));

                            break;
                    }

                    break;

                default:
                    GetDefaultConfiguration(superIO, v, t, f, c);
                    break;
            }
        }

        // Only used in IPMI which we don't support for now
        private static void GetDefaultConfiguration(ISuperIO superIO, ICollection<Voltage> v, ICollection<Temperature> t, ICollection<Fan> f, ICollection<Control> c)
        {
            for (int i = 0; i < superIO.Voltages.Length; i++)
                v.Add(new Voltage("Voltage #" + (i + 1), i, true));

            for (int i = 0; i < superIO.Temperatures.Length; i++)
                t.Add(new Temperature("Temperature #" + (i + 1), i));

            for (int i = 0; i < superIO.Fans.Length; i++)
                f.Add(new Fan("Fan #" + (i + 1), i));

            for (int i = 0; i < superIO.Controls.Length; i++)
                c.Add(new Control("Fan #" + (i + 1), i));
        }

        private static void GetIteConfigurationsA(ISuperIO superIO, Manufacturer manufacturer, Model model, IList<Voltage> v, IList<Temperature> t, IList<Fan> f, IList<Control> c)
        {
            switch (manufacturer)
            {
                case Manufacturer.ASUS:
                    switch (model)
                    {

                        default:
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+3.3V", 1));
                            v.Add(new Voltage("Voltage #3", 2, true));
                            v.Add(new Voltage("Voltage #4", 3, true));
                            v.Add(new Voltage("Voltage #5", 4, true));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("Voltage #8", 7, true));
                            v.Add(new Voltage("CMOS Battery", 8));

                            for (int i = 0; i < superIO.Temperatures.Length; i++)
                                t.Add(new Temperature("Temperature #" + (i + 1), i));

                            for (int i = 0; i < superIO.Fans.Length; i++)
                                f.Add(new Fan("Fan #" + (i + 1), i));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + (i + 1), i));

                            break;
                    }

                    break;
                case Manufacturer.ASRock:
                    switch (model)
                    {
                        default:
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("Voltage #2", 1, true));
                            v.Add(new Voltage("Voltage #3", 2, true));
                            v.Add(new Voltage("Voltage #4", 3, true));
                            v.Add(new Voltage("Voltage #5", 4, true));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("Voltage #8", 7, true));
                            v.Add(new Voltage("CMOS Battery", 8));

                            for (int i = 0; i < superIO.Temperatures.Length; i++)
                                t.Add(new Temperature("Temperature #" + (i + 1), i));

                            for (int i = 0; i < superIO.Fans.Length; i++)
                                f.Add(new Fan("Fan #" + (i + 1), i));

                            break;
                    }

                    break;

                case Manufacturer.Gigabyte:
                    switch (model)
                    {
                        default:
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("VDIMM", 1));
                            v.Add(new Voltage("+3.3V", 2));
                            v.Add(new Voltage("+5V", 3, 6.8f, 10));
                            v.Add(new Voltage("Voltage #5", 4, true));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("Voltage #8", 7, true));
                            v.Add(new Voltage("CMOS Battery", 8));

                            for (int i = 0; i < superIO.Temperatures.Length; i++)
                                t.Add(new Temperature("Temperature #" + (i + 1), i));

                            for (int i = 0; i < superIO.Fans.Length; i++)
                                f.Add(new Fan("Fan #" + (i + 1), i));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + (i + 1), i));

                            break;
                    }

                    break;

                default:
                    v.Add(new Voltage("Vcore", 0));
                    v.Add(new Voltage("Voltage #2", 1, true));
                    v.Add(new Voltage("Voltage #3", 2, true));
                    v.Add(new Voltage("Voltage #4", 3, true));
                    v.Add(new Voltage("Voltage #5", 4, true));
                    v.Add(new Voltage("Voltage #6", 5, true));
                    v.Add(new Voltage("Voltage #7", 6, true));
                    v.Add(new Voltage("Voltage #8", 7, true));
                    v.Add(new Voltage("CMOS Battery", 8));

                    for (int i = 0; i < superIO.Temperatures.Length; i++)
                        t.Add(new Temperature("Temperature #" + (i + 1), i));

                    for (int i = 0; i < superIO.Fans.Length; i++)
                        f.Add(new Fan("Fan #" + (i + 1), i));

                    for (int i = 0; i < superIO.Controls.Length; i++)
                        c.Add(new Control("Fan #" + (i + 1), i));

                    break;
            }
        }

        private static void GetIteConfigurationsB(ISuperIO superIO, Manufacturer manufacturer, Model model, IList<Voltage> v, IList<Temperature> t, IList<Fan> f, IList<Control> c)
        {
            switch (manufacturer)
            {
                case Manufacturer.ASUS:
                    switch (model)
                    {
                        case Model.PRIME_X370_PRO: // IT8665E
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("Southbridge 2.5V", 1));
                            v.Add(new Voltage("+12V", 2, 5, 1));
                            v.Add(new Voltage("+5V", 3, 1.5f, 1));
                            v.Add(new Voltage("Voltage #4", 4, true));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3.3V", 7, 10, 10));
                            v.Add(new Voltage("CMOS Battery", 8, 10, 10));
                            v.Add(new Voltage("Voltage #10", 9, true));
                            t.Add(new Temperature("CPU", 0));
                            t.Add(new Temperature("Motherboard", 1));
                            t.Add(new Temperature("VRM", 2));

                            for (int i = 3; i < superIO.Temperatures.Length; i++)
                                t.Add(new Temperature("Temperature #" + (i + 1), i));

                            // Don't know how to get the Pump Fans readings (bios? DC controller? driver?)
                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("Chassis Fan #1", 1));
                            f.Add(new Fan("Chassis Fan #2", 2));
                            f.Add(new Fan("AIO Pump", 3));
                            f.Add(new Fan("CPU Optional Fan", 4));
                            f.Add(new Fan("Water Pump", 5));

                            for (int i = 6; i < superIO.Fans.Length; i++)
                                f.Add(new Fan("Fan #" + (i + 1), i));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + (i + 1), i));

                            break;

                        case Model.TUF_X470_PLUS_GAMING: // IT8665E
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("Southbridge 2.5V", 1));
                            v.Add(new Voltage("+12V", 2, 5, 1));
                            v.Add(new Voltage("+5V", 3, 1.5f, 1));
                            v.Add(new Voltage("Voltage #4", 4, true));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3.3V", 7, 10, 10));
                            v.Add(new Voltage("CMOS Battery", 8, 10, 10));
                            v.Add(new Voltage("Voltage #10", 9, true));
                            t.Add(new Temperature("CPU", 0));
                            t.Add(new Temperature("Motherboard", 1));
                            t.Add(new Temperature("PCH", 2));

                            for (int i = 3; i < superIO.Temperatures.Length; i++)
                                t.Add(new Temperature("Temperature #" + (i + 1), i));

                            f.Add(new Fan("CPU Fan", 0));

                            for (int i = 1; i < superIO.Fans.Length; i++)
                                f.Add(new Fan("Fan #" + (i + 1), i));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + (i + 1), i));

                            break;

                        case Model.ROG_ZENITH_EXTREME: // IT8665E
                            v.Add(new Voltage("Vcore", 0, 10, 10));
                            v.Add(new Voltage("DIMM A/B", 1, 10, 10));
                            v.Add(new Voltage("+12V", 2, 5, 1));
                            v.Add(new Voltage("+5V", 3, 1.5f, 1));
                            v.Add(new Voltage("Southbridge 1.05V", 4, 10, 10));
                            v.Add(new Voltage("DIMM C/D", 5, 10, 10));
                            v.Add(new Voltage("PLL", 6, 10, 10));
                            v.Add(new Voltage("+3.3V", 7, 10, 10));
                            v.Add(new Voltage("CMOS Battery", 8, 10, 10));
                            t.Add(new Temperature("CPU", 0));
                            t.Add(new Temperature("Motherboard", 1));
                            t.Add(new Temperature("CPU Socket", 2));
                            t.Add(new Temperature("Temperature #4", 3));
                            t.Add(new Temperature("Temperature #5", 4));
                            t.Add(new Temperature("VRM", 5));

                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("Chassis Fan #1", 1));
                            f.Add(new Fan("Chassis Fan #2", 2));
                            f.Add(new Fan("High Amp Fan", 3));
                            f.Add(new Fan("Fan 5", 4));
                            f.Add(new Fan("Fan 6", 5));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + (i + 1), i));

                            break;

                        case Model.ROG_STRIX_X470_I: // IT8665E
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("Southbridge 2.5V", 1));
                            v.Add(new Voltage("+12V", 2, 5, 1));
                            v.Add(new Voltage("+5V", 3, 1.5f, 1));
                            v.Add(new Voltage("+3.3V", 7, 10, 10));
                            v.Add(new Voltage("CMOS Battery", 8, 10, 10));
                            t.Add(new Temperature("CPU", 0));
                            t.Add(new Temperature("Motherboard", 1));
                            t.Add(new Temperature("T_SEN", 2));
                            t.Add(new Temperature("PCIe x16", 3));
                            t.Add(new Temperature("VRM", 4));
                            t.Add(new Temperature("Temperature #6", 5));

                            f.Add(new Fan("CPU Fan", 0));

                            //Does not work when in AIO pump mode (shows 0). I don't know how to fix it.
                            f.Add(new Fan("Chassis Fan #1", 1));
                            f.Add(new Fan("Chassis Fan #2", 2));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + i, i));

                            break;

                        case Model.TUF_GAMING_B450_PLUS_II: // IT8665E
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("Vccp2", 1));
                            v.Add(new Voltage("+12V", 2, 5, 1));
                            v.Add(new Voltage("+5V", 3, 1.5f, 1));
                            v.Add(new Voltage("Voltage #5", 4, true));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("3VSB", 7, 10, 10));
                            v.Add(new Voltage("VBat", 8, 10, 10));
                            //v.Add(new Voltage("AVCC3", 15, 10, 10));

                            t.Add(new Temperature("CPU", 0));
                            t.Add(new Temperature("Motherboard", 1));
                            t.Add(new Temperature("Temperature #3", 2));
                            t.Add(new Temperature("Temperature #4", 3));
                            t.Add(new Temperature("Temperature #5", 4));
                            t.Add(new Temperature("Temperature #6", 5));

                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("Chassis Fan #1", 1));
                            f.Add(new Fan("Chassis Fan #2", 2));
                            f.Add(new Fan("Chassis Fan #3", 3));
                            //f.Add(new Fan("Chassis Fan #4", 4)); //Useless. Not connected to anything.
                            f.Add(new Fan("AIO Pump", 5));

                            c.Add(new Control("CPU Fan Control", 0));
                            c.Add(new Control("Chassis Fan #1 Control", 1));
                            c.Add(new Control("Chassis Fan #2 Control", 2));
                            c.Add(new Control("Chassis Fan #3 Control", 3));
                            //c.Add(new Control("Chassis Fan #4 Control", 4)); //Useless. Not connected to anything.
                            c.Add(new Control("AIO Pump Control", 5));

                            break;


                        default:
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("Voltage #2", 1, true));
                            v.Add(new Voltage("+12V", 2, 5, 1));
                            v.Add(new Voltage("+5V", 3, 1.5f, 1));
                            v.Add(new Voltage("Voltage #4", 4, true));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3.3V", 7, 10, 10));
                            v.Add(new Voltage("CMOS Battery", 8, 10, 10));

                            for (int i = 0; i < superIO.Temperatures.Length; i++)
                                t.Add(new Temperature("Temperature #" + (i + 1), i));

                            for (int i = 0; i < superIO.Fans.Length; i++)
                                f.Add(new Fan("Fan #" + (i + 1), i));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + (i + 1), i));

                            break;
                    }

                    break;

                case Manufacturer.Gigabyte:
                    switch (model)
                    {
                        case Model.AX370_Gaming_K7: // IT8686E
                        case Model.AX370_Gaming_5:
                        case Model.AB350_Gaming_3: // IT8686E
                            // Note: v3.3, v12, v5, and AVCC3 might be slightly off.
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+3.3V", 1, 0.65f, 1));
                            v.Add(new Voltage("+12V", 2, 5, 1));
                            v.Add(new Voltage("+5V", 3, 1.5f, 1));
                            v.Add(new Voltage("CPU NB/SoC", 4));
                            v.Add(new Voltage("VDDP", 5));
                            v.Add(new Voltage("VDIMM", 6));
                            v.Add(new Voltage("+3V Standby", 7, 10, 10));
                            v.Add(new Voltage("CMOS Battery", 8, 10, 10));
                            v.Add(new Voltage("AVCC3", 9, 7.53f, 1));
                            t.Add(new Temperature("System", 0));
                            t.Add(new Temperature("Chipset", 1));
                            t.Add(new Temperature("CPU", 2));
                            t.Add(new Temperature("PCIe x16", 3));
                            t.Add(new Temperature("VRM MOS", 4));

                            for (int i = 0; i < superIO.Fans.Length; i++)
                                f.Add(new Fan("Fan #" + (i + 1), i));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + (i + 1), i));

                            break;

                        case Model.B450_AORUS_PRO:
                            v.Add(new Voltage("Vcore", 0, 0, 1));
                            v.Add(new Voltage("+3.3V", 1, 6.5F, 10));
                            v.Add(new Voltage("+12V", 2, 5, 1));
                            v.Add(new Voltage("+5V", 3, 1.5F, 1));
                            v.Add(new Voltage("CPU NB/SoC", 4, 0, 1));
                            v.Add(new Voltage("VDDP", 5, 0, 1));
                            v.Add(new Voltage("VDIMM", 6, 0, 1));
                            v.Add(new Voltage("+3V Standby", 7, 10, 10));
                            v.Add(new Voltage("CMOS Battery", 8, 10, 10));
                            t.Add(new Temperature("System", 0));
                            t.Add(new Temperature("Chipset", 1));
                            t.Add(new Temperature("CPU", 2));
                            t.Add(new Temperature("PCIe x16", 3));
                            t.Add(new Temperature("VRM MOS", 4));
                            t.Add(new Temperature("VSoC MOS", 5));
                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("System Fan #1", 1));
                            f.Add(new Fan("System Fan #2", 2));
                            f.Add(new Fan("System Fan #3", 3));
                            f.Add(new Fan("CPU Optional Fan", 4));
                            c.Add(new Control("CPU Fan", 0));
                            c.Add(new Control("System Fan #1", 1));
                            c.Add(new Control("System Fan #2", 2));
                            c.Add(new Control("System Fan #3", 3));
                            c.Add(new Control("CPU Optional Fan", 4));

                            break;

                        case Model.B450_GAMING_X:
                        case Model.B450_AORUS_ELITE:
                        case Model.B450M_AORUS_ELITE:
                            v.Add(new Voltage("Vcore", 0, 0, 1));
                            v.Add(new Voltage("+3.3V", 1, 6.5F, 10));
                            v.Add(new Voltage("+12V", 2, 5, 1));
                            v.Add(new Voltage("+5V", 3, 1.5F, 1));
                            v.Add(new Voltage("CPU NB/Soc", 4, 0, 1));
                            v.Add(new Voltage("VDDP", 5, 0, 1));
                            v.Add(new Voltage("VDIMM", 6, 0, 1));
                            v.Add(new Voltage("+3V Standby", 7, 10, 10));
                            v.Add(new Voltage("CMOS Battery", 8, 10, 10));
                            t.Add(new Temperature("System", 0));
                            t.Add(new Temperature("Chipset", 1));
                            t.Add(new Temperature("CPU", 2));
                            t.Add(new Temperature("PCIe x16", 3));
                            t.Add(new Temperature("VRM MOS", 4));
                            t.Add(new Temperature("VSoC MOS", 5));
                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("System Fan #1", 1));
                            f.Add(new Fan("System Fan #2", 2));
                            f.Add(new Fan("System Fan #3", 3));
                            c.Add(new Control("CPU Fan", 0));
                            c.Add(new Control("System Fan #1", 1));
                            c.Add(new Control("System Fan #2", 2));
                            c.Add(new Control("System Fan #3", 3));

                            break;

                        case Model.B450M_GAMING: // ITE IT8686E
                        case Model.B450_AORUS_M:
                            v.Add(new Voltage("Vcore", 0, 0, 1));
                            v.Add(new Voltage("+3.3V", 1, 6.5F, 10));
                            v.Add(new Voltage("+12V", 2, 5, 1));
                            v.Add(new Voltage("+5V", 3, 1.5F, 1));
                            v.Add(new Voltage("CPU NB/SoC", 4, 0, 1));
                            v.Add(new Voltage("VDDP", 5, 0, 1));
                            v.Add(new Voltage("VDIMM", 6, 0, 1));
                            v.Add(new Voltage("+3V Standby", 7, 10, 10));
                            v.Add(new Voltage("CMOS Battery", 8, 10, 10));
                            t.Add(new Temperature("System", 0));
                            t.Add(new Temperature("Chipset", 1));
                            t.Add(new Temperature("CPU", 2));
                            t.Add(new Temperature("VRM MOS", 4));
                            t.Add(new Temperature("VSoC MOS", 5));
                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("System Fan #1", 1));
                            f.Add(new Fan("System Fan #2", 2));
                            c.Add(new Control("CPU Fan", 0));
                            c.Add(new Control("System Fan #1", 1));
                            c.Add(new Control("System Fan #2", 2));

                            break;

                        case Model.B450_I_AORUS_PRO_WIFI:
                        case Model.B450M_DS3H: // ITE IT8686E
                        case Model.B450M_S2H:
                        case Model.B450M_H:
                        case Model.B450M_K:
                            v.Add(new Voltage("Vcore", 0, 0, 1));
                            v.Add(new Voltage("+3.3V", 1, 6.5F, 10));
                            v.Add(new Voltage("+12V", 2, 5, 1));
                            v.Add(new Voltage("+5V", 3, 1.5F, 1));
                            v.Add(new Voltage("CPU NB/SoC", 4, 0, 1));
                            v.Add(new Voltage("VDDP", 5, 0, 1));
                            v.Add(new Voltage("VDIMM", 6, 0, 1));
                            v.Add(new Voltage("+3V Standby", 7, 10, 10));
                            v.Add(new Voltage("CMOS Battery", 8, 10, 10));
                            t.Add(new Temperature("System", 0));
                            t.Add(new Temperature("Chipset", 1));
                            t.Add(new Temperature("CPU", 2));
                            t.Add(new Temperature("VRM MOS", 4));
                            t.Add(new Temperature("VSoC MOS", 5));
                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("System Fan", 1));
                            c.Add(new Control("CPU Fan", 0));
                            c.Add(new Control("System Fan", 1));

                            break;

                        case Model.X470_AORUS_GAMING_7_WIFI: // ITE IT8686E
                            v.Add(new Voltage("Vcore", 0, 0, 1));
                            v.Add(new Voltage("+3.3V", 1, 6.5F, 10));
                            v.Add(new Voltage("+12V", 2, 5, 1));
                            v.Add(new Voltage("+5V", 3, 1.5F, 1));
                            v.Add(new Voltage("CPU NB/SoC", 4, 0, 1));
                            v.Add(new Voltage("VDDP", 5, 0, 1));
                            v.Add(new Voltage("DIMM A/B", 6, 0, 1));
                            v.Add(new Voltage("+3V Standby", 7, 10, 10));
                            v.Add(new Voltage("CMOS Battery", 8, 10, 10));
                            v.Add(new Voltage("AVCC3", 9, 54, 10));
                            t.Add(new Temperature("System #1", 0));
                            t.Add(new Temperature("Chipset", 1));
                            t.Add(new Temperature("CPU", 2));
                            t.Add(new Temperature("PCIe x16", 3));
                            t.Add(new Temperature("VRM", 4));
                            t.Add(new Temperature("Sensor #1", 5));
                            t.Add(new Temperature("Sensor #2", 6));

                            for (int i = 0; i < superIO.Fans.Length; i++)
                                f.Add(new Fan("Fan #" + (i + 1), i));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + (i + 1), i));

                            break;


                        case Model.B650_EAGLE_AX: // IT8689E
                        case Model.B650_AORUS_ELITE: // IT8689E
                        case Model.B650_AORUS_ELITE_AX: // IT8689E
                        case Model.B650_AORUS_ELITE_V2: // IT8689E
                        case Model.B650_AORUS_ELITE_AX_V2: // IT8689E
                        case Model.B650_AORUS_ELITE_AX_ICE: // IT8689E
                        case Model.B650_GAMING_X_AX: // IT8689E
                        case Model.B650E_AORUS_ELITE_AX_ICE: // IT8689E
                        case Model.B650M_AORUS_PRO: // IT8689E
                        case Model.B650M_AORUS_PRO_AX:
                        case Model.B650M_AORUS_ELITE:
                        case Model.B650M_AORUS_ELITE_AX:
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+3.3V", 1, 29.4f, 45.3f));
                            v.Add(new Voltage("+12V", 2, 10f, 2f));
                            v.Add(new Voltage("+5V", 3, 15f, 10f));
                            v.Add(new Voltage("CPU NB/SoC", 4));
                            v.Add(new Voltage("CPU MISC", 5));
                            v.Add(new Voltage("Dual DDR5 5V", 6, 1.5f, 1));
                            v.Add(new Voltage("+3V Standby", 7, 10f, 10f));
                            v.Add(new Voltage("CMOS Battery", 8, 10f, 10f));
                            v.Add(new Voltage("AVCC3", 9, 10f, 10f));
                            t.Add(new Temperature("System", 0));
                            t.Add(new Temperature("PCH", 1));
                            t.Add(new Temperature("CPU", 2));
                            t.Add(new Temperature("PCIe x16", 3));
                            t.Add(new Temperature("VRM MOS", 4));
                            t.Add(new Temperature("VSoC MOS", 5));
                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("System Fan #1", 1));
                            f.Add(new Fan("System Fan #2", 2));
                            f.Add(new Fan("System Fan #3", 3));

                            if (model == Model.B650_EAGLE_AX)
                            {
                                f.Add(new Fan("CPU Optional Fan", 4));
                            }
                            else
                            {
                                f.Add(new Fan("System Fan #4 / Pump", 4));
                                f.Add(new Fan("CPU Optional Fan", 5));
                            }

                            c.Add(new Control("CPU Fan", 0));
                            c.Add(new Control("System Fan #1", 1));
                            c.Add(new Control("System Fan #2", 2));
                            c.Add(new Control("System Fan #3", 3));

                            if (model == Model.B650_EAGLE_AX)
                            {
                                c.Add(new Control("CPU Optional Fan", 4));
                            }
                            else
                            {
                                c.Add(new Control("System Fan #4 / Pump", 4));
                                c.Add(new Control("CPU Optional Fan", 5));
                            }

                            break;

                        case Model.B650I_AX: // IT8689E
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+3.3V", 1, 29.4f, 45.3f));
                            v.Add(new Voltage("+12V", 2, 10f, 2f));
                            v.Add(new Voltage("+5V", 3, 15f, 10f));
                            v.Add(new Voltage("CPU NB/SoC", 4));
                            v.Add(new Voltage("CPU MISC", 5));

                            t.Add(new Temperature("System", 0));
                            t.Add(new Temperature("PCH", 1));
                            t.Add(new Temperature("CPU", 2));
                            t.Add(new Temperature("VRM MOS", 4));
                            t.Add(new Temperature("VSoC MOS", 5));

                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("System Fan #1", 1));
                            f.Add(new Fan("System Fan #2", 2));

                            c.Add(new Control("CPU Fan", 0));
                            c.Add(new Control("System Fan #1", 1));
                            c.Add(new Control("System Fan #2", 2));

                            break;

                        case Model.A320M_S2H_CF: // IT8686E
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+3.3V", 1, 29.4f, 45.3f));
                            v.Add(new Voltage("+12V", 2, 10f, 2f));
                            v.Add(new Voltage("+5V", 3, 15f, 10f));
                            v.Add(new Voltage("CPU NB/SoC", 4));
                            v.Add(new Voltage("CPU VDDP", 5));
                            v.Add(new Voltage("DRAM", 6));
                            v.Add(new Voltage("+3V Standby", 7, 1, 1));
                            v.Add(new Voltage("CMOS Battery", 8, 1, 1));
                            v.Add(new Voltage("AVCC3", 9, 1, 1));
                            t.Add(new Temperature("System", 0));
                            t.Add(new Temperature("Chipset", 1));
                            t.Add(new Temperature("CPU", 2));
                            t.Add(new Temperature("PCIe x16", 3));
                            t.Add(new Temperature("VRM MOS", 4));
                            t.Add(new Temperature("VSoC MOS", 5));
                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("System Fan", 1));
                            c.Add(new Control("CPU Fan", 0));
                            c.Add(new Control("System Fan", 1));

                            break;


                        case Model.X570_AORUS_MASTER: // IT8688E
                        case Model.X570_AORUS_ULTRA:
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+3.3V", 1, 29.4f, 45.3f));
                            v.Add(new Voltage("+12V", 2, 10f, 2f));
                            v.Add(new Voltage("+5V", 3, 15f, 10f));
                            v.Add(new Voltage("CPU NB/SoC", 4));
                            v.Add(new Voltage("VDDP", 5));
                            v.Add(new Voltage("DIMM A/B", 6));
                            v.Add(new Voltage("+3V Standby", 7, 1f, 10f));
                            v.Add(new Voltage("CMOS Battery", 8, 1f, 10f));
                            t.Add(new Temperature("System #1", 0));
                            t.Add(new Temperature("Sensor #1", 1));
                            t.Add(new Temperature("CPU", 2));
                            t.Add(new Temperature("PCIe x16", 3));
                            t.Add(new Temperature("VRM MOS", 4));
                            t.Add(new Temperature("PCH", 5));
                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("System Fan #1", 1));
                            f.Add(new Fan("System Fan #2", 2));
                            f.Add(new Fan("PCH Fan", 3));
                            f.Add(new Fan("CPU Optional Fan", 4));
                            c.Add(new Control("CPU Fan", 0));
                            c.Add(new Control("System Fan #1", 1));
                            c.Add(new Control("System Fan #2", 2));
                            c.Add(new Control("PCH Fan", 3));
                            c.Add(new Control("CPU Optional Fan", 4));

                            break;

                        case Model.X570_AORUS_PRO: // IT8688E
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+3.3V", 1, 29.4f, 45.3f));
                            v.Add(new Voltage("+12V", 2, 10f, 2f));
                            v.Add(new Voltage("+5V", 3, 15f, 10f));
                            v.Add(new Voltage("CPU NB/SoC", 4));
                            v.Add(new Voltage("VDDP", 5));
                            v.Add(new Voltage("DIMM A/B", 6));
                            v.Add(new Voltage("+3V Standby", 7, 10f, 10f));
                            v.Add(new Voltage("CMOS Battery", 8, 10f, 10f));
                            t.Add(new Temperature("System #1", 0));
                            t.Add(new Temperature("External #1", 1));
                            t.Add(new Temperature("CPU", 2));
                            t.Add(new Temperature("PCIe x16", 3));
                            t.Add(new Temperature("VRM MOS", 4));
                            t.Add(new Temperature("PCH", 5));
                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("System Fan #1", 1));
                            f.Add(new Fan("System Fan #2", 2));
                            f.Add(new Fan("PCH Fan", 3));
                            f.Add(new Fan("CPU Optional Fan", 4));
                            c.Add(new Control("CPU Fan", 0));
                            c.Add(new Control("System Fan #1", 1));
                            c.Add(new Control("System Fan #2", 2));
                            c.Add(new Control("PCH Fan", 3));
                            c.Add(new Control("CPU Optional Fan", 4));

                            break;

                        case Model.X570_GAMING_X: // IT8688E
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+3.3V", 1, 29.4f, 45.3f));
                            v.Add(new Voltage("+12V", 2, 10f, 2f));
                            v.Add(new Voltage("+5V", 3, 15f, 10f));
                            v.Add(new Voltage("CPU NB/SoC", 4));
                            v.Add(new Voltage("VDDP", 5));
                            v.Add(new Voltage("DIMM A/B", 6));
                            t.Add(new Temperature("System #1", 0));
                            t.Add(new Temperature("System #2", 1));
                            t.Add(new Temperature("CPU", 2));
                            t.Add(new Temperature("PCIe x16", 3));
                            t.Add(new Temperature("VRM MOS", 4));
                            t.Add(new Temperature("PCH", 5));
                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("System Fan #1", 1));
                            f.Add(new Fan("System Fan #2", 2));
                            f.Add(new Fan("PCH Fan", 3));
                            f.Add(new Fan("CPU Optional Fan", 4));
                            c.Add(new Control("CPU Fan", 0));
                            c.Add(new Control("System Fan #1", 1));
                            c.Add(new Control("System Fan #2", 2));
                            c.Add(new Control("PCH Fan", 3));
                            c.Add(new Control("CPU Optional Fan", 4));

                            break;

                        case Model.X870_AORUS_ELITE_WIFI7: // ITE IT8696E
                        case Model.X870_AORUS_ELITE_WIFI7_ICE: // ITE IT8696E
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+3.3V", 1, 6.49F, 10));
                            v.Add(new Voltage("+12V", 2, 5, 1));
                            v.Add(new Voltage("+5V", 3, 1.5F, 1));
                            v.Add(new Voltage("CPU NB/SoC", 4, 0, 1));
                            v.Add(new Voltage("CPU MISC", 5, 0, 1));
                            v.Add(new Voltage("CPU VDDIO", 6));
                            v.Add(new Voltage("DRAM VDD", 7));
                            v.Add(new Voltage("DRAM VDDQ", 8));
                            t.Add(new Temperature("System #1", 0));
                            t.Add(new Temperature("PCH", 1));
                            t.Add(new Temperature("CPU", 2));
                            t.Add(new Temperature("PCIe x16", 3));
                            t.Add(new Temperature("VRM MOS", 4));
                            t.Add(new Temperature("EC", 5));
                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("System Fan #1", 1));
                            f.Add(new Fan("System Fan #2", 2));
                            f.Add(new Fan("System Fan #3", 3));
                            f.Add(new Fan("CPU Optional Fan", 4));
                            c.Add(new Control("CPU Fan", 0));
                            c.Add(new Control("System Fan #1", 1));
                            c.Add(new Control("System Fan #2", 2));
                            c.Add(new Control("System Fan #3", 3));
                            c.Add(new Control("CPU Optional Fan", 4));
                            break;

                        case Model.B550_AORUS_MASTER:
                        case Model.B550_AORUS_PRO:
                        case Model.B550_AORUS_PRO_AC:
                        case Model.B550_AORUS_PRO_AX:
                        case Model.B550_VISION_D:
                            v.Add(new Voltage("Vcore", 0, 0, 1));
                            v.Add(new Voltage("+3.3V", 1, 6.5F, 10));
                            v.Add(new Voltage("+12V", 2, 5, 1));
                            v.Add(new Voltage("+5V", 3, 1.5F, 1));
                            v.Add(new Voltage("CPU NB/SoC", 4, 0, 1));
                            v.Add(new Voltage("VDDP", 5, 0, 1));
                            v.Add(new Voltage("VDIMM", 6, 0, 1));
                            v.Add(new Voltage("+3V Standby", 7, 10, 10));
                            v.Add(new Voltage("CMOS Battery", 8, 10, 10));
                            t.Add(new Temperature("System #1", 0));
                            t.Add(new Temperature("External #1", 1));
                            t.Add(new Temperature("CPU", 2));
                            t.Add(new Temperature("PCIe x16", 3));
                            t.Add(new Temperature("VRM MOS", 4));
                            t.Add(new Temperature("Chipset", 5));
                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("System Fan #1", 1));
                            f.Add(new Fan("System Fan #2", 2));
                            f.Add(new Fan("System Fan #3", 3));
                            f.Add(new Fan("CPU Optional Fan", 4));
                            c.Add(new Control("CPU Fan", 0));
                            c.Add(new Control("System Fan #1", 1));
                            c.Add(new Control("System Fan #2", 2));
                            c.Add(new Control("System Fan #3", 3));
                            c.Add(new Control("CPU Optional Fan", 4));

                            break;

                        case Model.B550_AORUS_ELITE:
                        case Model.B550_AORUS_ELITE_AX:
                        case Model.B550_GAMING_X:
                        case Model.B550_UD_AC:
                        case Model.B550M_AORUS_PRO:
                        case Model.B550M_AORUS_PRO_AX:
                            v.Add(new Voltage("Vcore", 0, 0, 1));
                            v.Add(new Voltage("+3.3V", 1, 6.5F, 10));
                            v.Add(new Voltage("+12V", 2, 5, 1));
                            v.Add(new Voltage("+5V", 3, 1.5F, 1));
                            v.Add(new Voltage("CPU NB/SoC", 4, 0, 1));
                            v.Add(new Voltage("VDDP", 5, 0, 1));
                            v.Add(new Voltage("VDIMM", 6, 0, 1));
                            v.Add(new Voltage("+3V Standby", 7, 10, 10));
                            v.Add(new Voltage("CMOS Battery", 8, 10, 10));
                            t.Add(new Temperature("System #1", 0));
                            t.Add(new Temperature("System #2", 1));
                            t.Add(new Temperature("CPU", 2));
                            t.Add(new Temperature("PCIe x16", 3));
                            t.Add(new Temperature("VRM MOS", 4));
                            t.Add(new Temperature("Chipset", 5));
                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("System Fan #1", 1));
                            f.Add(new Fan("System Fan #2", 2));
                            f.Add(new Fan("System Fan #3", 3));
                            f.Add(new Fan("CPU Optional Fan", 4));
                            c.Add(new Control("CPU Fan", 0));
                            c.Add(new Control("System Fan #1", 1));
                            c.Add(new Control("System Fan #2", 2));
                            c.Add(new Control("System Fan #3", 3));
                            c.Add(new Control("CPU Optional Fan", 4));

                            break;

                        case Model.B550I_AORUS_PRO_AX:
                        case Model.B550M_AORUS_ELITE:
                        case Model.B550M_GAMING:
                        case Model.B550M_DS3H:
                        case Model.B550M_DS3H_AC:
                        case Model.B550M_S2H:
                        case Model.B550M_H:
                            v.Add(new Voltage("Vcore", 0, 0, 1));
                            v.Add(new Voltage("+3.3V", 1, 6.5F, 10));
                            v.Add(new Voltage("+12V", 2, 5, 1));
                            v.Add(new Voltage("+5V", 3, 1.5F, 1));
                            v.Add(new Voltage("CPU NB/SoC", 4, 0, 1));
                            v.Add(new Voltage("VDDP", 5, 0, 1));
                            v.Add(new Voltage("VDIMM", 6, 0, 1));
                            v.Add(new Voltage("+3V Standby", 7, 10, 10));
                            v.Add(new Voltage("CMOS Battery", 8, 10, 10));
                            t.Add(new Temperature("System", 0));
                            t.Add(new Temperature("VSoC MOS", 1));
                            t.Add(new Temperature("CPU", 2));
                            t.Add(new Temperature("VRM MOS", 4));
                            t.Add(new Temperature("Chipset", 5));
                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("System Fan #1", 1));
                            f.Add(new Fan("System Fan #2", 2));
                            c.Add(new Control("CPU Fan", 0));
                            c.Add(new Control("System Fan #1", 1));
                            c.Add(new Control("System Fan #2", 2));

                            break;

                        case Model.X670E_AORUS_XTREME: // IT8689E
                        case Model.X870E_AORUS_PRO: // ITE IT8696E
                        case Model.X870E_AORUS_PRO_ICE: // ITE IT8696E
                        case Model.X870E_AORUS_XTREME_AI_TOP: // ITE IT8696E
                            v.Add(new Voltage("Vcore", 0, 0, 1));
                            v.Add(new Voltage("+3.3V", 1, 6.49F, 10));
                            v.Add(new Voltage("+12V", 2, 5, 1));
                            v.Add(new Voltage("+5V", 3, 1.5F, 1));
                            v.Add(new Voltage("CPU NB/SoC", 4, 0, 1));
                            v.Add(new Voltage("CPU MISC", 5, 0, 1));
                            v.Add(new Voltage("CPU VDDIO", 6, 0, 1));
                            v.Add(new Voltage("+3V Standby", 7, 10, 10));
                            v.Add(new Voltage("CMOS Battery", 8, 10, 10));
                            t.Add(new Temperature("System #1", 0));
                            t.Add(new Temperature("PCH", 1));
                            t.Add(new Temperature("CPU", 2));
                            t.Add(new Temperature("PCIe x16", 3));
                            t.Add(new Temperature("VRM MOS", 4));
                            t.Add(new Temperature("External #1", 5));
                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("System Fan #1", 1));
                            f.Add(new Fan("System Fan #2", 2));
                            f.Add(new Fan("System Fan #3", 3));
                            f.Add(new Fan("CPU Optional Fan", 4));
                            c.Add(new Control("CPU Fan", 0));
                            c.Add(new Control("System Fan #1", 1));
                            c.Add(new Control("System Fan #2", 2));
                            c.Add(new Control("System Fan #3", 3));
                            c.Add(new Control("CPU Optional Fan", 4));

                            break;

                        case Model.X670_AORUS_ELITE_AX:
                            v.Add(new Voltage("Vcore", 0, 0, 1));
                            v.Add(new Voltage("+3.3V", 1, 6.49F, 10));
                            v.Add(new Voltage("+12V", 2, 5, 1));
                            v.Add(new Voltage("+5V", 3, 1.5F, 1));
                            v.Add(new Voltage("CPU NB/SoC", 4, 0, 1));
                            v.Add(new Voltage("CPU MISC", 5, 0, 1));
                            v.Add(new Voltage("CPU VDDIO", 6, 0, 1));
                            v.Add(new Voltage("+3V Standby", 7, 10, 10, 0));
                            v.Add(new Voltage("CMOS Battery", 8, 10, 10));

                            t.Add(new Temperature("System #1", 0));
                            t.Add(new Temperature("PCH", 1));
                            t.Add(new Temperature("CPU", 2));
                            t.Add(new Temperature("PCIe x16", 3));
                            t.Add(new Temperature("VRM MOS", 4));

                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("System Fan #1", 1));
                            f.Add(new Fan("System Fan #2", 2));
                            f.Add(new Fan("System Fan #3", 3));
                            f.Add(new Fan("CPU Optional Fan", 4));

                            c.Add(new Control("CPU Fan", 0));
                            c.Add(new Control("System Fan #1", 1));
                            c.Add(new Control("System Fan #2", 2));
                            c.Add(new Control("System Fan #3", 3));
                            c.Add(new Control("CPU Optional Fan", 4));

                            break;

                        default:
                            v.Add(new Voltage("Vcore", 0, 0, 1));
                            v.Add(new Voltage("+3.3V", 1, 6.5f, 10));
                            v.Add(new Voltage("+12V", 2, 5, 1));
                            v.Add(new Voltage("+5V", 3, 1.5f, 1));
                            v.Add(new Voltage("CPU NB/SoC", 4, 0, 1));
                            v.Add(new Voltage("VDDP", 5, 0, 1));
                            v.Add(new Voltage("VDIMM", 6, 0, 1));
                            v.Add(new Voltage("+3V Standby", 7, 10, 10));
                            v.Add(new Voltage("CMOS Battery", 8, 10, 10));

                            for (int i = 0; i < superIO.Temperatures.Length; i++)
                                t.Add(new Temperature("Temperature #" + (i + 1), i));

                            for (int i = 0; i < superIO.Fans.Length; i++)
                                f.Add(new Fan("Fan #" + (i + 1), i));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + (i + 1), i));

                            break;
                    }

                    break;
                case Manufacturer.Biostar:
                    switch (model)
                    {
                        case Model.B660GTN: //IT8613E
                            // This board has some problems with their app controlling fans that I was able to replicate here so I guess is a BIOS problem with the pins.
                            // Biostar is aware so expect changes in the control pins with new bios.
                            // In the meantime, it's possible to control CPUFAN and CPUOPT1m but not SYSFAN1.
                            // The parameters are extracted from the Biostar app config file.
                            v.Add(new Voltage("Vcore", 0, 0, 1));
                            v.Add(new Voltage("VDIMM", 1, 0, 1));
                            v.Add(new Voltage("+12V", 2, 5, 1)); // Reads higher than it should.
                            v.Add(new Voltage("+5V", 3, 147, 100)); // Reads higher than it should.
                            // Commented because I don't know if it makes sense.
                            //v.Add(new Voltage("VCC ST", 4)); // Reads 4.2V.
                            //v.Add(new Voltage("CPU Input Auxiliary", 5)); // Reads 2.2V.
                            //v.Add(new Voltage("CPU GT", 6)); // Reads 2.6V.
                            //v.Add(new Voltage("+3V Standby", 7, 10, 10)); // Reads 5.8V ?
                            v.Add(new Voltage("CMOS Battery", 8, 10, 10)); // Reads higher than it should at 3.4V.
                            t.Add(new Temperature("System 1", 0));
                            t.Add(new Temperature("System 2", 1)); // Not sure what sensor is this.
                            t.Add(new Temperature("CPU", 2));
                            f.Add(new Fan("CPU Fan", 1));
                            f.Add(new Fan("CPU Optional fan", 2));
                            f.Add(new Fan("System Fan", 4));
                            c.Add(new Control("CPU Fan", 1));
                            c.Add(new Control("CPU Optional Fan", 2));
                            c.Add(new Control("System Fan", 4));

                            break;

                        case Model.X670E_Valkyrie: //IT8625E
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("CPU VDDIO", 1));
                            v.Add(new Voltage("+12V", 2, 10, 2));
                            // Voltage of unknown use
                            v.Add(new Voltage("Voltage #4", 3, true));
                            // The biostar utility shows CPU MISC Voltage.
                            v.Add(new Voltage("Voltage #5", 4));
                            v.Add(new Voltage("VDDP", 5));
                            v.Add(new Voltage("CPU NB/SoC", 6));

                            t.Add(new Temperature("CPU", 0));
                            t.Add(new Temperature("VRM", 1));
                            t.Add(new Temperature("System", 2));

                            f.Add(new Fan("CPU Fan", 0));
                            f.Add(new Fan("CPU Optional Fan", 1));
                            for (int i = 2; i < superIO.Fans.Length; i++)
                                f.Add(new Fan($"System Fan #{i - 1}", i));

                            c.Add(new Control("CPU Fan", 0));
                            c.Add(new Control("CPU Optional Fan", 1));
                            for (int i = 2; i < superIO.Controls.Length; i++)
                                c.Add(new Control($"System Fan #{i - 1}", i));

                            break;

                        default:
                            v.Add(new Voltage("Voltage #1", 0, true));
                            v.Add(new Voltage("Voltage #2", 1, true));
                            v.Add(new Voltage("Voltage #3", 2, true));
                            v.Add(new Voltage("Voltage #4", 3, true));
                            v.Add(new Voltage("Voltage #5", 4, true));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3V Standby", 7, 10, 10, 0, true));
                            v.Add(new Voltage("CMOS Battery", 8, 10, 10));

                            for (int i = 0; i < superIO.Temperatures.Length; i++)
                                t.Add(new Temperature("Temperature #" + (i + 1), i));

                            for (int i = 0; i < superIO.Fans.Length; i++)
                                f.Add(new Fan("Fan #" + (i + 1), i));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + (i + 1), i));

                            break;
                    }

                    break;


                default:
                    v.Add(new Voltage("Voltage #1", 0, true));
                    v.Add(new Voltage("Voltage #2", 1, true));
                    v.Add(new Voltage("Voltage #3", 2, true));
                    v.Add(new Voltage("Voltage #4", 3, true));
                    v.Add(new Voltage("Voltage #5", 4, true));
                    v.Add(new Voltage("Voltage #6", 5, true));
                    v.Add(new Voltage("Voltage #7", 6, true));
                    v.Add(new Voltage("+3V Standby", 7, 10, 10, 0, true));
                    v.Add(new Voltage("CMOS Battery", 8, 10, 10));

                    for (int i = 0; i < superIO.Temperatures.Length; i++)
                        t.Add(new Temperature("Temperature #" + (i + 1), i));

                    for (int i = 0; i < superIO.Fans.Length; i++)
                        f.Add(new Fan("Fan #" + (i + 1), i));

                    for (int i = 0; i < superIO.Controls.Length; i++)
                        c.Add(new Control("Fan #" + (i + 1), i));

                    break;
            }
        }

        private static void GetIteConfigurationsC(ISuperIO superIO, Manufacturer manufacturer, Model model, IList<Voltage> v, IList<Temperature> t, IList<Fan> f, IList<Control> c)
        {
            switch (manufacturer)
            {
                case Manufacturer.Gigabyte:
                    switch (model)
                    {
                        case Model.X570_AORUS_MASTER: // IT879XE
                        case Model.X570_AORUS_PRO:
                        case Model.X570_AORUS_ULTRA:
                        case Model.B550_AORUS_MASTER:
                        case Model.B550_AORUS_PRO:
                        case Model.B550_AORUS_PRO_AC:
                        case Model.B550_AORUS_PRO_AX:
                        case Model.B550_VISION_D:
                            v.Add(new Voltage("VIN0", 0));
                            v.Add(new Voltage("DDRVTT AB", 1));
                            v.Add(new Voltage("Chipset Core", 2));
                            v.Add(new Voltage("Voltage #4", 3, true));
                            v.Add(new Voltage("CPU VDD18", 4));
                            v.Add(new Voltage("PM_CLDO12", 5));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3V Standby", 7, 1f, 1f));
                            v.Add(new Voltage("CMOS Battery", 8, 1f, 1f));
                            t.Add(new Temperature("PCIe x8", 0));
                            t.Add(new Temperature("External #2", 1));
                            t.Add(new Temperature("System #2", 2));
                            f.Add(new Fan("System Fan #5 / Pump", 0));
                            f.Add(new Fan("System Fan #6 / Pump", 1));
                            f.Add(new Fan("System Fan #4", 2));
                            c.Add(new Control("System Fan #5 / Pump", 0));
                            c.Add(new Control("System Fan #6 / Pump", 1));
                            c.Add(new Control("System Fan #4", 2));

                            break;

                        case Model.X470_AORUS_GAMING_7_WIFI: // ITE IT8792
                            v.Add(new Voltage("VIN0", 0, 0, 1));
                            v.Add(new Voltage("DDR VTT", 1, 0, 1));
                            v.Add(new Voltage("Chipset Core", 2, 0, 1));
                            v.Add(new Voltage("VIN3", 3, 0, 1));
                            v.Add(new Voltage("CPU VDD18", 4, 0, 1));
                            v.Add(new Voltage("Chipset Core +2.5V", 5, 0.5F, 1));
                            v.Add(new Voltage("+3V Standby", 6, 1, 10));
                            v.Add(new Voltage("CMOS Battery", 7, 0.7F, 1));
                            t.Add(new Temperature("PCIe x8", 0));
                            t.Add(new Temperature("System #2", 2));

                            for (int i = 0; i < superIO.Fans.Length; i++)
                                f.Add(new Fan("Fan #" + (i + 1), i));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + (i + 1), i));

                            break;

                        case Model.X870E_AORUS_PRO:
                        case Model.X870E_AORUS_PRO_ICE: // ITE IT87952E
                        case Model.X870E_AORUS_XTREME_AI_TOP: // ITE IT87952E
                            v.Add(new Voltage("VIN0", 0));
                            v.Add(new Voltage("Voltage #2", 1, true));
                            v.Add(new Voltage("PM_VCC18", 2));
                            v.Add(new Voltage("VIN3", 3));
                            v.Add(new Voltage("CPU VDD18", 4));
                            v.Add(new Voltage("PM_VDD1V", 5));
                            v.Add(new Voltage("VIN6", 6));
                            v.Add(new Voltage("+3V Standby", 7, 1, 1));
                            v.Add(new Voltage("CMOS Battery", 8, 1, 1));
                            t.Add(new Temperature("PCIe x4", 0));
                            t.Add(new Temperature("External #2", 1));
                            t.Add(new Temperature("System #2", 2));
                            f.Add(new Fan("System Fan #5 / Pump", 0));
                            f.Add(new Fan("System Fan #6 / Pump", 1));
                            f.Add(new Fan("System Fan #4 ", 2));
                            c.Add(new Control("System Fan #5 / Pump", 0));
                            c.Add(new Control("System Fan #6 / Pump", 1));
                            c.Add(new Control("System Fan #4", 2));
                            break;

                        case Model.X870_AORUS_ELITE_WIFI7: // ITE IT87952E
                        case Model.X870_AORUS_ELITE_WIFI7_ICE: // ITE IT87952E
                            v.Add(new Voltage("Voltage #1", 0, true));
                            v.Add(new Voltage("Voltage #2", 1, true));
                            v.Add(new Voltage("Voltage #3", 2, true));
                            v.Add(new Voltage("Voltage #4", 3, true));
                            v.Add(new Voltage("Voltage #5", 4, true));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3V Standby", 7, 10, 10));
                            v.Add(new Voltage("CMOS Battery", 8, 10, 10));
                            t.Add(new Temperature("PCIe x4", 0));
                            t.Add(new Temperature("External #2", 1));
                            t.Add(new Temperature("System #2", 2));
                            f.Add(new Fan("System Fan #5 / Pump", 0));
                            f.Add(new Fan("System Fan #6 / Pump", 1));
                            f.Add(new Fan("System Fan #4", 2));
                            c.Add(new Control("System Fan #5 / Pump", 0));
                            c.Add(new Control("System Fan #6 / Pump", 1));
                            c.Add(new Control("System Fan #4", 2));
                            break;

                        default:
                            v.Add(new Voltage("VIN0", 0));
                            v.Add(new Voltage("DDR I/O", 1));
                            v.Add(new Voltage("Chipset Core", 2));
                            v.Add(new Voltage("Voltage #4", 3, true));
                            v.Add(new Voltage("CPU VDD18", 4));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3V Standby", 7, 1f, 1f));
                            v.Add(new Voltage("CMOS Battery", 8, 1f, 1f));

                            for (int i = 0; i < superIO.Temperatures.Length; i++)
                                t.Add(new Temperature("Temperature #" + (i + 1), i));

                            for (int i = 0; i < superIO.Fans.Length; i++)
                                f.Add(new Fan("Fan #" + (i + 1), i));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + (i + 1), i));

                            break;
                    }

                    break;

                default:
                    v.Add(new Voltage("Voltage #1", 0, true));
                    v.Add(new Voltage("Voltage #2", 1, true));
                    v.Add(new Voltage("Voltage #3", 2, true));
                    v.Add(new Voltage("Voltage #4", 3, true));
                    v.Add(new Voltage("Voltage #5", 4, true));
                    v.Add(new Voltage("Voltage #6", 5, true));
                    v.Add(new Voltage("Voltage #7", 6, true));
                    v.Add(new Voltage("+3V Standby", 7, 10, 10, 0, true));
                    v.Add(new Voltage("CMOS Battery", 8, 10, 10));

                    for (int i = 0; i < superIO.Temperatures.Length; i++)
                        t.Add(new Temperature("Temperature #" + (i + 1), i));

                    for (int i = 0; i < superIO.Fans.Length; i++)
                        f.Add(new Fan("Fan #" + (i + 1), i));

                    for (int i = 0; i < superIO.Controls.Length; i++)
                        c.Add(new Control("Fan #" + (i + 1), i));

                    break;
            }
        }

        private static void GetFintekConfiguration(ISuperIO superIO, Manufacturer manufacturer, Model model, IList<Voltage> v, IList<Temperature> t, IList<Fan> f, IList<Control> c)
        {
            v.Add(new Voltage("VCC3V", 0, 150, 150));
            v.Add(new Voltage("Vcore", 1));
            v.Add(new Voltage("Voltage #3", 2, true));
            v.Add(new Voltage("Voltage #4", 3, true));
            v.Add(new Voltage("Voltage #5", 4, true));
            v.Add(new Voltage("Voltage #6", 5, true));
            if (superIO.Chip != Chip.F71808E)
                v.Add(new Voltage("Voltage #7", 6, true));

            v.Add(new Voltage("VSB3V", 7, 150, 150));
            v.Add(new Voltage("CMOS Battery", 8, 150, 150));

            for (int i = 0; i < superIO.Temperatures.Length; i++)
                t.Add(new Temperature("Temperature #" + (i + 1), i));

            for (int i = 0; i < superIO.Fans.Length; i++)
                f.Add(new Fan("Fan #" + (i + 1), i));

            for (int i = 0; i < superIO.Controls.Length; i++)
                c.Add(new Control("Fan #" + (i + 1), i));
        }

        private static void GetNuvotonConfigurationF(ISuperIO superIO, Manufacturer manufacturer, Model model, IList<Voltage> v, IList<Temperature> t, IList<Fan> f, IList<Control> c)
        {
            v.Add(new Voltage("Vcore", 0));
            v.Add(new Voltage("Voltage #2", 1, true));
            v.Add(new Voltage("AVCC", 2, 34, 34));
            v.Add(new Voltage("+3.3V", 3, 34, 34));
            v.Add(new Voltage("Voltage #5", 4, true));
            v.Add(new Voltage("Voltage #6", 5, true));
            v.Add(new Voltage("Voltage #7", 6, true));
            v.Add(new Voltage("+3V Standby", 7, 34, 34));
            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
            t.Add(new Temperature("CPU Core", 0));
            t.Add(new Temperature("Temperature #1", 1));
            t.Add(new Temperature("Temperature #2", 2));
            t.Add(new Temperature("Temperature #3", 3));

            for (int i = 0; i < superIO.Fans.Length; i++)
                f.Add(new Fan("Fan #" + (i + 1), i));

            for (int i = 0; i < superIO.Controls.Length; i++)
                c.Add(new Control("Fan #" + (i + 1), i));
        }

        private static void GetNuvotonConfigurationD(ISuperIO superIO, Manufacturer manufacturer, Model model, int index, IList<Voltage> v, IList<Temperature> t, IList<Fan> f, IList<Control> c)
        {
            switch (manufacturer)
            {
                case Manufacturer.ASRock:
                    switch (model)
                    {
                        case Model.A320M_HDV: //NCT6779D
                            v.Add(new Voltage("Vcore", 0, 10, 10));
                            v.Add(new Voltage("Chipset 1.05V", 1, 0, 1));
                            v.Add(new Voltage("AVCC", 2, 10, 10));
                            v.Add(new Voltage("+3.3V", 3, 10, 10));
                            v.Add(new Voltage("+12V", 4, 56, 10));
                            v.Add(new Voltage("VcoreRef", 5, 0, 1));
                            v.Add(new Voltage("VDIMM", 6, 0, 1));
                            v.Add(new Voltage("+3V Standby", 7, 10, 10));
                            v.Add(new Voltage("CMOS Battery", 8, 10, 10));
                            //v.Add(new Voltage("#Unused #9", 9, 0, 1, 0, true));
                            //v.Add(new Voltage("#Unused #10", 10, 0, 1, 0, true));
                            //v.Add(new Voltage("#Unused #11", 11, 34, 34, 0, true));
                            v.Add(new Voltage("+5V", 12, 20, 10));
                            //v.Add(new Voltage("#Unused #13", 13, 10, 10, 0, true));
                            //v.Add(new Voltage("#Unused #14", 14, 0, 1, 0, true));

                            //t.Add(new Temperature("#Unused #0", 0));
                            //t.Add(new Temperature("#Unused #1", 1));
                            t.Add(new Temperature("Motherboard", 2));
                            //t.Add(new Temperature("#Unused #3", 3));
                            //t.Add(new Temperature("#Unused #4", 4));
                            t.Add(new Temperature("Auxiliary", 5));

                            for (int i = 0; i < superIO.Fans.Length; i++)
                                f.Add(new Fan("Fan #" + (i + 1), i));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + (i + 1), i));

                            break;

                        case Model.AB350_Pro4: //NCT6779D
                        case Model.AB350M_Pro4:
                        case Model.AB350M:
                        case Model.Fatal1ty_AB350_Gaming_K4:
                        case Model.AB350M_HDV:
                        case Model.B450_Steel_Legend:
                        case Model.B450M_Steel_Legend:
                        case Model.B450_Pro4:
                        case Model.B450M_Pro4:
                            v.Add(new Voltage("Vcore", 0, 10, 10));
                            //v.Add(new Voltage("#Unused", 1, 0, 1, 0, true));
                            v.Add(new Voltage("AVCC", 2, 10, 10));
                            v.Add(new Voltage("+3.3V", 3, 10, 10));
                            v.Add(new Voltage("+12V", 4, 28, 5));
                            v.Add(new Voltage("Vcore Refin", 5, 0, 1));
                            //v.Add(new Voltage("#Unused #6", 6, 0, 1, 0, true));
                            v.Add(new Voltage("+3V Standby", 7, 10, 10));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            //v.Add(new Voltage("#Unused #9", 9, 0, 1, 0, true));
                            //v.Add(new Voltage("#Unused #10", 10, 0, 1, 0, true));
                            v.Add(new Voltage("Chipset 1.05V", 11, 0, 1));
                            v.Add(new Voltage("+5V", 12, 20, 10));
                            //v.Add(new Voltage("#Unused #13", 13, 0, 1, 0, true));
                            v.Add(new Voltage("+1.8V", 14, 0, 1));
                            t.Add(new Temperature("CPU Core", 0));
                            t.Add(new Temperature("CPU", 1));
                            t.Add(new Temperature("Motherboard", 2));
                            t.Add(new Temperature("Auxiliary", 3));
                            t.Add(new Temperature("VRM", 4));
                            t.Add(new Temperature("Auxiliary Index #2", 5));
                            //t.Add(new Temperature("Temperature #6", 6));

                            f.Add(new Fan("CPU Fan", 1));
                            c.Add(new Control("CPU Fan", 1));
                            f.Add(new Fan("CPU Pump", 2));
                            c.Add(new Control("CPU Pump", 2));

                            f.Add(new Fan("Chassis #1", 0));
                            c.Add(new Control("Chassis #1", 0));
                            f.Add(new Fan("Chassis #2", 3));
                            c.Add(new Control("Chassis #2", 3));
                            f.Add(new Fan("Chassis #3", 4));
                            c.Add(new Control("Chassis #3", 4));

                            break;

                        case Model.B450M_Pro4_R2_0:
                            v.Add(new Voltage("Vcore", 0, 10, 10));
                            //v.Add(new Voltage("#Unused #1", 1, 0, 1, 0, true));
                            v.Add(new Voltage("AVCC", 2, 10, 10));
                            v.Add(new Voltage("+3.3V", 3, 10, 10));
                            v.Add(new Voltage("+12V", 4, 28, 5));
                            v.Add(new Voltage("Vcore Refin", 5, 0, 1));
                            //v.Add(new Voltage("#Unused #6", 6, 0, 1, 0, true));
                            v.Add(new Voltage("+3V Standby", 7, 10, 10));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            //v.Add(new Voltage("#Unused #9", 9, 0, 1, 0, true));
                            //v.Add(new Voltage("#Unused #10", 10, 0, 1, 0, true));
                            v.Add(new Voltage("Chipset 1.05V", 11, 0, 1));
                            v.Add(new Voltage("+5V", 12, 20, 10));
                            //v.Add(new Voltage("#Unused #13", 13, 0, 1, 0, true));
                            v.Add(new Voltage("+1.8V", 14, 0, 1));
                            //t.Add(new Temperature("Temperature #1", 1));
                            t.Add(new Temperature("Motherboard", 2));
                            //t.Add(new Temperature("Temperature #3", 3));
                            //t.Add(new Temperature("Temperature #4", 4));
                            //t.Add(new Temperature("Temperature #5", 5));
                            f.Add(new Fan("Chassis #1", 0));
                            f.Add(new Fan("CPU Fan", 1));
                            f.Add(new Fan("Chassis #2", 2));
                            f.Add(new Fan("CPU Pump", 3));
                            f.Add(new Fan("Chassis #3", 4));
                            c.Add(new Control("Chassis #1", 0));
                            c.Add(new Control("CPU Fan", 1));
                            c.Add(new Control("Chassis #2", 2));
                            c.Add(new Control("CPU Pump", 3));
                            c.Add(new Control("Chassis #3", 4));

                            break;

                        case Model.B550M_Pro4: //NCT6796D-R
                            v.Add(new Voltage("Vcore", 0, 10, 10));
                            v.Add(new Voltage("+5V", 1, 2, 1));
                            v.Add(new Voltage("AVCC", 2, 10, 10));
                            v.Add(new Voltage("+3.3V", 3, 10, 10));
                            v.Add(new Voltage("+12V", 4, 56, 10));
                            v.Add(new Voltage("CPU NB/SoC", 5, 0, 1));
                            v.Add(new Voltage("VDIMM", 6, 0, 1, 0));
                            v.Add(new Voltage("+3V Standby", 7, 10, 10));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("VPPM", 11, 3, 1));
                            v.Add(new Voltage("+1.8V", 14, 1, 1));

                            t.Add(new Temperature("Motherboard", 2));
                            t.Add(new Temperature("CPU", 8));

                            f.Add(new Fan("Chassis #3", 0));
                            c.Add(new Control("Chassis #3", 0));
                            f.Add(new Fan("CPU1", 1));
                            c.Add(new Control("CPU1", 1));
                            f.Add(new Fan("CPU Pump", 2));
                            c.Add(new Control("CPU Pump", 2));
                            f.Add(new Fan("Chassis #1", 3));
                            c.Add(new Control("Chassis #1", 3));
                            f.Add(new Fan("Chassis #2", 4));
                            c.Add(new Control("Chassis #2", 4));
                            f.Add(new Fan("Chassis #4", 6));
                            c.Add(new Control("Chassis #4", 6));

                            break;

                        case Model.X570_Taichi:
                            v.Add(new Voltage("Vcore", 0, 10, 10));
                            v.Add(new Voltage("Voltage #2", 1, true));
                            v.Add(new Voltage("AVCC", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("Voltage #5", 4, true));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("CPU Termination", 9));
                            v.Add(new Voltage("Voltage #11", 10, true));
                            v.Add(new Voltage("Voltage #12", 11, true));
                            v.Add(new Voltage("Voltage #13", 12, true));
                            v.Add(new Voltage("Voltage #14", 13, true));
                            v.Add(new Voltage("Voltage #15", 14, true));

                            t.Add(new Temperature("Motherboard", 2));
                            t.Add(new Temperature("CPU", 8));
                            t.Add(new Temperature("Southbridge", 9));

                            f.Add(new Fan("Chassis #3", 0));
                            f.Add(new Fan("CPU #1", 1));
                            f.Add(new Fan("CPU #2", 2));
                            f.Add(new Fan("Chassis #1", 3));
                            f.Add(new Fan("Chassis #2", 4));
                            f.Add(new Fan("Southbridge Fan", 5));
                            f.Add(new Fan("Chassis #4", 6));

                            c.Add(new Control("Chassis #3", 0));
                            c.Add(new Control("CPU #1", 1));
                            c.Add(new Control("CPU #2", 2));
                            c.Add(new Control("Chassis #1", 3));
                            c.Add(new Control("Chassis #2", 4));
                            c.Add(new Control("Southbridge Fan", 5));
                            c.Add(new Control("Chassis #4", 6));

                            break;

                        case Model.X570_Phantom_Gaming_ITX:
                            v.Add(new Voltage("+12V", 0));
                            v.Add(new Voltage("+5V", 1));
                            v.Add(new Voltage("Vcore", 2));
                            v.Add(new Voltage("Voltage #1", 3));
                            v.Add(new Voltage("VDIMM", 4));
                            v.Add(new Voltage("CPU I/O", 5));
                            v.Add(new Voltage("CPU SA", 6));
                            v.Add(new Voltage("Voltage #2", 7));
                            v.Add(new Voltage("AVCC3", 8));
                            v.Add(new Voltage("CPU Termination", 9));
                            v.Add(new Voltage("VRef", 10));
                            v.Add(new Voltage("VSB", 11));
                            v.Add(new Voltage("AVSB", 12));
                            v.Add(new Voltage("CMOS Battery", 13));

                            t.Add(new Temperature("Motherboard", 0));
                            //t.Add(new Temperature("System", 1)); //Unused
                            t.Add(new Temperature("CPU", 2));
                            t.Add(new Temperature("Southbridge", 3));
                            f.Add(new Fan("CPU Fan #1", 0)); //CPU_FAN1
                            f.Add(new Fan("Chassis Fan #1", 1)); //CHA_FAN1/WP
                            f.Add(new Fan("CPU Fan #2", 2)); //CPU_FAN2 (WP)
                            f.Add(new Fan("Chipset Fan", 3));

                            c.Add(new Control("CPU Fan #1", 0));
                            c.Add(new Control("Chassis Fan", 1));
                            c.Add(new Control("CPU Fan #2", 2));
                            c.Add(new Control("Chipset Fan", 3));
                            break;

                        case Model.X570_Phantom_Gaming_4: // NCT6796D (-R?)
                            // internal on NCT6796D have a 1/2 voltage divider (by way of two 34kOhm resistors)
                            // "Six internal signals connected to the power supplies (CPUVCORE, AVSB, VBAT, VTT, 3VSB, 3VCC)"
                            // "All the internal inputs of the ADC, AVSB, VBAT, 3VSB, 3VCC utilize an integrated voltage divider
                            //  with both resistors equal to 34kOhm"
                            // it seems that VTT doesn't actually have the 1/2 divider

                            // external sources can have whatever divider that gets them in the 0V to 2.048V range

                            // assuming Vf = 0, then Ri = R1 and Rf = R2 (from voltage divider equation)

                            v.Add(new Voltage("Vcore", 0, 1, 1));
                            v.Add(new Voltage("+5V", 1, 2, 1));
                            v.Add(new Voltage("AVCC", 2, 1, 1));
                            v.Add(new Voltage("+3.3V", 3, 1, 1));
                            v.Add(new Voltage("+12V", 4, 56, 10));
                            v.Add(new Voltage("CPU NB/SoC", 5));
                            v.Add(new Voltage("VDIMM", 6));
                            v.Add(new Voltage("+3V Standby", 7, 1, 1));
                            v.Add(new Voltage("CMOS Battery", 8, 1, 1));
                            v.Add(new Voltage("DIMM Termination", 9));
                            //v.Add(new Voltage("Voltage #11", 10, true)); // unknown. VIN5 pin
                            v.Add(new Voltage("VPPM", 11, 3, 1));
                            v.Add(new Voltage("PREM CPU SoC", 12));
                            v.Add(new Voltage("DIMM Write", 13));
                            v.Add(new Voltage("+1.8V", 14, 1, 1));
                            //v.Add(new Voltage("Voltage #16", 15, true)); // unknown. VIN9 pin

                            t.Add(new Temperature("CPU", 8)); // AKA SMBUSMASTER0
                            t.Add(new Temperature("Chipset", 9)); // AKA SMBUSMASTER1
                            t.Add(new Temperature("Motherboard", 2)); // AKA SYSTIN

                            // no idea what these sources are actually connected to.
                            //t.Add(new Temperature("CPUTIN", 1));
                            //t.Add(new Temperature("Auxiliary Index #0", 3));
                            //t.Add(new Temperature("Auxiliary Index #1", 4));
                            //t.Add(new Temperature("Auxiliary Index #2", 5));
                            //t.Add(new Temperature("Auxiliary Index #3", 6));
                            //t.Add(new Temperature("Auxiliary Index #4", 7));
                            //t.Add(new Temperature("TSENSOR", 8));
                            //t.Add(new Temperature("VIRTUAL_TEMP", 24));

                            // CHA_FAN3 header
                            f.Add(new Fan("Chassis Fan #3", 0));
                            c.Add(new Control("Chassis Fan #3", 0));

                            // CPU_FAN1 header
                            f.Add(new Fan("CPU Fan #1", 1));
                            c.Add(new Control("CPU Fan #1", 1));

                            // CPU_FAN2/WP header
                            f.Add(new Fan("CPU Fan #2", 2));
                            c.Add(new Control("CPU Fan #2", 2));

                            // CHA_FAN1/WP header
                            f.Add(new Fan("Chassis Fan #1", 3));
                            c.Add(new Control("Chassis Fan #1", 3));

                            // CHA_FAN2/WP header
                            f.Add(new Fan("Chassis Fan #2", 4));
                            c.Add(new Control("Chassis Fan #2", 4));

                            // SB_FAN1 header
                            f.Add(new Fan("Chipset Fan", 5));
                            c.Add(new Control("Chipset Fan", 5));

                            // fan/control 6 is not exposed to a header
                            //f.Add(new Fan("Fan #7", 6));
                            //c.Add(new Control("Fan #7", 6));

                            break;

                        case Model.B650M_C: // NCT6799D
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+12V", 1, 56, 10));
                            v.Add(new Voltage("AVCC", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+5V", 4, 20, 10));
                            v.Add(new Voltage("+1.05V ALW", 5));
                            v.Add(new Voltage("+3.3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("CPU Termination", 9, 1, 1));
                            v.Add(new Voltage("CPU NB/SoC", 10, 1, 1));
                            v.Add(new Voltage("CPU MISC", 11, 1, 1));
                            v.Add(new Voltage("+1.8V", 13, 1, 1));
                            v.Add(new Voltage("CPU VDDIO", 14));

                            t.Add(new Temperature("CPU Core", 9));
                            t.Add(new Temperature("Motherboard", 2));

                            f.Add(new Fan("CPU Fan #1", 1)); // CPU_FAN1
                            f.Add(new Fan("CPU Fan #2", 0)); // CPU_FAN2/WP
                            f.Add(new Fan("Chassis Fan #1", 3)); // CHA_FAN1/WP
                            f.Add(new Fan("Chassis Fan #2", 4)); // CHA_FAN2/WP
                            f.Add(new Fan("Chassis Fan #3", 6)); // CHA_FAN3/WP

                            c.Add(new Control("CPU Fan #1", 1)); // CPU_FAN1
                            c.Add(new Control("CPU Fan #2", 0)); // CPU_FAN2/WP
                            c.Add(new Control("Chassis Fan #1", 3)); // CHA_FAN1/WP
                            c.Add(new Control("Chassis Fan #2", 4)); // CHA_FAN2/WP
                            c.Add(new Control("Chassis Fan #3", 6)); // CHA_FAN3/WP
                            break;

                        case Model.B850M_STEEL_LEGEND_WIFI: // NCT6799D
                            v.Add(new Voltage("Vcore", 0, 10, 10));
                            v.Add(new Voltage("AVCC", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("CPU Termination", 9));

                            t.Add(new Temperature("CPU Core", 0));
                            t.Add(new Temperature("CPU", 1));
                            t.Add(new Temperature("Motherboard", 2));
                            t.Add(new Temperature("PCH TS10", 9));
                            t.Add(new Temperature("T_SEN", 24));

                            f.Add(new Fan("CPU Fan #1", 1)); // CPU_FAN1
                            f.Add(new Fan("CPU Fan #2 / Pump", 0)); // CPU_FAN2/PUMP
                            f.Add(new Fan("Chassis Fan #1", 3)); // CHA_FAN1
                            f.Add(new Fan("Chassis Fan #2", 4)); // CHA_FAN2
                            f.Add(new Fan("Chassis Fan #3", 6)); // CHA_FAN3

                            c.Add(new Control("CPU Fan #1", 1)); // CPU_FAN1
                            c.Add(new Control("CPU Fan #2 / Pump", 0)); // CPU_FAN2/PUMP
                            c.Add(new Control("Chassis Fan #1", 3)); // CHA_FAN1
                            c.Add(new Control("Chassis Fan #2", 4)); // CHA_FAN2
                            c.Add(new Control("Chassis Fan #3", 6)); // CHA_FAN3
                            break;

                        case Model.X870E_TAICHI: // NCT6799D
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+12V", 1, 56, 10));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+5V", 4, 20, 10));
                            v.Add(new Voltage("+1.05V Always-on", 5));
                            v.Add(new Voltage("+3.3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("CPU NB/SoC", 10, 1, 1));
                            v.Add(new Voltage("CPU MISC", 11, 1, 1));
                            v.Add(new Voltage("+1.8V", 13, 1, 1));

                            t.Add(new Temperature("Motherboard", 2));

                            t.Add(new Temperature("T_SEN #1", 5)); // T_SEN 1
                            t.Add(new Temperature("T_SEN #2", 6)); // T_SEN 2
                            t.Add(new Temperature("T_SEN #3", 8)); // T_SEN 3

                            f.Add(new Fan("CPU Fan #1", 1)); // CPU_FAN1
                            f.Add(new Fan("CPU Fan #2", 2)); // CPU_FAN2
                            f.Add(new Fan("AIO Pump", 3)); // AIO_PUMP
                            f.Add(new Fan("Chassis Fan #1", 0)); // CHA_FAN1
                            f.Add(new Fan("Chassis Fan #2", 4)); // CHA_FAN2

                            c.Add(new Control("CPU Fan #1", 1)); // CPU_FAN1
                            c.Add(new Control("CPU Fan #2", 2)); // CPU_FAN2
                            c.Add(new Control("AIO Pump", 3)); // AIO_PUMP
                            c.Add(new Control("Chassis Fan #1", 0)); // CHA_FAN1
                            c.Add(new Control("Chassis Fan #2", 4)); // CHA_FAN2
                            break;

                        case Model.X870E_NOVA_WIFI: //NCT6796D-S
                            v.Add(new Voltage("Vcore", 0)); // CPU Core Voltage
                            v.Add(new Voltage("+12V", 1, 56, 10));  // +12V
                            v.Add(new Voltage("Analog VCC", 2, 34, 34)); // AVCC
                            v.Add(new Voltage("+3.3V", 3, 34, 34)); // +3.3V
                            v.Add(new Voltage("+5V", 4, 20, 10)); // +5V
                            v.Add(new Voltage("+1.05 Standby", 5, 0, 1)); // +1.05V_ALW
                            v.Add(new Voltage("Voltage #4", 6, 0, 1)); // VIN4
                            v.Add(new Voltage("+3V Standby", 7, 34, 34)); // +3VSB
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34)); // VBAT
                            v.Add(new Voltage("CPU Termination", 9, 1, 1)); // VTT
                            v.Add(new Voltage("CPU NB/SoC", 10, 1, 1)); // VDDCR_SOC
                            v.Add(new Voltage("Voltage #6", 11, 34, 34, 0)); // VIN6
                            v.Add(new Voltage("Voltage #2", 12)); // VIN2
                            v.Add(new Voltage("+1.8V", 13, 10, 10, 0)); // +1.8V
                            v.Add(new Voltage("Voltage #7", 14, 0, 1, 0)); // VIN7
                            v.Add(new Voltage("Voltage #9", 15)); // VIN9
                            v.Add(new Voltage("VHIF", 16, 34, 34));
                            v.Add(new Voltage("Voltage #18", 17, 0, 1)); // VIN10

                            // Temperatures
                            t.Add(new Temperature("CPU Socket", 0)); // CPUTIN
                            t.Add(new Temperature("Motherboard", 1)); // SYSTIN
                            t.Add(new Temperature("Auxiliary #0", 2)); // AUXTIN0
                            t.Add(new Temperature("Auxiliary #1", 3)); // AUXTIN1
                            t.Add(new Temperature("T_SEN #1", 4)); // AUXTIN2 (T_SEN1)
                            t.Add(new Temperature("T_SEN #2", 5)); // AUXTIN3 (T_SEN2)
                            t.Add(new Temperature("Auxiliary #4", 6)); // AUXTIN4
                            t.Add(new Temperature("T_SEN #3", 7)); // AUXTIN5 (T_SEN3)
                            t.Add(new Temperature("CPU Core", 8)); // SMBUSMASTER0 (CPU Core)
                            t.Add(new Temperature("CPU (PECI)", 9)); // CPU (PECI)
                            t.Add(new Temperature("Virtual", 10)); // VIRTUAL_TEMP

                            // Fans
                            f.Add(new Fan("Chassis Fan #1", 0)); // CHA_FAN1
                            f.Add(new Fan("CPU Fan #1", 1)); // CPU_FAN1
                            f.Add(new Fan("CPU Fan #2", 2)); // CPU_FAN2
                            f.Add(new Fan("AIO Pump", 3)); // AIO_PUMP
                            f.Add(new Fan("Water Pump", 4)); // W_PUMP
                            f.Add(new Fan("Chassis Fan #2", 5)); // CHA_FAN2
                            f.Add(new Fan("Chassis Fan #3", 6)); // CHA_FAN3

                            // Controls
                            c.Add(new Control("Chassis Fan #1", 0)); // CHA_FAN1
                            c.Add(new Control("CPU Fan #1", 1)); // CPU_FAN1
                            c.Add(new Control("CPU Fan #2", 2)); // CPU_FAN2
                            c.Add(new Control("AIO Pump", 3)); // AIO_PUMP
                            c.Add(new Control("Water Pump", 4)); // W_PUMP
                            c.Add(new Control("Chassis Fan #2", 5)); // CHA_FAN2
                            c.Add(new Control("Chassis Fan #3", 6)); // CHA_FAN3
                            break;

                        case Model.B650M_HDV_M_2: //NCT6796D-S
                            v.Add(new Voltage("Vcore", 0)); // CPU Core Voltage
                            v.Add(new Voltage("+12V", 1, 56, 10)); // +12V
                            v.Add(new Voltage("Analog VCC", 2, 34, 34)); // AVCC
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+5V", 4, 20, 10));
                            v.Add(new Voltage("+1.05V Standby", 5, 0, 1)); // +1.05V_ALW
                            v.Add(new Voltage("Voltage #7", 6, 0, 1)); // VIN4
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("VTT", 9, 1, 1)); // VTT
                            v.Add(new Voltage("CPU NB/SoC", 10, 1, 1)); // VDDCR_SOC
                            v.Add(new Voltage("CPU Misc", 11, 34, 34)); // VDD_MISC
                            v.Add(new Voltage("Voltage #13", 12, 0, 1)); // VIN2
                            v.Add(new Voltage("+1.8V", 13, 10, 10));
                            v.Add(new Voltage("CPU VDDIO", 14, 0, 1)); // VDDIO
                            v.Add(new Voltage("VIN9", 15, 0, 1));
                            v.Add(new Voltage("VHIF", 16, 34, 34));
                            v.Add(new Voltage("Voltage #18", 17, 0, 1)); // VIN10

                            t.Add(new Temperature("CPU Socket", 0)); // CPUTIN
                            t.Add(new Temperature("Motherboard", 1)); // SYSTIN
                            t.Add(new Temperature("Auxiliary #0", 2)); // AUXTIN0
                            t.Add(new Temperature("Auxiliary #1", 3)); // AUXTIN1
                            t.Add(new Temperature("T_SEN #1", 4)); // AUXTIN2 (T_SEN1)
                            t.Add(new Temperature("T_SEN #2", 5)); // AUXTIN3 (T_SEN2)
                            t.Add(new Temperature("Auxiliary #4", 6)); // AUXTIN4
                            t.Add(new Temperature("T_SEN #3", 7)); // AUXTIN5 (T_SEN3)
                            t.Add(new Temperature("CPU Core", 8)); // SMBUSMASTER0 (CPU Core)
                            t.Add(new Temperature("CPU (PECI)", 9)); // CPU (PECI)
                            t.Add(new Temperature("Virtual", 10)); // VIRTUAL_TEMP

                            f.Add(new Fan("Chassis Fan #1", 0));
                            f.Add(new Fan("CPU Fan #1", 1)); // CPU1
                            f.Add(new Fan("CPU Fan #2", 2));
                            f.Add(new Fan("AIO Pump", 3));
                            f.Add(new Fan("Chassis Fan #2", 4)); // Chassis2
                            f.Add(new Fan("Fan #6", 5));
                            f.Add(new Fan("Chassis Fan #3", 6));

                            c.Add(new Control("Chassis Fan #1", 0));
                            c.Add(new Control("CPU Fan #1", 1));
                            c.Add(new Control("CPU Fan #2", 2));
                            c.Add(new Control("AIO Pump", 3));
                            c.Add(new Control("Chassis Fan #2", 4));
                            c.Add(new Control("Fan #6", 5));
                            c.Add(new Control("Chassis Fan #3", 6));
                            break;

                        default:
                            bool isAm5Era = superIO.Chip == Chip.NCT6796D || superIO.Chip == Chip.NCT6796DR ||
                                            superIO.Chip == Chip.NCT6796DS || superIO.Chip == Chip.NCT6797D ||
                                            superIO.Chip == Chip.NCT6798D || superIO.Chip == Chip.NCT6799D ||
                                            superIO.Chip == Chip.NCT6701D;

                            if (isAm5Era)
                            {
                                v.Add(new Voltage("Vcore", 0));
                                v.Add(new Voltage("+12V", 1, 56, 10));
                                v.Add(new Voltage("AVCC", 2, 34, 34));
                                v.Add(new Voltage("+3.3V", 3, 34, 34));
                                v.Add(new Voltage("+5V", 4, 20, 10));
                                v.Add(new Voltage("+1.05V Standby", 5, 0, 1));
                                v.Add(new Voltage("Voltage #7", 6, true));
                                v.Add(new Voltage("+3V Standby", 7, 34, 34));
                                v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                                v.Add(new Voltage("CPU Termination", 9, 1, 1));
                                v.Add(new Voltage("CPU NB/SoC", 10, 1, 1));
                                v.Add(new Voltage("Voltage #12", 11, true));
                                v.Add(new Voltage("Voltage #13", 12, true));
                                v.Add(new Voltage("+1.8V", 13, 10, 10));
                                v.Add(new Voltage("CPU VDDIO", 14, 0, 1));
                                v.Add(new Voltage("Voltage #16", 15, true));

                                t.Add(new Temperature("CPU", 0));
                                t.Add(new Temperature("Motherboard", 1));
                                t.Add(new Temperature("Auxiliary #0", 2));
                                t.Add(new Temperature("Auxiliary #1", 3));
                                t.Add(new Temperature("Auxiliary #2", 4));
                                t.Add(new Temperature("Auxiliary #3", 5));
                                t.Add(new Temperature("Auxiliary #4", 6));
                                t.Add(new Temperature("Auxiliary #5", 7));
                                t.Add(new Temperature("PCH TSI0", 8));
                                t.Add(new Temperature("CPU (PECI)", 9));
                                t.Add(new Temperature("Virtual", 10));
                            }
                            else
                            {
                                v.Add(new Voltage("Vcore", 0, 10, 10));
                                v.Add(new Voltage("+12V", 1, 56, 10));
                                v.Add(new Voltage("AVCC", 2, 34, 34));
                                v.Add(new Voltage("+3.3V", 3, 34, 34));
                                v.Add(new Voltage("Voltage #5", 4, true));
                                v.Add(new Voltage("Voltage #6", 5, true));
                                v.Add(new Voltage("Voltage #7", 6, true));
                                v.Add(new Voltage("+3V Standby", 7, 34, 34));
                                v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                                v.Add(new Voltage("CPU Termination", 9));
                                v.Add(new Voltage("Voltage #11", 10, true));
                                v.Add(new Voltage("Voltage #12", 11, true));
                                v.Add(new Voltage("Voltage #13", 12, true));
                                v.Add(new Voltage("Voltage #14", 13, true));
                                v.Add(new Voltage("Voltage #15", 14, true));

                                t.Add(new Temperature("CPU Core", 0));
                                t.Add(new Temperature("Temperature #1", 1));
                                t.Add(new Temperature("Temperature #2", 2));
                                t.Add(new Temperature("Temperature #3", 3));
                                t.Add(new Temperature("Temperature #4", 4));
                                t.Add(new Temperature("Temperature #5", 5));
                                t.Add(new Temperature("Temperature #6", 6));
                            }

                            for (int i = 0; i < superIO.Fans.Length; i++)
                                f.Add(new Fan("Fan #" + (i + 1), i));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + (i + 1), i));

                            break;
                    }

                    break;
                case Manufacturer.ASUS:
                    string[] fanControlNames;
                    switch (model)
                    {
                        case Model.TUF_GAMING_X570_PLUS_WIFI: //NCT6798D
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+5V", 1, 4, 1));
                            v.Add(new Voltage("AVSB", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+12V", 4, 11, 1));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("CPU Termination", 9));

                            t.Add(new Temperature("CPU", 22));
                            t.Add(new Temperature("Motherboard", 2));
                            t.Add(new Temperature("Chipset", 10));

                            f.Add(new Fan("CPU Fan", 1));
                            f.Add(new Fan("CPU Optional Fan", 6));
                            f.Add(new Fan("Chassis Fan #1", 0));
                            f.Add(new Fan("Chassis Fan #2", 2));
                            f.Add(new Fan("Chassis Fan #3", 3));
                            f.Add(new Fan("Chipset Fan", 4));
                            f.Add(new Fan("AIO Pump", 5));

                            c.Add(new Control("CPU Fan", 1));
                            c.Add(new Control("CPU Optional Fan", 6));
                            c.Add(new Control("Chassis Fan #1", 0));
                            c.Add(new Control("Chassis Fan #2", 2));
                            c.Add(new Control("Chassis Fan #3", 3));
                            c.Add(new Control("Chipset Fan", 4));
                            c.Add(new Control("AIO Pump", 5));

                            break;

                        case Model.TUF_GAMING_B550M_PLUS_WIFI: //NCT6798D
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("Voltage #2", 1, true));
                            v.Add(new Voltage("AVCC", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("Voltage #5", 4, true));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("CPU Termination", 9));
                            v.Add(new Voltage("Voltage #11", 10, true));
                            v.Add(new Voltage("Voltage #12", 11, true));
                            v.Add(new Voltage("Voltage #13", 12, true));
                            v.Add(new Voltage("Voltage #14", 13, true));
                            v.Add(new Voltage("Voltage #15", 14, true));
                            t.Add(new Temperature("PECI 0", 0));
                            t.Add(new Temperature("CPU", 1));
                            t.Add(new Temperature("System", 2));
                            t.Add(new Temperature("AUX 0", 3));
                            t.Add(new Temperature("AUX 1", 4));
                            t.Add(new Temperature("AUX 2", 5));
                            t.Add(new Temperature("AUX 3", 6));
                            t.Add(new Temperature("AUX 4", 7));
                            t.Add(new Temperature("SMBus 0", 8));
                            t.Add(new Temperature("SMBus 1", 9));
                            t.Add(new Temperature("PECI 1", 10));
                            t.Add(new Temperature("PCH Chip CPU Max", 11));
                            t.Add(new Temperature("PCH Chip", 12));
                            t.Add(new Temperature("PCH CPU", 13));
                            t.Add(new Temperature("PCH MCH", 14));
                            t.Add(new Temperature("Agent 0 DIMM 0", 15));
                            t.Add(new Temperature("Agent 0 DIMM 1", 16));
                            t.Add(new Temperature("Agent 1 DIMM 0", 17));
                            t.Add(new Temperature("Agent 1 DIMM 1", 18));
                            t.Add(new Temperature("Device 0", 19));
                            t.Add(new Temperature("Device 1", 20));
                            t.Add(new Temperature("PECI 0 Calibrated", 21));
                            t.Add(new Temperature("PECI 1 Calibrated", 22));
                            t.Add(new Temperature("Virtual", 23));

                            for (int i = 0; i < superIO.Fans.Length; i++)
                                f.Add(new Fan("Fan #" + (i + 1), i));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + (i + 1), i));

                            break;

                        case Model.ROG_CROSSHAIR_VIII_HERO: // NCT6798D
                        case Model.ROG_CROSSHAIR_VIII_HERO_WIFI: // NCT6798D
                        case Model.ROG_CROSSHAIR_VIII_DARK_HERO: // NCT6798D
                        case Model.ROG_CROSSHAIR_VIII_FORMULA: // NCT6798D
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("Voltage #2", 1, true));
                            v.Add(new Voltage("AVCC", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("Voltage #5", 4, true));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("CPU NB/SoC", 6));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("CPU Termination", 9));
                            v.Add(new Voltage("Voltage #11", 10, true));
                            v.Add(new Voltage("Voltage #12", 11, true));
                            v.Add(new Voltage("Voltage #13", 12, true));
                            v.Add(new Voltage("VDIMM", 13));
                            v.Add(new Voltage("Voltage #15", 14, true));
                            t.Add(new Temperature("PECI 0", 0));
                            t.Add(new Temperature("CPU", 1));
                            t.Add(new Temperature("Motherboard", 2));
                            t.Add(new Temperature("AUX 0", 3));
                            t.Add(new Temperature("AUX 1", 4));
                            t.Add(new Temperature("AUX 2", 5));
                            t.Add(new Temperature("AUX 3", 6));
                            t.Add(new Temperature("AUX 4", 7));
                            t.Add(new Temperature("SMBus 0", 8));
                            t.Add(new Temperature("SMBus 1", 9));
                            t.Add(new Temperature("PECI 1", 10));
                            t.Add(new Temperature("PCH Chip CPU Max", 11));
                            t.Add(new Temperature("PCH Chip", 12));
                            t.Add(new Temperature("PCH CPU", 13));
                            t.Add(new Temperature("PCH MCH", 14));
                            t.Add(new Temperature("Agent 0 DIMM 0", 15));
                            t.Add(new Temperature("Agent 0 DIMM 1", 16));
                            t.Add(new Temperature("Agent 1 DIMM 0", 17));
                            t.Add(new Temperature("Agent 1 DIMM 1", 18));
                            t.Add(new Temperature("Device 0", 19));
                            t.Add(new Temperature("Device 1", 20));
                            t.Add(new Temperature("PECI 0 Calibrated", 21));
                            t.Add(new Temperature("PECI 1 Calibrated", 22));
                            t.Add(new Temperature("Virtual", 23));

                            fanControlNames = new string[] { "Chassis Fan 1", "CPU Fan", "Chassis Fan 2", "Chassis Fan 3", "High Amp Fan", "Waterpump", "AIO Pump" };
                            System.Diagnostics.Debug.Assert(fanControlNames.Length == superIO.Fans.Length,
                                                            $"Expected {fanControlNames.Length} fan register in the SuperIO chip");

                            System.Diagnostics.Debug.Assert(superIO.Fans.Length == superIO.Controls.Length,
                                                            "Expected counts of cans controls and fan speed registers to be equal");

                            for (int i = 0; i < fanControlNames.Length; i++)
                                f.Add(new Fan(fanControlNames[i], i));

                            for (int i = 0; i < fanControlNames.Length; i++)
                                c.Add(new Control(fanControlNames[i], i));

                            break;

                        case Model.ROG_STRIX_B550_I_GAMING: //NCT6798D
                            v.Add(new Voltage("Vcore", 0, 10, 10));
                            v.Add(new Voltage("+5V", 1, 4, 1)); //Probably not updating properly
                            v.Add(new Voltage("AVCC", 2, 10, 10));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+12V", 4, 11, 1)); //Probably not updating properly
                                                                  //v.Add(new Voltage("#Unused #5", 5, 0, 1, 0, true));
                                                                  //v.Add(new Voltage("#Unused #6", 6, 0, 1, 0, true));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("CPU Termination", 9));

                            t.Add(new Temperature("CPU", 1));
                            t.Add(new Temperature("Motherboard", 2));
                            t.Add(new Temperature("PCH Chip CPU Max", 11));
                            t.Add(new Temperature("PCH Chip", 12));
                            t.Add(new Temperature("PCH CPU", 13));
                            t.Add(new Temperature("PCH MCH", 14));
                            t.Add(new Temperature("Agent 0 DIMM 0", 15));
                            //t.Add(new Temperature("Agent 0 DIMM 1", 16));
                            t.Add(new Temperature("Agent 1 DIMM 0", 17));
                            //t.Add(new Temperature("Agent 1 DIMM 1", 18));
                            t.Add(new Temperature("Device 0", 19));
                            t.Add(new Temperature("Device 1", 20));
                            t.Add(new Temperature("PECI 0 Calibrated", 21));
                            t.Add(new Temperature("PECI 1 Calibrated", 22));
                            t.Add(new Temperature("Virtual", 23));

                            for (int i = 0; i < superIO.Fans.Length; i++)
                            {
                                switch (i)
                                {
                                    case 0:
                                        f.Add(new Fan("Chassis Fan", 0));
                                        break;
                                    case 1:
                                        f.Add(new Fan("CPU Fan", 1));
                                        break;
                                    case 4:
                                        f.Add(new Fan("AIO Pump", 4));
                                        break;
                                }
                            }

                            for (int i = 0; i < superIO.Controls.Length; i++)
                            {
                                switch (i)
                                {
                                    case 0:
                                        c.Add(new Control("Chassis Fan", 0));
                                        break;
                                    case 1:
                                        c.Add(new Control("CPU Fan", 1));
                                        break;
                                    case 4:
                                        c.Add(new Control("AIO Pump", 4));
                                        break;
                                }
                            }

                            break;

                        case Model.ROG_ZENITH_II_EXTREME: // NCT6798D
                                                          // Voltage = value + (value - Vf) * Ri / Rf.
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+5V", 1, 4, 1));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+12V", 4, 6, 1));
                            v.Add(new Voltage("DIMM C/D", 11, 10, 10));
                            v.Add(new Voltage("DIMM A/B", 13));
                            v.Add(new Voltage("PLL", 14));

                            t.Add(new Temperature("CPU", 1));
                            t.Add(new Temperature("Motherboard", 2));
                            t.Add(new Temperature("Temperature #3", 3));
                            t.Add(new Temperature("Temperature #4", 4));
                            t.Add(new Temperature("Temperature #5", 5));
                            t.Add(new Temperature("Temperature #6", 6));
                            t.Add(new Temperature("Temperature #7", 7));
                            t.Add(new Temperature("Temperature #21", 21));

                            for (int i = 0; i < superIO.Fans.Length; i++)
                            {
                                switch (i)
                                {
                                    case 0:
                                        f.Add(new Fan("Chassis Fan", 0));
                                        break;
                                    case 1:
                                        f.Add(new Fan("CPU Fan", 1));
                                        break;
                                    case 2:
                                        f.Add(new Fan("CPU Optional Fan", 2));
                                        break;
                                    case 4:
                                        f.Add(new Fan("AIO Pump", 4));
                                        break;
                                }
                            }

                            for (int i = 0; i < superIO.Controls.Length; i++)
                            {
                                switch (i)
                                {
                                    case 0:
                                        c.Add(new Control("Chassis Fan", 0));
                                        break;
                                    case 1:
                                        c.Add(new Control("CPU Fan", 1));
                                        break;
                                    case 2:
                                        c.Add(new Control("CPU Optional Fan", 2));
                                        break;
                                    case 4:
                                        c.Add(new Control("AIO Pump", 4));
                                        break;
                                }
                            }

                            break;

                        case Model.ROG_STRIX_X570_I_GAMING: //NCT6798D
                            v.Add(new Voltage("Vcore", 0, 10, 10));
                            v.Add(new Voltage("+5V", 1, 4, 1)); //Probably not updating properly
                            v.Add(new Voltage("AVCC", 2, 10, 10));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+12V", 4, 11, 1)); //Probably not updating properly
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("CPU Termination", 9));
                            t.Add(new Temperature("CPU", 1));
                            t.Add(new Temperature("Motherboard", 2));
                            t.Add(new Temperature("Temperature #3", 3));
                            t.Add(new Temperature("Temperature #4", 4));
                            t.Add(new Temperature("Temperature #5", 5));
                            t.Add(new Temperature("Temperature #6", 6));
                            t.Add(new Temperature("Temperature #7", 7));
                            t.Add(new Temperature("Temperature #21", 21));

                            for (int i = 0; i < superIO.Fans.Length; i++)
                            {
                                switch (i)
                                {
                                    case 0:
                                        f.Add(new Fan("Chassis Fan", 0));
                                        break;
                                    case 1:
                                        f.Add(new Fan("CPU Fan", 1));
                                        break;
                                    case 4:
                                        f.Add(new Fan("AIO Pump", 4));
                                        break;
                                }
                            }

                            for (int i = 0; i < superIO.Controls.Length; i++)
                            {
                                switch (i)
                                {
                                    case 0:
                                        c.Add(new Control("Chassis Fan", 0));
                                        break;
                                    case 1:
                                        c.Add(new Control("CPU Fan", 1));
                                        break;
                                    case 4:
                                        c.Add(new Control("AIO Pump", 4));
                                        break;
                                }
                            }

                            break;

                        case Model.ROG_STRIX_B550_F_GAMING_WIFI: // NCT6798D-R
                            v.Add(new Voltage("Vcore", 0, 2, 2));
                            v.Add(new Voltage("+5V", 1, 4, 1));
                            v.Add(new Voltage("AVCC", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+12V", 4, 11, 1));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("CPU Termination", 9));
                            v.Add(new Voltage("Voltage #11", 10, true));
                            v.Add(new Voltage("Voltage #12", 11, true));
                            v.Add(new Voltage("Voltage #13", 12, true));
                            v.Add(new Voltage("Voltage #14", 13, true));
                            v.Add(new Voltage("Voltage #15", 14, true));
                            t.Add(new Temperature("CPU Core", 0));

                            for (int i = 0; i < superIO.Fans.Length; i++)
                                f.Add(new Fan("Fan #" + (i + 1), i));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + (i + 1), i));

                            break;

                        case Model.PRIME_B650_PLUS: // NCT6799D
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+5V", 1, 4, 1));
                            v.Add(new Voltage("AVSB", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+12V", 4, 11, 1));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("CPU Termination", 9));
                            v.Add(new Voltage("CPU VDDIO", 10, 1, 1));

                            t.Add(new Temperature("CPU", 22));
                            t.Add(new Temperature("Motherboard", 2));
                            t.Add(new Temperature("CPU Package", 3));

                            f.Add(new Fan("CPU Fan", 1));
                            f.Add(new Fan("CPU Optional Fan", 4));
                            f.Add(new Fan("Chassis Fan #1", 0));
                            f.Add(new Fan("Chassis Fan #2", 2));
                            f.Add(new Fan("Chassis Fan #3", 3));
                            f.Add(new Fan("AIO Pump", 5));

                            c.Add(new Control("CPU Fan", 1));
                            c.Add(new Control("Chassis Fan #1", 0));
                            c.Add(new Control("Chassis Fan #2", 2));
                            c.Add(new Control("Chassis Fan #3", 3));
                            c.Add(new Control("AIO Pump", 5));

                            break;

                        case Model.ROG_CROSSHAIR_X670E_GENE: // NCT6799D
                        case Model.ROG_CROSSHAIR_X670E_HERO:
                            v.Add(new Voltage("Vcore", 0, 2, 2)); // This is wrong
                            v.Add(new Voltage("+5V", 1, 4, 1));
                            v.Add(new Voltage("AVCC", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+12V", 4, 11, 1));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("CPU Termination", 9)); // This is wrong
                            v.Add(new Voltage("Voltage #11", 10, true));
                            v.Add(new Voltage("Voltage #12", 11, true));
                            v.Add(new Voltage("Voltage #13", 12, true));
                            v.Add(new Voltage("Voltage #14", 13, true));
                            v.Add(new Voltage("Voltage #15", 14, true));
                            t.Add(new Temperature("CPU Core", 0));
                            t.Add(new Temperature("Temperature #1", 1)); // No matching temp value
                            t.Add(new Temperature("Motherboard", 2)); // Matches MB in HWinfo
                            t.Add(new Temperature("Temperature #3", 3)); // No matching temp value
                            t.Add(new Temperature("Temperature #4", 4)); // No matching temp value
                            t.Add(new Temperature("Temperature #5", 5)); // No matching temp value
                            t.Add(new Temperature("Temperature #6", 6)); // No matching temp value
                            t.Add(new Temperature("Temperature #7", 8)); // Matches MB in HWinfo
                            t.Add(new Temperature("CPU", 22)); // Matches MB in HWinfo
                            t.Add(new Temperature("T_SEN", 24));

                            for (int i = 0; i < superIO.Fans.Length; i++)
                                f.Add(new Fan("Fan #" + (i + 1), i));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + (i + 1), i));

                            break;

                        case Model.ROG_STRIX_X670E_A_GAMING_WIFI: // NCT6799D
                        case Model.ROG_STRIX_X670E_E_GAMING_WIFI: // NCT6799D
                        case Model.ROG_STRIX_X670E_F_GAMING_WIFI: // NCT6799D
                            v.Add(new Voltage("Vcore", 0, 2, 2)); // This is wrong
                            v.Add(new Voltage("+5V", 1, 4, 1));
                            v.Add(new Voltage("AVCC", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+12V", 4, 11, 1));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("CPU Termination", 9)); // This is wrong
                            v.Add(new Voltage("Voltage #11", 10, true));
                            v.Add(new Voltage("Voltage #12", 11, true));
                            v.Add(new Voltage("Voltage #13", 12, true));
                            v.Add(new Voltage("Voltage #14", 13, true));
                            v.Add(new Voltage("Voltage #15", 14, true));
                            t.Add(new Temperature("CPU Core", 0));
                            t.Add(new Temperature("VRM", 1)); // Aligned with BIOS value ROG_STRIX_X670E_E_GAMING_WIFI
                            t.Add(new Temperature("Motherboard", 2)); // Aligned with Armoury Crate ROG_STRIX_X670E_E_GAMING_WIFI
                            t.Add(new Temperature("Temperature #3", 3)); // No matching temp value
                            t.Add(new Temperature("Temperature #4", 4)); // No matching temp value
                            t.Add(new Temperature("Temperature #5", 5)); // No matching temp value
                            t.Add(new Temperature("Temperature #6", 6)); // No matching temp value
                            t.Add(new Temperature("T_SEN", 24)); // Aligned with Armoury Crate ROG_STRIX_X670E_E_GAMING_WIFI

                            for (int i = 0; i < superIO.Fans.Length; i++)
                                f.Add(new Fan("Fan #" + (i + 1), i));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + (i + 1), i));

                            break;

                        case Model.ROG_STRIX_X870E_E_GAMING_WIFI: // NCT6701D
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+5V", 1, 4, 1));
                            v.Add(new Voltage("AVSB", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+12V", 4, 11, 1));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("CPU Termination", 9, true)); // Value does not match any in hwmonnitor
                            v.Add(new Voltage("VDDIO", 10, 1, 1));

                            t.Add(new Temperature("Motherboard", 2));
                            t.Add(new Temperature("CPU", 22));

                            fanControlNames = new string[] { "Chassis Fan #1", "CPU Fan", "Chassis Fan #2", "Chassis Fan #3", "Chassis Fan #4", "Chassis Fan #5", "AIO Pump" };

                            System.Diagnostics.Debug.Assert(fanControlNames.Length == superIO.Fans.Length, $"Expected {fanControlNames.Length} fan register in the SuperIO chip");
                            System.Diagnostics.Debug.Assert(superIO.Fans.Length == superIO.Controls.Length, "Expected counts of cans controls and fan speed registers to be equal");

                            for (int i = 0; i < fanControlNames.Length; i++)
                                f.Add(new Fan(fanControlNames[i], i));

                            for (int i = 0; i < fanControlNames.Length; i++)
                                c.Add(new Control(fanControlNames[i], i));

                            break;

                        case Model.PROART_X670E_CREATOR_WIFI: // NCT6799D
                            v.Add(new Voltage("Vcore", 0)); // This is wrong
                            v.Add(new Voltage("+5V", 1, 4, 1));
                            v.Add(new Voltage("AVCC", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+12V", 4, 11, 1));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            //v.Add(new Voltage("CPU Termination", 9)); // This is wrong
                            t.Add(new Temperature("CPU", 22));
                            t.Add(new Temperature("Motherboard", 2));
                            t.Add(new Temperature("T_SEN", 24)); // Aligned with Armoury Crate
                            t.Add(new Temperature("Temperature #1", 1)); // Unknown, Possibly VRM with 23 offset

                            for (int i = 0; i < superIO.Fans.Length; i++)
                                f.Add(new Fan("Fan #" + (i + 1), i));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + (i + 1), i));

                            break;

                        case Model.PRIME_X870_P: // NCT6701D
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+5V", 1, 4, 1));
                            v.Add(new Voltage("AVSB", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+12V", 4, 11, 1));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("CPU Termination", 9, 34, 34));
                            v.Add(new Voltage("CPU VDDIO", 10, 1, 1));
                            v.Add(new Voltage("Voltage #12", 11, true));
                            v.Add(new Voltage("+1.8V Standby", 12, true)); // Uknown values needed for tuning, hidden for now
                            v.Add(new Voltage("Voltage #14", 13, true));
                            v.Add(new Voltage("Voltage #15", 14, true));
                            v.Add(new Voltage("Voltage #16", 15, true));
                            // All voltage channels above 15 cannot be added?

                            t.Add(new Temperature("Motherboard", 2));
                            t.Add(new Temperature("VRM", 7));
                            t.Add(new Temperature("CPU", 22));

                            fanControlNames = new string[] { "Chassis Fan #1", "CPU Fan", "Chassis Fan #2", "Chassis Fan #3", "CPU_OPT", "Chassis Fan #4", "AIO Pump" };

                            for (int i = 0; i < fanControlNames.Length; i++)
                                f.Add(new Fan(fanControlNames[i], i));

                            for (int i = 0; i < fanControlNames.Length; i++)
                                c.Add(new Control(fanControlNames[i], i));

                            break;

                        case Model.ROG_STRIX_X870_I_GAMING_WIFI: // NCT6701D
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+5V", 1, 4.02f, 1));
                            v.Add(new Voltage("AVSB", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+12V", 4, 10.98f, 1));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("VTT", 9, 34, 34));
                            v.Add(new Voltage("CPU VDDIO", 10, 34, 34));
                            v.Add(new Voltage("CPU MISC", 11, 34, 34));
                            v.Add(new Voltage("1.8V Standby", 12, 7.66f, 10));

                            t.Add(new Temperature("CPU", 22));
                            t.Add(new Temperature("Motherboard", 2));

                            f.Add(new Fan("Chassis", 0));
                            f.Add(new Fan("CPU", 1));
                            f.Add(new Fan("Chipset / Disk", 5));
                            f.Add(new Fan("AIO Pump", 6));

                            c.Add(new Control("Chassis", 0));
                            c.Add(new Control("CPU", 1));
                            c.Add(new Control("Chipset / Disk", 5));
                            c.Add(new Control("AIO Pump", 6));

                            break;

                        case Model.ROG_CROSSHAIR_X870E_APEX: // NCT6701D
                            {
                                v.Add(new Voltage("Vcore", 0, 15, 136));
                                v.Add(new Voltage("+5V", 1, 4, 1));
                                v.Add(new Voltage("AVSB", 2, 34, 34));
                                v.Add(new Voltage("+3.3V", 3, 34, 34));
                                v.Add(new Voltage("+12V", 4, 11, 1));
                                v.Add(new Voltage("CPU MISC", 5, 9, 82));
                                v.Add(new Voltage("CPU NB/SoC", 6, 9, 82));
                                v.Add(new Voltage("+3V Standby", 7, 34, 34));
                                v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                                v.Add(new Voltage("VTT", 9, 34, 34));
                                v.Add(new Voltage("Chipset 0 VDD", 10, 1, 1));
                                v.Add(new Voltage("Chipset 1 VDD", 11, 1, 1));
                                v.Add(new Voltage("Chipset Standby", 12));
                                v.Add(new Voltage("CPU VDDIO", 13, 9, 82));
                                v.Add(new Voltage("1.8V PLL", 14, 17, 36));

                                // The following mappings only apply when FanXpert is off in Armoury Crate.
                                t.Add(new Temperature("CPU Package", 0));
                                t.Add(new Temperature("VRM", 1));
                                t.Add(new Temperature("Motherboard", 2));

                                // BIOS 1804 or above moved CPU Temp Sensor to 21
                                t.Add(new Temperature("CPU", 21));

                                // For BIOS 1803 or older
                                t.Add(new Temperature("CPU", 22));

                                // Add all unmapped temperature sensors
                                for (int i = 3; i < superIO.Temperatures.Length; i++)
                                {
                                    // 21 and 22 are not used at the same time
                                    if (i != 21 && i != 22)
                                    {
                                        t.Add(new Temperature($"Temperature #{i}", i));
                                    }
                                }

                                fanControlNames = new string[] { "Chassis Fan #1", "CPU Fan", "Chassis Fan #2", "Extra Flow Fan", "Unused", "Water Pump", "AIO Pump" };

                                System.Diagnostics.Debug.Assert(fanControlNames.Length == superIO.Fans.Length, $"Expected {fanControlNames.Length} fan register in the SuperIO chip");
                                System.Diagnostics.Debug.Assert(superIO.Fans.Length == superIO.Controls.Length, "Expected counts of fan controls and fan speed registers to be equal");

                                for (int i = 0; i < fanControlNames.Length; i++)
                                {
                                    f.Add(new Fan(fanControlNames[i], i));
                                }

                                for (int i = 0; i < fanControlNames.Length; i++)
                                {
                                    c.Add(new Control(fanControlNames[i], i));
                                }
                            }

                            break;

                        case Model.ROG_CROSSHAIR_X870E_HERO: // NCT6701D
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+5V", 1, 4, 1));
                            v.Add(new Voltage("AVSB", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+12V", 4, 11, 1));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("CPU Termination", 9, true)); // Value does not match any in hwmonitor
                            v.Add(new Voltage("CPU VDDIO", 10, 1, 1));

                            t.Add(new Temperature("CPU Package", 0));
                            t.Add(new Temperature("Motherboard", 2));
                            t.Add(new Temperature("CPU", 22));

                            fanControlNames = new string[] { "Chassis Fan #1", "CPU Fan", "Chassis Fan #2", "Chassis Fan #3", "Chassis Fan #4", "Water Pump", "AIO Pump" };

                            System.Diagnostics.Debug.Assert(fanControlNames.Length == superIO.Fans.Length, $"Expected {fanControlNames.Length} fan register in the SuperIO chip");
                            System.Diagnostics.Debug.Assert(superIO.Fans.Length == superIO.Controls.Length, "Expected counts of fan controls and fan speed registers to be equal");

                            for (int i = 0; i < fanControlNames.Length; i++)
                                f.Add(new Fan(fanControlNames[i], i));

                            for (int i = 0; i < fanControlNames.Length; i++)
                                c.Add(new Control(fanControlNames[i], i));

                            break;

                        case Model.PROART_X870E_CREATOR_WIFI: // NCT6701D
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("Voltage #2", 1, true));
                            v.Add(new Voltage("AVCC", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("Voltage #5", 4, true));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("CPU Termination", 9));
                            v.Add(new Voltage("Voltage #11", 10, true));
                            v.Add(new Voltage("Voltage #12", 11, true));
                            v.Add(new Voltage("Voltage #13", 12, true));
                            v.Add(new Voltage("Voltage #14", 13, true));
                            v.Add(new Voltage("Voltage #15", 14, true));

                            t.Add(new Temperature("Motherboard", 2));
                            t.Add(new Temperature("CPU", 22));

                            for (int i = 0; i < superIO.Fans.Length; i++)
                                f.Add(new Fan("Fan #" + (i + 1), i));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + (i + 1), i));

                            break;

                        case Model.ROG_STRIX_B850_A_GAMING_WIFI: // NCT6701D
                        case Model.TUF_GAMING_B850_BTF_WIFI_W: // NCT6701D
                        case Model.TUF_GAMING_X870_PRO_WIFI7_W_NEO: // NCT6701D
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+5V", 1, 4.02f, 1));
                            v.Add(new Voltage("AVSB", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+12V", 4, 10.98f, 1));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("Voltage #10", 9, true));
                            v.Add(new Voltage("CPU VDDIO", 10, 34, 34));
                            v.Add(new Voltage("CPU MISC", 11, 34, 34));
                            v.Add(new Voltage("1.8V Standby", 12, 7.66f, 10));
                            v.Add(new Voltage("Voltage #14", 13, true));
                            v.Add(new Voltage("Voltage #15", 14, true));
                            v.Add(new Voltage("Voltage #16", 15, true));

                            t.Add(new Temperature("Motherboard", 2));
                            t.Add(new Temperature("CPU Package", 26));
                            t.Add(new Temperature("CPU", 21));

                            if (model == Model.TUF_GAMING_X870_PRO_WIFI7_W_NEO)
                            {
                                t.Add(new Temperature("Temperature #14", 27));
                                t.Add(new Temperature("T_Sensor", 24));

                                fanControlNames = new string[] { "Chassis Fan #1", "CPU Fan", "Chassis Fan #2", "Chassis Fan #3", "Chassis Fan #4", "Water Pump+", "AIO Pump" };
                            }
                            else
                            {
                                t.Add(new Temperature("VRM", 1));
                                t.Add(new Temperature("T_Sensor", 24));

                                fanControlNames = new string[] { "CPU Fan", "CPU Optional Fan", "Chassis Fan #1", "Chassis Fan #2", "Chassis Fan #3", "Chassis Fan #4", "AIO Pump" };
                            }

                            System.Diagnostics.Debug.Assert(fanControlNames.Length == superIO.Fans.Length, $"Expected {fanControlNames.Length} fan register in the SuperIO chip");
                            System.Diagnostics.Debug.Assert(superIO.Fans.Length == superIO.Controls.Length, "Expected counts of fans, controls and fan speed registers to be equal");

                            for (int i = 0; i < fanControlNames.Length; i++)
                                f.Add(new Fan(fanControlNames[i], i));

                            for (int i = 0; i < fanControlNames.Length; i++)
                                c.Add(new Control(fanControlNames[i], i));

                            break;

                        case Model.ROG_STRIX_B850_E_GAMING_WIFI: // NCT6701D
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+5V", 1, 4.02f, 1));
                            v.Add(new Voltage("AVSB", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+12V", 4, 10.98f, 1));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("VTT", 9, 34, 34));
                            v.Add(new Voltage("CPU VDDIO", 10, 34, 34));
                            v.Add(new Voltage("CPU MISC", 11, 34, 34));
                            v.Add(new Voltage("1.8V Standby", 12, 7.66f, 10));
                            v.Add(new Voltage("Voltage #14", 13, true));
                            v.Add(new Voltage("Voltage #15", 14, true));
                            v.Add(new Voltage("Voltage #16", 15, true));

                            t.Add(new Temperature("CPU", 21));
                            //t.Add(new Temperature("CPU Package", 7));
                            t.Add(new Temperature("Motherboard", 2));
                            //t.Add(new Temperature("VRM", 1));

                            fanControlNames = new string[] { "Chassis Fan #1", "CPU Fan", "Chassis Fan #2", "Chassis Fan #3", "Chassis Fan #4", "Chassis Fan #5", "AIO Pump" };

                            System.Diagnostics.Debug.Assert(fanControlNames.Length == superIO.Fans.Length, $"Expected {fanControlNames.Length} fan register in the SuperIO chip");
                            System.Diagnostics.Debug.Assert(superIO.Fans.Length == superIO.Controls.Length, "Expected counts of cans controls and fan speed registers to be equal");

                            for (int i = 0; i < fanControlNames.Length; i++)
                                f.Add(new Fan(fanControlNames[i], i));

                            for (int i = 0; i < fanControlNames.Length; i++)
                                c.Add(new Control(fanControlNames[i], i));

                            break;

                        case Model.ROG_STRIX_B850_I_GAMING_WIFI: // NCT6701D
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+5V", 1, 4.02f, 1));
                            v.Add(new Voltage("AVSB", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+12V", 4, 10.98f, 1));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("VTT", 9, 34, 34));
                            v.Add(new Voltage("CPU VDDIO", 10, 34, 34));
                            v.Add(new Voltage("CPU MISC", 11, 34, 34));
                            v.Add(new Voltage("1.8V Standby", 12, 7.66f, 10));
                            v.Add(new Voltage("Voltage #14", 13, true));
                            v.Add(new Voltage("Voltage #15", 14, true));
                            v.Add(new Voltage("Voltage #16", 15, true));

                            t.Add(new Temperature("CPU", 22));
                            t.Add(new Temperature("Motherboard", 2));
                            t.Add(new Temperature("T-Sensor", 6));

                            f.Add(new Fan("Chassis Fan", 0));
                            f.Add(new Fan("CPU Fan", 1));
                            f.Add(new Fan("Extra Flow Fan", 5));
                            f.Add(new Fan("AIO Pump", 6));

                            c.Add(new Control("Chassis Fan", 0));
                            c.Add(new Control("CPU Fan", 1));
                            c.Add(new Control("Extra Flow Fan", 5));
                            c.Add(new Control("AIO Pump", 6));

                            break;

                        case Model.TUF_GAMING_B850M_PLUS_II: // NCT6701D
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+5V", 1, 4.02f, 1));
                            v.Add(new Voltage("AVSB", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+12V", 4, 10.98f, 1));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("VTT", 9, 34, 34));
                            v.Add(new Voltage("CPU VDDIO", 10, 34, 34));
                            v.Add(new Voltage("CPU MISC", 11, 34, 34));
                            v.Add(new Voltage("1.8V Standby", 12, 7.66f, 10));
                            v.Add(new Voltage("Voltage #14", 13, true));
                            v.Add(new Voltage("Voltage #15", 14, true));
                            v.Add(new Voltage("Voltage #16", 15, true));

                            t.Add(new Temperature("CPU", 22));
                            t.Add(new Temperature("Motherboard", 2));
                            t.Add(new Temperature("T-Sensor", 6));

                            f.Add(new Fan("Chassis Fan #1", 0)); // CHA_FAN_1
                            f.Add(new Fan("CPU Fan", 1)); // CPU_FAN
                            f.Add(new Fan("Chassis Fan #2", 2)); // CHA_FAN_2
                            f.Add(new Fan("Chassis Fan #3", 3)); // CHA_FAN_3
                            f.Add(new Fan("CPU Optional Fan", 4)); // CPU_OPT
                            f.Add(new Fan("AIO Pump", 5)); // AIO_PUMP

                            c.Add(new Control("Chassis Fan #1", 0)); // CHA_FAN_1
                            c.Add(new Control("CPU Fan", 1)); // CPU_FAN
                            c.Add(new Control("Chassis Fan #2", 2)); // CHA_FAN_2
                            c.Add(new Control("Chassis Fan #3", 3)); // CHA_FAN_3
                            c.Add(new Control("CPU Optional Fan", 4)); // CPU_OPT
                            c.Add(new Control("AIO Pump", 5)); // AIO_PUMP
                            break;

                        default:
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+5V", 1, 4, 1));
                            v.Add(new Voltage("AVCC", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+12V", 4, 11, 1));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("CPU Termination", 9, true));
                            v.Add(new Voltage("CPU VDDIO", 10, 1, 1));
                            v.Add(new Voltage("Voltage #12", 11, true));
                            v.Add(new Voltage("Voltage #13", 12, true));
                            v.Add(new Voltage("Voltage #14", 13, true));
                            v.Add(new Voltage("Voltage #15", 14, true));
                            t.Add(new Temperature("CPU Core", 0));
                            t.Add(new Temperature("Temperature #1", 1));
                            t.Add(new Temperature("Motherboard", 2));
                            t.Add(new Temperature("Temperature #3", 3));
                            t.Add(new Temperature("Temperature #4", 4));
                            t.Add(new Temperature("Temperature #5", 5));
                            t.Add(new Temperature("Temperature #6", 6));

                            for (int i = 0; i < superIO.Fans.Length; i++)
                                f.Add(new Fan("Fan #" + (i + 1), i));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + (i + 1), i));

                            break;
                    }

                    break;
                case Manufacturer.MSI:
                    switch (model)
                    {
                        case Model.B360M_PRO_VDH: // NCT6797D
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+5V", 1, 4, 1));
                            v.Add(new Voltage("AVCC", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+12V", 4, 11, 1));
                            //v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("CPU I/O", 6));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CPU Termination", 9));
                            v.Add(new Voltage("CPU SA", 10));
                            //v.Add(new Voltage("Voltage #12", 11, true));
                            v.Add(new Voltage("Northbridge/SoC", 12));
                            v.Add(new Voltage("VDIMM", 13, 1, 1));
                            //v.Add(new Voltage("Voltage #15", 14, true));
                            t.Add(new Temperature("CPU", 0));
                            t.Add(new Temperature("Auxiliary", 1));
                            t.Add(new Temperature("Motherboard", 2));
                            t.Add(new Temperature("Temperature #1", 5));
                            f.Add(new Fan("CPU Fan", 1));
                            f.Add(new Fan("System Fan #1", 2));
                            f.Add(new Fan("System Fan #2", 3));
                            c.Add(new Control("CPU Fan", 1));
                            c.Add(new Control("System Fan #1", 2));
                            c.Add(new Control("System Fan #2", 3));

                            break;

                        case Model.B450A_PRO: // NCT6797D
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+5V", 1, 4, 1));
                            v.Add(new Voltage("AVCC", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+12V", 4, 11, 1));
                            //v.Add(new Voltage("Voltage #6", 5, false));
                            //v.Add(new Voltage("CPU I/O", 6));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CPU Termination", 9));
                            v.Add(new Voltage("CPU SA", 10));
                            //v.Add(new Voltage("Voltage #12", 11, false));
                            v.Add(new Voltage("CPU NB/SoC", 12));
                            v.Add(new Voltage("VDIMM", 13, 1, 1));
                            //v.Add(new Voltage("Voltage #15", 14, false));
                            //t.Add(new Temperature("CPU", 0));
                            t.Add(new Temperature("CPU", 1));
                            t.Add(new Temperature("System", 2));
                            t.Add(new Temperature("VRM MOS", 3));
                            t.Add(new Temperature("PCH", 5));
                            t.Add(new Temperature("SMBus 0", 8));
                            f.Add(new Fan("Pump Fan", 0));
                            f.Add(new Fan("CPU Fan", 1));
                            f.Add(new Fan("System Fan #1", 2));
                            f.Add(new Fan("System Fan #2", 3));
                            f.Add(new Fan("System Fan #3", 4));
                            f.Add(new Fan("System Fan #4", 5));
                            c.Add(new Control("Pump Fan", 0));
                            c.Add(new Control("CPU Fan", 1));
                            c.Add(new Control("System Fan #1", 2));
                            c.Add(new Control("System Fan #2", 3));
                            c.Add(new Control("System Fan #3", 4));
                            c.Add(new Control("System Fan #4", 5));

                            break;

                        case Model.X570_Gaming_Plus:
                            // NCT6797D
                            // NCT771x : PCIe 1, M.2 1, not supported
                            // RF35204 : VRM not supported

                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+5V", 1, 4, 1));
                            v.Add(new Voltage("AVCC", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+12V", 4, 11, 1));
                            //v.Add(new Voltage("Voltage #6", 5));
                            v.Add(new Voltage("VIN4", 6));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("CPU Termination", 9));
                            //v.Add(new Voltage("Voltage #11", 10));
                            v.Add(new Voltage("Voltage #11", 11));
                            v.Add(new Voltage("CPU NB/SoC", 12));
                            v.Add(new Voltage("VDIMM", 13, 1, 1));
                            v.Add(new Voltage("Voltage #14", 14));

                            //t.Add(new Temperature("Unknown Temperature #1", 1));
                            t.Add(new Temperature("System", 2));
                            t.Add(new Temperature("MOS", 3));
                            t.Add(new Temperature("Chipset", 5));
                            t.Add(new Temperature("CPU", 9));

                            f.Add(new Fan("Pump Fan", 0));
                            f.Add(new Fan("CPU Fan", 1));
                            f.Add(new Fan("System Fan #1", 2));
                            f.Add(new Fan("System Fan #2", 3));
                            f.Add(new Fan("System Fan #3", 4));
                            f.Add(new Fan("System Fan #4", 5));
                            f.Add(new Fan("Chipset Fan", 6));

                            c.Add(new Control("Pump Fan", 0));
                            c.Add(new Control("CPU Fan", 1));
                            c.Add(new Control("System Fan #1", 2));
                            c.Add(new Control("System Fan #2", 3));
                            c.Add(new Control("System Fan #3", 4));
                            c.Add(new Control("System Fan #4", 5));
                            c.Add(new Control("Chipset Fan", 6));

                            break;

                        case Model.X570_MS7C35:
                            // NCT6797D
                            // NCT7802Y (on SMBus): SYS_FAN5, CPU 1.8V, Chipset SOC, Chipset CLDO - not supported
                            // Unknown: PCIe 1, PCIe 3, M.2_1

                            v.Add(new Voltage("Vcore", 0));           // CPUVCORE
                            v.Add(new Voltage("+5V", 1, 12, 3));      // VIN1
                            v.Add(new Voltage("AVCC", 2, 34, 34));    // AVSB, +3.3V analog power
                            v.Add(new Voltage("+3.3V", 3, 34, 34));   // 3VCC
                            v.Add(new Voltage("+12V", 4, 220, 20));  // VIN0
                            v.Add(new Voltage("VRM MOS", 6, true)); // VIN4, temperature input
                            v.Add(new Voltage("+3.3V Standby", 7, 34, 34));   // 3VSB, +3.3V digital power
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34, 0)); // VBAT
                            v.Add(new Voltage("CPU 1.8V", 9));        // VTT, CPU_1P8
                            v.Add(new Voltage("CPU VDDP", 10));       // VIN5
                            v.Add(new Voltage("Voltage #6", 11, true));    // VIN6, temperature input
                            v.Add(new Voltage("CPU NB/SoC", 12));     // VIN2, VCCP_NB
                            v.Add(new Voltage("VDIMM", 13, 1, 1));     // VIN3
                            v.Add(new Voltage("+5V Standby", 14, 768, 330));  // VIN7, ATX_5VSB

                            t.Add(new Temperature("CPU Socket", 1));  // CPUTIN, 10k at top side of the socket
                            t.Add(new Temperature("System", 2));      // SYSTIN, P-3906
                            t.Add(new Temperature("VRM MOS", 3));     // AUXTIN0, CPUMOSTIN, 10k at left side of cpu vrm
                            t.Add(new Temperature("Chipset", 5));     // AUXTIN2, 10k at back side of the chipset
                            t.Add(new Temperature("CPU", 23));
                            // Add Thermistor Sensors for voltage inputs that are marked ad
                            t.Add(new Temperature("MOS CPU", 24));  // (VIN 4 Voltage) NTC Near MOSFET CPU VRM
                            t.Add(new Temperature("PCH", 25));      // (Voltage #6) X570 Platform Control HUB TEMP (NTC On Bottom of PCB)

                            f.Add(new Fan("Pump Fan", 0));
                            f.Add(new Fan("CPU Fan", 1));
                            f.Add(new Fan("System Fan #1", 2));
                            f.Add(new Fan("System Fan #2", 3));
                            f.Add(new Fan("System Fan #3", 4));
                            f.Add(new Fan("System Fan #4", 5));
                            f.Add(new Fan("Chipset Fan", 6));

                            c.Add(new Control("Pump Fan", 0));
                            c.Add(new Control("CPU Fan", 1));
                            c.Add(new Control("System Fan #1", 2));
                            c.Add(new Control("System Fan #2", 3));
                            c.Add(new Control("System Fan #3", 4));
                            c.Add(new Control("System Fan #4", 5));
                            c.Add(new Control("Chipset Fan", 6));

                            break;

                        default:
                            v.Add(new Voltage("Vcore", 0));
                            v.Add(new Voltage("+5V", 1, 4, 1));
                            v.Add(new Voltage("AVCC", 2, 34, 34));
                            v.Add(new Voltage("+3.3V", 3, 34, 34));
                            v.Add(new Voltage("+12V", 4, 11, 1));
                            v.Add(new Voltage("Voltage #6", 5, true));
                            v.Add(new Voltage("Voltage #7", 6, true));
                            v.Add(new Voltage("+3V Standby", 7, 34, 34));
                            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                            v.Add(new Voltage("CPU Termination", 9));
                            v.Add(new Voltage("Voltage #11", 10, true));
                            v.Add(new Voltage("Voltage #12", 11, true));
                            v.Add(new Voltage("CPU NB/SoC", 12));
                            v.Add(new Voltage("VDIMM", 13, 1, 1));
                            v.Add(new Voltage("Voltage #15", 14, true));

                            t.Add(new Temperature("CPU Core", 0));
                            t.Add(new Temperature("Temperature #1", 1));
                            t.Add(new Temperature("System", 2));
                            t.Add(new Temperature("VRM MOS", 3));
                            t.Add(new Temperature("Temperature #4", 4));
                            t.Add(new Temperature("Chipset", 5));

                            for (int i = 0; i < superIO.Fans.Length; i++)
                                f.Add(new Fan("Fan #" + (i + 1), i));

                            for (int i = 0; i < superIO.Controls.Length; i++)
                                c.Add(new Control("Fan #" + (i + 1), i));

                            break;
                    }

                    break;

                default:
                    v.Add(new Voltage("Vcore", 0));
                    v.Add(new Voltage("Voltage #2", 1, true));
                    v.Add(new Voltage("AVCC", 2, 34, 34));
                    v.Add(new Voltage("+3.3V", 3, 34, 34));
                    v.Add(new Voltage("Voltage #5", 4, true));
                    v.Add(new Voltage("Voltage #6", 5, true));
                    v.Add(new Voltage("Voltage #7", 6, true));
                    v.Add(new Voltage("+3V Standby", 7, 34, 34));
                    v.Add(new Voltage("CMOS Battery", 8, 34, 34));
                    v.Add(new Voltage("CPU Termination", 9));
                    v.Add(new Voltage("Voltage #11", 10, true));
                    v.Add(new Voltage("Voltage #12", 11, true));
                    v.Add(new Voltage("Voltage #13", 12, true));
                    v.Add(new Voltage("Voltage #14", 13, true));
                    v.Add(new Voltage("Voltage #15", 14, true));
                    t.Add(new Temperature("CPU Core", 0));
                    t.Add(new Temperature("Temperature #1", 1));
                    t.Add(new Temperature("Temperature #2", 2));
                    t.Add(new Temperature("Temperature #3", 3));
                    t.Add(new Temperature("Temperature #4", 4));
                    t.Add(new Temperature("Temperature #5", 5));
                    t.Add(new Temperature("Temperature #6", 6));

                    for (int i = 0; i < superIO.Fans.Length; i++)
                        f.Add(new Fan("Fan #" + (i + 1), i));

                    for (int i = 0; i < superIO.Controls.Length; i++)
                        c.Add(new Control("Fan #" + (i + 1), i));

                    break;
            }
        }

        private static void GetWinbondConfigurationEhf(Manufacturer manufacturer, Model model, IList<Voltage> v, IList<Temperature> t, IList<Fan> f, IList<Control> c)
        {
            v.Add(new Voltage("Vcore", 0));
            v.Add(new Voltage("Voltage #2", 1, true));
            v.Add(new Voltage("AVCC", 2, 34, 34));
            v.Add(new Voltage("+3.3V", 3, 34, 34));
            v.Add(new Voltage("Voltage #5", 4, true));
            v.Add(new Voltage("Voltage #6", 5, true));
            v.Add(new Voltage("Voltage #7", 6, true));
            v.Add(new Voltage("+3V Standby", 7, 34, 34));
            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
            v.Add(new Voltage("Voltage #10", 9, true));
            t.Add(new Temperature("CPU", 0));
            t.Add(new Temperature("Auxiliary", 1));
            t.Add(new Temperature("System", 2));
            f.Add(new Fan("System Fan", 0));
            f.Add(new Fan("CPU Fan", 1));
            f.Add(new Fan("Auxiliary Fan", 2));
            f.Add(new Fan("CPU Fan #2", 3));
            f.Add(new Fan("Auxiliary Fan #2", 4));
            c.Add(new Control("System Fan", 0));
            c.Add(new Control("CPU Fan", 1));
            c.Add(new Control("Auxiliary Fan", 2));
        }

        private static void GetWinbondConfigurationHg(Manufacturer manufacturer, Model model, IList<Voltage> v, IList<Temperature> t, IList<Fan> f, IList<Control> c)
        {
            v.Add(new Voltage("Vcore", 0));
            v.Add(new Voltage("Voltage #2", 1, true));
            v.Add(new Voltage("AVCC", 2, 34, 34));
            v.Add(new Voltage("+3.3V", 3, 34, 34));
            v.Add(new Voltage("Voltage #5", 4, true));
            v.Add(new Voltage("Voltage #6", 5, true));
            v.Add(new Voltage("Voltage #7", 6, true));
            v.Add(new Voltage("+3V Standby", 7, 34, 34));
            v.Add(new Voltage("CMOS Battery", 8, 34, 34));
            t.Add(new Temperature("CPU", 0));
            t.Add(new Temperature("Auxiliary", 1));
            t.Add(new Temperature("System", 2));
            f.Add(new Fan("System Fan", 0));
            f.Add(new Fan("CPU Fan", 1));
            f.Add(new Fan("Auxiliary Fan", 2));
            f.Add(new Fan("CPU Fan #2", 3));
            f.Add(new Fan("Auxiliary Fan #2", 4));
            c.Add(new Control("System Fan", 0));
            c.Add(new Control("CPU Fan", 1));
            c.Add(new Control("Auxiliary Fan", 2));
        }

        public string GetReport()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(_superIO.GetReport());
            sb.AppendLine();
            sb.AppendLine("Temperature debug (SuperIO read):");

            try
            {
                // If there are named sensors created, iterate them; otherwise use superIO.Temperatures
                if (_temperatures != null && _temperatures.Count > 0)
                {
                    foreach (Sensor s in _temperatures)
                    {
                        int idx = s.Index;
                        float? v = _temperatures[idx]?.Value != null ? _temperatures[idx]?.Value : null;
                        sb.Append("  Index ").Append(idx).Append(" \"").Append(s.Name).Append("\": ");
                        sb.AppendLine(v.HasValue ? v.Value.ToString("F2", CultureInfo.InvariantCulture) : "null");
                    }
                }
                else
                {
                    // Fallback: dump raw superIO.Temperatures if available
                    var temps = (_superIO?.Temperatures);
                    if (temps != null)
                    {
                        for (int i = 0; i < temps.Length; i++)
                        {
                            sb.Append("  Raw Index ").Append(i).Append(": ");
                            sb.AppendLine(temps[i].HasValue ? temps[i].Value.ToString("F2", CultureInfo.InvariantCulture) : "null");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("  Exception while reading temperatures: " + ex.Message);
            }

            return sb.ToString();
        }

        public void Update()
        {
            _superIO.Update();

            foreach (Sensor sensor in _voltages)
            {
                float? value = _superIO.Voltages[sensor.Index];
                if (value.HasValue)
                {
                    sensor.Value = value + (value - sensor.Parameters[2].Value) * sensor.Parameters[0].Value / sensor.Parameters[1].Value;
                }
            }

            foreach (Sensor sensor in _temperatures)
            {
                float? value = _superIO.Temperatures[sensor.Index];
                if (value.HasValue)
                {
                    sensor.Value = value + sensor.Parameters[0].Value;
                }
                else
                {
                    if (_motherboardName == Model.X570_MS7C35) // Add Temp Value for CPU MOS TEMPERATURE & PCH TEMPERATURE
                    {
                        float? voltage = null;

                        if (sensor.Index == 24)
                        {
                            voltage = _superIO.Voltages[6];
                        }
                        else if (sensor.Index == 25)
                        {
                            voltage = _superIO.Voltages[11];
                        }

                        if (!voltage.HasValue || voltage.Value <= 0)
                            continue;

                        double R = (10000.0 * voltage.Value / (2.048 - 1.0));            //Convert voltage measured to resistance value
                        double T = ((298.15 * 3435.0) / ((298.15 * Math.Log(R / 10000.0)) + 3435.0));  // Use R value in steinhart and hart equation, calculate temperature value in kelvin
                        float Tc = (float)(T - 273.15);                            // Converting kelvin to celsius

                        sensor.Value = Tc + sensor.Parameters[0].Value;
                    }
                }
            }

            foreach (Sensor sensor in _fans)
            {
                float? value = _superIO.Fans[sensor.Index];
                if (value.HasValue)
                {
                    sensor.Value = value;
                }
            }
        }
    }
}

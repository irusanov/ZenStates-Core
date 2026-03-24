using System.Collections.Generic;

namespace ZenStates.Core
{
    internal static class EncodedValueDictionaries
    {
        // CPU On-Die Termination
        public static readonly Dictionary<int, string> ProcOdtDict = new Dictionary<int, string>
        {
            {0, "Hi-Z"},
            {1, "480.0 Ω"},
            {2, "240.0 Ω"},
            {3, "160.0 Ω"},
            {4, "120.0 Ω"},
            {5, "96.0 Ω"},
            {6, "80.0 Ω"},
            {7, "68.6 Ω"},
            {12, "60.0 Ω"},
            {13, "53.3 Ω"},
            {14, "48.0 Ω"},
            {15, "43.6 Ω"},
            {28, "40.0 Ω"},
            {29, "36.9 Ω"},
            {30, "34.3 Ω"},
            {31, "32.0 Ω"},
            {60, "30.0 Ω"},
            {61, "28.2 Ω"},
            {62, "26.7 Ω"},
            {63, "25.3 Ω"},
        };

        // Proc Data Drive Strength
        public static readonly Dictionary<int, string> ProcDataDrvStrenDict = new Dictionary<int, string>
        {
            {0, "Hi-Z"},
            {2, "240.0 Ω"},
            {4, "120.0 Ω"},
            {6, "80.0 Ω"},
            {12, "60.0 Ω"},
            {14, "48.0 Ω"},
            {28, "40.0 Ω"},
            {30, "34.3 Ω"},
        };

        // DRAM Data Drive Strength
        public static readonly Dictionary<int, string> DramDataDrvStrenDict = new Dictionary<int, string>
        {
            {0, "34.0 Ω"},
            {1, "40.0 Ω"},
            {2, "48.0 Ω"},
        };

        // Cad Bus Drive Strength
        public static readonly Dictionary<int, string> CadBusDrvStrenDict = new Dictionary<int, string>
        {
            {0, "Hi-Z" },
            {30, "30.0 Ω"},
            {40, "40.0 Ω"},
            {60, "60.0 Ω"},
            {120, "120.0 Ω"},
        };

        // Proc CA ODT impedance
        // Proc CK ODT impedance
        // Proc DQ ODT impedance
        // Proc DQS ODT impedance
        public static readonly Dictionary<int, string> ProcOdtImpedanceDict = new Dictionary<int, string>
        {
            {0, "Off" },
            {30, "30.0 Ω"},
            {40, "40.0 Ω"},
            {60, "60.0 Ω"},
            {120, "120.0 Ω"},
        };

        // RttNom, RttPark
        public static readonly Dictionary<int, string> RttDict = new Dictionary<int, string>
        {
            {0, "Off"},
            {1, "RZQ/1"},
            {2, "RZQ/2"},
            {3, "RZQ/3"},
            {4, "RZQ/4"},
            {5, "RZQ/5"},
            {6, "RZQ/6"},
            {7, "RZQ/7"},
        };

        public static readonly Dictionary<int, string> GroupOdtImpedanceDict = new Dictionary<int, string>
        {
            {0, "Off"},
            {1, "480.0 Ω"},
            {2, "240.0 Ω"},
            {3, "120.0 Ω"},
            {4, "80.0 Ω"},
            {5, "60.0 Ω"},
            {6, "48.0 Ω"},
            {7, "40.0 Ω"},
        };
    }
}
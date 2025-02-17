using System.Collections.Generic;

namespace ZenStates.Core
{
    internal sealed class AodDictionaries
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

        // Proc Data Drive Strength
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

        public static readonly Dictionary<string, int> AodDataDictionaryV1 = new Dictionary<string, int>
        {
            { "SMTEn", 8920 },
            { "MemClk", 8924 },
            { "Tcl", 8928 },
            { "Trcd", 8932 },
            { "Trp", 8936 },
            { "Tras", 8940 },
            { "Trc", 8944 },
            { "Twr", 8948 },
            { "Trfc", 8952 },
            { "Trfc2", 8956 },
            { "Trfcsb", 8960 },
            { "Trtp", 8964 },
            { "TrrdL", 8968 },
            { "TrrdS", 8972 },
            { "Tfaw", 8976 },
            { "TwtrL", 8980 },
            { "TwtrS", 8984 },
            { "TrdrdScL", 8988 },
            { "TrdrdSc", 8992 },
            { "TrdrdSd", 8996 },
            { "TrdrdDd", 9000 },
            { "TwrwrScL", 9004 },
            { "TwrwrSc", 9008 },
            { "TwrwrSd", 9012 },
            { "TwrwrDd", 9016 },
            { "Twrrd", 9020 },
            { "Trdwr", 9024 },
            { "CadBusDrvStren", 9028 },
            { "ProcDataDrvStren", 9032 },
            { "ProcOdt", 9036 },
            { "DramDataDrvStren", 9040 },
            { "RttNomWr", 9044 },
            { "RttNomRd", 9048 },
            { "RttWr", 9052 },
            { "RttPark", 9056 },
            { "RttParkDqs", 9060 },
            { "MemVddio", 9096 },
            { "MemVddq", 9100 },
            { "MemVpp", 9104 },
            { "ApuVddio", 9108 }
        };

        public static readonly Dictionary<string, int> AodDataDictionaryV2 = new Dictionary<string, int>
        {
            { "SMTEn", 8916 },
            { "MemClk", 8920 },
            { "Tcl", 8924 },
            { "Trcd", 8928 },
            { "Trp", 8932 },
            { "Tras", 8936 },
            { "Trc", 8940 },
            { "Twr", 8944 },
            { "Trfc", 8948 },
            { "Trfc2", 8952 },
            { "Trfcsb", 8956 },
            { "Trtp", 8960 },
            { "TrrdL", 8964 },
            { "TrrdS", 8968 },
            { "Tfaw", 8972 },
            { "TwtrL", 8976 },
            { "TwtrS", 8980 },
            { "TrdrdScL", 8984 },
            { "TrdrdSc", 8988 },
            { "TrdrdSd", 8992 },
            { "TrdrdDd", 9096 },
            { "TwrwrScL", 9000 },
            { "TwrwrSc", 9004 },
            { "TwrwrSd", 9008 },
            { "TwrwrDd", 9012 },
            { "Twrrd", 9016 },
            { "Trdwr", 9020 },
            { "CadBusDrvStren", 9024 },
            { "ProcDataDrvStren", 9028 },
            { "ProcOdt", 9032 },
            { "DramDataDrvStren", 9036 },
            { "RttNomWr", 9040 },
            { "RttNomRd", 9044 },
            { "RttWr", 9048 },
            { "RttPark", 9052 },
            { "RttParkDqs", 9056 },
            { "MemVddio", 9092 },
            { "MemVddq", 9096 },
            { "MemVpp", 9100 },
            { "ApuVddio", 9104 }
        };

        public static readonly Dictionary<string, int> AodDataDictionaryV3 = new Dictionary<string, int>
        {
            { "SMTEn", 8968 },
            { "MemClk", 8976 },
            { "Tcl", 8988 },
            { "TrcdWr", 8992 },
            { "TrcdRd", 8996 },
            { "Trp", 9000 },
            { "Trfc", 9004 },
            { "Tras", 9008 },
            { "Trc", 9012 },
            { "TrrdS", 9016 },
            { "TrrdL", 9020 },
            { "Tfaw", 9024 },
            { "TwtrL", 9028 },
            { "TwtrS", 9032 },
            { "Twr", 9036 },
            { "TrdrdScL", 9040 },
            { "TwrwrScL", 9044 },
            { "Trtp", 9048 },
            { "Tcke", 9052 },
            { "TrdrdSc", 9056 },
            { "TrdrdSd", 9060 },
            { "TrdrdDd", 9064 },
            { "TwrwrSc", 9068 },
            { "TwrwrSd", 9072 },
            { "TwrwrDd", 9076 },
            { "Twrrd", 9080 },
            { "Trdwr", 9084 },
            { "CadBusDrvStren", 9088 },
            { "ProcDataDrvStren", 9092 },
            { "ProcOdt", 9096 },
            { "DramDataDrvStren", 9100 },
            { "RttNomWr", 9104 },
            { "RttNomRd", 9108 },
            { "RttWr", 9112 },
            { "RttPark", 9116 },
            { "RttParkDqs", 9120 },
            { "MemVddio", 9156 },
            { "MemVddq", 9160 },
            { "MemVpp", 9164 },
            { "ApuVddio", 9168 }
        };

        // Phoenix, HawkPoint
        public static readonly Dictionary<string, int> AodDataDictionaryV4 = new Dictionary<string, int>
        {
            { "SMTEn", 8916 },
            { "MemClk", 8920 },
            { "Tcl", 8924 },
            { "Trcd", 8928 },
            { "Trp", 8932 },
            { "Tras", 8936 },
            { "Trc", 8940 },
            { "Twr", 8944 },
            { "Trfc", 8948 },
            { "Trfc2", 8952 },
            { "Trfcsb", 8956 },
            { "Trtp", 8960 },
            { "TrrdL", 8964 },
            { "TrrdS", 8968 },
            { "Tfaw", 8972 },
            { "TwtrL", 8976 },
            { "TwtrS", 8980 },
            { "TrdrdScL", 8984 },
            { "TrdrdSc", 8988 },
            { "TrdrdSd", 8992 },
            { "TrdrdDd", 8996 },
            { "TwrwrScL", 9000 },
            { "TwrwrSc", 9004 },
            { "TwrwrSd", 9008 },
            { "TwrwrDd", 9012 },
            { "Twrrd", 9016 },
            { "Trdwr", 9020 },
            { "CadBusDrvStren", 9024 },
            { "ProcDataDrvStren", 9028 },
            { "ProcCaOdt", 9032 },
            { "ProcCkOdt", 9036 },
            { "ProcDqOdt", 9040 },
            { "ProcDqsOdt", 9044 },
            { "DramDataDrvStren", 9048 },
            { "RttNomWr", 9052 },
            { "RttNomRd", 9056 },
            { "RttWr", 9060 },
            { "RttPark", 9064 },
            { "RttParkDqs", 9068 },
            { "MemVddio", 9116 },
            { "MemVddq", 9120 },
            { "MemVpp", 9124 },
            { "ApuVddio", 9128 }
        };

        // GraniteRidge

        public static readonly Dictionary<string, int> BaseAodDataDictionary_1Ah = new Dictionary<string, int>
        {
            { "SMTEn", 8920 },
            { "MemClk", 8924 },
            { "Tcl", 8928 },
            { "Trcd", 8932 },
            { "Trp", 8936 },
            { "Tras", 8940 },
            { "Trc", 8944 },
            { "Twr", 8948 },
            { "Trfc", 8952 },
            { "Trfc2", 8956 },
            { "Trfcsb", 8960 },
            { "Trtp", 8964 },
            { "TrrdL", 8968 },
            { "TrrdS", 8972 },
            { "Tfaw", 8976 },
            { "TwtrL", 8980 },
            { "TwtrS", 8984 },
            { "TrdrdScL", 8988 },
            { "TrdrdSc", 8992 },
            { "TrdrdSd", 8996 },
            { "TrdrdDd", 9000 },
            { "TwrwrScL", 9004 },
            { "TwrwrSc", 9008 },
            { "TwrwrSd", 9012 },
            { "TwrwrDd", 9016 },
            { "Twrrd", 9020 },
            { "Trdwr", 9024 },
            { "CadBusDrvStren", 9028 },
        };

        public static readonly Dictionary<string, int> AodDataDictionary_1Ah = new Dictionary<string, int>(BaseAodDataDictionary_1Ah)
        {
            { "ProcDataDrvStren", 9032 },

            { "RttNomWr", 9036 },
            { "RttNomRd", 9040 },
            { "RttWr", 9044 },
            { "RttPark", 9048 },
            { "RttParkDqs", 9052 },

            { "MemVddio", 9088 },
            { "MemVddq", 9092 },
            { "MemVpp", 9096 },
            { "ApuVddio", 9100 },

            { "ProcOdt", 9164 },
            { "ProcOdtPullUp", 9164 },
            { "ProcOdtPullDown", 9168 },
            { "DramDataDrvStren", 9172 }
        };

        public static readonly Dictionary<string, int> AodDataDictionary_1Ah_B404023 = new Dictionary<string, int>(BaseAodDataDictionary_1Ah)
        {
            { "RttNomWr", 9032 },
            { "RttNomRd", 9036 },
            { "RttWr", 9040 },
            { "RttPark", 9044 },
            { "RttParkDqs", 9048 },

            { "MemVddio", 9084 },
            { "MemVddq", 9088 },
            { "MemVpp", 9092 },
            { "ApuVddio", 9096 },

            { "ProcOdt", 9172 },
            { "ProcOdtPullUp", 9172 },
            { "ProcOdtPullDown", 9176 },
            { "DramDataDrvStren", 9180 },
            { "ProcDataDrvStren", 9196 },
        };

        // M-Die 24GB SR sticks
        public static readonly Dictionary<string, int> AodDataDictionary_1Ah_M = new Dictionary<string, int>(BaseAodDataDictionary_1Ah)
        {
            { "ProcDataDrvStren", 9032 },

            { "RttNomWr", 9036 },
            { "RttNomRd", 9040 },
            { "RttWr", 9044 },
            { "RttPark", 9048 },
            { "RttParkDqs", 9052 },

            { "MemVddio", 9088 },
            { "MemVddq", 9092 },
            { "MemVpp", 9096 },
            { "ApuVddio", 9100 },

            { "ProcOdt", 9176 },
            { "ProcOdtPullUp", 9176 },
            { "ProcOdtPullDown", 9180 },
            { "DramDataDrvStren", 9184 },
        };

        public static readonly Dictionary<string, int> AodDataDictionary_1Ah_B404023_M = new Dictionary<string, int>(BaseAodDataDictionary_1Ah)
        {
            { "RttNomWr", 9032 },
            { "RttNomRd", 9036 },
            { "RttWr", 9040 },
            { "RttPark", 9044 },
            { "RttParkDqs", 9048 },

            { "MemVddio", 9084 },
            { "MemVddq", 9088 },
            { "MemVpp", 9092 },
            { "ApuVddio", 9096 },

            { "ProcOdt", 9160 },
            { "ProcOdtPullUp", 9160 },
            { "ProcOdtPullDown", 9164 },
            { "DramDataDrvStren", 9168 },
            { "ProcDataDrvStren", 9176 }
        };
    }
}
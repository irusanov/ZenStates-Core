﻿using System;
using System.Collections.Generic;
using System.Text;

namespace ZenStates.Core
{
    internal sealed class AodDictionaries
    {
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

        public static readonly Dictionary<int, string> ProcDataDrvStrenDict = new Dictionary<int, string>
        {
            {2, "240.0 Ω"},
            {4, "120.0 Ω"},
            {6, "80.0 Ω"},
            {12, "60.0 Ω"},
            {14, "48.0 Ω"},
            {28, "40.0 Ω"},
            {30, "34.3 Ω"},
        };

        public static readonly Dictionary<int, string> DramDataDrvStrenDict = new Dictionary<int, string>
        {
            {0, "34.0 Ω"},
            {1, "40.0 Ω"},
            {2, "48.0 Ω"},
        };

        public static readonly Dictionary<int, string> CadBusDrvStrenDict = new Dictionary<int, string>
        {
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

        public static readonly Dictionary<string, int> AodDataDefaultDictionary = new Dictionary<string, int>
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
            { "ProcODT", 9036 },
            { "DramDataDrvStren", 9040 },
            { "RttNomWr", 9044 },
            { "RttNomRd", 9048 },
            { "RttWr", 9052 },
            { "RttPark", 9056 },
            { "RttParkDqs", 9060 },
            { "MemVddio", 9096 },
            { "MemVddq", 9100 },
            { "MemVpp", 9104 }
        };

        public static readonly Dictionary<string, int> AodDataNewDictionary = new Dictionary<string, int>
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
            { "ProcODT", 9032 },
            { "DramDataDrvStren", 9036 },
            { "RttNomWr", 9040 },
            { "RttNomRd", 9044 },
            { "RttWr", 9048 },
            { "RttPark", 9052 },
            { "RttParkDqs", 9056 },
            { "MemVddio", 9092 },
            { "MemVddq", 9096 },
            { "MemVpp", 9100 }
        };
    }
}

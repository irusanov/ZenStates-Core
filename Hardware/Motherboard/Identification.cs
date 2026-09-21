// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.
// Adаpted from LibreHardwareMonitor

using System;

namespace ZenStates.Core.Hardware.Motherboard
{
    internal class Identification
    {
        public static Manufacturer GetManufacturer(string name)
        {
            switch (name)
            {
                case var _ when name.IndexOf("abit.com.tw", StringComparison.OrdinalIgnoreCase) > -1:
                    return Manufacturer.Acer;
                case var _ when name.StartsWith("Acer", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Acer;
                case var _ when name.StartsWith("AMD", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.AMD;
                case var _ when name.Equals("Alienware", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Alienware;
                case var _ when name.StartsWith("AOpen", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.AOpen;
                case var _ when name.StartsWith("Apple", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Apple;
                case var _ when name.Equals("ASRock", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.ASRock;
                case var _ when name.StartsWith("ASUSTeK", StringComparison.OrdinalIgnoreCase):
                case var _ when name.StartsWith("ASUS ", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.ASUS;
                case var _ when name.StartsWith("Biostar", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Biostar;
                case var _ when name.StartsWith("Clevo", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Clevo;
                case var _ when name.StartsWith("Dell", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Dell;
                case var _ when name.Equals("DFI", StringComparison.OrdinalIgnoreCase):
                case var _ when name.StartsWith("DFI Inc", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.DFI;
                case var _ when name.Equals("ECS", StringComparison.OrdinalIgnoreCase):
                case var _ when name.StartsWith("ELITEGROUP", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.ECS;
                case var _ when name.Equals("EPoX COMPUTER CO., LTD", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.EPoX;
                case var _ when name.StartsWith("EVGA", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.EVGA;
                case var _ when name.Equals("FIC", StringComparison.OrdinalIgnoreCase):
                case var _ when name.StartsWith("First International Computer", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.FIC;
                case var _ when name.Equals("Foxconn", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Foxconn;
                case var _ when name.StartsWith("Framework", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Framework;
                case var _ when name.StartsWith("Fujitsu", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Fujitsu;
                case var _ when name.StartsWith("Gigabyte", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Gigabyte;
                case var _ when name.StartsWith("Hewlett-Packard", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("HP", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.HP;
                case var _ when name.Equals("IBM", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.IBM;
                case var _ when name.Equals("Intel", StringComparison.OrdinalIgnoreCase):
                case var _ when name.StartsWith("Intel Corp", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Intel;
                case var _ when name.StartsWith("Jetway", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Jetway;
                case var _ when name.StartsWith("Lenovo", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Lenovo;
                case var _ when name.Equals("LattePanda", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.LattePanda;
                case var _ when name.StartsWith("Medion", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Medion;
                case var _ when name.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Microsoft;
                case var _ when name.StartsWith("Micro-Star International", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("MSI", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.MSI;
                case var _ when name.StartsWith("NEC ", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("NEC", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.NEC;
                case var _ when name.StartsWith("Pegatron", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Pegatron;
                case var _ when name.StartsWith("Samsung", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Samsung;
                case var _ when name.StartsWith("Sapphire", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Sapphire;
                case var _ when name.StartsWith("Shuttle", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Shuttle;
                case var _ when name.StartsWith("Sony", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Sony;
                case var _ when name.StartsWith("Supermicro", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Supermicro;
                case var _ when name.StartsWith("Toshiba", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Toshiba;
                case var _ when name.Equals("XFX", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.XFX;
                case var _ when name.StartsWith("Zotac", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Zotac;
                case var _ when name.Equals("To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase):
                    return Manufacturer.Unknown;
                default:
                    return Manufacturer.Unknown;
            }
        }

        public static Model GetModel(string name)
        {
            switch (name)
            {
                case var _ when name.Equals("TUF GAMING B850-BTF WIFI W", StringComparison.OrdinalIgnoreCase):
                    return Model.TUF_GAMING_B850_BTF_WIFI_W;
                case var _ when name.Equals("TUF GAMING B850M-PLUS II", StringComparison.OrdinalIgnoreCase):
                    return Model.TUF_GAMING_B850M_PLUS_II;
                case var _ when name.Equals("TUF GAMING X870-PRO WIFI7 W NEO", StringComparison.OrdinalIgnoreCase):
                    return Model.TUF_GAMING_X870_PRO_WIFI7_W_NEO;
                case var _ when name.Equals("X870 AORUS ELITE WIFI7", StringComparison.OrdinalIgnoreCase):
                    return Model.X870_AORUS_ELITE_WIFI7;
                case var _ when name.Equals("X870 AORUS ELITE WIFI7 ICE", StringComparison.OrdinalIgnoreCase):
                    return Model.X870_AORUS_ELITE_WIFI7_ICE;
                case var _ when name.Equals("AB350 Pro4", StringComparison.OrdinalIgnoreCase):
                    return Model.AB350_Pro4;
                case var _ when name.Equals("AB350M Pro4", StringComparison.OrdinalIgnoreCase):
                    return Model.AB350M_Pro4;
                case var _ when name.Equals("AB350M", StringComparison.OrdinalIgnoreCase):
                    return Model.AB350M;
                case var _ when name.Equals("B450 Steel Legend", StringComparison.OrdinalIgnoreCase):
                    return Model.B450_Steel_Legend;
                case var _ when name.Equals("B450M Steel Legend", StringComparison.OrdinalIgnoreCase):
                    return Model.B450M_Steel_Legend;
                case var _ when name.Equals("B450 Pro4", StringComparison.OrdinalIgnoreCase):
                    return Model.B450_Pro4;
                case var _ when name.Equals("B450M Pro4", StringComparison.OrdinalIgnoreCase):
                    return Model.B450M_Pro4;
                case var _ when name.Equals("B450M Pro4 R2.0", StringComparison.OrdinalIgnoreCase):
                    return Model.B450M_Pro4_R2_0;
                case var _ when name.Equals("B550M Pro4", StringComparison.OrdinalIgnoreCase):
                    return Model.B550M_Pro4;
                case var _ when name.Equals("Fatal1ty AB350 Gaming K4", StringComparison.OrdinalIgnoreCase):
                    return Model.Fatal1ty_AB350_Gaming_K4;
                case var _ when name.Equals("AB350M-HDV", StringComparison.OrdinalIgnoreCase):
                    return Model.AB350M_HDV;
                case var _ when name.Equals("A320M-HDV", StringComparison.OrdinalIgnoreCase):
                    return Model.A320M_HDV;
                case var _ when name.Equals("ROG CROSSHAIR VIII HERO", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_CROSSHAIR_VIII_HERO;
                case var _ when name.Equals("ROG CROSSHAIR VIII HERO (WI-FI)", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_CROSSHAIR_VIII_HERO_WIFI;
                case var _ when name.Equals("ROG CROSSHAIR VIII DARK HERO", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_CROSSHAIR_VIII_DARK_HERO;
                case var _ when name.Equals("ROG CROSSHAIR X870E HERO BTF", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_CROSSHAIR_X870E_HERO_BTF;
                case var _ when name.Equals("ROG CROSSHAIR VIII FORMULA", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_CROSSHAIR_VIII_FORMULA;
                case var _ when name.Equals("ROG CROSSHAIR VIII IMPACT", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_CROSSHAIR_VIII_IMPACT;
                case var _ when name.Equals("PRIME B650-PLUS", StringComparison.OrdinalIgnoreCase):
                    return Model.PRIME_B650_PLUS;
                case var _ when name.Equals("ROG CROSSHAIR X670E EXTREME", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_CROSSHAIR_X670E_EXTREME;
                case var _ when name.Equals("ROG CROSSHAIR X670E HERO", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_CROSSHAIR_X670E_HERO;
                case var _ when name.Equals("ROG CROSSHAIR X670E GENE", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_CROSSHAIR_X670E_GENE;
                case var _ when name.Equals("PROART X670E-CREATOR WIFI", StringComparison.OrdinalIgnoreCase):
                    return Model.PROART_X670E_CREATOR_WIFI;
                case var _ when name.Equals("ROG STRIX B550-F GAMING (WI-FI)", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_STRIX_B550_F_GAMING_WIFI;
                case var _ when name.Equals("ROG STRIX X470-I GAMING", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_STRIX_X470_I;
                case var _ when name.Equals("ROG STRIX B550-E GAMING", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_STRIX_B550_E_GAMING;
                case var _ when name.Equals("ROG STRIX B550-I GAMING", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_STRIX_B550_I_GAMING;
                case var _ when name.Equals("ROG STRIX X570-E GAMING", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_STRIX_X570_E_GAMING;
                case var _ when name.Equals("ROG STRIX X570-E GAMING WIFI II", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_STRIX_X570_E_GAMING_WIFI_II;
                case var _ when name.Equals("ROG STRIX X570-I GAMING", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_STRIX_X570_I_GAMING;
                case var _ when name.Equals("ROG STRIX X570-F GAMING", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_STRIX_X570_F_GAMING;
                case var _ when name.Equals("AX370-Gaming K7", StringComparison.OrdinalIgnoreCase):
                    return Model.AX370_Gaming_K7;
                case var _ when name.Equals("PRIME X370-PRO", StringComparison.OrdinalIgnoreCase):
                    return Model.PRIME_X370_PRO;
                case var _ when name.Equals("PRIME X470-PRO", StringComparison.OrdinalIgnoreCase):
                    return Model.PRIME_X470_PRO;
                case var _ when name.Equals("PRIME X570-PRO", StringComparison.OrdinalIgnoreCase):
                    return Model.PRIME_X570_PRO;
                case var _ when name.Equals("ProArt X570-CREATOR WIFI", StringComparison.OrdinalIgnoreCase):
                    return Model.PROART_X570_CREATOR_WIFI;
                case var _ when name.Equals("Pro WS X570-ACE", StringComparison.OrdinalIgnoreCase):
                    return Model.PRO_WS_X570_ACE;
                case var _ when name.Equals("AB350-Gaming 3-CF", StringComparison.OrdinalIgnoreCase):
                    return Model.AB350_Gaming_3;
                case var _ when name.Equals("ROG ZENITH EXTREME", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_ZENITH_EXTREME;
                case var _ when name.Equals("ROG ZENITH II EXTREME", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_ZENITH_II_EXTREME;
                case var _ when name.Equals("X570 Pro4", StringComparison.OrdinalIgnoreCase):
                    return Model.X570_Pro4;
                case var _ when name.Equals("X570 Taichi", StringComparison.OrdinalIgnoreCase):
                    return Model.X570_Taichi;
                case var _ when name.Equals("X570 Phantom Gaming-ITX/TB3", StringComparison.OrdinalIgnoreCase):
                    return Model.X570_Phantom_Gaming_ITX;
                case var _ when name.Equals("X570 Phantom Gaming 4", StringComparison.OrdinalIgnoreCase):
                    return Model.X570_Phantom_Gaming_4;
                case var _ when name.Equals("AX370-Gaming 5", StringComparison.OrdinalIgnoreCase):
                    return Model.AX370_Gaming_5;
                case var _ when name.Equals("TUF X470-PLUS GAMING", StringComparison.OrdinalIgnoreCase):
                    return Model.TUF_X470_PLUS_GAMING;
                case var _ when name.Equals("TUF GAMING X870-PLUS WIFI", StringComparison.OrdinalIgnoreCase):
                    return Model.TUF_GAMING_X870_PLUS_WIFI;
                case var _ when name.Equals("B360M PRO-VDH (MS-7B24)", StringComparison.OrdinalIgnoreCase):
                    return Model.B360M_PRO_VDH;
                case var _ when name.Equals("A320M-S2H-CF", StringComparison.OrdinalIgnoreCase):
                    return Model.A320M_S2H_CF;
                case var _ when name.Equals("B550-A PRO (MS-7C56)", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("PRO B550-VC (MS-7C56)", StringComparison.OrdinalIgnoreCase):
                    return Model.B550A_PRO;
                case var _ when name.Equals("B450-A PRO (MS-7B86)", StringComparison.OrdinalIgnoreCase):
                    return Model.B450A_PRO;
                case var _ when name.Equals("B350 GAMING PLUS (MS-7A34)", StringComparison.OrdinalIgnoreCase):
                    return Model.B350_Gaming_Plus;
                case var _ when name.Equals("B450 AORUS PRO", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B450 AORUS PRO WIFI", StringComparison.OrdinalIgnoreCase):
                    return Model.B450_AORUS_PRO;
                case var _ when name.Equals("B450 GAMING X", StringComparison.OrdinalIgnoreCase):
                    return Model.B450_GAMING_X;
                case var _ when name.Equals("B450 AORUS ELITE", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B450 AORUS ELITE V2", StringComparison.OrdinalIgnoreCase):
                    return Model.B450_AORUS_ELITE;
                case var _ when name.Equals("B450M AORUS ELITE", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B450M AORUS ELITE-CF", StringComparison.OrdinalIgnoreCase):
                    return Model.B450M_AORUS_ELITE;
                case var _ when name.Equals("B450M GAMING", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B450M GAMING-CF", StringComparison.OrdinalIgnoreCase):
                    return Model.B450M_GAMING;
                case var _ when name.Equals("B450M AORUS M", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B450M AORUS M-CF", StringComparison.OrdinalIgnoreCase):
                    return Model.B450_AORUS_M;
                case var _ when name.Equals("B450M DS3H", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B450M DS3H WIFI", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B450M DS3H-CF", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B450M DS3H WIFI-CF", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B450M DS3H V2", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B450M DS3H V2-CF", StringComparison.OrdinalIgnoreCase):
                    return Model.B450M_DS3H;
                case var _ when name.Equals("B450M S2H", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B450M S2H V2", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B450M S2H-CF", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B450M S2H V2-CF", StringComparison.OrdinalIgnoreCase):
                    return Model.B450M_S2H;
                case var _ when name.Equals("B450M H", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B450M H-CF", StringComparison.OrdinalIgnoreCase):
                    return Model.B450M_H;
                case var _ when name.Equals("B450M K", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B450M K-CF", StringComparison.OrdinalIgnoreCase):
                    return Model.B450M_K;
                case var _ when name.Equals("B450M I AORUS PRO WIFI", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B450M I AORUS PRO WIFI-CF", StringComparison.OrdinalIgnoreCase):
                    return Model.B450_I_AORUS_PRO_WIFI;
                case var _ when name.Equals("X470 AORUS GAMING 7 WIFI-CF", StringComparison.OrdinalIgnoreCase):
                    return Model.X470_AORUS_GAMING_7_WIFI;
                case var _ when name.Equals("X570 AORUS MASTER", StringComparison.OrdinalIgnoreCase):
                    return Model.X570_AORUS_MASTER;
                case var _ when name.Equals("X570 AORUS PRO", StringComparison.OrdinalIgnoreCase):
                    return Model.X570_AORUS_PRO;
                case var _ when name.Equals("X570 AORUS ULTRA", StringComparison.OrdinalIgnoreCase):
                    return Model.X570_AORUS_ULTRA;
                case var _ when name.Equals("X570 GAMING X", StringComparison.OrdinalIgnoreCase):
                    return Model.X570_GAMING_X;
                case var _ when name.Equals("TUF GAMING X570-PLUS (WI-FI)", StringComparison.OrdinalIgnoreCase):
                    return Model.TUF_GAMING_X570_PLUS_WIFI;
                case var _ when name.Equals("TUF GAMING B550M-PLUS (WI-FI)", StringComparison.OrdinalIgnoreCase):
                    return Model.TUF_GAMING_B550M_PLUS_WIFI;
                case var _ when name.Equals("B550I AORUS PRO AX", StringComparison.OrdinalIgnoreCase):
                    return Model.B550I_AORUS_PRO_AX;
                case var _ when name.Equals("B550M AORUS PRO", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B550M AORUS PRO-P", StringComparison.OrdinalIgnoreCase):
                    return Model.B550M_AORUS_PRO;
                case var _ when name.Equals("B550M AORUS PRO AX", StringComparison.OrdinalIgnoreCase):
                    return Model.B550M_AORUS_PRO_AX;
                case var _ when name.Equals("B550M AORUS ELITE", StringComparison.OrdinalIgnoreCase):
                    return Model.B550M_AORUS_ELITE;
                case var _ when name.Equals("B550M GAMING", StringComparison.OrdinalIgnoreCase):
                    return Model.B550M_GAMING;
                case var _ when name.Equals("B550M DS3H", StringComparison.OrdinalIgnoreCase):
                    return Model.B550M_DS3H;
                case var _ when name.Equals("B550M DS3H AC", StringComparison.OrdinalIgnoreCase):
                    return Model.B550M_DS3H_AC;
                case var _ when name.Equals("B550M S2H", StringComparison.OrdinalIgnoreCase):
                    return Model.B550M_S2H;
                case var _ when name.Equals("B550M H", StringComparison.OrdinalIgnoreCase):
                    return Model.B550M_H;
                case var _ when name.Equals("B550 AORUS MASTER", StringComparison.OrdinalIgnoreCase):
                    return Model.B550_AORUS_MASTER;
                case var _ when name.Equals("B550 AORUS PRO", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B550 AORUS PRO V2", StringComparison.OrdinalIgnoreCase):
                    return Model.B550_AORUS_PRO;
                case var _ when name.Equals("B550 AORUS PRO AC", StringComparison.OrdinalIgnoreCase):
                    return Model.B550_AORUS_PRO_AC;
                case var _ when name.Equals("B550 AORUS PRO AX", StringComparison.OrdinalIgnoreCase):
                    return Model.B550_AORUS_PRO_AX;
                case var _ when name.Equals("B550 VISION D", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B550 VISION D-P", StringComparison.OrdinalIgnoreCase):
                    return Model.B550_VISION_D;
                case var _ when name.Equals("B550 AORUS ELITE", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B550 AORUS ELITE V2", StringComparison.OrdinalIgnoreCase):
                    return Model.B550_AORUS_ELITE;
                case var _ when name.Equals("B550 AORUS ELITE AX", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B550 AORUS ELITE AX V2", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B550 AORUS ELITE AX V3", StringComparison.OrdinalIgnoreCase):
                    return Model.B550_AORUS_ELITE_AX;
                case var _ when name.Equals("B550 GAMING X", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B550 GAMING X V2", StringComparison.OrdinalIgnoreCase):
                    return Model.B550_GAMING_X;
                case var _ when name.Equals("B550 UD AC", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B550 UD AC-Y1", StringComparison.OrdinalIgnoreCase):
                    return Model.B550_UD_AC;
                case var _ when name.Equals("B650 AORUS ELITE", StringComparison.OrdinalIgnoreCase):
                    return Model.B650_AORUS_ELITE;
                case var _ when name.Equals("B650 EAGLE AX", StringComparison.OrdinalIgnoreCase):
                    return Model.B650_EAGLE_AX;
                case var _ when name.Equals("B650 AORUS ELITE AX", StringComparison.OrdinalIgnoreCase):
                    return Model.B650_AORUS_ELITE_AX;
                case var _ when name.Equals("B650 AORUS ELITE V2", StringComparison.OrdinalIgnoreCase):
                    return Model.B650_AORUS_ELITE_V2;
                case var _ when name.Equals("B650 AORUS ELITE AX V2", StringComparison.OrdinalIgnoreCase):
                    return Model.B650_AORUS_ELITE_AX_V2;
                case var _ when name.Equals("B650 AORUS ELITE AX ICE", StringComparison.OrdinalIgnoreCase):
                    return Model.B650_AORUS_ELITE_AX_ICE;
                case var _ when name.Equals("B650 GAMING X AX", StringComparison.OrdinalIgnoreCase):
                    return Model.B650_GAMING_X_AX;
                case var _ when name.Equals("B650E AORUS ELITE AX ICE", StringComparison.OrdinalIgnoreCase):
                    return Model.B650E_AORUS_ELITE_AX_ICE;
                case var _ when name.Equals("B650M AORUS PRO", StringComparison.OrdinalIgnoreCase):
                    return Model.B650M_AORUS_PRO;
                case var _ when name.Equals("B650M AORUS PRO AX", StringComparison.OrdinalIgnoreCase):
                    return Model.B650M_AORUS_PRO_AX;
                case var _ when name.Equals("B650M AORUS ELITE", StringComparison.OrdinalIgnoreCase):
                    return Model.B650M_AORUS_ELITE;
                case var _ when name.Equals("B650M AORUS ELITE AX", StringComparison.OrdinalIgnoreCase):
                    return Model.B650M_AORUS_ELITE_AX;
                case var _ when name.Equals("B650I AX", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("A620I AX", StringComparison.OrdinalIgnoreCase):
                    return Model.B650I_AX;
                case var _ when name.Equals("ROG STRIX X670E-A GAMING WIFI", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_STRIX_X670E_A_GAMING_WIFI;
                case var _ when name.Equals("ROG STRIX X670E-E GAMING WIFI", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_STRIX_X670E_E_GAMING_WIFI;
                case var _ when name.Equals("ROG STRIX X670E-F GAMING WIFI", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_STRIX_X670E_F_GAMING_WIFI;
                case var _ when name.Equals("ROG STRIX B850-A GAMING WIFI", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_STRIX_B850_A_GAMING_WIFI;
                case var _ when name.Equals("ROG STRIX B850-E GAMING WIFI", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_STRIX_B850_E_GAMING_WIFI;
                case var _ when name.Equals("ROG STRIX B850-I GAMING WIFI", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_STRIX_B850_I_GAMING_WIFI;
                case var _ when name.Equals("ROG STRIX X870E-E GAMING WIFI", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_STRIX_X870E_E_GAMING_WIFI;
                case var _ when name.Equals("B660GTN", StringComparison.OrdinalIgnoreCase):
                    return Model.B660GTN;
                case var _ when name.Equals("X670E VALKYRIE", StringComparison.OrdinalIgnoreCase):
                    return Model.X670E_Valkyrie;
                case var _ when name.Equals("B650M-C", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B650M-CW", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B650M-CX", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("B650M-CWX", StringComparison.OrdinalIgnoreCase):
                    return Model.B650M_C;
                case var _ when name.Equals("B650M GAMING PLUS WIFI (MS-7E24)", StringComparison.OrdinalIgnoreCase):
                    return Model.B650M_Gaming_Plus_Wifi;
                case var _ when name.Equals("MEG X570 UNIFY", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("MEG X570 UNIFY (MS-7C35)", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("MEG X570 ACE", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("MEG X570 ACE (MS-7C35)", StringComparison.OrdinalIgnoreCase):
                    return Model.X570_MS7C35;
                case var _ when name.Equals("MPG X570 GAMING PLUS (MS-7C37)", StringComparison.OrdinalIgnoreCase):
                    return Model.X570_Gaming_Plus;
                case var _ when name.Equals("X670E AORUS XTREME", StringComparison.OrdinalIgnoreCase):
                    return Model.X670E_AORUS_XTREME;
                case var _ when name.Equals("X870E AORUS PRO", StringComparison.OrdinalIgnoreCase):
                    return Model.X870E_AORUS_PRO;
                case var _ when name.Equals("X870E AORUS PRO ICE", StringComparison.OrdinalIgnoreCase):
                    return Model.X870E_AORUS_PRO_ICE;
                case var _ when name.Equals("ROG STRIX X870-I GAMING WIFI", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_STRIX_X870_I_GAMING_WIFI;
                case var _ when name.Equals("X870E AORUS XTREME AI TOP", StringComparison.OrdinalIgnoreCase):
                    return Model.X870E_AORUS_XTREME_AI_TOP;
                case var _ when name.Equals("PROART X870E-CREATOR WIFI", StringComparison.OrdinalIgnoreCase):
                    return Model.PROART_X870E_CREATOR_WIFI;
                case var _ when name.Equals("PRIME X870-P", StringComparison.OrdinalIgnoreCase):
                    return Model.PRIME_X870_P;
                case var _ when name.Equals("ROG STRIX X870-I GAMING WIFI", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_STRIX_X870_I_GAMING_WIFI;
                case var _ when name.Equals("ROG CROSSHAIR X870E APEX", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_CROSSHAIR_X870E_APEX;
                case var _ when name.Equals("ROG CROSSHAIR X870E HERO", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_CROSSHAIR_X870E_HERO;
                case var _ when name.Equals("ROG CROSSHAIR X870E DARK HERO", StringComparison.OrdinalIgnoreCase):
                    return Model.ROG_CROSSHAIR_X870E_DARK_HERO;
                case var _ when name.Equals("MAG X870E TOMAHAWK WIFI (MS-7E59)", StringComparison.OrdinalIgnoreCase):
                    return Model.X870E_TOMAHAWK_WIFI;
                case var _ when name.Equals("MPG X870E CARBON WIFI (MS-7E49)", StringComparison.OrdinalIgnoreCase):
                    return Model.X870E_CARBON_WIFI;
                case var _ when name.Equals("B850 GAMING PLUS WIFI6E (MS-7E80)", StringComparison.OrdinalIgnoreCase):
                    return Model.B850_GAMING_PLUS_WIFI6E;
                case var _ when name.Equals("PRO B850-P WIFI (MS-7E56)", StringComparison.OrdinalIgnoreCase):
                    return Model.B850P_PRO_WIFI;
                case var _ when name.Equals("PRO B850-S WIFI6E (MS-7E80)", StringComparison.OrdinalIgnoreCase):
                    return Model.B850S_PRO_WIFI6E;
                case var _ when name.Equals("PRO B850M-A WIFI (MS-7E66)", StringComparison.OrdinalIgnoreCase):
                    return Model.B850MA_PRO_WIFI;
                case var _ when name.Equals("PRO B850M-A WIFI PZ (MS-7E78)", StringComparison.OrdinalIgnoreCase):
                    return Model.B850MA_PRO_WIFI_PZ;
                case var _ when name.Equals("PRO B850M-P WIFI (MS-7E71)", StringComparison.OrdinalIgnoreCase):
                    return Model.B850MP_PRO_WIFI;
                case var _ when name.Equals("B850 GAMING PLUS WIFI (MS-7E56)", StringComparison.OrdinalIgnoreCase):
                    return Model.B850_GAMING_PLUS_WIFI;
                case var _ when name.Equals("B850 GAMING PLUS WIFI PZ (MS-7E75)", StringComparison.OrdinalIgnoreCase):
                    return Model.B850_GAMING_PLUS_WIFI_PZ;
                case var _ when name.Equals("B850M GAMING PLUS WIFI (MS-7E66)", StringComparison.OrdinalIgnoreCase):
                    return Model.B850M_GAMING_PLUS_WIFI;
                case var _ when name.Equals("B850M GAMING PLUS WIFI6E (MS-7E81)", StringComparison.OrdinalIgnoreCase):
                    return Model.B850M_GAMING_PLUS_WIFI6E;
                case var _ when name.Equals("MAG B850M MORTAR (MS-7E61)", StringComparison.OrdinalIgnoreCase):
                    return Model.B850M_MORTAR;
                case var _ when name.Equals("MAG B850M MORTAR WIFI (MS-7E61)", StringComparison.OrdinalIgnoreCase):
                    return Model.B850M_MORTAR_WIFI;
                case var _ when name.Equals("MAG B850 TOMAHAWK WIFI (MS-7E53)", StringComparison.OrdinalIgnoreCase):
                    return Model.B850_TOMAHAWK_WIFI;
                case var _ when name.Equals("MAG B850 TOMAHAWK MAX WIFI (MS-7E62)", StringComparison.OrdinalIgnoreCase):
                    return Model.B850_TOMAHAWK_MAX_WIFI;
                case var _ when name.Equals("MPG B850 EDGE TI WIFI (MS-7E62)", StringComparison.OrdinalIgnoreCase):
                    return Model.B850_EDGE_TI_WIFI;
                case var _ when name.Equals("MPG B850I EDGE TI WIFI (MS-7E79)", StringComparison.OrdinalIgnoreCase):
                    return Model.B850I_EDGE_TI_WIFI;
                case var _ when name.Equals("B850MPOWER (MS-7E83)", StringComparison.OrdinalIgnoreCase):
                    return Model.B850MPOWER;
                case var _ when name.Equals("X870 GAMING PLUS WIFI (MS-7E47)", StringComparison.OrdinalIgnoreCase):
                    return Model.X870_GAMING_PLUS_WIFI;
                case var _ when name.Equals("X870E GAMING PLUS WIFI (MS-7E70)", StringComparison.OrdinalIgnoreCase):
                    return Model.X870E_GAMING_PLUS_WIFI;
                case var _ when name.Equals("MAG X870 TOMAHAWK WIFI (MS-7E51)", StringComparison.OrdinalIgnoreCase):
                    return Model.X870_TOMAHAWK_WIFI;
                case var _ when name.Equals("MAG X870E TOMAHAWK WIFI (MS-7E59)", StringComparison.OrdinalIgnoreCase):
                    return Model.X870E_TOMAHAWK_WIFI;
                case var _ when name.Equals("MAG X870E TOMAHAWK MAX WIFI PZ (MS-7E84)", StringComparison.OrdinalIgnoreCase):
                    return Model.X870E_TOMAHAWK_MAX_WIFI_PZ;
                case var _ when name.Equals("MEG X870E GODLIKE (MS-7E48)", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("MEG X870E GODLIKE X EDITION (MS-7E48)", StringComparison.OrdinalIgnoreCase):
                    return Model.X870E_GODLIKE;
                case var _ when name.Equals("PRO X870-P WIFI (MS-7E47)", StringComparison.OrdinalIgnoreCase):
                    return Model.X870P_PRO_WIFI;
                case var _ when name.Equals("PRO X870E-P WIFI (MS-7E70)", StringComparison.OrdinalIgnoreCase):
                    return Model.X870EP_PRO_WIFI;
                case var _ when name.Equals("MPG X870E CARBON WIFI (MS-7E49)", StringComparison.OrdinalIgnoreCase):
                    return Model.X870E_CARBON_WIFI;
                case var _ when name.Equals("MPG X870E EDGE TI WIFI (MS-7E59)", StringComparison.OrdinalIgnoreCase):
                    return Model.X870E_EDGE_TI_WIFI;
                case var _ when name.Equals("MEG X870E ACE MAX (MS-7E85)", StringComparison.OrdinalIgnoreCase):
                    return Model.X870E_ACE_MAX;
                case var _ when name.Equals("MEG X870E UNIFY-X MAX (MS-7E73)", StringComparison.OrdinalIgnoreCase):
                    return Model.X870E_UNIFY_X_MAX;
                case var _ when name.Equals("B850M Steel Legend WiFi", StringComparison.OrdinalIgnoreCase):
                    return Model.B850M_STEEL_LEGEND_WIFI;
                case var _ when name.Equals("X870E Taichi", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("X870E Taichi Lite", StringComparison.OrdinalIgnoreCase):
                    return Model.X870E_TAICHI;
                case var _ when name.Equals("X870E Nova WiFi", StringComparison.OrdinalIgnoreCase):
                    return Model.X870E_NOVA_WIFI;
                case var _ when name.Equals("B850I Lightning WiFi", StringComparison.OrdinalIgnoreCase):
                    return Model.B850I_LIGHTNING_WIFI;
                case var _ when name.StartsWith("B650M-HDV", StringComparison.OrdinalIgnoreCase):
                    return Model.B650M_HDV_M_2;
                case var _ when name.Equals("X670 AORUS ELITE AX", StringComparison.OrdinalIgnoreCase):
                    return Model.X670_AORUS_ELITE_AX;
                case var _ when name.Equals("TUF GAMING B450-PLUS II", StringComparison.OrdinalIgnoreCase):
                    return Model.TUF_GAMING_B450_PLUS_II;
                case var _ when name.Equals("FRANBMCP03", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANBMCP03;
                case var _ when name.Equals("FRANBMCP06", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANBMCP06;
                case var _ when name.Equals("FRANBMCP08", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANBMCP08;
                case var _ when name.Equals("FRANBMCP0A", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANBMCP0A;
                case var _ when name.Equals("FRANBMCP0B", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANBMCP0B;
                case var _ when name.Equals("FRANBMCP0C", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANBMCP0C;
                case var _ when name.Equals("FRANGACP04", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANGACP04;
                case var _ when name.Equals("FRANGACP06", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANGACP06;
                case var _ when name.Equals("FRANGACP08", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANGACP08;
                case var _ when name.Equals("FRANMACP04", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANMACP04;
                case var _ when name.Equals("FRANMACP06", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANMACP06;
                case var _ when name.Equals("FRANMACP08", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANMACP08;
                case var _ when name.Equals("FRANMBCP04", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANMBCP04;
                case var _ when name.Equals("FRANMCCP04", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANMCCP04;
                case var _ when name.Equals("FRANMCCP06", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANMCCP06;
                case var _ when name.Equals("FRANMCCP07", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANMCCP07;
                case var _ when name.Equals("FRANMDCP05", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANMDCP05;
                case var _ when name.Equals("FRANMDCP07", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANMDCP07;
                case var _ when name.Equals("FRANMECP02", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANMECP02;
                case var _ when name.Equals("FRANMECP05", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANMECP05;
                case var _ when name.Equals("FRANMECP06", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANMECP06;
                case var _ when name.Equals("FRANMZCP07", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANMZCP07;
                case var _ when name.Equals("FRANMZCP09", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANMZCP09;
                case var _ when name.Equals("FRANMFCP02", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANMFCP02;
                case var _ when name.Equals("FRANMFCP04", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANMFCP04;
                case var _ when name.Equals("FRANMFCP06", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANMFCP06;
                case var _ when name.Equals("FRAPMACP03", StringComparison.OrdinalIgnoreCase):
                    return Model.FRAPMACP03;
                case var _ when name.Equals("FRAPMACP05", StringComparison.OrdinalIgnoreCase):
                    return Model.FRAPMACP05;
                case var _ when name.Equals("FRANMGCP05", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANMGCP05;
                case var _ when name.Equals("FRANMGCP07", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANMGCP07;
                case var _ when name.Equals("FRANMGCP09", StringComparison.OrdinalIgnoreCase):
                    return Model.FRANMGCP09;
                case var _ when name.Equals("Base Board Product Name", StringComparison.OrdinalIgnoreCase):
                case var _ when name.Equals("To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase):
                    return Model.Unknown;
                default:
                    return Model.Unknown;
            }
        }
    }
}

using System;
using System.Text;
using ZenStates.Core.Common;
using ZenStates.Core.Drivers;

namespace ZenStates.Core.Hardware
{
    public class Mmio
    {
        // https://www.amd.com/system/files/TechDocs/52740_16h_Models_30h-3Fh_BKDG.pdf (page 859)
        internal const uint AMD_MMIO_BASE_ADDRESS = 0xFED80000;
        internal const uint MISC_BASE_ADDRESS = AMD_MMIO_BASE_ADDRESS + 0xE00;
        internal const uint SMBUS_BASE_ADDRESS = AMD_MMIO_BASE_ADDRESS + 0xA00;
        internal const uint SMUIO_BASE_ADDRESS = 0x0005A000;
        // internal const uint IOMUX_BASE = AMD_MMIO_BASE_ADDRESS + 0xD00;
        internal const uint MISC_GPPClkCntrl = MISC_BASE_ADDRESS + 0;
        internal const uint MISC_ClkOutputCntrl = MISC_BASE_ADDRESS + 0x04;
        internal const uint MISC_CGPLLConfig1 = MISC_BASE_ADDRESS + 0x08;
        internal const uint MISC_CGPLLConfig2 = MISC_BASE_ADDRESS + 0x0C;
        internal const uint MISC_CGPLLConfig3 = MISC_BASE_ADDRESS + 0x10;
        internal const uint MISC_CGPLLConfig4 = MISC_BASE_ADDRESS + 0x14;
        internal const uint MISC_CGPLLConfig5 = MISC_BASE_ADDRESS + 0x18;
        internal const uint MISC_CGPLLConfig6 = MISC_BASE_ADDRESS + 0x1C;
        internal const uint MISC_ClkCntl1 = MISC_BASE_ADDRESS + 0x40;
        internal const uint MISC_ClkCntl2 = MISC_BASE_ADDRESS + 0x44;
        internal const uint MISC_StrapStatus = MISC_BASE_ADDRESS + 0x80;
        internal const uint MISC_EclkModeStatus = AMD_MMIO_BASE_ADDRESS + 0xEAC;
        internal const uint SMBUS_BASE_ADDRESS_REG = 0x300;
        // internal const uint IOMUX_LPCCLK1 = IOMUX_BASE + 0x1F;

        private static Mmio _instance;
        private readonly IODriver io;
        private readonly Cpu.Family _family = Cpu.Family.UNSUPPORTED;
        private readonly EclkMode _eclkMode = EclkMode.UNKNOWN;
        private readonly ClkGen _clkGen = ClkGen.ERROR;

        public static Mmio Instance => _instance;

        public enum ClkGen : int
        {
            ERROR = -1,
            EXTERNAL = 0,
            INTERNAL = 1,
        }

        public enum EclkMode : int
        {
            UNKNOWN = -1,
            AUTO = 0,
            SYNC = 1,   // eCLK0 - CPU cores and the rest of the platform share one external reference
            ASYNC = 2,  // eCLK1 - CPU cores get their own external reference, split from the platform's
        }

        public Mmio(Cpu.Family family = Cpu.Family.UNSUPPORTED)
        {
            this.io = IODriver.Instance;
            _family = family;
            _instance = this;
            _eclkMode = GetEclkMode();
            _clkGen = GetStrapStatus();
        }

        private bool IsFam15 => _family == Cpu.Family.FAMILY_15H;

        private static int CalculateBclkIndex(int bclk)
        {
            if (bclk > 151)
                bclk = 151;
            else if (bclk < 96)
                bclk = 96;

            if ((bclk & 128) != 0)
                return bclk ^ 164;
            return bclk ^ 100;
        }

        private uint GetSmbusBaseAddress()
        {
            io.GetPhysLong(new UIntPtr(AMD_MMIO_BASE_ADDRESS + SMBUS_BASE_ADDRESS_REG), out uint data);
            if (data != 0)
            {
                uint value = (data >> 8) & 0x7F;
                return value << 8;
            }
            return 0;
        }

        private static int CalculateBclkFromIndex(int index)
        {
            if (index < 32)
                return index ^ 100;
            return index ^ 164;
        }

        /**
         * [17] ClkGenStrap
         *  1=Internal clocking mode; Use 48MHz crystal 
         *  clock as the reference clock. 0=External clocking mode; Use 100MHz differential spread clock as the 
         *  reference clock
         * [12] CPUClkSelStrap
         */
        public ClkGen GetStrapStatus()
        {
            if (io.GetPhysLong((UIntPtr)MISC_StrapStatus, out uint value))
                return (ClkGen)Utils.GetBit(value, 17);
            return ClkGen.ERROR;
        }

        /// <summary>
        /// Reads the eCLK sync/async/auto BIOS selection from MISC_EclkModeStatus
        /// Only tested on a single board (MSI X870E Unify-X) with a single CPU (Ryzen 9 9950X3D).
        /// </summary>
        public EclkMode GetEclkMode()
        {
            if (!io.GetPhysLong((UIntPtr)MISC_EclkModeStatus, out uint value))
                return EclkMode.UNKNOWN;

            switch (Utils.GetBits(value, 16, 8))
            {
                case 0x00: return EclkMode.AUTO;
                case 0x60: return EclkMode.SYNC;
                case 0xA0: return EclkMode.ASYNC;
                default: return EclkMode.UNKNOWN;
            }
        }

        private bool DisableSpreadSpectrum()
        {
            if (io.GetPhysLong((UIntPtr)MISC_CGPLLConfig1, out uint value))
            {
                return io.SetPhysLong((UIntPtr)MISC_CGPLLConfig1, Utils.ClearBit(value, 0));
            }
            return false;
        }

        private bool CG1AtomicUpdate()
        {
            if (io.GetPhysLong((UIntPtr)MISC_ClkCntl1, out uint value))
                return io.SetPhysLong((UIntPtr)MISC_ClkCntl1, Utils.SetBit(value, 30));
            return false;
        }

        /**
         * [11] GPP_CLK3_ClockOutputOverride. Read-write. Reset: 0. GPP_CLK3 clock output override control. GPP_CLK3 is a bi-directional pin depending on LPCCLK1 strap value. If Strap
         * (LPCCLK1)==1, GPP_CLK3 provides clock for external device and output buffer will be on. If Strap 
         * (LPCCLK1)==0, GPP_CLK3 receives clock from external clock chip and output buffer will be off. 
         * This override bit allows to invert the strap that controls GPP_CLK3 clock output buffer. 0=Use the 
         * strap value (LPCCLK1) to determine whether GPP_CLK3 clock output buffer is on or off. 1=Invert 
         * the strap that controls GPP_CLK3 clock output buffer
         * 
         * [10] CPU_CLK_ClockSourceOverride. Read-write. Reset: 0. CPU_CLK clock source override control. 
         * CPU_CLK clock source is controlled by strap (LPCCLK1). If Strap (LPCCLK1)==1, CPU_CLK 
         * clock source is from CG_PLL. If Strap (LPCCLK1)==0, CPU_CLK clock source is from external 
         * clock chip through GPP_CLK3_P/N pins. This override bit allows to invert the strap that controls 
         * CPU_CLK clock source. 0=Use the strap value (LPCCLK1) to determine whether CPU_CLK clock 
         * source from either CG_PLL or external clock chip. 1=Invert the strap that controls CPU_CLK clock 
         * source.
         */
        public bool SetBclk(double bclk)
        {
            if (bclk > 151)
                bclk = 151;
            else if (bclk < 96)
                bclk = 96;

            if (IsFam15)
            {
                if (_clkGen != ClkGen.INTERNAL)
                    return false; // external clocking mode or error

                // Family 15h (Bristol Ridge / Carrizo)
                // CGPLLConfig3 has a different bit shape here than the layout used below:
                // [9:0]=REFDIV, [21:10]=FBDIV (12 bits), [25:22]=FBDIV_Fraction (4 bits, tenths:
                // 1h-9h => *0.1, 0h/Ah-Fh => 0). The generic path's index/fraction offsets ([4:9]/[25:4]) and
                // the XOR-based CalculateBclkIndex don't apply to this family's PLL at all.
                if (!io.GetPhysLong((UIntPtr)MISC_CGPLLConfig3, out uint cfg3))
                    return false;

                uint refDiv = Utils.GetBits(cfg3, 0, 10);
                if (!io.GetPhysLong((UIntPtr)MISC_CGPLLConfig2, out uint cfg2))
                    return false;

                // [24:18] CG1PLL_PDIV_CoreCLK: post-divider from the VCO down to the "400 MHz core clock" domain.
                uint coreClkPostDiv = Utils.GetBits(cfg2, 18, 7);
                if (refDiv == 0 || coreClkPostDiv == 0)
                    return false; // PLL bypassed/uncalibrated - refuse rather than divide by zero or guess.

                // BCLK = 48MHz * (FBDIV + Fraction*0.1) / (REFDIV * PDIV_CoreCLK * 4)
                double fbdivTotal = bclk * refDiv * coreClkPostDiv * 4.0 / 48.0;

                int fbdivInt = (int)Math.Ceiling(fbdivTotal);
                uint fraction15h = (uint)Math.Round((fbdivInt - fbdivTotal) * 10.0);
                if (fraction15h > 9)
                {
                    // Fraction field only encodes 0.1-0.9 (Ah-Fh reads back as 0 per the datasheet)
                    // carry the overflow into the integer part instead of writing an invalid fraction code.
                    fraction15h = 0;
                    if (fbdivInt > 0)
                        fbdivInt -= 1;
                }

                if (fbdivInt < 0)
                    fbdivInt = 0;
                else if (fbdivInt > 0xFFF)
                    fbdivInt = 0xFFF;

                DisableSpreadSpectrum();

                uint newCfg3 = Utils.SetBits(cfg3, 10, 12, (uint)fbdivInt);
                newCfg3 = Utils.SetBits(newCfg3, 22, 4, fraction15h);

                if (!io.SetPhysLong((UIntPtr)MISC_CGPLLConfig3, newCfg3))
                    return false;

                // MISCx44[0] CG1_FBDIV_LoadEn: "Enable loading CG1PLL_FBDIV value from register MISCx10[CG1PLL_FBDIV]."
                if (!io.GetPhysLong((UIntPtr)MISC_ClkCntl2, out uint cfg44))
                    return false;
                if (!io.SetPhysLong((UIntPtr)MISC_ClkCntl2, Utils.SetBit(cfg44, 0)))
                    return false;

                // Same Atomic_Update bit (30) as the path below
                return CG1AtomicUpdate();
            }

            // Has no effect when in External mode, even though the MISC_CGPLLConfig3 is writable and can be read back.
            // TODO: Check AUTO mode (still External on MSI X870E Unify-X)
            if (_clkGen != ClkGen.INTERNAL)
                return false;

            DisableSpreadSpectrum();

            // CCG1PLL_FBDIV_Enable, bit 25
            if (!io.GetPhysLong((UIntPtr)MISC_ClkCntl1, out uint value))
                return false;

            bool res = io.SetPhysLong((UIntPtr)MISC_ClkCntl1, Utils.SetBit(value, 25));

            if (res)
            {
                int index = CalculateBclkIndex((int)bclk);
                uint fraction = (uint)((bclk - (int)bclk) / 0.0625);

                if (fraction > 15)
                    fraction = 15;

                if (!io.GetPhysLong((UIntPtr)MISC_CGPLLConfig3, out value))
                    return false;

                value = Utils.SetBits(value, 4, 9, (uint)index);
                value = Utils.SetBits(value, 25, 4, fraction);
                if (io.SetPhysLong((UIntPtr)MISC_CGPLLConfig3, value))
                    return CG1AtomicUpdate();

                return false;
            }

            return res;
        }

        public double? GetBclk()
        {
            // On MSI X870E Unify-X BCLK1 is correctly detected when in eCLK1 mode (async)
            // in eCLK0 mode (sync) it always reads 100 and setting any value has no effect.
            // In both cases StrapStatus is EXTERNAL
            //if (GetStrapStatus() != ClkGen.INTERNAL)
            //    return null;

            if (IsFam15)
            {
                if (_clkGen != ClkGen.INTERNAL)
                    return null;

                if (!io.GetPhysLong((UIntPtr)MISC_CGPLLConfig3, out uint cfg3))
                    return null;

                uint fbdiv = Utils.GetBits(cfg3, 10, 12);
                uint refDiv = Utils.GetBits(cfg3, 0, 10);
                uint fracRaw = Utils.GetBits(cfg3, 22, 4);
                // 0h and Ah-Fh both mean "no fraction" per CGPLLConfig3[CG1PLL_FBDIV_Fraction]'s definition table.
                double fraction15h = (fracRaw >= 1 && fracRaw <= 9) ? fracRaw * 0.1 : 0.0;

                if (!io.GetPhysLong((UIntPtr)MISC_CGPLLConfig2, out uint cfg2))
                    return null;

                uint coreClkPostDiv = Utils.GetBits(cfg2, 18, 7);
                if (refDiv == 0 || coreClkPostDiv == 0)
                    return null;

                return 48.0 * (fbdiv - fraction15h) / (refDiv * coreClkPostDiv * 4.0);
            }

            // Seems to be reading fine in eCLK0 (sync) on MSI X870E Unify-X.
            // In eCLK1 (async) mode, it reads the previously saved value in eCLK0, at least on MSI X870E Unify-X.
            // Writing has no real effect in External mode, except that the value is stored in the register and can be read back.
            // TODO: Check for 19h
            // TODO: Check on a board that has no external PLL
            if (_clkGen == ClkGen.EXTERNAL && _eclkMode == EclkMode.ASYNC)
                return null;

            if (io.GetPhysLong((UIntPtr)MISC_CGPLLConfig3, out uint value))
            {
                uint index = Utils.GetBits(value, 4, 9);
                uint fMul = Utils.GetBits(value, 25, 4);
                return CalculateBclkFromIndex((int)index) + fMul * 0.0625f;
            }
            return null;
        }

        public string GetReport()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(ReportBuilder.Heading("MMIO"));
            sb.AppendLine(string.Format("-- StrapStatus: {0}", GetStrapStatus()));
            sb.AppendLine(string.Format("-- EclkMode: {0}", GetEclkMode()));
            sb.AppendLine();
            sb.AppendLine("-- Raw Data");

            for (int i = 0; i < 0xFED8FFFF - 0xFED80000; i += 4)
            {
                if (io.GetPhysLong((UIntPtr)(AMD_MMIO_BASE_ADDRESS + i), out uint value))
                {
                    sb.AppendLine(string.Format("0x{0:X8}: 0x{1:X8}", AMD_MMIO_BASE_ADDRESS + i, value));
                }
            }

            return sb.ToString();
        }
    }
}

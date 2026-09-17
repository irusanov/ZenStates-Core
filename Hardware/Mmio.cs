using System;
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
        internal const uint MISC_ClkCntl1 = MISC_BASE_ADDRESS + 0x40;
        internal const uint MISC_ClkCntl2 = MISC_BASE_ADDRESS + 0x44;
        internal const uint MISC_StrapStatus = MISC_BASE_ADDRESS + 0x80;
        internal const uint SMBUS_BASE_ADDRESS_REG = 0x300;
        // internal const uint IOMUX_LPCCLK1 = IOMUX_BASE + 0x1F;

        private static Mmio _instance;
        private readonly IODriver io;
        private readonly Cpu.Family _family = Cpu.Family.UNSUPPORTED;

        public static Mmio Instance => _instance;

        public enum ClkGen : int
        {
            ERROR = -1,
            EXTERNAL = 0,
            INTERNAL = 1,
        }

        public Mmio(Cpu.Family family = Cpu.Family.UNSUPPORTED)
        {
            this.io = IODriver.Instance;
            _family = family;
            _instance = this;
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
            if (GetStrapStatus() != ClkGen.INTERNAL)
                return false; // external clocking mode or error

            if (IsFam15)
            {
                if (bclk > 151)
                    bclk = 151;
                else if (bclk < 96)
                    bclk = 96;

                // Family 15h (Bristol Ridge / Carrizo)
                // CGPLLConfig3 has a different bit shape here than the 16h layout used below:
                // [9:0]=REFDIV, [21:10]=FBDIV (12 bits), [25:22]=FBDIV_Fraction (4 bits, tenths:
                // 1h-9h => *0.1, 0h/Ah-Fh => 0). The 16h path's index/fraction offsets ([4:9]/[25:4]) and
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

                // Same Atomic_Update bit (30) as the 16h path below
                return CG1AtomicUpdate();
            }

            DisableSpreadSpectrum();

            // CCG1PLL_FBDIV_Enable, bit 25
            bool res = io.GetPhysLong((UIntPtr)MISC_ClkCntl1, out uint value);
            res = io.SetPhysLong((UIntPtr)MISC_ClkCntl1, Utils.SetBit(value, 25));

            if (res)
            {
                int index = CalculateBclkIndex((int)bclk);
                uint fraction = (uint)((bclk - (int)bclk) / 0.0625);

                if (fraction > 15)
                    fraction = 15;

                res = io.GetPhysLong((UIntPtr)MISC_CGPLLConfig3, out value);
                value = Utils.SetBits(value, 4, 9, (uint)index);
                value = Utils.SetBits(value, 25, 4, fraction);
                if (io.SetPhysLong((UIntPtr)MISC_CGPLLConfig3, value))
                    return CG1AtomicUpdate();
            }

            return res;
        }

        public double? GetBclk()
        {
            if (GetStrapStatus() != ClkGen.INTERNAL)
                return null;

            if (IsFam15)
            {
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

            if (io.GetPhysLong((UIntPtr)MISC_CGPLLConfig3, out uint value))
            {
                uint index = Utils.GetBits(value, 4, 9);
                uint fMul = Utils.GetBits(value, 25, 4);
                return CalculateBclkFromIndex((int)index) + fMul * 0.0625f;
            }
            return null;
        }
    }
}

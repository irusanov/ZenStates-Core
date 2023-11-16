using System;

namespace ZenStates.Core
{
    public class ACPI_MMIO
    {
        // https://www.amd.com/system/files/TechDocs/52740_16h_Models_30h-3Fh_BKDG.pdf (page 859)
        internal const uint ACPI_MMIO_BASE_ADDRESS = 0xFED80000;
        internal const uint MISC_BASE = ACPI_MMIO_BASE_ADDRESS + 0xE00;
        // internal const uint IOMUX_BASE = ACPI_MMIO_BASE_ADDRESS + 0xD00;
        internal const uint MISC_GPPClkCntrl = MISC_BASE + 0;
        internal const uint MISC_ClkOutputCntrl = MISC_BASE + 0x04;
        internal const uint MISC_CGPLLConfig1 = MISC_BASE + 0x08;
        internal const uint MISC_CGPLLConfig2 = MISC_BASE + 0x0C;
        internal const uint MISC_CGPLLConfig3 = MISC_BASE + 0x10;
        internal const uint MISC_CGPLLConfig4 = MISC_BASE + 0x14;
        internal const uint MISC_CGPLLConfig5 = MISC_BASE + 0x18;
        internal const uint MISC_ClkCntl1 = MISC_BASE + 0x40;
        internal const uint MISC_StrapStatus = MISC_BASE + 0x80;
        // internal const uint IOMUX_LPCCLK1 = IOMUX_BASE + 0x1F;

        private readonly IOModule io;

        public ACPI_MMIO(IOModule io)
        {
            this.io = io;
        }

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
        public int GetStrapStatus()
        {
            if (io.GetPhysLong((UIntPtr)MISC_StrapStatus, out uint value))
                return (int)Utils.GetBits(value, 17, 1);
            return -1;
        }

        private bool DisableSpreadSpectrum()
        {
            if (io.GetPhysLong((UIntPtr)MISC_CGPLLConfig1, out uint value))
                return io.SetPhysLong((UIntPtr)MISC_CGPLLConfig1, Utils.SetBits(value, 0, 0, 0));
            return false;
        }

        private bool CG1AtomicUpdate()
        {
            if (io.GetPhysLong((UIntPtr)MISC_ClkCntl1, out uint value))
                return io.SetPhysLong((UIntPtr)MISC_ClkCntl1, Utils.SetBits(value, 30, 1, 1));
            return false;
        }

        /**
         * [11] GPP_CLK3_ClockOutputOverride. Read-write. Reset: 0. GPP_CLK3 clock output override control. GPP_CLK3 is a bi-directional pin depending on LPCCLK1 strap value. If Strap 
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
            DisableSpreadSpectrum();

            // CCG1PLL_FBDIV_Enable, bit 25
            bool res = io.GetPhysLong((UIntPtr)MISC_ClkCntl1, out uint value);
            res = io.SetPhysLong((UIntPtr)MISC_ClkCntl1, Utils.SetBits(value, 25, 1, 1));

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

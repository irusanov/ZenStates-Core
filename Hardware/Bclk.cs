using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using ZenStates.Core.Common;
using ZenStates.Core.Drivers;
using ZenStates.Core.Hardware.DRAM;
using ZenStates.Core.OHWM;

namespace ZenStates.Core.Hardware
{
    /// <summary>
    /// Result of <see cref="Bclk.MeasureBclk"/>: BCLK derived from the TSC rate against the ACPI timer, not from any register.
    /// </summary>
    public sealed class BclkMeasurement
    {
        /// <summary>Implied BCLK in MHz (median of the samples), null when the measurement failed.</summary>
        public double? BclkMhz { get; set; }
        /// <summary>Measured TSC frequency in MHz (median).</summary>
        public double TscMhz { get; set; }
        /// <summary>P0 core frequency in MHz at a 100 MHz BCLK, from PStateDef0.</summary>
        public double P0Mhz { get; set; }
        /// <summary>Largest minus smallest implied BCLK between samples, in MHz. Large values mean an unreliable measurement.</summary>
        public double SpreadMhz { get; set; }
        public int Samples { get; set; }
        /// <summary>Why the measurement could not be made, null on success.</summary>
        public string Error { get; set; }
    }

    /// <summary>
    /// Result of <see cref="Bclk.MeasureMemoryBclk"/>: the reference clock of the memory side (UMC, and on AM5 the fabric and PCIe
    /// too), from the UMC clock cycle counter against the ACPI timer. Differs from <see cref="BclkMeasurement"/> in
    /// asynchronous eCLK mode, where the cores have a reference clock of their own.
    /// </summary>
    public sealed class MemoryBclkMeasurement
    {
        /// <summary>
        /// Implied memory reference clock in MHz, the highest sample; null when the measurement failed. The counter stops while
        /// MEMCLK is gated (memory power-down / self-refresh at idle), so a sample can only come out low, never high.
        /// </summary>
        public double? BclkMhz { get; set; }
        /// <summary>Measured rate of the UMC clock counter in MHz, of the highest sample.</summary>
        public double CounterMhz { get; set; }
        /// <summary>MEMCLK in MHz at a 100 MHz reference clock, from the memory clock ratio.</summary>
        public double NominalMemClkMhz { get; set; }
        /// <summary>MEMCLK over the counted clock: 1 when the counter counts MEMCLK, 2 when it counts MEMCLK / 2.</summary>
        public int Divider { get; set; }
        /// <summary>The UMC instance that was measured.</summary>
        public int Umc { get; set; } = -1;
        /// <summary>The implied clock of each sample in MHz, in the order taken.</summary>
        public double[] SampleMhz { get; set; }
        /// <summary>Samples within <see cref="Bclk.MemoryBclkTolerance"/> of the highest one, itself included.</summary>
        public int AgreeingSamples { get; set; }
        public int Samples { get; set; }
        /// <summary>Why the measurement could not be made, null on success.</summary>
        public string Error { get; set; }
    }

    /// <summary>
    /// The reference clock (BCLK): the FCH clock generator registers (strap, eCLK mode, CPU reference clock source,
    /// CG1_PLL), reading and setting BCLK through them, and measuring the real clock of the CPU side (TSC against the
    /// ACPI timer) and of the memory side (UMC clock counter against the ACPI timer). The memory side value is what
    /// MCLK, FCLK, UCLK and the timings in ns are scaled with (<see cref="GetMemoryBclk"/>); <see cref="Cpu"/> measures it
    /// during start and then periodically on a background thread.
    /// </summary>
    public sealed class Bclk
    {
        // FCH::MISC clock generator registers, https://www.amd.com/system/files/TechDocs/52740_16h_Models_30h-3Fh_BKDG.pdf
        // (page 859) and AMD PPR 55570 (Family 17h Model 18h)
        internal const uint MISC_GPPClkCntrl = Mmio.MISC_BASE_ADDRESS + 0;
        internal const uint MISC_ClkOutputCntrl = Mmio.MISC_BASE_ADDRESS + 0x04;
        internal const uint MISC_CGPLLConfig1 = Mmio.MISC_BASE_ADDRESS + 0x08;
        internal const uint MISC_CGPLLConfig2 = Mmio.MISC_BASE_ADDRESS + 0x0C;
        internal const uint MISC_CGPLLConfig3 = Mmio.MISC_BASE_ADDRESS + 0x10;
        internal const uint MISC_CGPLLConfig4 = Mmio.MISC_BASE_ADDRESS + 0x14;
        internal const uint MISC_CGPLLConfig5 = Mmio.MISC_BASE_ADDRESS + 0x18;
        internal const uint MISC_CGPLLConfig6 = Mmio.MISC_BASE_ADDRESS + 0x1C;
        internal const uint MISC_ClkCntl1 = Mmio.MISC_BASE_ADDRESS + 0x40;
        internal const uint MISC_ClkCntl2 = Mmio.MISC_BASE_ADDRESS + 0x44;
        internal const uint MISC_StrapStatus = Mmio.MISC_BASE_ADDRESS + 0x80;
        // MISCx0AC is FCH::MISC::CGPLLCntrlReg4. Bits 23:22 are CPU_Refclk_Selection_Override and bits 21:20 are
        // SMU_100M_Refclk_Selection (AMD PPR 55570, Family 17h Model 18h). The "eCLK mode" decoding below reads the same register.
        internal const uint MISC_EclkModeStatus = Mmio.AMD_MMIO_BASE_ADDRESS + 0xEAC;
        internal const uint MISC_CGPLLCntrlReg4 = Mmio.MISC_BASE_ADDRESS + 0xAC;
        internal const uint MISC_CGPLLConfig8 = Mmio.MISC_BASE_ADDRESS + 0x34;

        private static Bclk _instance;

        private readonly Cpu _cpu;
        private readonly Mmio _mmio;
        private readonly Cpu.Family _family;
        private readonly Cpu.CodeName _codeName;
        private readonly EclkMode _eclkMode = EclkMode.UNKNOWN;
        private readonly ClkGen _clkGen = ClkGen.ERROR;

        // Set by Close; the measurement thread checks it before each step
        private volatile bool _closed;

        /// <summary>
        /// The instance of the running <see cref="Cpu"/>, for code that has no reference to it (the power table, the timings);
        /// null before it is created and after it is closed.
        /// </summary>
        public static Bclk Instance => _instance;

        public enum ClkGen : int
        {
            ERROR = -1,
            EXTERNAL = 0,
            INTERNAL = 1,
        }

        /// <summary>
        /// Source of the CPU reference clock as selected by MISCx0AC[23:22] (CPU_Refclk_Selection_Override).
        /// The field is an override that is XORed with the strap default, so on its own it only tells the whole story in
        /// internal clocking mode (see <see cref="GetStrapStatus"/>). Changing it requires a cold reset.
        /// </summary>
        public enum CpuRefClkSource : int
        {
            UNKNOWN = -1,
            CG1_PLL = 0,        // 100 MHz generated by CG1_PLL (default in internal clocking mode)
            EXT_GPP0_SRC = 1,
            EXTERNAL_GPP1 = 2,  // external input through GPP1 (default in external clocking mode)
            RESERVED = 3,
        }

        public enum EclkMode : int
        {
            UNKNOWN = -1,
            AUTO = 0,
            SYNC = 1,   // eCLK0 - CPU cores and the rest of the platform share one external reference
            ASYNC = 2,  // eCLK1 - CPU cores get their own external reference, split from the platform's
        }

        /// <summary>
        /// Human readable reason of the last <see cref="SetBclk"/> failure or warning, null when the last call was clean.
        /// </summary>
        public string LastSetStatus { get; private set; }

        internal Bclk(Cpu cpu, Mmio mmio)
        {
            _cpu = cpu;
            _mmio = mmio;
            _family = cpu.info.family;
            _codeName = cpu.info.codeName;
            _instance = this;

            if (_mmio.IsAvailable)
            {
                _eclkMode = GetEclkMode();
                _clkGen = GetStrapStatus();
            }
        }

        /// <summary>
        /// Stops the background measurement (waiting up to 3 s for a running one, so it finishes with the drivers still
        /// there) and clears <see cref="Instance"/>. Called by <see cref="Cpu"/> before it disposes the drivers.
        /// </summary>
        internal void Close()
        {
            _closed = true;
            _memoryBclkWake.Set();

            System.Threading.Thread measurement = _memoryBclkThread;
            if (measurement != null && measurement.IsAlive && measurement != System.Threading.Thread.CurrentThread)
                measurement.Join(3000);

            if (ReferenceEquals(_instance, this))
                _instance = null;
        }

        private bool IsFam15 => _family == Cpu.Family.FAMILY_15H;

        private bool IsInpOutOpen => _cpu.io != null && _cpu.io.IsInpOutDriverOpen();

        /// <summary>
        /// Raven Ridge / Picasso / Dali: APUs with the FCH on the SoC. Their register layout follows the Family 17h Model 18h PPR
        /// (55570), where the CPU reference clock only comes from CG1_PLL when the strap is internal and
        /// MISCx0AC[23:22] selects CG1_PLL. Newer platforms keep the behaviour that was tested on them.
        /// </summary>
        public bool IsLegacyApuClocking =>
            _family == Cpu.Family.FAMILY_17H &&
            (_codeName == Cpu.CodeName.RavenRidge || _codeName == Cpu.CodeName.Picasso || _codeName == Cpu.CodeName.Dali);

        /// <summary>Reads MISCx0AC[23:22], the CPU reference clock selection override.</summary>
        public CpuRefClkSource GetCpuRefClkSource()
        {
            if (!_mmio.IsAvailable)
                return CpuRefClkSource.UNKNOWN;

            if (!_mmio.ReadRegister(MISC_CGPLLCntrlReg4, out uint value))
                return CpuRefClkSource.UNKNOWN;

            return (CpuRefClkSource)Utils.GetBits(value, 22, 2);
        }

        /// <summary>
        /// True when the CPU cores are clocked from the FCH CG1_PLL, i.e. when writing its feedback divider can change the core clock.
        /// </summary>
        public bool IsCpuClockedFromCgPll()
        {
            return _clkGen == ClkGen.INTERNAL && GetCpuRefClkSource() == CpuRefClkSource.CG1_PLL;
        }

        private bool WaitCfgUpdateDone()
        {
            Stopwatch sw = Stopwatch.StartNew();
            do
            {
                if (!_mmio.ReadRegister(MISC_ClkCntl1, out uint v))
                    return false;
                if (Utils.GetBit(v, 30) == 0)
                    return true;
                Thread.Sleep(1);
            } while (sw.ElapsedMilliseconds < 100);

            return false;
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
        public ClkGen GetStrapStatus()
        {
            if (!_mmio.IsAvailable)
                return ClkGen.ERROR;

            if (_mmio.ReadRegister(MISC_StrapStatus, out uint value))
                return (ClkGen)Utils.GetBit(value, 17);
            return ClkGen.ERROR;
        }

        /// <summary>
        /// Reads the eCLK sync/async/auto BIOS selection from MISC_EclkModeStatus
        /// Only tested on a single board (MSI X870E Unify-X) with a single CPU (Ryzen 9 9950X3D).
        /// </summary>
        public EclkMode GetEclkMode()
        {
            if (!_mmio.IsAvailable)
                return EclkMode.UNKNOWN;

            // On the FCH-on-SoC APUs the same bits are CPU_Refclk_Selection_Override, see GetCpuRefClkSource().
            // The sync/async values below were only observed on a Family 1Ah board.
            if (IsLegacyApuClocking)
                return EclkMode.UNKNOWN;

            if (!_mmio.ReadRegister(MISC_EclkModeStatus, out uint value))
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
            if (_mmio.ReadRegister(MISC_CGPLLConfig1, out uint value))
            {
                return _mmio.WriteRegister(MISC_CGPLLConfig1, Utils.ClearBit(value, 0));
            }
            return false;
        }

        private bool CG1AtomicUpdate()
        {
            if (_mmio.ReadRegister(MISC_ClkCntl1, out uint value))
                return _mmio.WriteRegister(MISC_ClkCntl1, Utils.SetBit(value, 30));
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
            bool ok = WriteBclk(bclk);
            // The memory side follows in synchronous mode; measure it again rather than keep the old value
            if (ok && _cpu.Options.MeasureMemoryBclk)
                MeasureInBackground();
            return ok;
        }

        private bool WriteBclk(double bclk)
        {
            LastSetStatus = null;

            if (!_mmio.IsAvailable)
            {
                LastSetStatus = "IO driver is not available.";
                return false;
            }

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
                if (!_mmio.ReadRegister(MISC_CGPLLConfig3, out uint cfg3))
                    return false;

                uint refDiv = Utils.GetBits(cfg3, 0, 10);
                if (!_mmio.ReadRegister(MISC_CGPLLConfig2, out uint cfg2))
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

                if (!_mmio.WriteRegister(MISC_CGPLLConfig3, newCfg3))
                    return false;

                // MISCx44[0] CG1_FBDIV_LoadEn: "Enable loading CG1PLL_FBDIV value from register MISCx10[CG1PLL_FBDIV]."
                if (!_mmio.ReadRegister(MISC_ClkCntl2, out uint cfg44))
                    return false;
                if (!_mmio.WriteRegister(MISC_ClkCntl2, Utils.SetBit(cfg44, 0)))
                    return false;

                // Same Atomic_Update bit (30) as the path below
                return CG1AtomicUpdate();
            }

            // Has no effect when in External mode, even though the MISC_CGPLLConfig3 is writable and can be read back.
            // TODO: Check AUTO mode (still External on MSI X870E Unify-X)
            if (_clkGen != ClkGen.INTERNAL)
            {
                LastSetStatus = "Clock generator strap is not internal (" + _clkGen + "), the write would have no effect.";
                return false;
            }

            // Raven / Picasso / Dali: the CG1_PLL feedback divider only matters if the CPU reference clock comes from CG1_PLL.
            // MISCx0AC[23:22] != 0 means an external source is selected and a cold reset is needed to change that.
            if (IsLegacyApuClocking)
            {
                CpuRefClkSource refSource = GetCpuRefClkSource();
                if (refSource != CpuRefClkSource.CG1_PLL)
                {
                    LastSetStatus = "CPU reference clock source is " + refSource + ", not CG1_PLL. Writing the PLL would have no effect.";
                    return false;
                }
            }

            // Raven / Picasso / Dali: CGPllCfg3[28:25] are reserved, there is no 1/16 MHz fraction field. Round to a whole MHz
            // instead of writing reserved bits and reporting a value that is never applied.
            if (IsLegacyApuClocking)
            {
                double whole = Math.Round(bclk, MidpointRounding.AwayFromZero);
                if (Math.Abs(whole - bclk) > 0.0001)
                    LastSetStatus = "Fractional BCLK is not supported on this platform, rounded to " + whole.ToString("0") + " MHz.";
                bclk = whole;
            }

            DisableSpreadSpectrum();

            // CCG1PLL_FBDIV_Enable, bit 25
            if (!_mmio.ReadRegister(MISC_ClkCntl1, out uint value))
                return false;

            bool res = _mmio.WriteRegister(MISC_ClkCntl1, Utils.SetBit(value, 25));

            if (res)
            {
                int index = CalculateBclkIndex((int)bclk);
                uint fraction = (uint)((bclk - (int)bclk) / 0.0625);

                if (fraction > 15)
                    fraction = 15;

                if (!_mmio.ReadRegister(MISC_CGPLLConfig3, out value))
                    return false;

                value = Utils.SetBits(value, 4, 9, (uint)index);
                if (!IsLegacyApuClocking)
                    value = Utils.SetBits(value, 25, 4, fraction);
                if (!_mmio.WriteRegister(MISC_CGPLLConfig3, value))
                {
                    LastSetStatus = "Writing CGPLLConfig3 failed.";
                    return false;
                }

                if (!CG1AtomicUpdate())
                {
                    LastSetStatus = "Requesting the CG1_PLL update (MiscClkCntl1[30]) failed.";
                    return false;
                }

                // MiscClkCntl1[30] CG1_cfg_update_req is cleared by hardware once the request has been sent to CG1_PLL.
                // Stays set when the PLL never accepted the new value. Only strict on the platforms that document it (PPR 55570).
                bool updateDone = WaitCfgUpdateDone();
                bool readBackOk = _mmio.ReadRegister(MISC_CGPLLConfig3, out uint readBack) &&
                                  Utils.GetBits(readBack, 4, 9) == (uint)index;

                if (!updateDone)
                    LastSetStatus = "CG1_cfg_update_req (MiscClkCntl1[30]) did not clear, the PLL did not accept the new value.";
                else if (!readBackOk)
                    LastSetStatus = "CGPLLConfig3 does not read back the written value.";

                return IsLegacyApuClocking ? updateDone && readBackOk : true;
            }

            return res;
        }

        public double? GetBclk()
        {
            if (!_mmio.IsAvailable)
                return null;

            // On MSI X870E Unify-X BCLK1 is correctly detected when in eCLK1 mode (async)
            // in eCLK0 mode (sync) it always reads 100 and setting any value has no effect.
            // In both cases StrapStatus is EXTERNAL
            //if (GetStrapStatus() != ClkGen.INTERNAL)
            //    return null;

            // Raven / Picasso / Dali: the register only holds a stored value unless the cores are clocked from CG1_PLL.
            // Return nothing instead of a number that does not correspond to the real clock.
            if (IsLegacyApuClocking && !IsCpuClockedFromCgPll())
                return null;

            if (IsFam15)
            {
                if (_clkGen != ClkGen.INTERNAL)
                    return null;

                if (!_mmio.ReadRegister(MISC_CGPLLConfig3, out uint cfg3))
                    return null;

                uint fbdiv = Utils.GetBits(cfg3, 10, 12);
                uint refDiv = Utils.GetBits(cfg3, 0, 10);
                uint fracRaw = Utils.GetBits(cfg3, 22, 4);
                // 0h and Ah-Fh both mean "no fraction" per CGPLLConfig3[CG1PLL_FBDIV_Fraction]'s definition table.
                double fraction15h = (fracRaw >= 1 && fracRaw <= 9) ? fracRaw * 0.1 : 0.0;

                if (!_mmio.ReadRegister(MISC_CGPLLConfig2, out uint cfg2))
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

            if (_mmio.ReadRegister(MISC_CGPLLConfig3, out uint value))
            {
                uint index = Utils.GetBits(value, 4, 9);
                uint fMul = IsLegacyApuClocking ? 0 : Utils.GetBits(value, 25, 4);
                return CalculateBclkFromIndex((int)index) + fMul * 0.0625f;
            }
            return null;
        }

        /// <summary>
        /// Decoded CG1_PLL / reference clock registers. Field positions follow FCH::MISC in AMD PPR 55570 (Family 17h Model 18h).
        /// Not applied to Family 15h, which has a different CGPLLConfig3 layout.
        /// </summary>
        private void AppendClockDetails(StringBuilder sb)
        {
            if (!_mmio.IsAvailable || IsFam15)
                return;

            sb.AppendLine("-- Clock registers (Family 17h Model 18h PPR layout)");

            if (_mmio.ReadRegister(MISC_CGPLLConfig1, out uint cfg1))
                sb.AppendLine(string.Format("-- CGPllCfg1   0x{0:X8}: SSC={1} RefClkSourceSwitchMode={2} CG1_REFDIVSRC_Override={3}",
                    cfg1, Utils.GetBit(cfg1, 0), Utils.GetBit(cfg1, 4), Utils.GetBit(cfg1, 8)));

            if (_mmio.ReadRegister(MISC_CGPLLConfig3, out uint cfg3))
            {
                uint idx = Utils.GetBits(cfg3, 4, 9);
                sb.AppendLine(string.Format("-- CGPllCfg3   0x{0:X8}: fcw0_int_Override={1} (decoded BCLK {2}) refclk_div_Override={3} fracn_en_Override={4} fraction[28:25]={5}",
                    cfg3, idx, CalculateBclkFromIndex((int)idx), Utils.GetBits(cfg3, 0, 2), Utils.GetBit(cfg3, 29), Utils.GetBits(cfg3, 25, 4)));
            }

            if (_mmio.ReadRegister(MISC_CGPLLConfig4, out uint cfg4))
                sb.AppendLine(string.Format("-- CGPllCfg4   0x{0:X8}: fcw1_frac_Override={1}", cfg4, Utils.GetBits(cfg4, 8, 16)));

            if (_mmio.ReadRegister(MISC_CGPLLConfig5, out uint cfg5))
                sb.AppendLine(string.Format("-- CGPllCfg5   0x{0:X8}: fcw_slew_frac_Override={1}", cfg5, Utils.GetBits(cfg5, 16, 16)));

            if (_mmio.ReadRegister(MISC_CGPLLConfig6, out uint cfg6))
                sb.AppendLine(string.Format("-- CGPllCfg6   0x{0:X8}: gp_coarse_mant_Override={1}", cfg6, Utils.GetBits(cfg6, 13, 4)));

            if (_mmio.ReadRegister(MISC_CGPLLConfig8, out uint cfg8))
                sb.AppendLine(string.Format("-- CGPllCfg8   0x{0:X8}: fcw1_int_Override={1}", cfg8, Utils.GetBits(cfg8, 23, 9)));

            if (_mmio.ReadRegister(MISC_ClkCntl1, out uint cntl1))
                sb.AppendLine(string.Format("-- MiscClkCntl1 0x{0:X8}: CG1_FBDIV_LoadEn={1} CG1_cfg_update_req={2} OscClkSwitchEn={3}",
                    cntl1, Utils.GetBit(cntl1, 25), Utils.GetBit(cntl1, 30), Utils.GetBit(cntl1, 14)));

            if (_mmio.ReadRegister(MISC_CGPLLCntrlReg4, out uint ref4))
                sb.AppendLine(string.Format("-- CGPLLCntrlReg4 0x{0:X8}: CPU_Refclk_Selection_Override={1} SMU_100M_Refclk_Selection={2}",
                    ref4, Utils.GetBits(ref4, 22, 2), Utils.GetBits(ref4, 20, 2)));

            double? bclk = GetBclk();
            sb.AppendLine(string.Format("-- GetBclk(): {0}", bclk.HasValue ? bclk.Value.ToString("0.0###") : "n/a"));
            if (!string.IsNullOrEmpty(LastSetStatus))
                sb.AppendLine(string.Format("-- LastSetStatus: {0}", LastSetStatus));
        }

        /// <summary>
        /// The clock lines of the debug report's MMIO section (<see cref="Mmio.GetReport"/>): strap, eCLK mode, CPU reference
        /// clock source and the decoded clock registers. Same text as before the clock code left <see cref="Mmio"/>.
        /// </summary>
        internal void AppendRegisterReport(StringBuilder sb)
        {
            sb.AppendLine(string.Format("-- StrapStatus: {0}", GetStrapStatus()));
            sb.AppendLine(string.Format("-- EclkMode: {0}", GetEclkMode()));
            sb.AppendLine(string.Format("-- CpuRefClkSource: {0}", GetCpuRefClkSource()));
            sb.AppendLine(string.Format("-- LegacyApuClocking: {0}", IsLegacyApuClocking));
            AppendClockDetails(sb);
        }

        /// <summary>
        /// Measures the real BCLK without reading any PLL register: the TSC runs at the P0 frequency (PPR 55570, HWCR[TscFreqSel]),
        /// P0 = PStateDef0 FID / DID * 200 MHz at a 100 MHz BCLK, and the ACPI timer is clocked from the crystal, independent of CG1_PLL.
        /// BCLK = measured TSC MHz / P0 MHz * 100. Blocks for about samples * durationMs, call it from a background thread.
        /// Family 17h/19h/1Ah; the result is meaningless if the TSC is virtualized or P0 was changed while measuring.
        /// </summary>
        public BclkMeasurement MeasureBclk(int durationMs = 200, int samples = 3)
        {
            lock (MeasurementLock)
                return MeasureBclkLocked(durationMs, samples);
        }

        private BclkMeasurement MeasureBclkLocked(int durationMs, int samples)
        {
            BclkMeasurement r = new BclkMeasurement();

            if (!_mmio.IsAvailable || !_cpu.IsAmdModuleLoaded)
            {
                r.Error = "MMIO or MSR access is not available.";
                return r;
            }

            if ((int)_family < (int)Cpu.Family.FAMILY_17H || (int)_family > (int)Cpu.Family.FAMILY_1AH)
            {
                r.Error = "Unsupported CPU family.";
                return r;
            }

            if (Opcode.Rdtsc == null)
            {
                r.Error = "RDTSC is not available.";
                return r;
            }

            if (durationMs < 50) durationMs = 50;
            if (samples < 1) samples = 1;

            if (!TryGetP0Mhz(out double p0Mhz, out string p0Error))
            {
                r.Error = p0Error;
                return r;
            }

            r.P0Mhz = p0Mhz;

            double[] results = new double[samples];
            double[] tscMhz = new double[samples];
            GroupAffinity previous = ThreadAffinity.Set(GroupAffinity.ForLogicalProcessor(0));

            try
            {
                for (int i = 0; i < samples; i++)
                {
                    if (!ReadTscAndTimer(out ulong tsc0, out uint tmr0))
                    {
                        r.Error = "Could not read the ACPI timer.";
                        return r;
                    }

                    System.Threading.Thread.Sleep(durationMs);

                    if (!ReadTscAndTimer(out ulong tsc1, out uint tmr1))
                    {
                        r.Error = "Could not read the ACPI timer.";
                        return r;
                    }

                    uint ticks = unchecked(tmr1 - tmr0);
                    if (ticks == 0 || tsc1 <= tsc0)
                    {
                        r.Error = "The ACPI timer or the TSC did not advance.";
                        return r;
                    }

                    double seconds = ticks / Mmio.AcpiPmTimerHz;
                    tscMhz[i] = (tsc1 - tsc0) / seconds / 1e6;
                    results[i] = tscMhz[i] / p0Mhz * 100.0;
                }
            }
            finally
            {
                if (previous != GroupAffinity.Undefined)
                    ThreadAffinity.Set(previous);
            }

            Array.Sort(results);
            Array.Sort(tscMhz);
            r.Samples = samples;
            r.BclkMhz = results[samples / 2];
            r.TscMhz = tscMhz[samples / 2];
            r.SpreadMhz = results[samples - 1] - results[0];
            return r;
        }

        /// <summary>
        /// P0 core frequency in MHz at a 100 MHz BCLK, from PStateDef0 (MSR C0010064, PstateEn is bit 63).
        /// Family 17h/19h: CpuFid [7:0] / CpuDfsId [13:8] * 200 MHz.
        /// Family 1Ah: CpuFid [11:0] in 5 MHz steps, no divider (same field layout as the [11:0] of <see cref="Cpu.HwPstateStatus"/> on 1Ah,
        /// see <see cref="Cpu.GetCoreMulti"/>).
        /// </summary>
        private bool TryGetP0Mhz(out double p0Mhz, out string error)
        {
            p0Mhz = 0;
            error = null;

            uint eax = 0, edx = 0;
            if (!_cpu.ReadMsr(0xC0010064, ref eax, ref edx) || (edx & 0x80000000) == 0)
            {
                error = "Could not read PStateDef0.";
                return false;
            }

            if (_family >= Cpu.Family.FAMILY_1AH)
            {
                uint fid1a = eax & 0xFFF;
                if (fid1a == 0)
                {
                    error = "PStateDef0 has no valid frequency.";
                    return false;
                }

                p0Mhz = 5.0 * fid1a;
                return true;
            }

            uint fid = eax & 0xFF;
            uint did = (eax >> 8) & 0x3F;
            if (fid == 0 || did == 0)
            {
                error = "PStateDef0 has no valid frequency.";
                return false;
            }

            p0Mhz = 200.0 * fid / did;
            return true;
        }

        /// <summary>
        /// Debug report section: BCLK measured from the TSC against the ACPI timer, next to the PLL register value, and the
        /// memory side reference clock from the UMC clock counter (<see cref="MeasureMemoryBclk"/>). Takes about two seconds.
        /// Never throws.
        /// </summary>
        public string GetMeasurementReport()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(ReportBuilder.Heading("BCLK Measurement"));
            sb.AppendLine("-- Method: TSC rate against the ACPI timer (no PLL register is read for the result)");

            try
            {
                BclkMeasurement m = MeasureBclk();
                System.Globalization.CultureInfo ic = System.Globalization.CultureInfo.InvariantCulture;

                if (m.P0Mhz > 0)
                    sb.AppendLine(string.Format(ic, "-- P0 at 100 MHz BCLK (PStateDef0): {0:0.00} MHz", m.P0Mhz));

                if (m.BclkMhz.HasValue)
                {
                    sb.AppendLine(string.Format(ic, "-- Measured TSC: {0:0.00} MHz", m.TscMhz));
                    sb.AppendLine(string.Format(ic, "-- Measured BCLK: {0:0.00} MHz (spread {1:0.000} MHz, {2} samples)",
                        m.BclkMhz.Value, m.SpreadMhz, m.Samples));
                }
                else
                {
                    sb.AppendLine("-- Measured BCLK: not available (" + m.Error + ")");
                }

                double? reg = GetBclk();
                sb.AppendLine(string.Format(ic, "-- Register BCLK (GetBclk): {0}",
                    reg.HasValue ? reg.Value.ToString("0.00", ic) + " MHz" : "n/a"));

                if (m.BclkMhz.HasValue && reg.HasValue && Math.Abs(m.BclkMhz.Value - reg.Value) > 0.5)
                    sb.AppendLine("-- The register value does not match the measured clock.");
            }
            catch (Exception ex)
            {
                sb.AppendLine("-- Measured BCLK: failed (" + ex.Message + ")");
            }

            sb.AppendLine();
            sb.AppendLine("-- Memory side: UMC clock counter against the ACPI timer (the counter is started through InpOut)");

            try
            {
                MemoryBclkMeasurement mm = MeasureMemoryBclk();
                System.Globalization.CultureInfo ic = System.Globalization.CultureInfo.InvariantCulture;

                if (mm.BclkMhz.HasValue)
                {
                    sb.AppendLine(string.Format(ic, "-- UMC{0} counter: {1:0.00} MHz ({2}), MEMCLK at 100 MHz BCLK: {3:0} MHz",
                        mm.Umc, mm.CounterMhz, mm.Divider == 1 ? "MEMCLK" : "MEMCLK / 2", mm.NominalMemClkMhz));
                    sb.AppendLine(string.Format(ic, "-- Measured memory BCLK: {0:0.00} MHz (highest sample, {1} of {2} within {3:0.00} MHz)",
                        mm.BclkMhz.Value, mm.AgreeingSamples, mm.Samples, MemoryBclkTolerance));
                }

                if (mm.SampleMhz != null)
                {
                    string[] values = new string[mm.SampleMhz.Length];
                    for (int i = 0; i < values.Length; i++)
                        values[i] = mm.SampleMhz[i].ToString("0.000", ic);
                    sb.AppendLine("-- Samples: " + string.Join(", ", values) + " MHz (lower ones: MEMCLK gated while idle)");
                }
                else
                {
                    sb.AppendLine("-- Measured memory BCLK: not available (" + mm.Error + ")");
                }

                // The same counters as MSRs (UMC_PerfMonCtl / UMC_PerfMonCntr, C001_0800+), PPR 57238 Vol 1
                if (Opcode.Cpuid != null &&
                    Opcode.Cpuid(0x80000000, 0, out uint maxExt, out _, out _, out _) && maxExt >= 0x80000022 &&
                    Opcode.Cpuid(0x80000022, 0, out uint pmEax, out uint pmEbx, out uint pmEcx, out _))
                    sb.AppendLine(string.Format(ic, "-- CPUID 8000_0022: EAX 0x{0:X8}, EBX 0x{1:X8} (UMC PMCs {2}), ECX 0x{3:X8} (active UMC mask)",
                        pmEax, pmEbx, (pmEbx >> 16) & 0xFF, pmEcx));
            }
            catch (Exception ex)
            {
                sb.AppendLine("-- Measured memory BCLK: failed (" + ex.Message + ")");
            }

            double? applied = MemoryBclk;
            sb.AppendLine(applied.HasValue
                ? string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "-- Memory clocks use: {0:0.00} MHz ({1}, last measurement)", applied.Value, MemoryBclkSource)
                : "-- Memory clocks use: the PLL register or 100 MHz (no measured value)");
            sb.AppendLine("-- Last measurement: " + (MemoryBclkStatus ?? "not run (yet)"));

            return sb.ToString();
        }

        // TSC read before and after the timer read; their midpoint pairs the two clocks. The latency is the same at both ends of a sample.
        private bool ReadTscAndTimer(out ulong tsc, out uint timer)
        {
            ulong a = Opcode.Rdtsc();
            bool ok = _mmio.TryReadAcpiPmTimer(out timer);
            ulong b = Opcode.Rdtsc();
            tsc = a / 2 + b / 2;
            return ok;
        }

        // Only one clock measurement at a time: two of them would start and stop the UMC counter under each other
        private static readonly object MeasurementLock = new object();

        /// <summary>Length of one sample of the measurement during start and of the periodic one.</summary>
        public const int StartupSampleMs = 100;
        /// <summary>Samples of the measurement during start and of the periodic one.</summary>
        public const int StartupSamples = 4;

        private System.Threading.Thread _memoryBclkThread;
        private readonly System.Threading.AutoResetEvent _memoryBclkWake = new System.Threading.AutoResetEvent(false);

        private readonly object _memoryBclkLock = new object();
        private double? _measuredMemoryBclk;
        private string _measuredMemoryBclkSource;

        /// <summary>
        /// Raised, on the measuring thread, when <see cref="RefreshMemoryBclk"/> has changed the memory side reference clock
        /// (<see cref="MemoryBclk"/>): MEMCLK, UCLK, FCLK and <see cref="BaseDramTimings.Frequency"/> follow it from then on.
        /// </summary>
        public event EventHandler MemoryBclkChanged;

        /// <summary>The measured memory side reference clock in MHz, null until measured or when it could not be.</summary>
        public double? MemoryBclk => MeasuredMemoryBclkValue;

        /// <summary>Where <see cref="MemoryBclk"/> comes from ("UMC clock counter", "TSC"), null when there is no value.</summary>
        public string MemoryBclkSource
        {
            get { lock (_memoryBclkLock) return _measuredMemoryBclkSource; }
        }

        /// <summary>How the last <see cref="RefreshMemoryBclk"/> went, for the debug report; null before the first one.</summary>
        public string MemoryBclkStatus { get; private set; }

        private double? MeasuredMemoryBclkValue
        {
            get { lock (_memoryBclkLock) return _measuredMemoryBclk; }
        }

        private void SetMeasuredMemoryBclk(double? mhz, string source)
        {
            lock (_memoryBclkLock)
            {
                _measuredMemoryBclk = mhz;
                _measuredMemoryBclkSource = mhz.HasValue ? source : null;
            }
        }

        /// <summary>
        /// Reference clock of MEMCLK, UCLK and FCLK in MHz: the measured one when there is one, else the PLL register
        /// (<see cref="GetBclk"/>), which only covers the CPU side and is not readable in every clocking mode.
        /// </summary>
        public double? GetMemoryBclk() => MeasuredMemoryBclkValue ?? GetBclk();

        /// <summary>Measures again on the background thread now, e.g. after <see cref="SetBclk"/>.</summary>
        public void MeasureInBackground()
        {
            if (!_mmio.IsAvailable || _closed)
                return;

            System.Threading.Thread running = _memoryBclkThread;
            if (running != null && running.IsAlive)
                _memoryBclkWake.Set();
            else
                StartBackgroundMeasurement(true);
        }

        /// <summary>
        /// Starts the background thread that measures the memory side reference clock every
        /// <see cref="CoreOptions.MemoryBclkRefreshIntervalMs"/> (or only when woken, with 0), and right away with
        /// <paramref name="measureNow"/>. Called once by <see cref="Cpu"/> after start.
        /// </summary>
        internal void StartBackgroundMeasurement(bool measureNow)
        {
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    // InpOut can still be opening right after start (the main window waits up to 5 s for it)
                    for (int i = 0; i < 100 && !_closed && !IsInpOutOpen; i++)
                        System.Threading.Thread.Sleep(100);

                    bool measure = measureNow;
                    while (!_closed)
                    {
                        if (measure)
                        {
                            try
                            {
                                // A failed periodic measurement (heavy load, bus contention) keeps the last value
                                RefreshMemoryBclk(StartupSampleMs, StartupSamples, MemoryBclk.HasValue);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Memory BCLK measurement: " + ex.Message);
                            }
                        }

                        int interval = _cpu.Options.MemoryBclkRefreshIntervalMs;
                        if (interval > 0)
                            _memoryBclkWake.WaitOne(interval, false);
                        else
                            _memoryBclkWake.WaitOne();

                        measure = true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Memory BCLK thread: " + ex.Message);
                }
            })
            {
                IsBackground = true,
                Name = "Memory BCLK measurement",
            };

            _memoryBclkThread = thread;
            thread.Start();
        }

        /// <summary>
        /// Measures the reference clock of the memory side and makes MEMCLK, UCLK, FCLK and the timings in ns use it
        /// (<see cref="GetMemoryBclk"/>). Blocks for about samples * durationMs (up to 3 tries). <see cref="Cpu"/> runs it
        /// during start, before the first power table read (<see cref="CoreOptions.MeasureMemoryBclk"/>; on a background thread
        /// when InpOut is not open yet), then on a background thread every <see cref="CoreOptions.MemoryBclkRefreshIntervalMs"/>
        /// and after <see cref="SetBclk"/>, as tools can change BCLK while running.
        /// <list type="bullet">
        /// <item>The UMC clock counter where there is one (AM5: Raphael, Granite Ridge), which also sees asynchronous
        /// eCLK mode. At least two samples must agree, the others are low while MEMCLK is gated.</item>
        /// <item>Else the TSC measurement (<see cref="MeasureBclk"/>): AM4 and the APUs, where the memory side runs from
        /// the CPU reference clock. Not in asynchronous eCLK mode, and not when the PLL register is readable and
        /// disagrees by more than 1 MHz (P0 changed while running, the TSC still runs at the old P0).</item>
        /// </list>
        /// The value is rounded to 0.01 MHz, as both methods resolve it to a few kHz. Null when neither applies; the PLL
        /// register is used then, as before.
        /// </summary>
        /// <param name="durationMs">Length of one sample.</param>
        /// <param name="samples">Samples of the UMC counter; the TSC fallback takes 3.</param>
        /// <param name="keepOnFailure">Keep the current value when this measurement gives none (periodic measurements).</param>
        /// <param name="progress">Called with the method about to be tried ("UMC clock counter", "TSC"), e.g. for a splash screen;
        /// null for none.</param>
        /// <returns>The value in use afterwards.</returns>
        public double? RefreshMemoryBclk(int durationMs = 200, int samples = 5, bool keepOnFailure = false,
            Action<string> progress = null)
        {
            if (!_mmio.IsAvailable || _closed)
                return null;

            double? bclk = null;
            string source = null;
            var status = new StringBuilder();
            System.Globalization.CultureInfo ic = System.Globalization.CultureInfo.InvariantCulture;

            // A busy system can leave too few ungated samples; try again before falling back
            for (int attempt = 1; attempt <= 3 && !bclk.HasValue && !_closed; attempt++)
            {
                progress?.Invoke(attempt == 1 ? "UMC clock counter" : "UMC clock counter, attempt " + attempt);
                MemoryBclkMeasurement umc = MeasureMemoryBclk(durationMs, samples);
                if (umc.BclkMhz.HasValue && umc.AgreeingSamples >= 2)
                {
                    bclk = umc.BclkMhz;
                    source = "UMC clock counter";
                    status.AppendFormat(ic, "UMC attempt {0}: {1:0.000} MHz, {2} of {3} samples agree", attempt, umc.BclkMhz.Value,
                        umc.AgreeingSamples, umc.Samples);
                }
                else if (umc.BclkMhz.HasValue)
                {
                    status.AppendFormat(ic, "UMC attempt {0}: only {1} of {2} samples agree; ", attempt, umc.AgreeingSamples, umc.Samples);
                }
                else
                {
                    // Not there or not usable (AM4, no InpOut): no point in trying again
                    status.AppendFormat(ic, "UMC: {0}; ", umc.Error);
                    break;
                }
            }

            if (!bclk.HasValue)
            {
                if (GetEclkMode() == EclkMode.ASYNC)
                {
                    status.Append("TSC not used in asynchronous eCLK mode");
                }
                else
                {
                    progress?.Invoke("TSC");
                    BclkMeasurement tsc = MeasureBclk(durationMs, 3);
                    double? register = GetBclk();
                    if (!tsc.BclkMhz.HasValue)
                    {
                        status.Append("TSC: " + tsc.Error);
                    }
                    else if (tsc.SpreadMhz >= 0.1)
                    {
                        status.AppendFormat(ic, "TSC: spread {0:0.000} MHz too large", tsc.SpreadMhz);
                    }
                    else if (register.HasValue && Math.Abs(register.Value - tsc.BclkMhz.Value) > 1.0)
                    {
                        status.AppendFormat(ic, "TSC: {0:0.000} MHz disagrees with the PLL register ({1:0.00} MHz)",
                            tsc.BclkMhz.Value, register.Value);
                    }
                    else
                    {
                        bclk = tsc.BclkMhz;
                        source = "TSC";
                        status.AppendFormat(ic, "TSC: {0:0.000} MHz", tsc.BclkMhz.Value);
                    }
                }
            }

            if (bclk.HasValue && (bclk.Value < 80 || bclk.Value > 150))
            {
                status.AppendFormat(ic, "; {0:0.000} MHz is out of range", bclk.Value);
                bclk = null;
            }

            if (bclk.HasValue)
                bclk = Math.Round(bclk.Value, 2);

            double? previous = MeasuredMemoryBclkValue;
            if (!bclk.HasValue && keepOnFailure && previous.HasValue)
            {
                status.AppendFormat(ic, "; kept {0:0.00} MHz", previous.Value);
                MemoryBclkStatus = status.ToString();
                return previous;
            }

            MemoryBclkStatus = status.ToString();

            if (_closed)
                return null;

            SetMeasuredMemoryBclk(bclk, source);

            // Only a real change: the periodic measurement repeats the same value most of the time
            if (bclk.HasValue != previous.HasValue || (bclk.HasValue && Math.Abs(bclk.Value - previous.Value) >= 0.005))
                MemoryBclkChanged?.Invoke(this, EventArgs.Empty);

            return bclk;
        }

        // UMC clock cycle counter, PPR 55901 (UMC::PerfMonCtlClk / PerfMonCtrClkLo / PerfMonCtrClkHi) and PPR 57238 Vol 4,
        // 9.2 [UMC Performance Monitors]: a dedicated 48-bit counter of MEMCLK cycles.
        private const uint UMC_SMN_BASE = 0x50000;
        private const uint UMC_PERFMON_CTL_CLK = 0xD00;
        private const uint UMC_PERFMON_CTR_CLK_LO = 0xD04;
        private const uint UMC_PERFMON_CTR_CLK_HI = 0xD08;
        private const uint UMC_CTR_CLK_EN = 1u << 31;
        private const uint UMC_GLBL_MON_EN = 1u << 25;
        private const int UMC_MAX_INSTANCES = 12;

        /// <summary>MHz within which a sample of <see cref="MeasureMemoryBclk"/> counts as agreeing with the highest one.</summary>
        public const double MemoryBclkTolerance = 0.05;

        /// <summary>
        /// Measures the reference clock of the memory side, which <see cref="MeasureBclk"/> does not see in asynchronous eCLK mode
        /// (the cores on one reference clock, the SoC - memory, fabric and PCIe - on another). The UMC clock cycle counter is
        /// timed against the ACPI timer: memory BCLK = counted MHz * divider / (MEMCLK ratio * 100) * 100.
        /// The divider is 1 or 2, whichever brings the result closest to the nominal clock, as the counter may count MEMCLK / 2.
        /// The counter stops while MEMCLK is gated (memory power-down / self-refresh when idle), which can only make a sample low,
        /// so the highest sample is the result and the report lists them all.
        /// <para>
        /// The counter is off after reset. PawnIO can read the UMC registers but not write them, so it is switched on through
        /// InpOut and the ECAM range (<see cref="Cpu.IoWriteSmn"/>) and the control register is restored afterwards; a counter that is already
        /// running (another tool) is only read. Family 19h and later, where the register reads with GlblMonEn set
        /// (Raphael, Granite Ridge); it reads 0 on AM4. Blocks for about samples * durationMs, call it from a background thread.
        /// </para>
        /// </summary>
        public MemoryBclkMeasurement MeasureMemoryBclk(int durationMs = 200, int samples = 5)
        {
            lock (MeasurementLock)
                return MeasureMemoryBclkLocked(durationMs, samples);
        }

        private MemoryBclkMeasurement MeasureMemoryBclkLocked(int durationMs, int samples)
        {
            MemoryBclkMeasurement r = new MemoryBclkMeasurement();

            if (!_mmio.IsAvailable || !IsInpOutOpen)
            {
                r.Error = "InpOut is not available (needed to start the counter and to read the ACPI timer).";
                return r;
            }

            if (!_cpu.IsAmdModuleLoaded)
            {
                r.Error = "SMN access is not available.";
                return r;
            }

            if ((int)_family < (int)Cpu.Family.FAMILY_19H)
            {
                r.Error = "Unsupported CPU family.";
                return r;
            }

            if (durationMs < 50) durationMs = 50;
            if (samples < 1) samples = 1;

            int umc = FindActiveUmc();
            if (umc < 0)
            {
                r.Error = "No active memory channel found.";
                return r;
            }

            r.Umc = umc;
            uint umcBase = ((uint)umc << 20) | UMC_SMN_BASE;

            double nominal = GetNominalMemClkMhz(umc);
            if (nominal <= 0)
            {
                r.Error = "The memory clock ratio is not known.";
                return r;
            }

            r.NominalMemClkMhz = nominal;

            uint ctl = 0;
            if (!_cpu.ReadDwordEx(umcBase | UMC_PERFMON_CTL_CLK, ref ctl))
            {
                r.Error = "Could not read the UMC counter control register.";
                return r;
            }

            if ((ctl & UMC_GLBL_MON_EN) == 0)
            {
                r.Error = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "The UMC clock counter is not available (PerfMonCtlClk = 0x{0:X8}).", ctl);
                return r;
            }

            bool started = false;
            try
            {
                if ((ctl & UMC_CTR_CLK_EN) == 0)
                {
                    uint address = umcBase | UMC_PERFMON_CTL_CLK;
                    bool written = _cpu.TryIoWriteSmn(address, ctl | UMC_CTR_CLK_EN, out string writeError);
                    // Restored in finally also when the write reports a failure, it may still have reached the register
                    started = true;

                    uint check = 0;
                    bool pawnRead = _cpu.ReadDwordEx(address, ref check);
                    if (!written || !pawnRead || (check & UMC_CTR_CLK_EN) == 0)
                    {
                        // Which step failed: the InpOut write, or the register ignoring it (then the InpOut read back
                        // tells whether the SMN path through InpOut works at all)
                        string inpOutRead = _cpu.TryIoReadSmn(address, out uint ioCheck, out string readError)
                            ? string.Format(System.Globalization.CultureInfo.InvariantCulture, "0x{0:X8}", ioCheck)
                            : "failed, " + readError;
                        r.Error = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "Could not start the UMC clock counter through InpOut. Write: {0}; read back through PawnIO: {1}; through InpOut: {2}",
                            written ? "done" : "failed, " + writeError,
                            pawnRead ? string.Format(System.Globalization.CultureInfo.InvariantCulture, "0x{0:X8}", check) : "failed",
                            inpOutRead);
                        return r;
                    }
                }

                double[] results = new double[samples];
                double[] counterMhz = new double[samples];
                int divider = 1;

                for (int i = 0; i < samples; i++)
                {
                    if (!ReadUmcClockAndTimer(umcBase, out ulong c0, out uint t0))
                    {
                        r.Error = "Could not read the UMC clock counter or the ACPI timer.";
                        return r;
                    }

                    System.Threading.Thread.Sleep(durationMs);

                    if (!ReadUmcClockAndTimer(umcBase, out ulong c1, out uint t1))
                    {
                        r.Error = "Could not read the UMC clock counter or the ACPI timer.";
                        return r;
                    }

                    uint ticks = unchecked(t1 - t0);
                    if (ticks == 0 || c1 <= c0)
                    {
                        r.Error = "The ACPI timer or the UMC clock counter did not advance.";
                        return r;
                    }

                    counterMhz[i] = (c1 - c0) / (ticks / Mmio.AcpiPmTimerHz) / 1e6;

                }

                // Highest sample: gating only loses counts
                int best = 0;
                for (int i = 1; i < samples; i++)
                {
                    if (counterMhz[i] > counterMhz[best])
                        best = i;
                }

                // MEMCLK or MEMCLK / 2: the reference clock is within a few percent of 100 MHz, the two candidates 2x apart
                divider = Math.Abs(counterMhz[best] * 2 / nominal - 1) < Math.Abs(counterMhz[best] / nominal - 1) ? 2 : 1;

                for (int i = 0; i < samples; i++)
                    results[i] = counterMhz[i] * divider / nominal * 100.0;

                double bclk = results[best];
                r.CounterMhz = counterMhz[best];
                r.SampleMhz = results;

                if (bclk < 50 || bclk > 200)
                {
                    r.Error = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "The counted rate ({0:0.0} MHz) does not match MEMCLK ({1:0} MHz) or MEMCLK / 2.", r.CounterMhz, nominal);
                    return r;
                }

                int agreeing = 0;
                foreach (double sample in results)
                {
                    if (bclk - sample <= MemoryBclkTolerance)
                        agreeing++;
                }

                r.Samples = samples;
                r.Divider = divider;
                r.BclkMhz = bclk;
                r.AgreeingSamples = agreeing;
                return r;
            }
            finally
            {
                // Leave the counter as it was found; the count itself is kept (it only grows while enabled)
                if (started && !_cpu.IoWriteSmn(umcBase | UMC_PERFMON_CTL_CLK, ctl))
                    Debug.WriteLine("MeasureMemoryBclk: could not restore UMC::PerfMonCtlClk.");
            }
        }

        /// <summary>The first UMC with a channel and a DIMM, as the debug report finds them; -1 when there is none.</summary>
        private int FindActiveUmc()
        {
            for (int i = 0; i < UMC_MAX_INSTANCES; i++)
            {
                uint offset = (uint)i << 20;
                uint channel = 0, dimm0 = 0, dimm1 = 0;
                if (!_cpu.ReadDwordEx(offset | 0x50DF0, ref channel) || channel == 0xFFFFFFFF)
                    continue;

                if (Utils.GetBits(channel, 19, 1) != 0)
                    continue;

                _cpu.ReadDwordEx(offset | 0x50000, ref dimm0);
                _cpu.ReadDwordEx(offset | 0x50008, ref dimm1);
                if ((dimm0 & 1) == 1 || (dimm1 & 1) == 1)
                    return i;
            }

            return -1;
        }

        /// <summary>MEMCLK in MHz at a 100 MHz reference clock: the memory clock ratio of the channel (or the first one) * 100.</summary>
        private double GetNominalMemClkMhz(int umc)
        {
            List<KeyValuePair<uint, BaseDramTimings>> timings = _cpu.memoryConfig?.Timings;
            if (timings == null || timings.Count == 0)
                return 0;

            BaseDramTimings selected = null;
            foreach (KeyValuePair<uint, BaseDramTimings> entry in timings)
            {
                if (entry.Key == (uint)umc || entry.Key == (uint)umc << 20)
                {
                    selected = entry.Value;
                    break;
                }
            }

            if (selected == null)
                selected = timings[0].Value;

            return selected == null ? 0 : selected.Ratio * 100.0;
        }

        // ACPI timer ticks (3.58 MHz) allowed between the two timer reads around a counter read: 100 us, at most 0.025 %
        // pairing error over a 200 ms sample
        private const uint MaxCounterPairTicks = 358;

        // Counter read between two timer reads, all under one hold of the PCI bus lock: taken per read, another tool
        // holding the lock between them would put an unknown wait into the sample. The timer midpoint pairs the two.
        // A pair that took too long is read again.
        private bool ReadUmcClockAndTimer(uint umcBase, out ulong counter, out uint timer)
        {
            counter = 0;
            timer = 0;

            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (!Mutexes.WaitPciBus(5000))
                    return false;

                uint a, b;
                try
                {
                    if (!_mmio.TryReadAcpiPmTimer(out a) ||
                        !ReadUmcClockCounterNoLock(umcBase, out counter) ||
                        !_mmio.TryReadAcpiPmTimer(out b))
                        return false;
                }
                finally
                {
                    Mutexes.ReleasePciBus();
                }

                uint window = unchecked(b - a);
                timer = unchecked(a + window / 2);
                if (window <= MaxCounterPairTicks)
                    return true;
            }

            // Still slow after the retries: keep the last pair, the agreement check rejects it if it is off
            return true;
        }

        // Low half first, as the PPR requires. The low half wraps about once a second at DDR5 speeds: if the high half
        // changed between two reads of the pair, the second pair is the one read after the carry. Caller holds the PCI bus lock.
        private bool ReadUmcClockCounterNoLock(uint umcBase, out ulong counter)
        {
            counter = 0;
            uint lo0 = 0, hi0 = 0, lo1 = 0, hi1 = 0;

            if (!_cpu.ReadDwordExNoLock(umcBase | UMC_PERFMON_CTR_CLK_LO, ref lo0, 3) ||
                !_cpu.ReadDwordExNoLock(umcBase | UMC_PERFMON_CTR_CLK_HI, ref hi0, 3) ||
                !_cpu.ReadDwordExNoLock(umcBase | UMC_PERFMON_CTR_CLK_LO, ref lo1, 3) ||
                !_cpu.ReadDwordExNoLock(umcBase | UMC_PERFMON_CTR_CLK_HI, ref hi1, 3))
                return false;

            if ((hi0 & 0xFFFF) == (hi1 & 0xFFFF))
                counter = ((ulong)(hi0 & 0xFFFF) << 32) | lo0;
            else
                counter = ((ulong)(hi1 & 0xFFFF) << 32) | lo1;

            return true;
        }

    }
}

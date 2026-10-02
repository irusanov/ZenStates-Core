using ZenStates.Core.Hardware.DRAM.DDR5.Spd;

namespace ZenStates.Core.Hardware.DRAM.DDR5.Pmic
{
    /// <summary>
    /// Picks the PMIC class for a model (the type the SPD lists) and vendor (R0x3C / R0x3D). The one place to add a new
    /// model or vendor variant.
    /// </summary>
    public static class Ddr5PmicFactory
    {
        /// <summary>The PMIC class for this model and vendor, or null when the model is not supported.</summary>
        public static Ddr5Pmic Create(Ddr5PmicType type, byte vendorBank, byte vendorCode)
        {
            switch (type)
            {
                case Ddr5PmicType.PMIC5100:
                    return Pmic5100Richtek.IsRichtek(vendorBank, vendorCode) ? new Pmic5100Richtek() : new Pmic5100();
                default:
                    return null;
            }
        }

        public static bool IsSupported(Ddr5PmicType type)
        {
            return type == Ddr5PmicType.PMIC5100;
        }
    }
}

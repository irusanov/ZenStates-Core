using ZenStates.Core.Hardware.Motherboard.Lpc;

namespace ZenStates.Core.Hardware.Mock
{
    /// <summary>
    /// One SuperIO chip as a debug report dumped it: the chip's identity and its hardware monitor
    /// registers. <see cref="CreateChip"/> replays it through the regular chip class, so voltages,
    /// temperatures and fans decode exactly as they did on the captured machine.
    /// </summary>
    public sealed class SuperIoDump
    {
        /// <summary>Chip class the report named in its "LPC ..." line, e.g. "Nct677X" or "IT87XX".</summary>
        public string ChipClass { get; internal set; }

        /// <summary>Chip ID as printed, i.e. the <see cref="Chip"/> enum value.</summary>
        public ushort ChipId { get; internal set; }

        /// <summary>"Chip Revision" (Nuvoton) or "Chip Version" (ITE).</summary>
        public byte Revision { get; internal set; }

        public ushort BaseAddress { get; internal set; }

        public ushort GpioAddress { get; internal set; }

        /// <summary>
        /// Captured registers. Nuvoton addresses already carry the bank in their high byte; ITE
        /// registers are keyed (bank &lt;&lt; 8) | register, since the report dumps each bank separately.
        /// </summary>
        public VirtualRegisters Registers { get; } = new VirtualRegisters("SuperIO");

        public Chip Chip => (Chip)ChipId;

        /// <summary>
        /// The chip replayed from <see cref="Registers"/>, or null for a chip class that has no
        /// replay support (Winbond/Fintek, which AMD boards rarely carry).
        /// </summary>
        public ISuperIO CreateChip()
        {
            switch (ChipClass)
            {
                case "Nct677X":
                    return new Nct677X(Chip, Revision, BaseAddress, Registers);
                case "IT87XX":
                    return new IT87XX(Chip, BaseAddress, GpioAddress, Revision, Registers);
                default:
                    return null;
            }
        }
    }
}

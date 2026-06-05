using System;

namespace ZenStates.Core.SMUCommands
{
    internal class SetCurveShaperMargin : BaseSMUCommand
    {
        public SetCurveShaperMargin(SMU smu) : base(smu) { }
        public CmdResult Execute(int marginHigh = 0, int marginMedium = 0, int marginLow = 0, int frequencyTier = 0)
        {
            if (frequencyTier < 0 || frequencyTier > 4)
                throw new ArgumentOutOfRangeException(nameof(frequencyTier), "Frequency tier must be between 0 and 4.");
                
            if (CanExecute())
            {
                result.args[0] =
                    ((uint)EncodeMargin(marginHigh) << 24) |
                    ((uint)EncodeMargin(marginMedium) << 16) |
                    ((uint)EncodeMargin(marginLow) << 8) |
                    (1u << 7) |
                    ((uint)frequencyTier & 0x7Fu);

                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_SetCurveShaperMargin, ref result.args);
            }

            return base.Execute();
        }

        private static int EncodeMargin(int margin)
        {
            if (margin < -50)
                margin = -50;
            else if (margin > 30)
                margin = 30;

            return unchecked((byte)(sbyte)margin);
        }
    }
}

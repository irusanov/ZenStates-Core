namespace ZenStates.Core.SMUCommands
{
    internal class SetFixedGfxClk : BaseSMUCommand
    {
        public SetFixedGfxClk(SMU smuInstance) : base(smuInstance) { }

        public CmdResult Execute(uint freq)
        {
            if (CanExecute() && smu.Rsmu.SMU_MSG_SetFixedGfxClkFreq > 0)
            {
                result.args[0] = freq;
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_SetFixedGfxClkFreq, ref result.args);
            }
            return base.Execute();
        }
    }
}

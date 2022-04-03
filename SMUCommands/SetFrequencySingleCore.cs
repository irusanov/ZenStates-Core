namespace ZenStates.Core.SMUCommands
{
    internal class SetFrequencySingleCore : BaseSMUCommand
    {
        public SetFrequencySingleCore(SMU smu) : base(smu) { }
        public CmdResult Execute(uint coreMask, uint frequency)
        {
            if (CanExecute())
            {
                result.args[0] = coreMask | frequency & 0xfffff;
                // TODO: Add Manual OC mode
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_SetOverclockFrequencyPerCore, ref result.args);
            }

            return base.Execute();
        }
    }
}

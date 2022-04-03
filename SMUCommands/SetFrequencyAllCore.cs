namespace ZenStates.Core.SMUCommands
{
    internal class SetFrequencyAllCore : BaseSMUCommand
    {
        public SetFrequencyAllCore(SMU smu) : base(smu) { }
        public CmdResult Execute(uint frequency)
        {
            if (CanExecute())
            {
                result.args[0] = frequency & 0xfffff;
                // TODO: Add Manual OC mode
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_SetOverclockFrequencyAllCores, ref result.args);
            }

            return base.Execute();
        }
    }
}

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
                // TODO: Add lo and hi frequency limits
                uint cmd = smu.Rsmu.SMU_MSG_SetOverclockFrequencyPerCore;
                if (cmd != 0)
                    result.status = smu.SendRsmuCommand(cmd, ref result.args);
                else
                    result.status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_SetOverclockFrequencyPerCore, ref result.args);
            }

            return base.Execute();
        }
    }
}

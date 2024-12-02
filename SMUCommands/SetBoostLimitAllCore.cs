namespace ZenStates.Core.SMUCommands
{
    internal class SetBoostLimitAllCore : BaseSMUCommand
    {
        public SetBoostLimitAllCore(SMU smu) : base(smu) { }
        public CmdResult Execute(uint frequency)
        {
            if (CanExecute())
            {
                result.args[0] = frequency & 0xfffff;
                uint cmd = smu.Rsmu.SMU_MSG_SetBoostLimitFrequencyAllCores;
                if (cmd != 0)
                    result.status = smu.SendRsmuCommand(cmd, ref result.args);
                else
                    result.status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_SetBoostLimitFrequencyAllCores, ref result.args);
            }

            return base.Execute();
        }
    }
}

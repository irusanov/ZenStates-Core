namespace ZenStates.Core.SMUCommands
{
    internal class GetBoostLimitFrequency : BaseSMUCommand
    {
        public GetBoostLimitFrequency(SMU smu) : base(smu) { }
        public override CmdResult Execute()
        {
            if (CanExecute())
            {
                if (smu.SMU_TYPE == SMU.SmuType.TYPE_APU0)
                {
                    result.args[0] = 3; // Fix for older APUs
                }

                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetBoostLimitFrequency, ref result.args);
            }

            return base.Execute();
        }
    }
}

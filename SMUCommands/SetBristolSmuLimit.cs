namespace ZenStates.Core.SMUCommands
{
    internal class SetBristolSmuLimit : BaseSMUCommand
    {
        public SetBristolSmuLimit(SMU smu) : base(smu) { }
        public CmdResult Execute(uint cmdMp1 = 0, uint limit1 = 0U, uint limit2 = 0U)
        {
            if (CanExecute() && smu.SMU_TYPE == SMU.SmuType.TYPE_CPU9)
            {
                result.args[0] = limit1 * 1000;
                result.args[1] = limit2 * 1000;

                if (cmdMp1 != smu.Mp1Smu.SMU_MSG_SetSlowLimit)
                {
                    result.args[2] = limit2 * 1000;
                    // Second arg is usually Soc limit, part of first arg,
                    // SetSlowLimit require only 2 arguments
                }

                if (cmdMp1 != 0)
                    result.status = smu.SendMp1Command(cmdMp1, ref result.args);

            }

            return base.Execute();
        }
    }
}

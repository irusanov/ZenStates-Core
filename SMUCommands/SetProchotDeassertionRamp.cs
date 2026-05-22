namespace ZenStates.Core.SMUCommands
{
    internal class SetProchotDeassertionRamp : BaseSMUCommand
    {
        public SetProchotDeassertionRamp(SMU smu) : base(smu) { }
        public CmdResult Execute(uint cmdRsmu, uint cmdMp1 = 0, uint arg = 0U)
        {
            if (CanExecute())
            {
                bool isBristol = smu.SMU_TYPE == SMU.SmuType.TYPE_CPU9;
                if (isBristol && arg > 100)
                    arg = 100;

                result.args[0] = arg;

                SMU.Status status = smu.SendRsmuCommand(cmdRsmu, ref result.args);
                if (status != SMU.Status.OK)
                {
                    result.args = Utils.MakeCmdArgs(arg);
                    status = smu.SendMp1Command(cmdMp1, ref result.args);
                }

                result.status = status;
            }

            return base.Execute();
        }
    }
}

namespace ZenStates.Core.Hardware.Smu.Commands
{
    internal class SetPowerLimitTime : BaseSMUCommand
    {
        public SetPowerLimitTime(SMU smu) : base(smu) { }        
        
        public override bool CanExecute()
        {
            return base.CanExecute() && smu.SMU_TYPE != SMU.SmuType.TYPE_CPU9;
        }
        
        public CmdResult Execute(uint cmdRsmu, uint cmdMp1 = 0, uint arg = 0U)
        {
            // Smu time limits are different on Bristol
            if (CanExecute())
            {
                result.args[0] = arg;

                // Fix for some mobile APUs, firstly apply MP1 variant
                var olderSmu = smu.SMU_TYPE == SMU.SmuType.TYPE_APU0 ||
                               smu.SMU_TYPE == SMU.SmuType.TYPE_APU1 ||
                               smu.SMU_TYPE == SMU.SmuType.TYPE_CPU9;

                SMU.Status status;

                if (olderSmu)
                {
                    status = cmdMp1 != 0 ? smu.SendMp1Command(cmdMp1, ref result.args)
                        : smu.SendRsmuCommand(cmdRsmu, ref result.args);
                }
                else
                {
                    status = smu.SendRsmuCommand(cmdRsmu, ref result.args);
                    if (status != SMU.Status.OK)
                    {
                        result.args = Utils.MakeCmdArgs(arg);
                        status = smu.SendMp1Command(cmdMp1, ref result.args);
                    }
                }

                result.status = status;
            }

            return base.Execute();
        }
    }
}

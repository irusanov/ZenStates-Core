namespace ZenStates.Core.SMUCommands
{
    internal class SetSmuLimit : BaseSMUCommand
    {
        public SetSmuLimit(SMU smu) : base(smu) { }
        public CmdResult Execute(uint cmdRsmu, uint cmdMp1 = 0, uint arg = 0U)
        {
            // Smu limits are different on Bristol, but SetFastLimit works as on Ryzen
            if (CanExecute() && (smu.SMU_TYPE != SMU.SmuType.TYPE_CPU9 || cmdMp1 == smu.Mp1Smu.SMU_MSG_SetFastLimit))
            {
                var limit = arg * 1000;
                result.args[0] = limit;

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
                        result.args = Utils.MakeCmdArgs(limit);
                        status = smu.SendMp1Command(cmdMp1, ref result.args);
                    }
                }

                result.status = status;
            }

            return base.Execute();
        }
    }
}

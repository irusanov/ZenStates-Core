namespace ZenStates.Core.SMUCommands
{
    internal class SetOcMode : BaseSMUCommand
    {
        public SetOcMode(SMU smu) : base(smu) { }

        // TODO: Set OC vid based on current PState0 VID
        public CmdResult Execute(bool enabled, uint arg = 0U)
        {
            if (CanExecute())
            {
                bool olderSmu = smu.SMU_TYPE == SMU.SmuType.TYPE_APU0 || smu.SMU_TYPE == SMU.SmuType.TYPE_CPU0;

                if (olderSmu && enabled)
                    arg = 1;

                uint cmd = enabled ? smu.Rsmu.SMU_MSG_EnableOcMode : smu.Rsmu.SMU_MSG_DisableOcMode;
                uint fallback = enabled ? smu.Mp1Smu.SMU_MSG_EnableOcMode : smu.Mp1Smu.SMU_MSG_DisableOcMode;

                result.args[0] = arg;

                SMU.Status status = SMU.Status.UNKNOWN_CMD;

                if (olderSmu)
                {
                    // Apply BOTH commands: Disable Prochot (on supported systems), enable or disable prochot volt/freq override
                    smu.SendRsmuCommand(cmd, ref result.args);
                    // Reset args for the second command
                    result.args = Utils.MakeCmdArgs(arg);
                    status = smu.SendMp1Command(fallback, ref result.args);
                }
                else
                {
                    // Apply only one command, if failed apply fallback
                    if (cmd != 0)
                    {
                        status = smu.SendRsmuCommand(cmd, ref result.args);

                        if (status != SMU.Status.OK && fallback != 0)
                        {
                            result.args[0] = arg;
                            status = smu.SendMp1Command(fallback, ref result.args);
                        }
                    }
                    else if (fallback != 0)
                    {
                        status = smu.SendMp1Command(fallback, ref result.args);
                    }
                }

                result.status = status;

                // Reset the scalar to 1.0 when disabling OC mode. Auto-reset seems to be broken for some SMU versions
                // The PBO Scalar is used to get the OC Mode (scalar = 0)
                if (!enabled && result.Success)
                    new SetPBOScalar(smu).Execute(1);
            }

            return base.Execute();
        }
    }
}

namespace ZenStates.Core.Hardware.Smu.Commands
{
    internal class SetOcMode : BaseSMUCommand
    {
        public SetOcMode(SMU smu) : base(smu) { }

        public CmdResult Execute(bool enabled, uint arg = 0U)
        {
            if (!CanExecute())
                return base.Execute();

            result.args[0] = arg;

            switch (smu.SMU_TYPE)
            {
                case SMU.SmuType.TYPE_CPU9:
                    result.status = ExecuteBristolOcMode(enabled);
                    break;
                case SMU.SmuType.TYPE_APU0:
                case SMU.SmuType.TYPE_CPU0:
                    result.status = ExecuteLegacyOcMode(enabled, arg);
                    break;
                default:
                    result.status = ExecuteOcMode(enabled, arg);
                    break;
            };

            // Reset the scalar to 1.0 when disabling OC mode (auto-reset is broken on some SMU fw)
            if (!enabled && result.Success && smu.SMU_TYPE != SMU.SmuType.TYPE_CPU9)
                new SetPBOScalar(smu).Execute(1);

            return base.Execute();
        }

        private SMU.Status ExecuteBristolOcMode(bool enabled)
        {
            uint cmd = enabled ? smu.GpuMb.SMU_MSG_EnableOcMode : smu.GpuMb.SMU_MSG_DisableOcMode;
            var status = smu.SendGpuMbCommand(cmd, ref result.args);

            if (status != SMU.Status.OK && enabled)
                status = smu.SendGpuMbCommand(smu.GpuMb.SMU_MSG_EnableOcModeAlt, ref result.args);

            return status;
        }

        private SMU.Status ExecuteLegacyOcMode(bool enabled, uint arg)
        {
            if (enabled)
                result.args[0] = 1;

            uint cmd = enabled ? smu.Rsmu.SMU_MSG_EnableOcMode : smu.Rsmu.SMU_MSG_DisableOcMode;
            uint fallback = enabled ? smu.Mp1Smu.SMU_MSG_EnableOcMode : smu.Mp1Smu.SMU_MSG_DisableOcMode;

            // Apply BOTH commands: Disable PROCHOT + enable/disable volt/freq override
            smu.SendRsmuCommand(cmd, ref result.args);

            result.args = Utils.MakeCmdArgs(enabled ? 1U : arg);
            return smu.SendMp1Command(fallback, ref result.args);
        }

        private SMU.Status ExecuteOcMode(bool enabled, uint arg)
        {
            uint cmd = enabled ? smu.Rsmu.SMU_MSG_EnableOcMode : smu.Rsmu.SMU_MSG_DisableOcMode;
            uint fallback = enabled ? smu.Mp1Smu.SMU_MSG_EnableOcMode : smu.Mp1Smu.SMU_MSG_DisableOcMode;

            SMU.Status status = SMU.Status.UNKNOWN_CMD;

            if (cmd != 0)
                status = smu.SendRsmuCommand(cmd, ref result.args);

            if ((cmd == 0 || status != SMU.Status.OK) && fallback != 0)
            {
                result.args[0] = arg;
                status = smu.SendMp1Command(fallback, ref result.args);
            }

            return status;
        }
    }
}

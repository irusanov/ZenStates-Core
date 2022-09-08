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
                uint cmd = enabled ? smu.Rsmu.SMU_MSG_EnableOcMode : smu.Rsmu.SMU_MSG_DisableOcMode;
                result.args[0] = arg;

                if (cmd != 0)
                    result.status = smu.SendRsmuCommand(cmd, ref result.args);
                else
                    result.status = smu.SendMp1Command(enabled ? smu.Mp1Smu.SMU_MSG_EnableOcMode : smu.Mp1Smu.SMU_MSG_DisableOcMode, ref result.args);

                // Reset the scalar to 1.0 when disabling OC mode. Auto-reset seems to be broken for some SMU versions
                // The PBO Scalar is used to get the OC Mode (scalar = 0)
                if (!enabled && result.Success)
                    result = new SetPBOScalar(smu).Execute(1);
            }

            return base.Execute();
        }
    }
}

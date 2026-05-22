namespace ZenStates.Core.SMUCommands
{
    internal class SetGfxClkOverdrive : BaseSMUCommand
    {
        public SetGfxClkOverdrive(SMU smuInstance) : base(smuInstance) { }

        public CmdResult Execute(uint freq, uint vid)
        {
            if (CanExecute())
            {
                result.args[0] = (freq & 0xFFFF) | (vid << 16);
                var cmd = smu.Rsmu.SMU_MSG_SetGfxclkOverdriveByFreqVid;
                var status = SMU.Status.UNKNOWN_CMD;
                if (cmd > 0)
                {
                    status = smu.SendRsmuCommand(cmd, ref result.args);
                }
                else if (smu.Mp1Smu.SMU_MSG_SetGfxclkOverdriveByFreqVid > 0)
                {
                    status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_SetGfxclkOverdriveByFreqVid, ref result.args);
                }
                result.status = status;
            }
            return base.Execute();
        }
    }
}

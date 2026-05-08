namespace ZenStates.Core.SMUCommands
{
    internal class SetSmuFeature : BaseSMUCommand
    {
        public SetSmuFeature(SMU smu) : base(smu) { }

        public CmdResult Execute(bool enabled, int bit = 0)
        {
            if (CanExecute() && bit > 0 && bit < 64)
            {
                uint cmd = enabled ? smu.Mp1Smu.SMC_MSG_EnableSmuFeatures : smu.Mp1Smu.SMC_MSG_DisableSmuFeatures;

                result.args[0] = 0;
                result.args[1] = 0;
                if (bit < 32)
                    result.args[0] = 1u << bit; 
                else
                    result.args[1] = 1u << (bit % 32);

                SMU.Status status = SMU.Status.UNKNOWN_CMD;

                if (cmd != 0)
                    status = smu.SendMp1Command(cmd, ref result.args);
                
                result.status = status;
            }

            return base.Execute();
        }
    }
}

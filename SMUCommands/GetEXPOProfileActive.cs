namespace ZenStates.Core.SMUCommands
{
    internal class GetEXPOProfileActive : BaseSMUCommand
    {
        public bool IsEXPOProfileActive { get; protected set; } = false;
        public GetEXPOProfileActive(SMU smu) : base(smu) { }
        public override CmdResult Execute()
        {
            if (CanExecute())
            {
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetEXPOProfileActive, ref result.args);

                if (result.Success)
                {
                    if (smu.SMU_TYPE == SMU.SmuType.TYPE_APU2)
                    {
                        if ((result.args[0] & 0xF) == 2)
                        {
                            IsEXPOProfileActive = (result.args[0] >> 24 & 1) == 1;
                        }
                    }
                    else
                    {
                        IsEXPOProfileActive = (result.args[0] >> 24 & 1) == 1;
                    }
                }
            }

            return base.Execute();
        }
    }
}

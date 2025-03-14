namespace ZenStates.Core.SMUCommands
{
    internal class GetEXPOProfileActive : BaseSMUCommand
    {
        public bool IsEXPOProfileActive { get; protected set; }
        public GetEXPOProfileActive(SMU smu) : base(smu) { }
        public override CmdResult Execute()
        {
            if (CanExecute())
            {
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetEXPOProfileActive, ref result.args);

                if (result.Success)
                {
                    IsEXPOProfileActive = (result.args[0] & 0x1000000) != 0;
                }
            }

            return base.Execute();
        }
    }
}

namespace ZenStates.Core.SMUCommands
{
    internal class GetPsmMarginSingleCore : BaseSMUCommand
    {
        public GetPsmMarginSingleCore(SMU smu) : base(smu) { }
        public CmdResult Execute(uint coreMask)
        {
            if (CanExecute())
            {
                result.args[0] = coreMask & 0xfff00000;

                if (smu.Rsmu.SMU_MSG_GetDldoPsmMargin > 0)
                    result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetDldoPsmMargin, ref result.args);
                else
                    result.status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_GetDldoPsmMargin, ref result.args);
            }

            return base.Execute();
        }
    }
}

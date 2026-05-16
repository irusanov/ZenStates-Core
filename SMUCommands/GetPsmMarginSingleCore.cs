namespace ZenStates.Core.SMUCommands
{
    internal class GetPsmMarginSingleCore : BaseSMUCommand
    {
        public GetPsmMarginSingleCore(SMU smu) : base(smu) { }
        public CmdResult Execute(uint coreMask)
        {
            if (CanExecute())
            {
                result.args[0] = coreMask;

                if (smu.Rsmu.SMU_MSG_GetDldoPsmMargin > 0)
                    result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetDldoPsmMargin, ref result.args);
            }

            return base.Execute();
        }
    }
}

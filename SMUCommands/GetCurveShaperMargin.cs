namespace ZenStates.Core.SMUCommands
{
    internal class GetAllCurveShaperMargins : BaseSMUCommand
    {
        public GetAllCurveShaperMargins(SMU smu) : base(smu) { }
        public override CmdResult Execute()
        {
            if (CanExecute())
            {
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetCurveShaperMargin, ref result.args);
            }

            return base.Execute();
        }
    }
}

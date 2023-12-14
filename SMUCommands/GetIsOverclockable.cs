using System;

namespace ZenStates.Core.SMUCommands
{
    internal class GetIsOverclockable : BaseSMUCommand
    {
        public bool IsOverclockable { get; protected set; }
        public GetIsOverclockable(SMU smu) : base(smu) { }
        public override CmdResult Execute()
        {
            if (CanExecute())
            {
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_IsOverclockable, ref result.args);

                if (result.Success)
                {
                    IsOverclockable = Convert.ToBoolean(result.args[0] & 1);
                }
            }

            return base.Execute();
        }
    }
}

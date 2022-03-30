using System;
using System.Collections.Generic;
using System.Text;

namespace ZenStates.Core.SMUCommands
{
    internal class GetSmuVersion : BaseSMUCommand
    {
        public GetSmuVersion(SMU smu) : base(smu) {}
        public override CmdResult Execute()
        {
            if (CanExecute())
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetSmuVersion, ref result.args);

            return base.Execute();
        }
    }
}

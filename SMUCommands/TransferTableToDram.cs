using System;
using System.Collections.Generic;
using System.Text;

namespace ZenStates.Core.SMUCommands
{
    internal class TransferTableToDram : BaseSMUCommand
    {
        public TransferTableToDram(SMU smu) : base(smu) {}
        public override CmdResult Execute()
        {
            if (CanExecute())
            {
                if (smu.SMU_TYPE == SMU.SmuType.TYPE_APU0)
                {
                    result.args[0] = 3;
                    result.args[1] = 0;
                }
                else
                {
                    result.args[0] = 1;
                    result.args[1] = 1;
                }

                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_TransferTableToDram, ref result.args);
            }

            return base.Execute();
        }
    }
}

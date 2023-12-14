using System.Threading;

namespace ZenStates.Core.SMUCommands
{
    internal class TransferTableToDram : BaseSMUCommand
    {
        public TransferTableToDram(SMU smu) : base(smu) { }
        public override CmdResult Execute()
        {
            if (CanExecute())
            {
                SendCommand();

                // I was getting CMD_REJECTED_PREREQ for some reason, but hwinfo wass able to overcome this
                // Resending the command was enough to clear the status and to successfully pass from the second attempt
                // Add a sleep to give it a better chance
                if (result.status == SMU.Status.CMD_REJECTED_PREREQ)
                {
                    Thread.Sleep(10);
                    result.args = Utils.MakeCmdArgs();
                    SendCommand();
                }
            }

            return base.Execute();
        }

        private void SendCommand()
        {
            if (smu.SMU_TYPE == SMU.SmuType.TYPE_APU0 || smu.SMU_TYPE == SMU.SmuType.TYPE_APU1)
            {
                result.args[0] = 3;
            }
            result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_TransferTableToDram, ref result.args);
        }
    }
}

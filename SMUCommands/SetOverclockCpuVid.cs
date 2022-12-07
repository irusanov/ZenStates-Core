using System;
namespace ZenStates.Core.SMUCommands
{
    internal class SetOverclockCpuVid : BaseSMUCommand
    {
        public SetOverclockCpuVid(SMU smuInstance) : base(smuInstance) { }

        public CmdResult Execute(byte vid)
        {
            if (CanExecute())
            {
                result.args[0] = Convert.ToUInt32(vid);
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_SetOverclockCpuVid, ref result.args);
            }
            return base.Execute();
        }
    }
}

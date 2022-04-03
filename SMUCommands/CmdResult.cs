namespace ZenStates.Core.SMUCommands
{
    internal class CmdResult
    {
        public SMU.Status status;
        public uint[] args;
        public bool Success => status == SMU.Status.OK;

        public CmdResult()
        {
            args = Utils.MakeCmdArgs();
            status = SMU.Status.FAILED;
        }
    }
}

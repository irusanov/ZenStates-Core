namespace ZenStates.Core.SMUCommands
{
    internal class CmdResult
    {
        public SMU.Status status;
        public uint[] args;
        public bool Success => status == SMU.Status.OK;

        public CmdResult(uint maxArgs)
        {
            args = Utils.MakeCmdArgs(maxArgs: maxArgs);
            status = SMU.Status.FAILED;
        }
    }
}

namespace ZenStates.Core.SMUCommands
{
    internal class GetIsOverclockable : BaseSMUCommand
    {
        public Cpu.OcCaps Capabilities { get; protected set; }

        public GetIsOverclockable(SMU smu) : base(smu) { }
        public override CmdResult Execute()
        {
            if (CanExecute())
            {
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_IsOverclockable, ref result.args);

                if (result.Success)
                {
                    Capabilities = new Cpu.OcCaps(result.args[0]);
                }
            }

            return base.Execute();
        }
    }
}

namespace ZenStates.Core.SMUCommands
{
    internal class SetBristolSustainPowerLimit : BaseSMUCommand
    {
        public SetBristolSustainPowerLimit(SMU smu) : base(smu) { }

        public override bool CanExecute()
        {
            return base.CanExecute() && smu.SMU_TYPE == SMU.SmuType.TYPE_CPU9;
        }

        public CmdResult Execute(uint cmdMp1 = 0, uint stapm = 0U, uint stapmTime = 0u)
        {
            if (CanExecute())
            {
                if (stapmTime > 180)
                    stapmTime = 180;

                result.args[0] = stapm * 1000;
                result.args[1] = stapmTime != 0 ? 2u : 0u;
                result.args[2] = stapmTime * 1000;

                if (cmdMp1 != 0)
                    result.status = smu.SendMp1Command(cmdMp1, ref result.args);
            }

            return base.Execute();
        }
    }
}

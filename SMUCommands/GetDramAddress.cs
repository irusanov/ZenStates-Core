namespace ZenStates.Core.SMUCommands
{
    internal class GetDramAddress : BaseSMUCommand
    {
        public GetDramAddress(SMU smu) : base(smu) { }

        public override CmdResult Execute()
        {
            if (CanExecute())
            {
                switch (smu.SMU_TYPE)
                {
                    // SummitRidge, PinnacleRidge, Colfax
                    case SMU.SmuType.TYPE_CPU0:
                    case SMU.SmuType.TYPE_CPU1:
                        result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetDramBaseAddress - 1, ref result.args);
                        if (!result.Success)
                            break;

                        // reset args
                        result.args = Utils.MakeCmdArgs();
                        result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetDramBaseAddress, ref result.args);
                        if (!result.Success)
                            break;

                        // save base address
                        uint address = result.args[0];

                        // reset args
                        result.args = Utils.MakeCmdArgs();
                        result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetDramBaseAddress + 2, ref result.args);

                        // restore base address
                        if (result.Success)
                            result.args = Utils.MakeCmdArgs(address);

                        break;

                    // Matisse, CastlePeak, Rome, Vermeer, Raphael, Chagall?, Milan?
                    case SMU.SmuType.TYPE_CPU2:
                    case SMU.SmuType.TYPE_CPU3:
                    case SMU.SmuType.TYPE_CPU4:
                    // Renoir, Cezanne, VanGogh, Rembrandt
                    case SMU.SmuType.TYPE_APU1:
                    case SMU.SmuType.TYPE_APU2:
                        result.args = Utils.MakeCmdArgs(new uint[2] { 1, 1 });
                        result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetDramBaseAddress, ref result.args);
                        break;

                    // RavenRidge, RavenRidge2, Picasso
                    case SMU.SmuType.TYPE_APU0:
                        uint[] parts = new uint[2];

                        result.args = Utils.MakeCmdArgs(3);
                        result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetDramBaseAddress - 1, ref result.args);
                        if (!result.Success)
                            break;

                        result.args = Utils.MakeCmdArgs(3);
                        result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetDramBaseAddress, ref result.args);
                        if (!result.Success)
                            break;

                        // First base
                        parts[0] = result.args[0];

                        result.args = Utils.MakeCmdArgs(5);
                        result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetDramBaseAddress - 1, ref result.args);
                        if (!result.Success)
                            break;

                        result.args = Utils.MakeCmdArgs(5);
                        result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetDramBaseAddress, ref result.args);
                        if (!result.Success)
                            break;

                        // Second base
                        parts[1] = result.args[0];

                        // return both base addresses
                        result.args = Utils.MakeCmdArgs(new uint[2] { parts[0], parts[1] });
                        break;
                }
            }

            return base.Execute();
        }
    }
}

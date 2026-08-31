using System;

namespace ZenStates.Core.Hardware.Smu.Commands
{
    // Set DLDO Psm margin for all cores
    // CO margin range from -30 to 30 before Zen 4, and from -50 to 50 on Zen 4 and newer
    // Margin arg 16 bits (lowest 16 bits of the command arg)
    // [15-0] CO margin
    internal class SetPsmMarginAllCores : BaseSMUCommand
    {
        public SetPsmMarginAllCores(SMU smu) : base(smu) { }
        private const float MinSafeGuardbandV = 0.020f;
        private const float MillivoltsPerCount = 3.0f;

        public override bool CanExecute()
        {
            return base.CanExecute() && (
                smu.Mp1Smu.SMU_MSG_SetAllDldoPsmMargin > 0 ||
                smu.Rsmu.SMU_MSG_SetAllDldoPsmMargin > 0 ||
                smu.Rsmu.SMU_MSG_SetOverclockCpuVid > 0);
        }

        public CmdResult Execute(int margin)
        {
            if (!CanExecute())
                return base.Execute();

            switch (smu.SMU_TYPE)
            {
                case SMU.SmuType.TYPE_CPU9:
                    result.status = ExecuteBristolPsmMargin(margin);
                    break;

                case SMU.SmuType.TYPE_CPU0:
                    result.status = ExecuteLegacyPsmMargin(margin);
                    break;

                default:
                    result.status = ExecutePsmMargin(margin);
                    break;
            }

            return base.Execute();
        }
        
        private SMU.Status ExecuteBristolPsmMargin(int margin)
        {
            float[] gbvHi = ReadGbv(smu.Mp1Smu.SMU_MSG_GetSecondaryDldoPsmMargin);
            if (gbvHi == null) return SMU.Status.CMD_REJECTED_PREREQ;

            float slopeV = (gbvHi[0] - gbvHi[3]) / 3.0f;
            float baseLineP3 = gbvHi[0] + slopeV;
            float baseLineP2 = baseLineP3 + slopeV;
            float baseLineP1 = baseLineP2 + slopeV;
            float baseLineP0 = baseLineP1 + slopeV;

            float targetOffsetV = margin * MillivoltsPerCount / 1000.0f;

            // Apply Curve Optimizer only to P0–P3, using weighting factors.
            // The weights ensure stability at idle (P3) despite aggressive undervolting at P0
            float newGbvP0 = baseLineP0 + targetOffsetV * 1.00f; // 100% offset
            float newGbvP1 = baseLineP1 + targetOffsetV * 0.75f; // 75%
            float newGbvP2 = baseLineP2 + targetOffsetV * 0.50f; // 50%
            float newGbvP3 = baseLineP3 + targetOffsetV * 0.25f; // 25%

            newGbvP2 = Math.Max(newGbvP2, MinSafeGuardbandV);
            newGbvP3 = Math.Max(newGbvP3, MinSafeGuardbandV * 1.5f); // Slightly more for P3

            result.args[0] = Utils.FloatToHexIeee754(newGbvP0); // SmuArg[0] = P0
            result.args[1] = Utils.FloatToHexIeee754(newGbvP1); // SmuArg[1] = P1
            result.args[2] = Utils.FloatToHexIeee754(newGbvP2); // SmuArg[2] = P2
            result.args[3] = Utils.FloatToHexIeee754(newGbvP3); // SmuArg[3] = P3
            result.args[4] = 0;
            result.args[5] = 0;

            return smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_SetAllDldoPsmMargin, ref result.args);
        }

        private SMU.Status ExecuteLegacyPsmMargin(int margin)
        {
            if (margin < 0 && smu.Rsmu.SMU_MSG_SetOverclockCpuVid > 0)
            {
                result.args[0] = Utils.CurveOptimizerToVid(margin);
                return smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_SetOverclockCpuVid, ref result.args);
            }
            return ExecutePsmMargin(margin);
        }

        private SMU.Status ExecutePsmMargin(int margin)
        {
            result.args[0] = Utils.MakePsmMarginArg(margin);
            return smu.Mp1Smu.SMU_MSG_SetAllDldoPsmMargin > 0
                ? smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_SetAllDldoPsmMargin, ref result.args)
                : smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_SetAllDldoPsmMargin, ref result.args);
        }
        
        private float[] ReadGbv(uint cmdId)
        {
            uint[] args = new uint[6];
            var status = smu.SendMp1Command(cmdId, ref args);
            if (status != SMU.Status.OK) return null;

            return new float[] {
                Utils.HexIeee754ToFloat(args[0]),
                Utils.HexIeee754ToFloat(args[1]),
                Utils.HexIeee754ToFloat(args[2]),
                Utils.HexIeee754ToFloat(args[3])
            };
        }
    }
}

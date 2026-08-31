using System;

namespace ZenStates.Core.Hardware.Smu.Commands
{
    internal class GetPsmMarginSingleCore : BaseSMUCommand
    {
        public GetPsmMarginSingleCore(SMU smu) : base(smu) { }
        private const float MillivoltsPerCount = 3.0f;
        public CmdResult Execute(uint coreMask)
        {
            if (CanExecute())
            {
                result.args[0] = coreMask;

                if (smu.Rsmu.SMU_MSG_GetDldoPsmMargin > 0)
                    result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetDldoPsmMargin, ref result.args);
                else if (smu.SMU_TYPE == SMU.SmuType.TYPE_CPU9)
                {
                    float[] gbvLo = ReadGbv(smu.Mp1Smu.SMU_MSG_GetDldoPsmMargin);
                    float[] gbvHi = ReadGbv(smu.Mp1Smu.SMU_MSG_GetSecondaryDldoPsmMargin);

                    if (gbvHi == null || gbvLo == null)
                    {
                        result.status = SMU.Status.CMD_REJECTED_PREREQ;
                        result.args[0] = 0;
                        return base.Execute();
                    }

                    // Calculating the Slope step based on the lowest points (P4-P7)
                    // Extrapolate the ideal default P0 upwards from P4
                    float extrapolatedDefaultP0 = gbvHi[0] + 4.0f * ((gbvHi[0] - gbvHi[3]) / 3.0f);

                    float offsetMv = (gbvLo[0] - extrapolatedDefaultP0) * 1000.0f;
                    int offsetCount = (int)Math.Round(offsetMv / MillivoltsPerCount);
                    result.args[0] = (uint)offsetCount;
                }
            }

            return base.Execute();
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

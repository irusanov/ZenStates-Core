using System;

namespace ZenStates.Core.SMUCommands
{
    internal class GetPBOScalar : BaseSMUCommand
    {
        public float Scalar { get; protected set; }
        public GetPBOScalar(SMU smu) : base(smu)
        {
            Scalar = 0.0f;
        }

        public override CmdResult Execute()
        {
            if (CanExecute())
            {
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetPBOScalar, ref result.args);
                if (result.Success)
                {
                    byte[] bytes = BitConverter.GetBytes(result.args[0]);
                    Scalar = BitConverter.ToSingle(bytes, 0);
                    if (Scalar < Constants.PBO_SCALAR_MIN || Scalar > Constants.PBO_SCALAR_MAX)
                        Scalar = Constants.PBO_SCALAR_DEFAULT;
                }
            }

            return base.Execute();
        }
    }
}

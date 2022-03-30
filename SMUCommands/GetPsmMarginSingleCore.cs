using System;
using System.Collections.Generic;
using System.Text;

namespace ZenStates.Core.SMUCommands
{
    internal class GetPsmMarginSingleCore : BaseSMUCommand
    {
        public int Margin { get; internal set; } = 0;
        public GetPsmMarginSingleCore(SMU smu) : base(smu) {}
        public CmdResult Execute(uint coreMask)
        {
            if (CanExecute())
            {
                result.args[0] = coreMask & 0xfff00000;
                result.status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_GetDldoPsmMargin, ref result.args);

                if (result.Success)
                {
                    uint ret = result.args[0];
                    if ((ret >> 31 & 1) == 1)
                        Margin = -(Convert.ToInt32(~ret) + 1);
                    else
                        Margin = Convert.ToInt32(ret);
                }
            }

            return base.Execute();
        }
    }
}

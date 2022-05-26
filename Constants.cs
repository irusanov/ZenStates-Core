using System;
using System.Collections.Generic;
using System.Text;

namespace ZenStates.Core
{
    internal static class Constants
    {
        internal const string VENDOR_AMD = "AuthenticAMD";
        internal const string VENDOR_HYGON = "HygonGenuine";
        internal const uint F17H_M01H_SVI = 0x0005A000;
        internal const uint F17H_M60H_SVI = 0x0006F000; // Renoir only?
        internal const uint F17H_M01H_SVI_TEL_PLANE0 = (F17H_M01H_SVI + 0xC);
        internal const uint F17H_M01H_SVI_TEL_PLANE1 = (F17H_M01H_SVI + 0x10);
        internal const uint F17H_M30H_SVI_TEL_PLANE0 = (F17H_M01H_SVI + 0x14);
        internal const uint F17H_M30H_SVI_TEL_PLANE1 = (F17H_M01H_SVI + 0x10);
        internal const uint F17H_M60H_SVI_TEL_PLANE0 = (F17H_M60H_SVI + 0x38);
        internal const uint F17H_M60H_SVI_TEL_PLANE1 = (F17H_M60H_SVI + 0x3C);
        internal const uint F17H_M70H_SVI_TEL_PLANE0 = (F17H_M01H_SVI + 0x10);
        internal const uint F17H_M70H_SVI_TEL_PLANE1 = (F17H_M01H_SVI + 0xC);
        internal const uint F19H_M21H_SVI_TEL_PLANE0 = (F17H_M01H_SVI + 0x10);
        internal const uint F19H_M21H_SVI_TEL_PLANE1 = (F17H_M01H_SVI + 0xC);
        internal const uint F17H_M70H_CCD_TEMP = 0x00059954;
        internal const uint THM_CUR_TEMP = 0x00059800;
        internal const uint THM_CUR_TEMP_RANGE_SEL_MASK = 0x80000;
    }
}

namespace ZenStates.Core
{
    internal static class Constants
    {
        internal const string VENDOR_AMD = "AuthenticAMD";
        internal const string VENDOR_HYGON = "HygonGenuine";
        internal const uint MSR_HW_PSTATE_STATUS = 0xC0010293;
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
        internal const uint F19H_M01H_SVI_TEL_PLANE0 = (F17H_M01H_SVI + 0x10);
        internal const uint F19H_M01H_SVI_TEL_PLANE1 = (F17H_M01H_SVI + 0x14);
        internal const uint F19H_M21H_SVI_TEL_PLANE0 = (F17H_M01H_SVI + 0x10);
        internal const uint F19H_M21H_SVI_TEL_PLANE1 = (F17H_M01H_SVI + 0xC);
        internal const uint F17H_CCD_TEMP = 0x00059954;
        internal const uint F19H_CCD_TEMP = 0x00059B08;
        internal const uint THM_CUR_TEMP = 0x00059800;
        internal const uint THM_CUR_TEMP_RANGE_SEL_MASK = 0x80000;
        internal const uint DEFAULT_MAILBOX_ARGS = 6;
        internal const uint HSMP_MAILBOX_ARGS = 8;
        internal const float PBO_SCALAR_MIN = 0.0f;
        internal const float PBO_SCALAR_MAX = 10.0f;
        internal const float PBO_SCALAR_DEFAULT = 1.0f;
        internal static readonly string[] MISIDENTIFIED_DALI_APU = {
            "Athlon Silver 3050GE",
            "Athlon Silver 3050U",
            "3015e",
            "3020e",
            "Athlon Gold 3150U",
            "Athlon Silver 3050e",
            "Ryzen 3 3250U",
            "Athlon 3000G",
            "Athlon 300GE",
            "Athlon 300U",
            "Athlon 320GE",
            "Ryzen 3 3200U",
        };
    }
}

using System;
using System.Runtime.InteropServices;

namespace ZenStates.Core
{
    public enum ApobLayoutVersion
    {
        V1,
        V2
    }

    public static class ApobDataReader
    {
        private const int MAX_CHANNELS = 12;
        private static readonly byte[] EndPattern = new byte[8] { 0xff, 0xff, 0x00, 0x00, 0x01, 0x00, 0xff, 0xff };

        public static ApobData Read(byte[] data, ApobLayoutVersion version, int offset = 0)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            switch (version)
            {
                case ApobLayoutVersion.V1:
                    return ReadV1(data, offset);

                case ApobLayoutVersion.V2:
                    return ReadV2(data, offset);

                default:
                    throw new ArgumentOutOfRangeException("version");
            }
        }

        private static ApobData ReadV1(byte[] data, int offset)
        {
            int blockSize = Marshal.SizeOf(typeof(ApobDataV1));
            if (data.Length < blockSize)
                throw new ArgumentException("Buffer too small for Apob V1.", "data");

            ApobDataV1 raw = Read<ApobDataV1>(data, blockSize, offset);

            return new ApobData(
                raw.RttNomRd,
                raw.RttNomWr,
                raw.RttWr,
                raw.RttPark,
                raw.RttParkDqs,
                raw.DramDataDs,
                raw.CkOdtA,
                raw.CsOdtA,
                raw.CaOdtA,
                raw.CkOdtB,
                raw.CsOdtB,
                raw.CaOdtB,
                raw.ProcOdt,
                raw.ProcDqDs,
                raw.ProcCaDs
            );
        }

        private static ApobData ReadV2(byte[] data, int offset)
        {
            int blockSize = Marshal.SizeOf(typeof(ApobDataV2));
            if (data.Length < blockSize)
                throw new ArgumentException("Buffer too small for Apob V2.", "data");

            ApobDataV2 raw = Read<ApobDataV2>(data, blockSize, offset);

            return new ApobData(
                raw.RttNomRd,
                raw.RttNomWr,
                raw.RttWr,
                raw.RttPark,
                raw.RttParkDqs,
                raw.DramDataDs,
                raw.CkOdtA,
                raw.CsOdtA,
                raw.CaOdtA,
                raw.CkOdtB,
                raw.CsOdtB,
                raw.CaOdtB,
                raw.ProcOdt,
                raw.ProcDqDs,
                raw.ProcCaDs,
                raw.ProcCkDs,
                raw.ProcCsDs,
                raw.RttNomRdP0,
                raw.RttNomWrP0,
                raw.RttWrP0,
                raw.RttParkP0,
                raw.RttParkDqsP0,
                raw.DramDqDsPullUpP0,
                raw.DramDqDsPullDownP0,
                raw.ProcOdtPullUpP0,
                raw.ProcOdtPullDownP0,
                raw.ProcDqDsPullUpP0,
                raw.ProcDqDsPullDownP0
            );
        }

        private static T Read<T>(byte[] data, int blockSize, int offset) where T : struct
        {
            byte[] buffer = new byte[blockSize * MAX_CHANNELS];

            Buffer.BlockCopy(data, offset, buffer, 0, buffer.Length);

            for (int i = 0; i < MAX_CHANNELS; i++)
            {
                byte[] channelBuffer = new byte[blockSize];
                Buffer.BlockCopy(buffer, i * blockSize, channelBuffer, 0, blockSize);

                if (Utils.AllZero(channelBuffer))
                {
                    continue;
                }

                if (Utils.FindSequence(buffer, 0, EndPattern) > -1)
                {
                    break;
                }

                // return first valid channel's data, as all channels should have the same values for these fields
                return Utils.ByteArrayToStructure<T>(channelBuffer);
            }

            return new T();
        }
    }
}
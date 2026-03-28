using System;
using System.Runtime.InteropServices;

namespace ZenStates.Core
{
    public enum ApobLayoutVersion
    {
        V60 = 1,
        V90,
        VA4,
    }

    public static class ApobDataReader
    {
        //private const int MAX_CHANNELS = 12;
        //private static readonly byte[] EndPattern = new byte[6] { 0xff, 0xff, 0x01, 0x00, 0xff, 0xff };

        public static ApobData Read(byte[] data, ApobLayoutVersion version, int offset = 0)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            switch (version)
            {
                case ApobLayoutVersion.V60:
                    return ReadV1(data, offset);

                case ApobLayoutVersion.V90:
                    return ReadV2(data, offset);

                case ApobLayoutVersion.VA4:
                    return ReadV3(data, offset);

                default:
                    throw new ArgumentOutOfRangeException("version");
            }
        }

        private static ApobData ReadV1(byte[] data, int offset)
        {
            int blockSize = Marshal.SizeOf(typeof(ApobData60));
            if (data.Length < blockSize)
                throw new ArgumentException("Buffer too small for Apob V1.", "data");

            ApobData60 raw = Read<ApobData60>(data, blockSize, offset);

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
                raw.ProcCsDs
            );
        }

        private static ApobData ReadV2(byte[] data, int offset)
        {
            int blockSize = Marshal.SizeOf(typeof(ApobData90));
            if (data.Length < blockSize)
                throw new ArgumentException("Buffer too small for Apob V2.", "data");

            ApobData90 raw = Read<ApobData90>(data, blockSize, offset);

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

        private static ApobData ReadV3(byte[] data, int offset)
        {
            int blockSize = Marshal.SizeOf(typeof(ApobDataA4));
            if (data.Length < blockSize)
                throw new ArgumentException("Buffer too small for Apob V3.", "data");

            ApobDataA4 raw = Read<ApobDataA4>(data, blockSize, offset);

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
                null, null, null, null, null, null, null, null, null, null, null, null, null,
                raw.ProcCaOdt,
                raw.ProcCkOdt,
                raw.ProcDqOdt,
                raw.ProcDqsOdt,
                raw.ProcDqDs
            );
        }

        //private static T Read<T>(byte[] data, int blockSize, int offset) where T : struct
        //{
        //    byte[] buffer = new byte[blockSize * MAX_CHANNELS];

        //    Buffer.BlockCopy(data, offset, buffer, 0, buffer.Length);

        //    for (int i = 0; i < MAX_CHANNELS; i++)
        //    {
        //        byte[] channelBuffer = new byte[blockSize];
        //        Buffer.BlockCopy(buffer, i * blockSize, channelBuffer, 0, blockSize);

        //        if (Utils.AllZero(channelBuffer))
        //        {
        //            continue;
        //        }

        //        if (Utils.FindSequence(buffer, 0, EndPattern) > -1)
        //        {
        //            break;
        //        }

        //        // return first valid channel's data, as all channels should have the same values for these fields
        //        return Utils.ByteArrayToStructure<T>(channelBuffer);
        //    }

        //    return new T();
        //}

        private static T Read<T>(byte[] data, int blockSize, int offset) where T : struct
        {
            byte[] buffer = new byte[blockSize];
            Buffer.BlockCopy(data, offset, buffer, 0, buffer.Length);

            return Utils.ByteArrayToStructure<T>(buffer);
        }
    }
}
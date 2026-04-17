using System;
using System.Runtime.InteropServices;

namespace ZenStates.Core
{
    public static class ApobDataReader
    {
        public static ApobData Read(byte[] data, Cpu.CodeName codeName, uint offset = 0)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            // TODO: This should be the other way around - initialize everything in a centralized place/struct based on code name
            switch (codeName)
            {
                // 19H
                case Cpu.CodeName.Milan:
                case Cpu.CodeName.Chagall:
                case Cpu.CodeName.Genoa:
                case Cpu.CodeName.StormPeak:
                case Cpu.CodeName.Vermeer:
                case Cpu.CodeName.Raphael:
                case Cpu.CodeName.DragonRange:
                    return ReadV1(data, offset);
                // 1AH
                case Cpu.CodeName.Turin:
                case Cpu.CodeName.TurinD:
                case Cpu.CodeName.ShimadaPeak:
                case Cpu.CodeName.StrixPoint:
                case Cpu.CodeName.StrixHalo:
                case Cpu.CodeName.KrackanPoint:
                case Cpu.CodeName.KrackanPoint2:
                case Cpu.CodeName.GraniteRidge:
                case Cpu.CodeName.Bergamo:
                    return ReadV2(data, offset);
                // AM5 APU
                case Cpu.CodeName.Rembrandt:
                case Cpu.CodeName.HawkPoint:
                case Cpu.CodeName.Phoenix:
                case Cpu.CodeName.Phoenix2:
                    return ReadV3(data, offset);
                default:
                    return ReadV2(data, offset);
            }
        }

        private static ApobData ReadV1(byte[] data, uint offset)
        {
            int blockSize = Marshal.SizeOf(typeof(ApobData19h));
            if (data.Length < blockSize)
                throw new ArgumentException("Buffer too small for Apob V1.", "data");

            ApobData19h raw = Read<ApobData19h>(data, blockSize, offset);

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

        private static ApobData ReadV2(byte[] data, uint offset)
        {
            int blockSize = Marshal.SizeOf(typeof(ApobData1Ah));
            if (data.Length < blockSize)
                throw new ArgumentException("Buffer too small for Apob V2.", "data");

            ApobData1Ah raw = Read<ApobData1Ah>(data, blockSize, offset);

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

        private static ApobData ReadV3(byte[] data, uint offset)
        {
            int blockSize = Marshal.SizeOf(typeof(ApobData19h_8000));
            if (data.Length < blockSize)
                throw new ArgumentException("Buffer too small for Apob V3.", "data");

            ApobData19h_8000 raw = Read<ApobData19h_8000>(data, blockSize, offset);

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

        private static T Read<T>(byte[] data, int blockSize, uint offset) where T : struct
        {
            byte[] buffer = new byte[blockSize];
            Buffer.BlockCopy(data, (int)offset, buffer, 0, buffer.Length);

            return Utils.ByteArrayToStructure<T>(buffer);
        }
    }
}
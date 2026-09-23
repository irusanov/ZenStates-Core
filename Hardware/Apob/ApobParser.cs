using System;

namespace ZenStates.Core.Hardware.Apob
{
    internal static class ApobDataReader
    {
        private const int MaxSearchLength = 1024; // 1024 bytes

        internal static bool TryRead(byte[] data, uint offset, ApobBlockLayout layout, out ApobData result)
        {
            result = null;
            if (data == null || layout == null)
                return false;

            try
            {
                result = new ApobData(data, offset, layout);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        internal static ApobData Read(byte[] data, uint offset, ApobBlockLayout layout)
        {
            return new ApobData(data, offset, layout);
        }

        internal static bool TryReadCcdl(
            byte[] data,
            ApobCcdlLayout layout,
            out uint ccdl,
            out uint ccdlrw,
            out uint ccdlrw2,
            uint targetCcdl = 0,
            uint targetCcdlWr2 = 0
        )
        {
            ccdl = 0;
            ccdlrw = 0;
            ccdlrw2 = 0;

            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));

            int matchIndex = Utils.FindSequence(data, 0, layout.Magic);
            if (matchIndex < 0)
                return false;

            long offset = (long)matchIndex + layout.Magic.Length + layout.CcdlBlockOffset;
            long requiredSize = layout.ValueWidth == ApobValueWidth.UInt16 ? 6 : 12;

            if (offset < 0 || offset + requiredSize > data.Length)
                return false;

            if (layout.ValueWidth == ApobValueWidth.UInt16)
            {
                ccdl = Utils.ReadUInt16(data, (uint)offset);
                ccdlrw = Utils.ReadUInt16(data, (uint)(offset + 2));
                ccdlrw2 = Utils.ReadUInt16(data, (uint)(offset + 4));
            }
            else
            {
                ccdl = Utils.ReadUInt32(data, (uint)offset);
                ccdlrw = Utils.ReadUInt32(data, (uint)(offset + 4));
                ccdlrw2 = Utils.ReadUInt32(data, (uint)(offset + 8));
            }

            // Valid when the values are in range and either targetCcdlWr2 is 0 (not available) or ccdlrw2 matches targetCcdlWr2
            bool isValid = ApobCcdlValidation.IsValid(ccdl, ccdlrw, ccdlrw2) && (targetCcdlWr2 == 0 || ccdlrw2 == targetCcdlWr2);

            // Fallback
            bool isTargetCcdlWr2Valid = targetCcdlWr2 >= ApobCcdlValidation.MinTccdlWr2 && targetCcdlWr2 <= ApobCcdlValidation.MaxTccdlWr2;
            bool isTargetCcdlValid = targetCcdl >= ApobCcdlValidation.MinTccdl && targetCcdl <= ApobCcdlValidation.MaxTccdl;

            if (!isValid && (isTargetCcdlWr2Valid || isTargetCcdlValid))
            {
                long scanStartOffset = (long)matchIndex + layout.Magic.Length;
                long scanEndOffset = Math.Min(data.Length, scanStartOffset + MaxSearchLength);

                const int targeCcdlWr2Weight = 2;
                const int targetCcdlWeight = 1;

                int maxScore = (isTargetCcdlWr2Valid ? targeCcdlWr2Weight : 0) + (isTargetCcdlValid ? targetCcdlWeight : 0);

                uint bestC = 0, bestRw = 0, bestRw2 = 0;
                int bestScore = -1;
                bool found = false;

                for (long pos = scanStartOffset; pos + requiredSize <= scanEndOffset; pos++)
                {
                    uint c, rw, rw2;

                    if (layout.ValueWidth == ApobValueWidth.UInt16)
                    {
                        c = Utils.ReadUInt16(data, (uint)pos);
                        rw = Utils.ReadUInt16(data, (uint)(pos + 2));
                        rw2 = Utils.ReadUInt16(data, (uint)(pos + 4));
                    }
                    else
                    {
                        c = Utils.ReadUInt32(data, (uint)pos);
                        rw = Utils.ReadUInt32(data, (uint)(pos + 4));
                        rw2 = Utils.ReadUInt32(data, (uint)(pos + 8));
                    }

                    if (!ApobCcdlValidation.IsValid(c, rw, rw2))
                        continue;

                    bool matchesTargetCcdlWr2 = isTargetCcdlWr2Valid && rw2 == targetCcdlWr2;
                    bool matchesTargetCcdl = isTargetCcdlValid && c == targetCcdl;
                    int score = (matchesTargetCcdlWr2 ? targeCcdlWr2Weight : 0) + (matchesTargetCcdl ? targetCcdlWeight : 0);

                    if (score > bestScore)
                    {
                        bestC = c;
                        bestRw = rw;
                        bestRw2 = rw2;
                        bestScore = score;
                        found = true;

                        if (score >= maxScore)
                            break; // can't do better
                    }
                }

                if (found)
                {
                    ccdl = bestC;
                    ccdlrw = bestRw;
                    ccdlrw2 = bestRw2;
                    isValid = true;
                }
            }

            // Could not find a valid match, reset values
            if (!isValid)
            {
                ccdl = 0;
                ccdlrw = 0;
                ccdlrw2 = 0;
            }

            return isValid;
        }
    }
}

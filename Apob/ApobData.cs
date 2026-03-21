using System;
using System.Runtime.InteropServices;

namespace ZenStates.Core
{
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct ApobData
    {
        [FieldOffset(0)]
        private readonly uint _signature;

        [FieldOffset(0x4)]
        private readonly byte _version;

        [FieldOffset(0x1A7C)]
        private readonly byte _ckOdtA;

        [FieldOffset(0x1A7D)]
        private readonly byte _csOdtA;

        [FieldOffset(0x1A7E)]
        private readonly byte _caOdtA;

        [FieldOffset(0x1A7F)]
        private readonly byte _ckOdtB;

        [FieldOffset(0x1A80)]
        private readonly byte _csOdtB;

        [FieldOffset(0x1A81)]
        private readonly byte _caOdtB;
       
        /// <summary>
        /// Public fields
        /// </summary>
        public byte Version => _version;
        public string Signature => System.Text.Encoding.ASCII.GetString(BitConverter.GetBytes(_signature));

        public GroupOdtImpedance CkOdtA => new GroupOdtImpedance(_ckOdtA);

        public GroupOdtImpedance CsOdtA => new GroupOdtImpedance(_csOdtA);

        public GroupOdtImpedance CaOdtA => new GroupOdtImpedance(_caOdtA);

        public GroupOdtImpedance CkOdtB => new GroupOdtImpedance(_ckOdtB);

        public GroupOdtImpedance CsOdtB => new GroupOdtImpedance(_csOdtB);

        public GroupOdtImpedance CaOdtB => new GroupOdtImpedance(_caOdtB);
    }
}

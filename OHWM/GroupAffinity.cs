/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2020 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

namespace ZenStates.Core.OHWM
{

    /// <summary>
    /// This structure describes a group-specific affinity.
    /// </summary>
    public struct GroupAffinity
    {

        public static GroupAffinity Undefined =
          new GroupAffinity(ushort.MaxValue, 0);

        public GroupAffinity(ushort group, ulong mask)
        {
            this.Group = group;
            this.Mask = mask;
        }

        /// <summary>
        /// Affinity for a single logical processor. A processor group holds at most 64 logical
        /// processors, so an <paramref name="index"/> of 64 or more is carried into the following
        /// group(s) (group += index / 64, bit = index % 64). Shifting by index directly would
        /// wrap (1UL &lt;&lt; 64 == 1) and silently target the wrong processor.
        /// </summary>
        public static GroupAffinity Single(ushort group, int index)
        {
            if (index < 0)
                throw new System.ArgumentOutOfRangeException(nameof(index));

            return new GroupAffinity((ushort)(group + index / 64), 1UL << (index % 64));
        }

        /// <summary>
        /// Affinity for a zero-based, system-wide logical processor index, resolved against the
        /// actual processor group sizes where the OS reports them.
        /// </summary>
        public static GroupAffinity ForLogicalProcessor(int index)
        {
            return ThreadAffinity.GetLogicalProcessorAffinity(index);
        }

        public ushort Group { get; }

        public ulong Mask { get; }

        public override bool Equals(object o)
        {
            if (o == null || GetType() != o.GetType()) return false;
            GroupAffinity a = (GroupAffinity)o;
            return (Group == a.Group) && (Mask == a.Mask);
        }

        public override int GetHashCode()
        {
            return Group.GetHashCode() ^ Mask.GetHashCode();
        }

        public static bool operator ==(GroupAffinity a1, GroupAffinity a2)
        {
            return (a1.Group == a2.Group) && (a1.Mask == a2.Mask);
        }

        public static bool operator !=(GroupAffinity a1, GroupAffinity a2)
        {
            return (a1.Group != a2.Group) || (a1.Mask != a2.Mask);
        }

    }
}

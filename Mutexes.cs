using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace ZenStates.Core
{
    public static class Mutexes
    {
        private static Mutex isaBusMutex;
        private static Mutex pciBusMutex;

        private static readonly string isaMutexName = "Global\\Access_ISABUS.HTP.Method";
        private static readonly string pciMutexName = "Global\\Access_PCI";

        static Mutex CreateOrOpenExistingMutex(string name)
        {
            try
            {
                var worldRule = new MutexAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), MutexRights.FullControl, AccessControlType.Allow);
                var mutexSecurity = new MutexSecurity();
                mutexSecurity.AddAccessRule(worldRule);

#if NETFRAMEWORK
                return new Mutex(false, name, out _, mutexSecurity);
#else
                return MutexAcl.Create(false, name, out _, mutexSecurity);
#endif
            }
            catch (UnauthorizedAccessException)
            {
                try
                {
                    return Mutex.OpenExisting(name);
                }
                catch
                {
                    // Ignored.
                }
            }

            return null;
        }

        public static void Open()
        {
            isaBusMutex = CreateOrOpenExistingMutex(isaMutexName);
            pciBusMutex = CreateOrOpenExistingMutex(pciMutexName);
        }

        public static void Close()
        {
            isaBusMutex?.Close();
            pciBusMutex?.Close();
        }

        public static bool WaitIsaBus(int millisecondsTimeout)
        {
            return isaBusMutex.WaitOne(millisecondsTimeout);
        }

        public static void ReleaseIsaBus()
        {
            isaBusMutex?.ReleaseMutex();
        }

        public static bool WaitPciBus(int millisecondsTimeout)
        {
            return pciBusMutex.WaitOne(millisecondsTimeout);
        }

        public static void ReleasePciBus()
        {
            pciBusMutex?.ReleaseMutex();
        }

        public static bool WaitMutex(Mutex mutex, int millisecondsTimeout = 5000)
        {
            if (pciBusMutex == null)
                return true;

            try
            {
                return pciBusMutex.WaitOne(millisecondsTimeout, false);
            }
            catch (AbandonedMutexException)
            {
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}

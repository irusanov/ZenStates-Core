using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace ZenStates.Core
{
    public static class Mutexes
    {
        private static Mutex _isaBusMutex;
        private static Mutex _pciBusMutex;

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

        /// <summary>
        /// Cleates or opens the mutexes.
        /// </summary>
        public static void Open()
        {
            _isaBusMutex = CreateOrOpenExistingMutex(isaMutexName);
            _pciBusMutex = CreateOrOpenExistingMutex(pciMutexName);
        }

        /// <summary>
        /// Closes the mutexes.
        /// </summary>
        public static void Close()
        {
            _isaBusMutex?.Close();
            _pciBusMutex?.Close();
        }

        public static bool WaitIsaBus(int millisecondsTimeout)
        {
            return WaitMutex(_isaBusMutex, millisecondsTimeout);
        }

        public static void ReleaseIsaBus()
        {
            _isaBusMutex?.ReleaseMutex();
        }

        public static bool WaitPciBus(int millisecondsTimeout)
        {
            return WaitMutex(_pciBusMutex, millisecondsTimeout);
        }

        public static void ReleasePciBus()
        {
            _pciBusMutex?.ReleaseMutex();
        }

        private static bool WaitMutex(Mutex mutex, int millisecondsTimeout = 5000)
        {
            if (mutex == null)
                return true;

            try
            {
                return mutex.WaitOne(millisecondsTimeout, false);
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

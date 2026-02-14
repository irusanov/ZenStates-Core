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

        /// <summary>
        /// Opens the mutexes.
        /// </summary>
        public static void Open()
        {
            _isaBusMutex = CreateOrOpenExistingMutex("Global\\Access_ISABUS.HTP.Method");
            _pciBusMutex = CreateOrOpenExistingMutex("Global\\Access_PCI");

            Mutex CreateOrOpenExistingMutex(string name)
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
                return false;

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

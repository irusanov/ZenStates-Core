using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace ZenStates.Core.Hardware
{
    public static class Mutexes
    {
        private static Mutex _isaBusMutex;
        private static Mutex _pciBusMutex;
        private static Mutex _smbusMutex;

        /// <summary>
        /// Opens the mutexes.
        /// </summary>
        public static void Open()
        {
            _isaBusMutex = CreateOrOpenExistingMutex("Global\\Access_ISABUS.HTP.Method");
            _smbusMutex = CreateOrOpenExistingMutex("Global\\Access_SMBUS.HTP.Method");
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
            Mutex isa = _isaBusMutex;
            Mutex pci = _pciBusMutex;
            Mutex smbus = _smbusMutex;

            _isaBusMutex = null;
            _pciBusMutex = null;
            _smbusMutex = null;

            isa?.Close();
            pci?.Close();
            smbus?.Close();
        }

        public static bool WaitIsaBus(int millisecondsTimeout)
        {
            return WaitMutex(_isaBusMutex, millisecondsTimeout);
        }

        public static void ReleaseIsaBus()
        {
            ReleaseMutex(_isaBusMutex);
        }

        public static bool WaitPciBus(int millisecondsTimeout)
        {
            return WaitMutex(_pciBusMutex, millisecondsTimeout);
        }

        public static void ReleasePciBus()
        {
            ReleaseMutex(_pciBusMutex);
        }

        public static bool WaitSmbus(int millisecondsTimeout)
        {
            return WaitMutex(_smbusMutex, millisecondsTimeout);
        }

        public static void ReleaseSmbus()
        {
            ReleaseMutex(_smbusMutex);
        }

        private static void ReleaseMutex(Mutex mutex)
        {
            if (mutex == null)
                return;

            try
            {
                mutex.ReleaseMutex();
            }
            catch (ObjectDisposedException)
            {
                // Closed by a concurrent Mutexes.Close(); nothing left to release.
            }
            catch (ApplicationException)
            {
                // Not the owner - a release without a matching successful wait.
            }
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
                // The previous owner died holding it; ownership passes to us.
                return true;
            }
            catch (ObjectDisposedException)
            {
                // Closed underneath us by a concurrent Mutexes.Close().
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}

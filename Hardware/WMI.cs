using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.ServiceProcess;
using static System.Management.ManagementObjectCollection;

namespace ZenStates.Core.Hardware
{
    public static class WMI
    {
        public static object TryGetProperty(ManagementObject wmiObj, string propertyName)
        {
            object retval = null;
            try
            {
                retval = wmiObj.GetPropertyValue(propertyName);
            }
            catch (ManagementException ex)
            {
                Debug.WriteLine(ex.Message);
            }

            return retval;
        }

        //root\wmi
        public static ManagementScope Connect(string scope)
        {
            try
            {
                using (var sc = new ServiceController("Winmgmt"))
                {
                    if (sc.Status != ServiceControllerStatus.Running)
                        throw new ManagementException(@"Windows Management Instrumentation service is not running");
                }

                ManagementScope mScope = new ManagementScope(scope);
                mScope.Connect();

                if (mScope.IsConnected)
                    return mScope;
                else
                    throw new ManagementException($@"Failed to connect to {scope}");
            }
            catch (ManagementException ex)
            {
                Debug.WriteLine(@"WMI: {0}", ex.Message);
                throw;
            }
        }

        public static ManagementObject Query(string scope, string wmiClass)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    new ManagementScope(scope),
                    new ObjectQuery($"SELECT * FROM {wmiClass}")))
                {
                    using (ManagementObjectCollection results = searcher.Get())
                    using (ManagementObjectEnumerator enumerator = results.GetEnumerator())
                    {
                        if (enumerator.MoveNext())
                        {
                            ManagementObject current = enumerator.Current as ManagementObject;
                            if (current != null)
                            {
                                ManagementObject detached = new ManagementObject(current.Path.Path);
                                detached.Get();
                                return detached;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            return null;
        }

        public static List<string> GetWmiNamespaces(string root)
        {
            List<string> namespaces = new List<string>();
            try
            {
                using (ManagementClass nsClass = new ManagementClass(
                    new ManagementScope(root), new ManagementPath("__namespace"), null))
                using (ManagementObjectCollection instances = nsClass.GetInstances())
                {
                    foreach (var obj in instances)
                    {
                        using (var ns = (ManagementObject)obj)
                        {
                            string namespaceName = root + "\\" + ns["Name"];
                            namespaces.Add(namespaceName);
                            namespaces.AddRange(GetWmiNamespaces(namespaceName));
                        }
                    }
                }

                namespaces.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            return namespaces;
        }

        public static List<string> GetClassNamesWithinWmiNamespace(string wmiNamespaceName)
        {
            List<string> classNames = new List<string>();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher
                (new ManagementScope(wmiNamespaceName),
                    new WqlObjectQuery("SELECT * FROM meta_class")))
                {
                    using (ManagementObjectCollection objectCollection = searcher.Get())
                    {
                        foreach (var obj in objectCollection)
                        {
                            using (var wmiClass = (ManagementClass)obj)
                            {
                                string stringified = wmiClass.ToString();
                                string[] parts = stringified.Split(':');
                                if (parts.Length > 1)
                                    classNames.Add(parts[1]);
                            }
                        }
                    }
                }

                classNames.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            return classNames;
        }

        public static string GetInstanceName(string scope, string wmiClass)
        {
            using (ManagementObject queryObject = Query(scope, wmiClass))
            {
                string name = "";

                if (queryObject == null)
                    return name;

                try
                {
                    var obj = TryGetProperty(queryObject, "InstanceName");
                    if (obj != null) name = obj.ToString();
                }
                catch
                {
                    // ignored
                }

                return name;
            }
        }

        public static ManagementBaseObject InvokeMethod(ManagementObject mo, string methodName, string inParamName, uint arg)
        {
            try
            {
                using (ManagementBaseObject inParams = mo.GetMethodParameters(methodName))
                {
                    if (inParams != null && !String.IsNullOrEmpty(inParamName))
                        inParams[inParamName] = arg;

                    return mo.InvokeMethod(methodName, inParams, null);
                }
            }
            catch (ManagementException)
            {
                return null;
            }
        }

        public static ManagementBaseObject InvokeMethodAndGetValue(ManagementObject mo, string methodName, string propName,
            string inParamName, uint arg)
        {
            try
            {
                ManagementBaseObject outParams = InvokeMethod(mo, methodName, inParamName, arg);
                return (ManagementBaseObject)outParams?.Properties[propName].Value;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }

        public static byte[] RunCommand(ManagementObject mo, uint commandID, uint commandArg = 0x0)
        {
            try
            {
                using (ManagementBaseObject inParams = mo.GetMethodParameters("RunCommand"))
                {
                    byte[] buffer = new byte[8];

                    var cmd = BitConverter.GetBytes(commandID);
                    var arg = BitConverter.GetBytes(commandArg);

                    Buffer.BlockCopy(cmd, 0, buffer, 0, 4);
                    Buffer.BlockCopy(arg, 0, buffer, 4, 4);

                    inParams["Inbuf"] = buffer;

                    using (ManagementBaseObject outParams = mo.InvokeMethod("RunCommand", inParams, null))
                    using (ManagementBaseObject pack = (ManagementBaseObject)outParams?.Properties["Outbuf"].Value)
                    {
                        byte[] result = (byte[])pack?.GetPropertyValue("Result");
                        return result != null ? (byte[])result.Clone() : null;
                    }
                }
            }
            catch (ManagementException ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }
    }
}

using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace ZenStates.Core
{
    public static class Utils
    {
        public static bool Is64Bit => OpenHardwareMonitor.Hardware.OperatingSystem.Is64BitOperatingSystem;

        public static uint SetBits(uint val, int offset, int n, uint newVal)
        {
            return val & ~(((1U << n) - 1) << offset) | (newVal << offset);
        }

        public static uint GetBits(uint val, int offset, int n)
        {
            return (val >> offset) & ~(~0U << n);
        }

        public static uint BitSlice(uint val, int hi, int lo)
        {
            uint mask = (2U << hi - lo) - 1U;
            return val >> lo & mask;
        }

        public static uint CountSetBits(uint v)
        {
            uint result = 0;

            while (v > 0)
            {
                if ((v & 1) == 1)
                    result++;

                v >>= 1;
            }

            return result;
        }

        // https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/master/LibreHardwareMonitorLib/Hardware/SMBios.cs#L918
        public static bool IsBitSet(byte b, int pos)
        {
            return (b & (1 << pos)) != 0;
        }

        public static string GetStringPart(uint val)
        {
            return val != 0 ? Convert.ToChar(val).ToString() : "";
        }

        public static string IntToStr(uint val)
        {
            uint part1 = val & 0xff;
            uint part2 = val >> 8 & 0xff;
            uint part3 = val >> 16 & 0xff;
            uint part4 = val >> 24 & 0xff;

            return $"{GetStringPart(part1)}{GetStringPart(part2)}{GetStringPart(part3)}{GetStringPart(part4)}";
        }

        public static double VidToVoltage(uint vid)
        {
            return 1.55 - vid * 0.00625;
        }

        public static double VidToVoltageSVI3(uint vid)
        {
            return Math.Round(0.245 + vid * 0.005, 3);
        }

        public static uint VoltageToVidSVI3(double targetVoltage)
        {
            if (targetVoltage < 0.245)
                return 0;
            return (uint)Math.Round((targetVoltage - 0.245) / 0.005);
        }

        private static bool CheckAllZero<T>(ref T[] typedArray)
        {
            if (typedArray == null)
                return true;

            foreach (var value in typedArray)
            {
                if (Convert.ToUInt32(value) != 0)
                    return false;
            }

            return true;
        }

        public static bool AllZero(byte[] arr) => CheckAllZero(ref arr);

        public static bool AllZero(int[] arr) => CheckAllZero(ref arr);

        public static bool AllZero(uint[] arr) => CheckAllZero(ref arr);

        public static bool AllZero(float[] arr) => CheckAllZero(ref arr);

        public static uint[] MakeCmdArgs(uint[] args, int maxArgs = Constants.DEFAULT_MAILBOX_ARGS)
        {
            uint[] cmdArgs = new uint[maxArgs];
            int length = Math.Min(maxArgs, args.Length);

            for (int i = 0; i < length; i++)
                cmdArgs[i] = args[i];

            return cmdArgs;
        }

        public static uint[] MakeCmdArgs(uint arg = 0, int maxArgs = Constants.DEFAULT_MAILBOX_ARGS)
        {
            return MakeCmdArgs(new uint[] { arg }, maxArgs);
        }

        // CO margin range seems to be from -30 to 30
        // Margin arg seems to be 16 bits (lowest 16 bits of the command arg)
        // Update 01 Nov 2022 - the range is different on Raphael, -40 is successfully set
        public static uint MakePsmMarginArg(int margin)
        {
            // if (margin > 30)
            //     margin = 30;
            // else if (margin < -30)
            //     margin = -30;

            int offset = margin < 0 ? 0x100000 : 0;
            return Convert.ToUInt32(offset + margin) & 0xffff;
        }

        public static T ByteArrayToStructure<T>(byte[] byteArray) where T : new()
        {
            T structure;
            GCHandle handle = GCHandle.Alloc(byteArray, GCHandleType.Pinned);
            try
            {
                structure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            }
            finally
            {
                handle.Free();
            }
            return structure;
        }

        public static uint ReverseBytes(uint value)
        {
            return (value & 0x000000FFU) << 24 | (value & 0x0000FF00U) << 8 |
                (value & 0x00FF0000U) >> 8 | (value & 0xFF000000U) >> 24;
        }

        public static string GetStringFromBytes(uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            // Array.Reverse(bytes);
            return System.Text.Encoding.ASCII.GetString(bytes).Replace("\0", " ");
        }

        public static string GetStringFromBytes(ulong value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            // Array.Reverse(bytes);
            return System.Text.Encoding.ASCII.GetString(bytes).Replace("\0", " ");
        }

        public static string GetStringFromBytes(byte[] value)
        {
            return System.Text.Encoding.ASCII.GetString(value).Replace("\0", " ");
        }

        /// <summary>Looks for the next occurrence of a sequence in a byte array</summary>
        /// <param name="source">Array that will be scanned</param>
        /// <param name="start">Index in the array at which scanning will begin</param>
        /// <param name="pattern">Sequence the array will be scanned for</param>
        /// <returns>
        ///   The index of the next occurrence of the sequence of -1 if not found
        /// </returns>
        public static int FindSequence(byte[] source, int start, byte[] pattern)
        {
            if (source == null || source.Length == 0 || pattern == null || pattern.Length == 0)
                return -1;

            if (pattern.Length > source.Length)
                return -1;

            if (start < 0 || start >= source.Length)
                return -1;

            int end = source.Length - pattern.Length;
            byte firstByte = pattern[0];

            while (start <= end)
            {
                if (source[start] == firstByte)
                {
                    int offset;
                    for (offset = 1; offset < pattern.Length; offset++)
                    {
                        if (source[start + offset] != pattern[offset])
                        {
                            break;
                        }
                    }

                    if (offset == pattern.Length)
                        return start;
                }

                start++;
            }

            return -1;
        }


        public static bool ArrayMembersEqual(float[] array1, float[] array2, int numElements)
        {
            if (array1 == null || array2 == null)
                return false;

            if (array1.Length < numElements || array2.Length < numElements)
            {
                // throw new ArgumentException("Arrays are not long enough to compare the specified number of elements.");
                Console.WriteLine("Arrays are not long enough to compare the specified number of elements.");
                return false;
            }

            for (int i = 0; i < numElements; i++)
            {
                if (array1[i] != array2[i])
                {
                    return false;
                }
            }

            return true;
        }

        public static bool PartialStringMatch(string str, string[] arr)
        {
            bool match = false;
            for (int i = 0; i < arr.Length; i++)
            {
                if (str.Contains(arr[i])) { match = true; break; }
            }
            return match;
        }

        public static float ToNanoseconds(uint value, float frequency)
        {
            if (frequency != 0)
            {
                float refiValue = Convert.ToSingle(value);
                float trefins = refiValue * 2000f / frequency;
                if (trefins > refiValue) trefins /= 2;
                return trefins;
            }
            return 0;
        }

        public static void RemoveRegistryKey(string keyPath)
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(keyPath, true))
                {
                    if (key != null)
                    {
                        Registry.LocalMachine.DeleteSubKeyTree(keyPath);
                        Console.WriteLine($"Deleted registry key: HKEY_LOCAL_MACHINE\\{keyPath}");
                    }
                    else
                    {
                        Console.WriteLine($"Registry key not found: HKEY_LOCAL_MACHINE\\{keyPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete registry key: {ex.Message}");
            }
        }

        public class CommandExecutionResult
        {
            public bool Success { get; set; }
            public string StandardOutput { get; set; }
            public string StandardError { get; set; }
            public int ExitCode { get; set; }
            public string State { get; set; }
        }

        public static CommandExecutionResult ExecuteCommand(string command)
        {
            var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var result = new CommandExecutionResult();
            var standardOutput = new StringBuilder();
            var standardError = new StringBuilder();

            using (var process = Process.Start(processInfo))
            {
                if (process == null)
                {
                    result.Success = false;
                    result.StandardError = "Failed to start process.";
                    return result;
                }

                process.OutputDataReceived += (sender, e) => { if (e.Data != null) standardOutput.AppendLine(e.Data); };
                process.BeginOutputReadLine();
                process.ErrorDataReceived += (sender, e) => { if (e.Data != null) standardError.AppendLine(e.Data); };
                process.BeginErrorReadLine();
                process.WaitForExit();

                result.Success = process.ExitCode == 0;
                result.StandardOutput = standardOutput.ToString();
                result.StandardError = standardError.ToString();
                result.ExitCode = process.ExitCode;

                // Parse the state from the standard output
                var stateMatch = Regex.Match(result.StandardOutput, @"STATE\s+:\s+\d+\s+(\w+)");
                if (stateMatch.Success)
                {
                    result.State = stateMatch.Groups[1].Value;
                }
                else
                {
                    result.State = "Unknown";
                }
            }

            return result;
        }

        public static bool DeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Console.WriteLine($"Deleted file: {filePath}");
                }
                else
                {
                    Console.WriteLine($"File not found: {filePath}");
                }
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public static bool HasDependentServices(string serviceName)
        {
            CommandExecutionResult result = ExecuteCommand($"sc.exe qc {serviceName}");
            string output = result.StandardOutput;
            bool hasDependents = false;
            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Trim().StartsWith("DEPENDENCIES"))
                {
                    string dependencies = line.Split(new[] { ':' }, 2)[1].Trim();
                    if (!string.IsNullOrEmpty(dependencies))
                    {
                        hasDependents = true;
                        Console.WriteLine($"Dependent services: {dependencies}");
                    }
                }
            }
            return hasDependents;
        }

        public static int GetServiceProcessId(string serviceName)
        {
            CommandExecutionResult result = ExecuteCommand($"sc.exe queryex {serviceName}");
            string output = result.StandardOutput;

            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Trim().StartsWith("PID"))
                {
                    if (int.TryParse(line.Split(':')[1].Trim(), out int pid))
                    {
                        return pid;
                    }
                }
            }
            return -1;
        }

        public static void KillProcess(int processId)
        {
            try
            {
                Process process = Process.GetProcessById(processId);
                process.Kill();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to kill process {processId}: {ex.Message}");
            }
        }

        public static bool ServiceExists(string serviceName)
        {
            CommandExecutionResult result = ExecuteCommand($"sc query {serviceName}");
            string output = result.StandardOutput;

            return !output.Contains("FAILED 1060");
        }
    }
}

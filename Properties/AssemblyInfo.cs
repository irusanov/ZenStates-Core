using System.Runtime.InteropServices;

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(true)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("39c55831-97a2-498d-a9dd-27bf677e579a")]

#if !NET20
// Every P/Invoke in this assembly names its module without a path (inpoutx64.dll,
// WinIo32.dll, kernel32.dll, libc). Without this attribute the runtime resolves those
// through the default search order, which includes the current working directory — and
// this library runs elevated, so a DLL planted next to the launching process would load
// with full privileges. Restricting the search to the application directory and System32
// closes that path for the implicit loads; IODriver.LoadDll does the same for its explicit one.
[assembly: DefaultDllImportSearchPaths(
    DllImportSearchPath.ApplicationDirectory |
    DllImportSearchPath.System32 |
    DllImportSearchPath.SafeDirectories)]
#endif

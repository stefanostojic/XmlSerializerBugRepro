# XmlSerializer NullReferenceException after tier-up on .NET 10

Minimal reproduction of a JIT codegen bug where `XmlSerializer` begins throwing `NullReferenceException` once its runtime-emitted serialization methods are promoted to Tier-1.

**Regression: works on .NET 9, fails on .NET 10.**

Also doesn't work on .NET 11 preview 6.

## Symptom

```
System.InvalidOperationException: There was an error generating the XML document.
---> System.NullReferenceException: Object reference not set to an instance of an object.
    at Microsoft.Xml.Serialization.GeneratedAssembly.XmlSerializationWriterFoo.Write3_Foo(String n, String ns, Foo o, Boolean isNullable, Boolean needType)
    at Microsoft.Xml.Serialization.GeneratedAssembly.XmlSerializationWriterFoo.Write4_Foo(Object o)
    at InvokeStub_XmlSerializationWriterFoo.Write4_Foo(Object, Span`1)
    at System.Reflection.MethodBaseInvoker.InvokeWithOneArg(Object obj, BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)
    --- End of inner exception stack trace ---
    at System.Xml.Serialization.XmlSerializer.Serialize(XmlWriter xmlWriter, Object o, XmlSerializerNamespaces namespaces, String encodingStyle, String id)
    at System.Xml.Serialization.XmlSerializer.Serialize(XmlWriter xmlWriter, Object o)
    at WebApi.SerializationReproService.Serialization() in C:\Users\stefan.ostojic\source\repos\XmlSerializerBugRepro\XmlSerializerBugRepro\SerializationReproService.cs:line 75
    at WebApi.SerializationReproService.TryRun(Int32 count, Int32& failedAtCall) in C:\Users\stefan.ostojic\source\repos\XmlSerializerBugRepro\XmlSerializerBugRepro\SerializationReproService.cs:line 40
```

The first ~33 serialization calls succeed. From roughly the 34nd onward, every call fails, and it keeps failing until the process is restarted.

## Reproducing

1. Run the app in `Release` configuration, not `Debug`. 
2. It automatically starts a background service which runs the serialization. 
3. It throws an exception after ~34 iterations.

Exit code `1` means the bug reproduced; `0` means it did not.

### Hypothesis (based on AI)

Dynamic PGO devirtualizes the `IEnumerator<T>` interface calls in the emitted writer; the inliner then exposes the box's allocation site; escape analysis stack-allocates a box that does escape across the generated method's recursive self-call.

The IL here is machine-emitted by `XmlSerializationWriterILGen` into `Microsoft.Xml.Serialization.GeneratedAssembly` and has a shape a C# compiler would not produce, which may be why this went unnoticed.

## Workarounds

Any one of these 5 workarounds independently fix the issue:
- Setting the environment variable `DOTNET_TieredPGO` to `0`
- Setting the environment variable `DOTNET_JitNoInline` to `1`
- Setting the environment variable `DOTNET_JitObjectStackAllocation` to `0`
- `CustomList<T>` also implementing `ICollection`, instead of only `IEnumerable<T>`
- Building the app in `Debug` instead of `Release` configuration

And of course, since this seems to be introduced in .NET 10, switching to .NET 9 also fixes it:
- Changing the `<TargetFramework>` in `.csproj` from `net10.0` to older versions such as `net9.0` and `net8.0`

## Source-level fix

Implementing non-generic `ICollection`

```csharp
public class CustomList<T> : IEnumerable<T>, ICollection
{
    // existing members unchanged

    public int Count => _innerList.Count;
    public T this[int index]
    {
        get => _innerList[index];
        set => _innerList[index] = value;
    }
    void ICollection.CopyTo(Array array, int index) => ((ICollection)_innerList).CopyTo(array, index);
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;
}
```

AI says this moves `XmlSerializer` to its indexed-loop codegen path, eliminating the boxed enumerator entirely.

## Environment

```
.NET SDK:
 Version:           11.0.100-preview.6.26359.118
 Commit:            ba53d0ed33
 Workload version:  11.0.100-manifests.f9248ba3
 MSBuild version:   18.9.0-preview-26359-118+ba53d0ed3

Runtime Environment:
 OS Name:     Windows
 OS Version:  10.0.26200
 OS Platform: Windows
 RID:         win-x64
 Base Path:   C:\Program Files\dotnet\sdk\11.0.100-preview.6.26359.118\

.NET workloads installed:
There are no installed workloads to display.
Configured to use workload sets when installing new manifests.
No workload sets are installed. 

Host:
  Version:      11.0.0-preview.6.26359.118
  Architecture: x64
  Commit:       ba53d0ed33

.NET SDKs installed:
  1.0.0 [C:\Program Files\dotnet\sdk]
  3.1.426 [C:\Program Files\dotnet\sdk]
  5.0.408 [C:\Program Files\dotnet\sdk]
  7.0.317 [C:\Program Files\dotnet\sdk]
  7.0.410 [C:\Program Files\dotnet\sdk]
  8.0.206 [C:\Program Files\dotnet\sdk]
  8.0.423 [C:\Program Files\dotnet\sdk]
  9.0.119 [C:\Program Files\dotnet\sdk]
  9.0.310 [C:\Program Files\dotnet\sdk]
  9.0.316 [C:\Program Files\dotnet\sdk]
  10.0.300 [C:\Program Files\dotnet\sdk]
  10.0.302 [C:\Program Files\dotnet\sdk]
  10.0.400-preview.0.26322.102 [C:\Program Files\dotnet\sdk]
  11.0.100-preview.6.26359.118 [C:\Program Files\dotnet\sdk]

.NET runtimes installed:
  Microsoft.AspNetCore.All 2.1.30 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.All]
  Microsoft.AspNetCore.App 2.1.30 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 3.1.32 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 5.0.17 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 6.0.36 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 7.0.20 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 8.0.6 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 8.0.23 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 8.0.27 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 8.0.29 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 9.0.12 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 9.0.16 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 9.0.18 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 10.0.8 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 10.0.9 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 10.0.10 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 11.0.0-preview.6.26359.118 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.NETCore.App 1.0.4 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 1.1.1 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 2.1.30 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 2.2.8 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 3.1.32 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 5.0.17 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 6.0.36 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 7.0.20 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 8.0.6 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 8.0.23 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 8.0.27 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 8.0.29 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 9.0.12 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 9.0.16 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 9.0.18 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 10.0.8 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 10.0.9 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 10.0.10 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 11.0.0-preview.6.26359.118 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.WindowsDesktop.App 3.1.32 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
  Microsoft.WindowsDesktop.App 5.0.17 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
  Microsoft.WindowsDesktop.App 6.0.36 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
  Microsoft.WindowsDesktop.App 7.0.20 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
  Microsoft.WindowsDesktop.App 8.0.6 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
  Microsoft.WindowsDesktop.App 8.0.23 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
  Microsoft.WindowsDesktop.App 8.0.27 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
  Microsoft.WindowsDesktop.App 8.0.29 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
  Microsoft.WindowsDesktop.App 9.0.12 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
  Microsoft.WindowsDesktop.App 9.0.16 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
  Microsoft.WindowsDesktop.App 9.0.18 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
  Microsoft.WindowsDesktop.App 10.0.8 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
  Microsoft.WindowsDesktop.App 10.0.9 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
  Microsoft.WindowsDesktop.App 10.0.10 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
  Microsoft.WindowsDesktop.App 11.0.0-preview.6.26359.118 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]

Other architectures found:
  x86   [C:\Program Files (x86)\dotnet]
    registered at [HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x86\InstallLocation]

Environment variables:
  Not set

global.json file:
  Not found
```
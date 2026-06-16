// Polyfill: enables C# 9+ record/init features in netstandard2.1 targets.
// The real type lives in System.Private.CoreLib in .NET 5+; it is internal,
// so this internal copy is safe and does not conflict with newer runtimes.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

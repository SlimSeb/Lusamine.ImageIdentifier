#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices;

/// <summary>
/// Polyfill enabling <c>init</c>-only setters (and records) on target
/// frameworks before .NET 5
/// </summary>
internal static class IsExternalInit
{
}
#endif
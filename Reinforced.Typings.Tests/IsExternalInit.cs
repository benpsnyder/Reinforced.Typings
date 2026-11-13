#if NETFRAMEWORK
// Lets the record test cases compile on net481, which lacks this compiler-support type.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
#endif

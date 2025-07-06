#if NETFRAMEWORK
namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Indicates that when a method returns the specified boolean value,
    /// the associated parameter is not null.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class NotNullWhenAttribute : Attribute
    {
        public NotNullWhenAttribute(bool returnValue) { }
    }
}
#endif

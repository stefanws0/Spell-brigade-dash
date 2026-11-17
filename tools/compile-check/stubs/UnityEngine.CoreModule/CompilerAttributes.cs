// COMPILE-CHECK STUB ONLY.
// Real interop assemblies (e.g. Assembly-CSharp, Unity packages) can contain PUBLIC copies of compiler-generated
// attributes that the original C# compiler embedded as internal types. Il2CppInterop makes them public, and they
// have interop-style constructors, so if our code needs one of these the compiler binds to the broken copy and
// fails with CS0656. These stand-ins reproduce that, so the compile check catches it.
using System;

namespace System.Runtime.CompilerServices
{
    public sealed class NullableAttribute : Il2CppSystem.Object
    {
        public NullableAttribute(IntPtr pointer) : base(pointer) { }
    }

    public sealed class NullableContextAttribute : Il2CppSystem.Object
    {
        public NullableContextAttribute(IntPtr pointer) : base(pointer) { }
    }

    public sealed class IsReadOnlyAttribute : Il2CppSystem.Object
    {
        public IsReadOnlyAttribute(IntPtr pointer) : base(pointer) { }
    }
}

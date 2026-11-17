// COMPILE-CHECK STUB ONLY - not the real game assembly.
using System;
using Il2CppInterop.Runtime.InteropTypes;

namespace Il2CppSystem
{
    public class Object : Il2CppObjectBase
    {
        public Object(IntPtr pointer) : base(pointer) { }
        public Type GetIl2CppType() => null;
        public virtual string ToString() => null;
    }

    public class Type : Object
    {
        public Type(IntPtr pointer) : base(pointer) { }
        public string FullName => null;
        public string Name => null;
    }

    public class ValueType : Object
    {
        public ValueType(IntPtr pointer) : base(pointer) { }
    }
}

namespace Il2CppSystem.Collections.Generic
{
    public class List<T> : Il2CppSystem.Object
    {
        public List(IntPtr pointer) : base(pointer) { }
        public int Count => 0;
        public T this[int index] { get => default; set { } }
        public void Add(T item) { }
        public bool Remove(T item) => false;
        public bool Contains(T item) => false;
    }
}

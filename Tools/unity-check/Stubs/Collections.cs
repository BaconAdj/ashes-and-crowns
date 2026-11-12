using System; using System.Collections.Generic;
namespace Unity.Collections
{
    public struct NativeList<T> : IDisposable where T : unmanaged { public NativeList(Allocator a) { } public NativeList(int cap, Allocator a) { } public int Length => 0; public int Capacity { get => 0; set { } } public bool IsCreated => false; public T this[int i] { get => default; set { } } public void Add(in T v) { } public void AddNoResize(T v) { } public void Clear() { } public void Dispose() { } public void RemoveAtSwapBack(int i) { } public void Resize(int n, NativeArrayOptions o) { } public NativeArray<T> AsArray() => default; public ParallelWriter AsParallelWriter() => default; public struct ParallelWriter { public void AddNoResize(T v) { } }
        public Unity.Jobs.JobHandle Dispose(Unity.Jobs.JobHandle h) => default; }
    public struct NativeParallelMultiHashMap<K, V> : IDisposable where K : unmanaged, IEquatable<K> where V : unmanaged { public NativeParallelMultiHashMap(int cap, Allocator a) { } public bool IsCreated => false; public int Capacity { get => 0; set { } } public void Clear() { } public void Dispose() { } public void Add(K k, V v) { } public int Count() => 0;
        public bool TryGetFirstValue(K k, out V v, out NativeParallelMultiHashMapIterator<K> it) { v = default; it = default; return false; } public bool TryGetNextValue(out V v, ref NativeParallelMultiHashMapIterator<K> it) { v = default; return false; } public ParallelWriter AsParallelWriter() => default; public struct ParallelWriter { public void Add(K k, V v) { } } }
    public struct NativeParallelMultiHashMapIterator<K> where K : unmanaged { }
}

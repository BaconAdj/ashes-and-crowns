using UnityEngine;
namespace Unity.Mathematics
{
    public struct float2 { public float x, y; public float2(float x, float y) { this.x = x; this.y = y; } 
        public static float2 operator +(float2 a, float2 b) => default; public static float2 operator -(float2 a, float2 b) => default; public static float2 operator *(float2 a, float b) => default; public static float2 operator *(float a, float2 b) => default; public static float2 operator /(float2 a, float b) => default; public static float2 operator -(float2 a) => default;
        public static implicit operator Vector2(float2 a) => default; public static implicit operator float2(Vector2 a) => default; }
    public struct float3 { public float x, y, z; public float3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } public float3(float v) { x = y = z = v; }
        public float2 xz { get => default; set { } } public float2 xy => default; public static readonly float3 zero;
        public static float3 operator +(float3 a, float3 b) => default; public static float3 operator -(float3 a, float3 b) => default; public static float3 operator *(float3 a, float b) => default; public static float3 operator *(float a, float3 b) => default; public static float3 operator *(float3 a, float3 b) => default; public static float3 operator /(float3 a, float b) => default; public static float3 operator -(float3 a) => default;
        public static implicit operator Vector3(float3 a) => default; public static implicit operator float3(Vector3 a) => default; public static implicit operator float3(float a) => default; }
    public struct float4 { public float x, y, z, w; public float4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; } public float4(float3 a, float w) { x = y = z = 0; this.w = w; } public float3 xyz => default; public static implicit operator Vector4(float4 a) => default; public static implicit operator float4(Vector4 a) => default; }
    public struct quaternion { public float4 value; public static readonly quaternion identity; public static quaternion RotateY(float a) => default; public static quaternion RotateX(float a) => default; public static quaternion RotateZ(float a) => default; public static quaternion LookRotation(float3 f, float3 u) => default; public static quaternion LookRotationSafe(float3 f, float3 u) => default; public static quaternion AxisAngle(float3 a, float r) => default; public static quaternion EulerXYZ(float x, float y, float z) => default; public static quaternion Euler(float x, float y, float z) => default;
        public static implicit operator Quaternion(quaternion a) => default; public static implicit operator quaternion(Quaternion a) => default; }
    public struct float4x4 { public float4 c0, c1, c2, c3; public static readonly float4x4 identity; public static float4x4 TRS(float3 t, quaternion r, float3 s) => default; public static implicit operator Matrix4x4(float4x4 a) => default; public static implicit operator float4x4(Matrix4x4 a) => default; }
    public struct Random { public Random(uint seed) { } public float NextFloat() => 0; public float NextFloat(float a, float b) => 0; public int NextInt(int a, int b) => 0; }
    public static class math
    {
        public const float PI = 3.14159265f;
        public static float distancesq(float3 a, float3 b) => 0; public static float distancesq(float2 a, float2 b) => 0; public static float distance(float3 a, float3 b) => 0; public static float distance(float2 a, float2 b) => 0;
        public static float sin(float a) => 0; public static float cos(float a) => 0; public static float saturate(float a) => 0; public static float floor(float a) => 0; public static float abs(float a) => 0; public static int abs(int a) => 0;
        public static float min(float a, float b) => 0; public static int min(int a, int b) => 0; public static float max(float a, float b) => 0; public static int max(int a, int b) => 0;
        public static float lerp(float a, float b, float t) => 0; public static float3 lerp(float3 a, float3 b, float t) => default; public static float2 lerp(float2 a, float2 b, float t) => default;
        public static float atan2(float a, float b) => 0; public static float3 mul(quaternion q, float3 v) => default; public static quaternion mul(quaternion a, quaternion b) => default; public static float4x4 mul(float4x4 a, float4x4 b) => default;
        public static float length(float3 a) => 0; public static float length(float2 a) => 0; public static float lengthsq(float3 a) => 0; public static float lengthsq(float2 a) => 0; public static float sqrt(float a) => 0;
        public static float3 normalizesafe(float3 a, float3 d = default) => default; public static float2 normalizesafe(float2 a, float2 d = default) => default; public static float3 normalize(float3 a) => default; public static float fmod(float a, float b) => 0; public static float exp(float a) => 0;
        public static float clamp(float a, float b, float c) => 0; public static int clamp(int a, int b, int c) => 0; public static float dot(float3 a, float3 b) => 0; public static float dot(float2 a, float2 b) => 0; public static float3 cross(float3 a, float3 b) => default; public static float radians(float a) => 0; public static float sign(float a) => 0; public static float pow(float a, float b) => 0; public static float select(float a, float b, bool c) => 0;
    }
}

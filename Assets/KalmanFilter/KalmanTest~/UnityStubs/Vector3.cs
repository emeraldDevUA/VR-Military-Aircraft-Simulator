// Stub of UnityEngine.Vector3 so the filter compiles outside Unity.
// Do NOT copy into Unity: it has the real Vector3.
namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;

        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }

        public float magnitude => (float)System.Math.Sqrt(x * x + y * y + z * z);

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float k) => new Vector3(a.x * k, a.y * k, a.z * k);

        public override string ToString() => $"({x:F1}, {y:F1}, {z:F1})";
    }
}

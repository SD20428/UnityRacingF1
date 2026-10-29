using UnityEngine;

namespace Overdrive
{
    // Projects onto the accepted checkpoint segment, never the nearest world-space branch.
    public sealed class TrackReferenceAdapter
    {
        readonly WaypointPath path;
        readonly float[] cumulative;
        public float Length { get; }
        public bool Valid => path && path.Valid && Length > .01f;
        public TrackReferenceAdapter(WaypointPath source)
        {
            path = source;
            if (!path || !path.Valid) return;
            cumulative = new float[path.points.Length + 1];
            for (int i = 0; i < path.points.Length; i++)
                cumulative[i + 1] = cumulative[i] + Vector3.Distance(path.Position(i), path.Position(i + 1));
            Length = cumulative[cumulative.Length - 1];
        }

        public bool TryProject(CircuitProgress progress, Vector3 position, out TrackPosition result)
        {
            result = default;
            if (!Valid || !progress || !progress.enabled || progress.checkpoints == null ||
                progress.checkpoints.Length != path.points.Length || !Finite(position)) return false;
            int index = progress.LastCheckpoint;
            if (index < 0 || index >= path.points.Length) return false;
            // A different circuit must not be interpreted in this reference frame.
            for (int i = 0; i < path.points.Length; i++)
                if (progress.checkpoints[i] != path.points[i]) return false;
            Vector3 start = path.Position(index), segment = path.Position(index + 1) - start;
            float length = segment.magnitude;
            if (length < .01f || !Finite(segment)) return false;
            Vector3 tangent = segment / length;
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            if (right.sqrMagnitude < .5f) return false;
            float along = Mathf.Clamp(Vector3.Dot(position - start, tangent), 0, length);
            result = new TrackPosition
            {
                distance = cumulative[index] + along,
                lateral = Vector3.Dot(position - (start + tangent * along), right),
                tangent = tangent, segment = index
            };
            return Finite(result.distance) && Finite(result.lateral);
        }

        public static float SignedDistance(float from, float to, float length)
        {
            if (!Finite(from) || !Finite(to) || !Finite(length) || length <= .01f) return float.NaN;
            return Mathf.Repeat(to - from + length * .5f, length) - length * .5f;
        }
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }

    public struct TrackPosition
    {
        public float distance, lateral;
        public Vector3 tangent;
        public int segment;
    }
}

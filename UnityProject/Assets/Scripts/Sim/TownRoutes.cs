using System;
using System.Collections.Generic;

namespace AshesAndCrowns.Sim
{
    /// <summary>
    /// The way through a town on foot: along the streets, not through the houses. Built from street centre lines; streets
    /// that cross are joined where they cross. A route goes from the nearest point of the nearest street, by the shortest
    /// way along the streets, to the nearest point of the street nearest the destination.
    /// </summary>
    public sealed class TownRoutes
    {
        private readonly List<float> _nx = new List<float>(64), _nz = new List<float>(64);
        private readonly List<int> _ea = new List<int>(96), _eb = new List<int>(96);
        private readonly List<float> _el = new List<float>(96);
        private float[] _dist; private int[] _prev; private bool[] _done;

        public int NodeCount => _nx.Count;
        public int EdgeCount => _ea.Count;

        /// <summary>segments: x0, z0, x1, z1 per street.</summary>
        public TownRoutes(IList<float> segments)
        {
            int count = segments.Count / 4;
            for (int i = 0; i < count; i++)
            {
                float x0 = segments[i * 4], z0 = segments[i * 4 + 1], x1 = segments[i * 4 + 2], z1 = segments[i * 4 + 3];
                // every place another street meets this one, as a fraction along it
                var cuts = new List<float> { 0f, 1f };
                for (int j = 0; j < count; j++)
                {
                    if (j == i) continue;
                    if (Meet(x0, z0, x1, z1, segments[j * 4], segments[j * 4 + 1], segments[j * 4 + 2], segments[j * 4 + 3], out float t)) cuts.Add(t);
                }
                cuts.Sort();
                int prev = -1; float prevT = 0f;
                foreach (float t in cuts)
                {
                    if (prev >= 0 && t - prevT < 1e-4f) continue;
                    int n = Node(x0 + (x1 - x0) * t, z0 + (z1 - z0) * t);
                    if (prev >= 0 && n != prev) { _ea.Add(prev); _eb.Add(n); _el.Add(Len(prev, n)); }
                    prev = n; prevT = t;
                }
            }
            _dist = new float[_nx.Count + 2]; _prev = new int[_nx.Count + 2]; _done = new bool[_nx.Count + 2];
        }

        private float Len(int a, int b) { float dx = _nx[a] - _nx[b], dz = _nz[a] - _nz[b]; return (float)Math.Sqrt(dx * dx + dz * dz); }

        private int Node(float x, float z)
        {
            for (int i = 0; i < _nx.Count; i++) if (Math.Abs(_nx[i] - x) < 4.5f && Math.Abs(_nz[i] - z) < 4.5f) return i;   // lanes that stop a few metres short of a street still join it
            _nx.Add(x); _nz.Add(z); return _nx.Count - 1;
        }

        /// <summary>Does segment B touch or come within a few metres of segment A? t is where along A.</summary>
        private static bool Meet(float ax0, float az0, float ax1, float az1, float bx0, float bz0, float bx1, float bz1, out float t)
        {
            t = 0f;
            float adx = ax1 - ax0, adz = az1 - az0, bdx = bx1 - bx0, bdz = bz1 - bz0;
            float den = adx * bdz - adz * bdx;
            float alen = (float)Math.Sqrt(adx * adx + adz * adz), blen = (float)Math.Sqrt(bdx * bdx + bdz * bdz);
            if (alen < 1e-3f || blen < 1e-3f) return false;
            if (Math.Abs(den) > 1e-5f)
            {
                float ta = ((bx0 - ax0) * bdz - (bz0 - az0) * bdx) / den, tb = ((bx0 - ax0) * adz - (bz0 - az0) * adx) / den;
                float slackA = 5f / alen, slackB = 5f / blen;
                if (ta < -slackA || ta > 1f + slackA || tb < -slackB || tb > 1f + slackB) return false;
                t = ta < 0f ? 0f : ta > 1f ? 1f : ta; return true;
            }
            return false;   // parallel streets do not join
        }

        private int NearestEdge(float x, float z, out float px, out float pz)
        {
            int best = -1; float bestD = float.MaxValue; px = x; pz = z;
            for (int e = 0; e < _ea.Count; e++)
            {
                float x0 = _nx[_ea[e]], z0 = _nz[_ea[e]], dx = _nx[_eb[e]] - x0, dz = _nz[_eb[e]] - z0; float l2 = dx * dx + dz * dz;
                float t = l2 > 1e-6f ? ((x - x0) * dx + (z - z0) * dz) / l2 : 0f; t = t < 0f ? 0f : t > 1f ? 1f : t;
                float qx = x0 + dx * t, qz = z0 + dz * t; float d = (qx - x) * (qx - x) + (qz - z) * (qz - z);
                if (d < bestD) { bestD = d; best = e; px = qx; pz = qz; }
            }
            return best;
        }

        /// <summary>Writes the waypoints (x, z pairs) from the street nearest `from` to the street nearest `to` into `into`
        /// and returns how many points were written (0 when there are no streets). Never more than into.Length / 2.</summary>
        public int Route(float fromX, float fromZ, float toX, float toZ, float[] into)
        {
            if (_ea.Count == 0 || into == null || into.Length < 4) return 0;
            int ea = NearestEdge(fromX, fromZ, out float ax, out float az), eb = NearestEdge(toX, toZ, out float bx, out float bz);
            int max = into.Length / 2, n = 0;
            if (ea == eb) { into[0] = ax; into[1] = az; into[2] = bx; into[3] = bz; return 2; }
            int N = _nx.Count, S = N, T = N + 1;
            for (int i = 0; i < N + 2; i++) { _dist[i] = float.MaxValue; _prev[i] = -1; _done[i] = false; }
            _dist[S] = 0f;
            while (true)
            {
                int u = -1; float du = float.MaxValue;
                for (int i = 0; i < N + 2; i++) if (!_done[i] && _dist[i] < du) { du = _dist[i]; u = i; }
                if (u < 0 || u == T) break;
                _done[u] = true;
                if (u == S)
                {
                    Relax(S, _ea[ea], D(ax, az, _ea[ea])); Relax(S, _eb[ea], D(ax, az, _eb[ea]));
                    continue;
                }
                for (int e = 0; e < _ea.Count; e++)
                {
                    if (_ea[e] == u) Relax(u, _eb[e], _el[e]); else if (_eb[e] == u) Relax(u, _ea[e], _el[e]);
                }
                if (u == _ea[eb] || u == _eb[eb]) Relax(u, T, D(bx, bz, u));
            }
            if (_prev[T] < 0) { into[0] = ax; into[1] = az; into[2] = bx; into[3] = bz; return 2; }   // streets not joined: go straight
            // walk back from the end, then reverse
            int len = 0; for (int v = T; v >= 0; v = _prev[v]) len++;
            int skip = len > max ? len - max : 0;
            int k = len - 1 - skip;
            for (int v = T; v >= 0; v = _prev[v])
            {
                if (k >= 0 && k < max)
                {
                    float x = v == T ? bx : v == S ? ax : _nx[v], z = v == T ? bz : v == S ? az : _nz[v];
                    into[k * 2] = x; into[k * 2 + 1] = z; n++;
                }
                k--;
            }
            return n;
        }

        private float D(float x, float z, int node) { float dx = _nx[node] - x, dz = _nz[node] - z; return (float)Math.Sqrt(dx * dx + dz * dz); }
        private void Relax(int u, int v, float w) { float d = _dist[u] + w; if (d < _dist[v]) { _dist[v] = d; _prev[v] = u; } }
    }
}

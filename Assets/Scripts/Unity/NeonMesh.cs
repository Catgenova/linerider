using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonLineRider.Unity
{
    /// <summary>
    /// Accumulates coloured quads/triangles for one material and pushes them into a Mesh each frame.
    /// Positions are given in simulation pixels (y down) unless a method says "Units".
    /// </summary>
    public sealed class NeonMesh
    {
        private readonly List<Vector3> _verts = new List<Vector3>(4096);
        private readonly List<Color32> _colors = new List<Color32>(4096);
        private readonly List<int> _tris = new List<int>(8192);
        private readonly Mesh _mesh;
        public readonly GameObject GameObject;
        public readonly MeshRenderer Renderer;
        public readonly Material Material;
        public float Z;

        public NeonMesh(string name, Material material, float z, Transform parent)
        {
            Material = material;
            Z = z;
            GameObject = new GameObject(name);
            GameObject.transform.SetParent(parent, false);
            var filter = GameObject.AddComponent<MeshFilter>();
            Renderer = GameObject.AddComponent<MeshRenderer>();
            Renderer.sharedMaterial = material;
            Renderer.shadowCastingMode = ShadowCastingMode.Off;
            Renderer.receiveShadows = false;
            _mesh = new Mesh { name = name };
            _mesh.MarkDynamic();
            _mesh.indexFormat = IndexFormat.UInt32;
            filter.sharedMesh = _mesh;
        }

        public int VertexCount => _verts.Count;

        public void Clear()
        {
            _verts.Clear();
            _colors.Clear();
            _tris.Clear();
        }

        public void Apply()
        {
            _mesh.Clear(false);
            if (_verts.Count == 0)
            {
                Renderer.enabled = false;
                return;
            }
            Renderer.enabled = true;
            _mesh.SetVertices(_verts);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_tris, 0, false);
            _mesh.RecalculateBounds();
        }

        public void QuadUnits(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 ca, Color32 cb, Color32 cc, Color32 cd)
        {
            int i = _verts.Count;
            a.z = b.z = c.z = d.z = Z;
            _verts.Add(a);
            _verts.Add(b);
            _verts.Add(c);
            _verts.Add(d);
            _colors.Add(ca);
            _colors.Add(cb);
            _colors.Add(cc);
            _colors.Add(cd);
            _tris.Add(i);
            _tris.Add(i + 1);
            _tris.Add(i + 2);
            _tris.Add(i);
            _tris.Add(i + 2);
            _tris.Add(i + 3);
        }

        public void QuadUnits(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 color)
        {
            QuadUnits(a, b, c, d, color, color, color, color);
        }

        public void TriangleUnits(Vector3 a, Vector3 b, Vector3 c, Color32 ca, Color32 cb, Color32 cc)
        {
            int i = _verts.Count;
            a.z = b.z = c.z = Z;
            _verts.Add(a);
            _verts.Add(b);
            _verts.Add(c);
            _colors.Add(ca);
            _colors.Add(cb);
            _colors.Add(cc);
            _tris.Add(i);
            _tris.Add(i + 1);
            _tris.Add(i + 2);
        }

        /// <summary>A line segment in sim px with a width in sim px, round-ish caps by overshoot.</summary>
        public void Segment(double x1, double y1, double x2, double y2, double widthPx, Color32 color, bool caps = true)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6)
            {
                dx = 1;
                dy = 0;
                len = 1;
            }
            double ux = dx / len;
            double uy = dy / len;
            double hw = widthPx / 2;
            double cap = caps ? hw : 0;
            double nx = -uy * hw;
            double ny = ux * hw;
            double ax = x1 - ux * cap, ay = y1 - uy * cap;
            double bx = x2 + ux * cap, by = y2 + uy * cap;
            QuadUnits(U.W(ax + nx, ay + ny), U.W(bx + nx, by + ny), U.W(bx - nx, by - ny), U.W(ax - nx, ay - ny), color);
        }

        public void SegmentGradient(double x1, double y1, double x2, double y2, double widthPx, Color32 c1, Color32 c2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6) return;
            double nx = -dy / len * widthPx / 2;
            double ny = dx / len * widthPx / 2;
            QuadUnits(U.W(x1 + nx, y1 + ny), U.W(x2 + nx, y2 + ny), U.W(x2 - nx, y2 - ny), U.W(x1 - nx, y1 - ny), c1, c2, c2, c1);
        }

        public void Polyline(IList<Vec2dF> points, double widthPx, Color32 color)
        {
            for (int i = 1; i < points.Count; i++)
            {
                Segment(points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y, widthPx, color);
            }
        }

        public void Circle(double cx, double cy, double r, double widthPx, Color32 color, int segments = 28)
        {
            double prevX = cx + r, prevY = cy;
            for (int i = 1; i <= segments; i++)
            {
                double a = i / (double)segments * Math.PI * 2;
                double x = cx + Math.Cos(a) * r;
                double y = cy + Math.Sin(a) * r;
                Segment(prevX, prevY, x, y, widthPx, color, false);
                prevX = x;
                prevY = y;
            }
        }

        public void Disc(double cx, double cy, double r, Color32 color, int segments = 24)
        {
            Vector3 c = U.W(cx, cy);
            double prevX = cx + r, prevY = cy;
            for (int i = 1; i <= segments; i++)
            {
                double a = i / (double)segments * Math.PI * 2;
                double x = cx + Math.Cos(a) * r;
                double y = cy + Math.Sin(a) * r;
                TriangleUnits(c, U.W(prevX, prevY), U.W(x, y), color, color, color);
                prevX = x;
                prevY = y;
            }
        }

        /// <summary>Radial gradient disc: centre colour fading to edge colour.</summary>
        public void DiscGradient(double cx, double cy, double r, Color32 center, Color32 edge, int segments = 32)
        {
            Vector3 c = U.W(cx, cy);
            double prevX = cx + r, prevY = cy;
            for (int i = 1; i <= segments; i++)
            {
                double a = i / (double)segments * Math.PI * 2;
                double x = cx + Math.Cos(a) * r;
                double y = cy + Math.Sin(a) * r;
                TriangleUnits(c, U.W(prevX, prevY), U.W(x, y), center, edge, edge);
                prevX = x;
                prevY = y;
            }
        }

        /// <summary>Axis-aligned rectangle in sim px.</summary>
        public void Rect(double x, double y, double w, double h, Color32 color)
        {
            QuadUnits(U.W(x, y), U.W(x + w, y), U.W(x + w, y + h), U.W(x, y + h), color);
        }

        public void RectGradient(double x, double y, double w, double h, Color32 top, Color32 bottom)
        {
            QuadUnits(U.W(x, y), U.W(x + w, y), U.W(x + w, y + h), U.W(x, y + h), top, top, bottom, bottom);
        }

        public void RectOutline(double x, double y, double w, double h, double widthPx, Color32 color)
        {
            Segment(x, y, x + w, y, widthPx, color, false);
            Segment(x + w, y, x + w, y + h, widthPx, color, false);
            Segment(x + w, y + h, x, y + h, widthPx, color, false);
            Segment(x, y + h, x, y, widthPx, color, false);
        }

        public void Polygon(IList<Vec2dF> points, Color32 color)
        {
            if (points.Count < 3) return;
            Vector3 a = U.W(points[0].X, points[0].Y);
            for (int i = 1; i < points.Count - 1; i++)
            {
                TriangleUnits(a, U.W(points[i].X, points[i].Y), U.W(points[i + 1].X, points[i + 1].Y), color, color, color);
            }
        }
    }

    /// <summary>Lightweight point used by the renderer (sim px).</summary>
    public struct Vec2dF
    {
        public double X;
        public double Y;

        public Vec2dF(double x, double y)
        {
            X = x;
            Y = y;
        }
    }
}

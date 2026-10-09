using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SpaceScape.Ship
{
    /// A still copy of an item, only its meshes, cut off by up to two planes: what an item conduit shows of an item
    /// travelling through it (loading.md → Item conduits). Each frame it is placed, every mesh is clipped on the CPU
    /// to the side of both planes it is kept on, so an item is swallowed by a solid corner or grows out of a wall
    /// whatever its materials. A mesh wholly kept is drawn as it is; one wholly cut off is hidden. Never spawned,
    /// nothing to grab, no colliders. An item's meshes must be Read/Write enabled to be cut.
    public sealed class ClippedItemPicture
    {
        private sealed class Part
        {
            public Transform Transform;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Mesh Source;
            public Mesh Cut;
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public Vector2[] Uvs;
            public int[][] Triangles;
        }

        private static readonly List<Vector3> Positions = new List<Vector3>();
        private static readonly List<Vector3> NormalsOut = new List<Vector3>();
        private static readonly List<Vector2> UvsOut = new List<Vector2>();
        private static readonly List<int>[] TrianglesOut = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        private static readonly List<int> Polygon = new List<int>(8);
        private static readonly List<int> Clipped = new List<int>(8);
        private static readonly HashSet<Mesh> Warned = new HashSet<Mesh>();

        private readonly List<Part> _parts = new List<Part>();

        /// Its root, at the item's origin.
        public Transform Root { get; }

        /// The item it shows.
        public Item Prefab { get; }

        /// The item's length along its own up, in metres: how far it travels to pass a plane completely.
        public float Length { get; }

        public ClippedItemPicture(Item prefab, string name)
        {
            Prefab = prefab;
            Root = new GameObject(name).transform;
            Length = prefab.TryGetComponent<BoxCollider>(out var box) ? box.size.y * Mathf.Abs(prefab.transform.lossyScale.y) : 1f;

            var source = prefab.transform;
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var t = renderer.transform;
                var go = new GameObject(t.name);
                go.layer = t.gameObject.layer;
                go.transform.SetParent(Root, false);
                go.transform.localPosition = source.InverseTransformPoint(t.position);
                go.transform.localRotation = Quaternion.Inverse(source.rotation) * t.rotation;
                go.transform.localScale = t.lossyScale;
                var part = new Part
                {
                    Transform = go.transform,
                    Filter = go.AddComponent<MeshFilter>(),
                    Renderer = go.AddComponent<MeshRenderer>(),
                    Source = filter.sharedMesh,
                };
                part.Filter.sharedMesh = part.Source;
                part.Renderer.sharedMaterials = renderer.sharedMaterials;
                if (part.Source.isReadable)
                {
                    part.Vertices = part.Source.vertices;
                    part.Normals = part.Source.normals;
                    part.Uvs = part.Source.uv;
                    part.Triangles = new int[Mathf.Min(part.Source.subMeshCount, TrianglesOut.Length)][];
                    for (int i = 0; i < part.Triangles.Length; i++) part.Triangles[i] = part.Source.GetTriangles(i);
                    part.Cut = new Mesh { name = $"{part.Source.name} (cut)" };
                    part.Cut.MarkDynamic();
                }
                else if (Warned.Add(part.Source))
                    Debug.LogWarning($"ClippedItemPicture: mesh '{part.Source.name}' of '{prefab.name}' is not Read/Write enabled, so it is shown uncut", prefab);
                _parts.Add(part);
            }
        }

        /// Places it and cuts off everything on the far side of either plane (a plane keeps the side its normal
        /// points to).
        public void Show(Vector3 position, Quaternion rotation, Plane keepA, Plane keepB)
        {
            if (!Root.gameObject.activeSelf) Root.gameObject.SetActive(true);
            Root.SetPositionAndRotation(position, rotation);
            foreach (var part in _parts) Cut(part, keepA, keepB);
        }

        public void Hide()
        {
            if (Root.gameObject.activeSelf) Root.gameObject.SetActive(false);
        }

        public void Destroy()
        {
            foreach (var part in _parts) if (part.Cut != null) Object.Destroy(part.Cut);
            if (Root != null) Object.Destroy(Root.gameObject);
        }

        private static void Cut(Part part, Plane a, Plane b)
        {
            if (part.Vertices == null) { part.Renderer.enabled = true; return; }

            // Into the mesh's own space: the signed distance stays exact under any scale.
            var toWorld = part.Transform.localToWorldMatrix;
            var la = Local(a, toWorld, part.Transform);
            var lb = Local(b, toWorld, part.Transform);
            var v = part.Vertices;
            bool allKept = true, allGoneA = true, allGoneB = true;
            for (int i = 0; i < v.Length; i++)
            {
                float da = la.GetDistanceToPoint(v[i]), db = lb.GetDistanceToPoint(v[i]);
                if (da < 0f || db < 0f) allKept = false;
                if (da >= 0f) allGoneA = false;
                if (db >= 0f) allGoneB = false;
            }
            if (allGoneA || allGoneB) { part.Renderer.enabled = false; return; }
            part.Renderer.enabled = true;
            if (allKept)
            {
                if (part.Filter.sharedMesh != part.Source) part.Filter.sharedMesh = part.Source;
                return;
            }

            Positions.Clear();
            NormalsOut.Clear();
            UvsOut.Clear();
            for (int s = 0; s < part.Triangles.Length; s++)
            {
                var into = TrianglesOut[s];
                into.Clear();
                var tris = part.Triangles[s];
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    Polygon.Clear();
                    for (int k = 0; k < 3; k++) Polygon.Add(Add(part, tris[t + k]));
                    ClipPolygon(la);
                    if (Polygon.Count >= 3) ClipPolygon(lb);
                    for (int k = 1; k + 1 < Polygon.Count; k++)
                    {
                        into.Add(Polygon[0]);
                        into.Add(Polygon[k]);
                        into.Add(Polygon[k + 1]);
                    }
                }
            }

            var cut = part.Cut;
            cut.Clear();
            cut.SetVertices(Positions);
            if (NormalsOut.Count == Positions.Count) cut.SetNormals(NormalsOut);
            if (UvsOut.Count == Positions.Count) cut.SetUVs(0, UvsOut);
            cut.subMeshCount = part.Triangles.Length;
            for (int s = 0; s < part.Triangles.Length; s++) cut.SetTriangles(TrianglesOut[s], s, false);
            cut.RecalculateBounds();
            if (part.Filter.sharedMesh != cut) part.Filter.sharedMesh = cut;
        }

        private static Plane Local(Plane world, Matrix4x4 toWorld, Transform t)
        {
            Vector3 point = t.InverseTransformPoint(-world.normal * world.distance);
            Vector3 normal = toWorld.transpose.MultiplyVector(world.normal);
            return new Plane(normal, point);
        }

        /// Copies a source vertex into the output, returning its index there.
        private static int Add(Part part, int i)
        {
            Positions.Add(part.Vertices[i]);
            if (part.Normals.Length == part.Vertices.Length) NormalsOut.Add(part.Normals[i]);
            if (part.Uvs.Length == part.Vertices.Length) UvsOut.Add(part.Uvs[i]);
            return Positions.Count - 1;
        }

        /// Clips the polygon in Polygon to the kept side of a plane (Sutherland-Hodgman), adding new vertices
        /// where its edges cross.
        private static void ClipPolygon(Plane plane)
        {
            Clipped.Clear();
            for (int i = 0; i < Polygon.Count; i++)
            {
                int a = Polygon[i], b = Polygon[(i + 1) % Polygon.Count];
                float da = plane.GetDistanceToPoint(Positions[a]), db = plane.GetDistanceToPoint(Positions[b]);
                if (da >= 0f) Clipped.Add(a);
                if ((da >= 0f) == (db >= 0f)) continue;
                float t = da / (da - db);
                Positions.Add(Vector3.Lerp(Positions[a], Positions[b], t));
                if (NormalsOut.Count > a && NormalsOut.Count > b) NormalsOut.Add(Vector3.Lerp(NormalsOut[a], NormalsOut[b], t).normalized);
                if (UvsOut.Count > a && UvsOut.Count > b) UvsOut.Add(Vector2.Lerp(UvsOut[a], UvsOut[b], t));
                Clipped.Add(Positions.Count - 1);
            }
            Polygon.Clear();
            Polygon.AddRange(Clipped);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry
{
    class MeshData
    {
        public Vector3[] vertices;
        public int[] triangles;
        public Bounds bounds;

        MeshData() { }

        public MeshData(Vector3[] vertices, int[] tris, Bounds bounds)
        {
            this.vertices = vertices;
            this.triangles = tris;
            this.bounds = bounds;
        }
    }
}

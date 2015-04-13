using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Microsoft.Kinect.Toolkit;
using Microsoft.Kinect.Toolkit.Fusion;
namespace Microsoft.Kinect.InteractiveKinectFusionExplorer
{
    /// <summary>
    /// CustomMesh is based on ColorMesh in Microsoft.Kinect.Toolkit.Fusion
    /// but contains editable datalists for its components rather than ReadOnlyCollections
    /// ...this is also the reason why this cant inherit ColorMesh
    /// Objects from this class are only used for mesh export in interaction mode
    /// </summary>
    public class CustomMesh
    {
        /// <summary>
        /// The vertices collection.
        /// </summary>
        private List<Vector3> vertices = new List<Vector3>();

        /// <summary>
        /// The normals collection.
        /// </summary>
        private List<Vector3> normals = new List<Vector3>();

        /// <summary>
        /// The triangle indexes collection.
        /// </summary>
        private List<int> triangleIndexes = new List<int>();

        /// <summary>
        /// The colors collection.
        /// </summary>
        private List<int> colors = new List<int>();

        /// <summary>
        /// Initializes a CustomMesh object
        /// </summary>
        public CustomMesh() { }

        /// <summary>
        /// Gets the collection of vertices. Each vertex has a corresponding normal with the same index.
        /// </summary>
        /// <returns>Returns the collection of the vertices.</returns>
        public List<Vector3> GetVertices()
        {
            return this.vertices;
        }

        /// <summary>
        /// Gets the collection of normals. Each normal has a corresponding vertex with the same index.
        /// </summary>
        /// <returns>Returns the collection of the normals.</returns>
        public List<Vector3> GetNormals()
        {
            return this.normals;
        }

        /// <summary>
        /// Gets the collection of triangle indexes. There are 3 indexes per triangle.
        /// </summary>
        /// <returns>Returns the collection of the triangle indexes.</returns>
        public List<int> GetTriangleIndexes()
        {
            return this.triangleIndexes;
        }

        /// <summary>
        /// Gets the collection of colors. There is one color per-vertex. Each color has a corresponding
        /// vertex with the same index.
        /// </summary>
        /// <returns>Returns the collection of the colors.</returns>
        public List<int> GetColors()
        {
            return this.colors;
        }

        /// <summary>
        /// Adds a vertex to the vertex collection
        /// </summary>
        /// <param name="vertice">Vertex</param>
        public void AddVertice(Vector3 vertice)
        {
            this.vertices.Add(vertice);
        }

        /// <summary>
        /// Adds a normal to the normal collection
        /// </summary>
        /// <param name="normal">Normal</param>
        public void AddNormal(Vector3 normal)
        {
            this.normals.Add(normal);
        }

        /// <summary>
        /// Adds a triangle index to the triangle index collection
        /// </summary>
        /// <param name="triangle">triangle index</param>
        public void AddTriangleIndex(int triangle)
        {
            this.triangleIndexes.Add(triangle);
        }

        /// <summary>
        /// Adds a color value to the color collection
        /// </summary>
        /// <param name="color">Color</param>
        public void AddColor(int color)
        {
            this.colors.Add(color);
        }
    }
}

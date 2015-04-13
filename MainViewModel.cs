using HelixToolkit.Wpf;
using Microsoft.Kinect.Toolkit.Fusion;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Microsoft.Kinect.InteractiveKinectFusionExplorer
{
    public class MainViewModel
    {
        //model group that stores all the models being displayed
        private Model3DGroup modelGroup;

        //wall model is stored separately to speed up comparison
        private GeometryModel3D wallModel;

        //optional functions
        private bool reduceVertices = true;
        private bool segmentMesh = true;
        private bool segmentWall = true;
        private bool interpolateColor = false;
        private bool captureColor = false;
        private float blockSize = 0.20f;
        private float wallSensitivity = 0.20f;

        //used for mesh segmentation
        private double minZ = double.PositiveInfinity;
        private double minX = double.PositiveInfinity;
        private double minY = double.PositiveInfinity;
        private double maxZ = double.NegativeInfinity;
        private double maxY = double.NegativeInfinity;
        private double maxX = double.NegativeInfinity;

        //color storage if color was captured
        Dictionary<MeshGeometry3D, List<int>> colors = new Dictionary<MeshGeometry3D, List<int>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class.
        /// This is only for testing purposes and imports an external model file
        /// </summary>
        /// <param name="fileName">file name of the model</param>
        public MainViewModel(String fileName)
        {
            ModelImporter importer = new ModelImporter();
            modelGroup = importer.Load(@"fileName");

            this.Model = modelGroup;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class.
        /// Initializes all relevant values, options and builds the model
        /// </summary>
        /// <param name="mesh">Mesh that will be displayed</param>
        /// <param name="reduceVertices">indicates if duplicate vertices should be removed</param>
        /// <param name="segmentMesh">indicates if the mesh should automatically be segmented in equal blocks</param>
        /// <param name="segmentWall">indicates if the wall should automatically be segmented</param>
        /// <param name="blockSize">iff 'segmentMesh' is true, this specifies the block size</param>
        /// <param name="wallSensitivity">iff 'segmentWall' is true, this specifies the wall depth</param>
        /// <param name="interpolateColor">iff 'captureColor' and 'segmentMesh' is true, this indicates if segment colors should be interpolated values of the real pixel colors</param>
        /// <param name="captureColor">indicates if color data is present</param>
        public MainViewModel(ColorMesh mesh, bool reduceVertices, bool segmentMesh, bool segmentWall,
            float blockSize, float wallSensitivity, bool interpolateColor, bool captureColor)
        {
            this.blockSize = blockSize;
            this.wallSensitivity = wallSensitivity;
            this.segmentWall = segmentWall;
            this.reduceVertices = reduceVertices;
            this.segmentMesh = segmentMesh;
            this.interpolateColor = interpolateColor;
            this.captureColor = captureColor;
            modelGroup = new Model3DGroup();

            minZ = double.PositiveInfinity;
            minX = double.PositiveInfinity;
            minY = double.PositiveInfinity;
            maxZ = double.NegativeInfinity;
            maxY = double.NegativeInfinity;
            maxX = double.NegativeInfinity;

            MeshGeometry3D newMesh = ConvertToMeshGeometry(mesh);

            if (this.reduceVertices)
            {
                newMesh = DeleteDuplicateVerticesAndTriangles(newMesh);
            }

            if (this.segmentMesh)
            {
                modelGroup = SegmentMesh(newMesh, this.blockSize, this.segmentWall);
            }
            else
            {
                //load whole mesh as one
                GeometryModel3D geoModel3D = new GeometryModel3D();
                var blueMaterial = MaterialHelper.CreateMaterial(Colors.Blue);
                geoModel3D.Geometry = newMesh;
                geoModel3D.Material = blueMaterial;
                modelGroup.Children.Add(geoModel3D);
            }
            this.Model = modelGroup;

        }

        /// <summary>
        /// Gets or sets the main model.
        /// </summary>
        /// <value>The model.</value>
        public Model3D Model { get; set; }

        /// <summary>
        /// Converts a ColorMesh to a MeshGeometry3D object for easier handling and displaying
        /// note that color is being stored separately in the colors-Dictionary
        /// </summary>
        /// <param name="colorMesh">ColorMesh</param>
        /// <returns>A MeshGeometry3D object containing same data except color</returns>
        private MeshGeometry3D ConvertToMeshGeometry(ColorMesh colorMesh)
        {
            MeshGeometry3D meshGeometry = new MeshGeometry3D();
            List<int> meshColors = new List<int>();
            for (int i = 0; i < colorMesh.GetVertices().Count; i++)
            {
                Vector3 vec = colorMesh.GetVertices()[i];
                Vector3 norm = colorMesh.GetNormals()[i];
                meshColors.Add(colorMesh.GetColors()[i]);
                meshGeometry.Positions.Add(new Point3D(vec.X, vec.Y * -1, vec.Z * -1));
                meshGeometry.Normals.Add(new Vector3D(norm.X, norm.Y * -1, norm.Z * -1));
                if (vec.X >= maxX) maxX = vec.X;
                if (vec.Y * -1 >= maxY) maxY = vec.Y * -1;
                if (vec.Z * -1 >= maxZ) maxZ = vec.Z * -1;
                if (vec.X <= minX) minX = vec.X;
                if (vec.Y * -1 <= minY) minY = vec.Y * -1;
                if (vec.Z * -1 <= minZ) minZ = vec.Z * -1;
            }
            foreach (int triangle in colorMesh.GetTriangleIndexes())
            {
                meshGeometry.TriangleIndices.Add(triangle);
            }
            colors.Add(meshGeometry, meshColors);
            return meshGeometry;
        }

        /// <summary>
        /// Segments a mesh into equal parts
        /// note that color is being stored separately in the colors-Dictionary
        /// </summary>
        /// <param name="mesh">Mesh</param>
        /// <param name="blockSize">size of segments in EACH direction</param>
        /// <param name="segmentWall">indicates if the wall should be automatically segmented</param>
        /// <returns>model group containing all the new segments, but no color</returns>
        private Model3DGroup SegmentMesh(MeshGeometry3D mesh, float blockSize, bool segmentWall)
        {
            //material and colors for easier processing
            var redMaterial = MaterialHelper.CreateMaterial(Colors.Red);
            Color surfaceColor = Colors.DarkGray;

            //temporary color list to store intermediate color values
            Dictionary<MeshGeometry3D, List<int>> tmpColorList = colors;

            //stopwatch and vertex count for benchmarking
            Stopwatch sw = new Stopwatch();
            int vertices = 0;
            int optimizedVertices = 0;
            sw.Start();
            Console.WriteLine("Cutting mesh into equal blocks");

            if (this.segmentWall)
            {
                wallModel = new GeometryModel3D();
                Point3D wallPlanePoint = new Point3D(0, 0, minZ + wallSensitivity);



                //temporary color storage for wall mesh
                List<int> newColorsWall = new List<int>();

                //cut mesh to separate wall, this only kindof works iff camera position is looking straight at the wall
                MeshGeometry3D wallMesh = MeshGeometryHelper.Cut(mesh, wallPlanePoint, new Vector3D(0, 0, -1), tmpColorList[mesh], out newColorsWall);
                wallMesh.Normals = MeshGeometryHelper.CalculateNormals(wallMesh);
                wallModel.Geometry = cleanMesh(wallMesh);
                wallModel.Material = redMaterial;
                modelGroup.Children.Add(wallModel);

                //temporary color storage for the remaining mesh
                List<int> newColorsRest = new List<int>();

                //remaining mesh after wall separation
                mesh = MeshGeometryHelper.Cut(mesh, wallPlanePoint, new Vector3D(0, 0, 1), tmpColorList[mesh], out newColorsRest);
                tmpColorList.Remove(mesh);

                //update real color values for wall
                colors.Remove(mesh);
                colors.Add(wallMesh, newColorsWall);
                tmpColorList.Add(mesh, newColorsRest);
            }

            //assign new variable to avoid ambiguity in color list
            MeshGeometry3D newMesh = mesh;

            //cut mesh into equal cubes
            for (double x = minX; x < maxX + blockSize; x += blockSize)
            {
                //cut in x direction
                Point3D xPlanePoint = new Point3D(x, 0, 0);
                List<int> newXColors = new List<int>();
                MeshGeometry3D xMeshSlice = MeshGeometryHelper.Cut(newMesh, xPlanePoint, new Vector3D(-1, 0, 0), tmpColorList[newMesh], out newXColors);
                List<int> newNewMeshColors = new List<int>();
                newMesh = MeshGeometryHelper.Cut(newMesh, xPlanePoint, new Vector3D(1, 0, 0), tmpColorList[newMesh], out newNewMeshColors);
                tmpColorList.Remove(newMesh);
                tmpColorList.Add(newMesh, newNewMeshColors);
                tmpColorList.Add(xMeshSlice, newXColors);

                for (double y = minY; y < maxY + blockSize; y += blockSize)
                {
                    //cut in y direction
                    Point3D yPlanePoint = new Point3D(0, y, 0);
                    List<int> newYColors = new List<int>();
                    MeshGeometry3D yMeshSlice = MeshGeometryHelper.Cut(xMeshSlice, yPlanePoint, new Vector3D(0, -1, 0), tmpColorList[xMeshSlice], out newYColors);
                    List<int> newXColorsSec = new List<int>();
                    xMeshSlice = MeshGeometryHelper.Cut(xMeshSlice, yPlanePoint, new Vector3D(0, 1, 0), tmpColorList[xMeshSlice], out newXColorsSec);
                    tmpColorList.Remove(xMeshSlice);
                    tmpColorList.Add(xMeshSlice, newXColorsSec);
                    tmpColorList.Add(yMeshSlice, newYColors);

                    for (double z = minZ; z < maxZ + blockSize; z += blockSize)
                    {
                        //finally cut in z direction
                        GeometryModel3D singleModel = new GeometryModel3D();
                        Point3D zPlanePoint = new Point3D(0, 0, z);
                        List<int> newZColors = new List<int>();
                        MeshGeometry3D zMeshSlice = MeshGeometryHelper.Cut(yMeshSlice, zPlanePoint, new Vector3D(0, 0, -1), tmpColorList[yMeshSlice], out newZColors);
                        List<int> newYColorsSec = new List<int>();
                        yMeshSlice = MeshGeometryHelper.Cut(yMeshSlice, zPlanePoint, new Vector3D(0, 0, 1), tmpColorList[yMeshSlice], out newYColorsSec);
                        tmpColorList.Remove(yMeshSlice);
                        tmpColorList.Add(yMeshSlice, newYColorsSec);

                        //add real color value
                        colors.Add(zMeshSlice, newZColors);

                        //benchmark purposes
                        vertices += zMeshSlice.Positions.Count;
                        if (zMeshSlice.Positions.Count == 0) continue;
                        zMeshSlice.Normals = MeshGeometryHelper.CalculateNormals(zMeshSlice);

                        //clean mesh to remove unused vertices
                        zMeshSlice = cleanMesh(zMeshSlice);

                        singleModel.Geometry = zMeshSlice;

                        //benchmark purposes
                        optimizedVertices += (singleModel.Geometry as MeshGeometry3D).Positions.Count;

                        //interpolate color to kindof resemble the real pixel values
                        if (interpolateColor)
                        {
                            //get average rgb components
                            List<int> avgRed = new List<int>();
                            List<int> avgGreen = new List<int>();
                            List<int> avgBlue = new List<int>();

                            foreach (int col in colors[zMeshSlice])
                            {
                                avgRed.Add((col >> 16) & 255);
                                avgGreen.Add((col >> 8) & 255);
                                avgBlue.Add(col & 255);
                            }
                            int red = (int)avgRed.Average();
                            int green = (int)avgGreen.Average();
                            int blue = (int)avgBlue.Average();

                            var r = System.Convert.ToByte(red);
                            var g = System.Convert.ToByte(green);
                            var b = System.Convert.ToByte(blue);

                            Color tmpColor = Color.FromRgb(r, g, b);
                            singleModel.Material = new DiffuseMaterial(new SolidColorBrush(tmpColor));
                            singleModel.BackMaterial = new DiffuseMaterial(new SolidColorBrush(tmpColor));
                        }
                        else
                        {
                            //cubes have different shades of gray based on their depth values
                            var r = System.Convert.ToByte((int)((surfaceColor.R * ((z + Math.Abs(minZ)) / (maxZ + Math.Abs(minZ))))));
                            var g = System.Convert.ToByte((int)((surfaceColor.G * ((z + Math.Abs(minZ)) / (maxZ + Math.Abs(minZ))))));
                            var b = System.Convert.ToByte((int)((surfaceColor.B * ((z + Math.Abs(minZ)) / (maxZ + Math.Abs(minZ))))));
                            Color tmpColor = Color.FromRgb(r, g, b);
                            singleModel.Material = new DiffuseMaterial(new SolidColorBrush(tmpColor));
                            singleModel.BackMaterial = new DiffuseMaterial(new SolidColorBrush(tmpColor));
                        }
                        modelGroup.Children.Add(singleModel);
                    }
                }
            }
            sw.Stop();
            Console.WriteLine("Elapsed time: {0}", sw.Elapsed);
            Console.WriteLine("Original vertex count after segmentation: " + vertices);
            Console.WriteLine("Optimized vertex count after segmentation: " + optimizedVertices);

            return modelGroup;
        }

        /// <summary>
        /// Deletes duplicate vertices and triangles of a mesh and updates triangles accordingly
        /// </summary>
        /// <param name="mesh">Mesh</param>
        /// <returns>Mesh without duplicate vertices and updated triangle indices</returns>
        private MeshGeometry3D DeleteDuplicateVerticesAndTriangles(MeshGeometry3D mesh)
        {
            //stopwatch for benchmarking purposes
            Stopwatch sw = new Stopwatch();
            sw.Start();

            MeshGeometry3D newMesh = new MeshGeometry3D();

            //index map maps an old vertex position to a new one
            Dictionary<int, int> indexMap = new Dictionary<int, int>();

            //vector map is used to increase performance of this algorithm
            Dictionary<double, List<int>> vectorMap = new Dictionary<double, List<int>>();

            //uncomment if pixels at -1000 keep popping up
            //List<int> erasedVecList = new List<int>();

            //used to store new color values if present
            List<int> newColors = new List<int>();

            Console.WriteLine("Original vertex count: " + mesh.Positions.Count);

            //look for duplicate vertices, remove them and map their previous position to the new one
            int position = 0;
            for (int i = 0; i < mesh.Positions.Count; i++)
            {
                Point3D point = mesh.Positions[i];
                Vector3D normal = mesh.Normals[i];
                /* uncomment if pixels at -1000 keep popping up
                 * if(vec.Z <= -2)
                {
                    erasedVecList.Add(i);
                    continue;
                }*/
                bool found = false;
                if (vectorMap.ContainsKey(point.X))
                {
                    for (int j = 0; j < vectorMap[point.X].Count; j++)
                    {
                        Point3D tmpPoint = mesh.Positions[vectorMap[point.X][j]];
                        if (tmpPoint.X == point.X && tmpPoint.Y == point.Y && tmpPoint.Z == point.Z)
                        {
                            found = true;
                            indexMap.Add(i, indexMap[vectorMap[point.X][j]]);
                            break;
                        }
                    }
                    if (!found)
                    {
                        vectorMap[point.X].Add(i);
                        indexMap.Add(i, position);
                        position++;
                        newMesh.Positions.Add(new Point3D(point.X, point.Y, point.Z));
                        newMesh.Normals.Add(new Vector3D(normal.X, normal.Y, normal.Z));
                        newColors.Add(colors[mesh][i]);
                    }
                }
                else
                {
                    List<int> newList = new List<int>();
                    newList.Add(i);
                    vectorMap.Add(point.X, newList);
                    indexMap.Add(i, position);
                    position++;
                    newMesh.Positions.Add(new Point3D(point.X, point.Y, point.Z));
                    newMesh.Normals.Add(new Vector3D(normal.X, normal.Y, normal.Z));
                    newColors.Add(colors[mesh][i]);

                }
            }
            //benchmark purposes
            sw.Stop();
            Console.WriteLine("Vertex count after optimizing: " + newMesh.Positions.Count);
            Console.WriteLine("Elapsed time: {0}", sw.Elapsed);
            sw.Reset();
            sw.Start();
            Console.WriteLine("Original triangle count: " + mesh.TriangleIndices.Count / 3);

            //triangleMap is used to increase performance of this algorithm
            Dictionary<int, List<int>> triangleMap = new Dictionary<int, List<int>>();

            //update triangle indices with new values, based on the vertex indexmap, also remove duplicate triangles
            int triPosition = 0;
            for (int i = 0; i < mesh.TriangleIndices.Count; i += 3)
            {
                int index1 = indexMap[mesh.TriangleIndices[i]];
                int index2 = indexMap[mesh.TriangleIndices[i + 1]];
                int index3 = indexMap[mesh.TriangleIndices[i + 2]];

                if (index1 == index2 || index1 == index3 || index2 == index3)
                    continue;


                int sum = index1 + index2 + index3;

                if (triangleMap.ContainsKey(sum))
                {
                    int[] firstArray = new int[3] { index1, index2, index3 };
                    Array.Sort(firstArray);
                    bool found = false;
                    for (int j = 0; j < triangleMap[sum].Count; j++)
                    {
                        int cIndex1 = newMesh.TriangleIndices[triangleMap[sum][j]];
                        int cIndex2 = newMesh.TriangleIndices[triangleMap[sum][j] + 1];
                        int cIndex3 = newMesh.TriangleIndices[triangleMap[sum][j] + 2];
                        int[] secondArray = new int[3] { cIndex1, cIndex2, cIndex3 };
                        Array.Sort(secondArray);
                        if (firstArray.SequenceEqual(secondArray))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        triangleMap[sum].Add(triPosition);

                        triPosition += 3;

                        newMesh.TriangleIndices.Add(index1);
                        newMesh.TriangleIndices.Add(index2);
                        newMesh.TriangleIndices.Add(index3);
                    }
                }
                else
                {
                    List<int> newList = new List<int>();
                    newList.Add(triPosition);
                    triangleMap.Add(sum, newList);

                    triPosition += 3;

                    newMesh.TriangleIndices.Add(index1);
                    newMesh.TriangleIndices.Add(index2);
                    newMesh.TriangleIndices.Add(index3);
                }
            }
            //benchmark purposes
            sw.Stop();
            Console.WriteLine("Triangle count after optimizing: " + newMesh.TriangleIndices.Count / 3);
            Console.WriteLine("Elapsed time: {0}", sw.Elapsed);

            colors.Remove(mesh);
            colors.Add(newMesh, newColors);

            return newMesh;
        }

        /// <summary>
        /// Removes unused vertices from a mesh
        /// This is necessary because the Cut-Method by Helix3D used in SegmentMesh()
        /// does not remove unused vertices and only updates triangles
        /// Without this method the center point of a mesh is completely off and transformation
        /// is not exact
        /// </summary>
        /// <param name="mesh">Mesh</param>
        /// <returns></returns>
        private MeshGeometry3D cleanMesh(MeshGeometry3D mesh)
        {
            MeshGeometry3D cleanedMesh = new MeshGeometry3D();
            List<int> tmpList = new List<int>();
            List<int> newColors = new List<int>();
            Dictionary<int, int> tmpIndex = new Dictionary<int, int>();

            //get all used vertices for comparison
            foreach (int tri in mesh.TriangleIndices)
            {
                if (!tmpList.Contains(tri)) tmpList.Add(tri);
            }

            int position = 0;
            for (int i = 0; i < mesh.Positions.Count; i++)
            {
                if (!tmpList.Contains(i)) continue;

                cleanedMesh.Positions.Add(mesh.Positions[i]);
                if (this.captureColor) newColors.Add(colors[mesh][i]);
                cleanedMesh.Normals.Add(mesh.Normals[i]);
                tmpIndex.Add(i, position);
                position++;
            }

            foreach (int tri in mesh.TriangleIndices)
            {
                cleanedMesh.TriangleIndices.Add(tmpIndex[tri]);
            }

            if (this.captureColor)
            {
                colors.Remove(mesh);
                colors.Add(cleanedMesh, newColors);
            }
            return cleanedMesh;
        }

        /// <summary>
        /// Changes the material of all meshes to a diffuse material with the specified color.
        /// This overwrites every other color setting, but does not effect stored vertex colors
        /// for mesh export
        /// </summary>
        /// <param name="color">specified color</param>
        public void ChangeMaterial(Color color)
        {
            foreach (GeometryModel3D geoModel in modelGroup.Children)
            {
                geoModel.Material = new DiffuseMaterial(new SolidColorBrush(color));
            }
        }

        /// <summary>
        /// Tells the caller if a model is the segmented wall model or not
        /// </summary>
        /// <param name="hitModel">Model</param>
        /// <returns>is the model the wall model?</returns>
        public bool isWallModel(GeometryModel3D hitModel)
        {
            return (wallModel == hitModel);
        }

        /// <summary>
        /// Returns a CustomMesh - object that contains every single position, triangle, normal AND COLOR
        /// from every single mesh in this MainViewModel for mesh exportation.
        /// </summary>
        /// <returns>Combined mesh object including vertex colors</returns>
        public CustomMesh GetCustomMesh()
        {
            //stopwatch for benchmark purposes
            Stopwatch sw = new Stopwatch();

            CustomMesh customMesh = new CustomMesh();
            MeshGeometry3D tmpMesh = new MeshGeometry3D();

            //temporary color storage
            List<int> newColors = new List<int>();

            Dictionary<int, int> indexMap = new Dictionary<int, int>();
            Console.WriteLine("Starting to export mesh..");
            sw.Start();
            int position = 0;

            //combine all mesh segments into one
            foreach (GeometryModel3D geoModel in modelGroup.Children)
            {
                MeshGeometry3D currentMesh = (geoModel.Geometry) as MeshGeometry3D;
                indexMap.Clear();
                for (int i = 0; i < currentMesh.Positions.Count; i++)
                {
                    tmpMesh.Positions.Add(geoModel.Transform.Transform((currentMesh.Positions[i])));
                    tmpMesh.Normals.Add((currentMesh.Normals[i]));
                    if (this.captureColor) newColors.Add(colors[currentMesh][i]);
                    indexMap.Add(i, position);
                    position++;
                }
                for (int i = 0; i < currentMesh.TriangleIndices.Count; i++)
                {
                    int index = currentMesh.TriangleIndices[i];
                    tmpMesh.TriangleIndices.Add(indexMap[index]);
                }
            }
            if (this.captureColor) colors.Add(tmpMesh, newColors);

            //benchmark purposes
            sw.Stop();
            Console.WriteLine("Elapsed time after loading mesh: {0}", sw.Elapsed);
            sw.Restart();
            //optimize output mesh
            tmpMesh = DeleteDuplicateVerticesAndTriangles(tmpMesh);
            //benchmark purposes
            sw.Stop();
            Console.WriteLine("Elapsed time after optimizing mesh: {0}", sw.Elapsed);
            sw.Restart();

            //create CustomMesh object
            foreach (Vector3D vec in tmpMesh.Positions)
            {
                Vector3 tmpVec = new Vector3();
                tmpVec.X = (float)vec.X;
                tmpVec.Y = (float)vec.Y;
                tmpVec.Z = (float)vec.Z;
                customMesh.AddVertice(tmpVec);
            }

            if (this.captureColor)
            {
                foreach (int color in colors[tmpMesh])
                {
                    customMesh.AddColor(color);
                }
            }

            foreach (Vector3D norm in tmpMesh.Normals)
            {
                Vector3 tmpNorm = new Vector3();
                tmpNorm.X = (float)norm.X;
                tmpNorm.Y = (float)norm.Y;
                tmpNorm.Z = (float)norm.Z;
                customMesh.AddNormal(tmpNorm);
            }

            foreach (int triangleIndex in tmpMesh.TriangleIndices)
            {
                customMesh.AddTriangleIndex(triangleIndex);
            }
            //benchmark purposes
            sw.Stop();
            Console.WriteLine("Elapsed time after converting mesh: {0}", sw.Elapsed);

            return customMesh;
        }
    }
}

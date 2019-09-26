
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Jobs;

public class CuttingDispatcher : MonoBehaviour
{
    [Header("Main")]
    public ComputeShader shader;
    private const string calcActionsHandle = "CSsplitMesh";
    private const string splitVertHandle = "CSsplitVerticies";
    private const string createVertHandle = "CScreateExtraVertecies";
    private const float threads = 1024;

    public delegate void FinishSplitting(List<Vector3> vertices, List<Vector3> normals, int[] triangles,Vector3 pos,Quaternion rot,Vector3 normal);
    public FinishSplitting finishSplitting;


    public CutCorpse prefab;

    public MeshFilter toCut;
    public Transform cutPlane;


    //private float dist;
    //private Vector3 normal;
    
    private void Awake()
    {
        finishSplitting += FinishSplittingAction;
    }
    private void Start()
    {
        //Calculate(toCut.transform, toCut.mesh, cutPlane);
    }


    //getting the plane normal and distance
    private static void CalculatePlane(Vector3 pos, Vector3 norm, out Vector3 normal,out float dist)
    {
       normal = norm;
       dist = Vector3.Dot(norm.normalized, pos.normalized);
    }
    private static void CalculatePlane(Transform tr, out Vector3 normal, out float dist)
    {
        CalculatePlane(tr.position, tr.up, out  normal, out  dist);
    }
    private static void CalculatePlane(Transform tr, Transform tr2, out Vector3 normal, out float dist)
    {

        //Debug.Log("tr 1 = " + tr.up + "\ttr 2 = " + tr2.up+"\toutput = "+ norm);
        CalculatePlane(tr.position-tr2.position , tr.rotation * tr2.up, out normal, out dist);
    }

    //public Transform inputA;
    //public Transform inputB;
    //public float faaaaa;
    //private void Update()
    //{
    //    var v = inputB.rotation * inputA.up;
    //    Debug.Log(inputA.up + " + " + inputB.up + " = " + v);
    //}
    //public void Calculate(Transform transform, Transform cutSurface)
    //{

    //    var meshF = transform.GetComponent<MeshFilter>();
    //    if (meshF != null)
    //    {
    //        CalculatePlane(cutSurface, transform, out Vector3 normal, out float dist);

    //        //StartCoroutine( CutMesh(meshF.mesh, transform));
    //    }
    //}
    public void Calculate(Transform transform, Mesh mesh, Transform cutSurface)
    {
        CalculatePlane(cutSurface, transform, out Vector3 normal, out float dist);
        if( RunOnOtherThread(mesh, normal, dist, transform.position, transform.rotation,-cutSurface.forward))
        Destroy(transform.gameObject);
    }

    //public bool CalculateConcurrent(object o)
    //{
    //    object[] objs = (object[])o;
    //    return CutMesh((ComputeShader)objs[0], (Vector3[])objs[1], (Vector3[])objs[2], (int[])objs[3], (Vector3)objs[4], (float)objs[5], (Vector3)objs[6], (Quaternion)objs[7], (FinishSplitting)objs[8]);
    //}

    bool RunOnOtherThread(Mesh mesh,Vector3 normal,float dist, Vector3 pos, Quaternion rotation,Vector3 forward)
    {
        //Thread thread = new Thread(CalculateConcurrent);
        //thread.Start(new object[]
        //{
        //    shader,
        //    mesh.vertices,
        //    mesh.normals,
        //    mesh.triangles,
        //    normal,
        //    dist,
        //    pos,
        //    rotation,
        //    finishSplitting
        //});
        return CutMesh(
                shader,
            mesh.vertices,
            mesh.normals,
            mesh.triangles,
            normal,
            dist,
            pos,
            rotation,
            finishSplitting,
            forward);

    }



    private static bool CutMesh(ComputeShader shader,Vector3[] vertices, Vector3[] normals, int[] triangles, Vector3 normal, float dist, Vector3 pos, Quaternion rotation, FinishSplitting finishSplitting,Vector3 forward)
    {
        //TODO
        //plane needs to be calc. beforehand
        // (?) dispatch the code individually for everty submesh for the mesh
        // dispatch the code on a separate thread

        //System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();

     List<Vector3> mesh_A_vertices = new List<Vector3>();
     List<Vector3> mesh_B_vertices = new List<Vector3>();
     List<Vector3> mesh_A_normals = new List<Vector3>();
     List<Vector3> mesh_B_normals = new List<Vector3>();
     List<int> trisA = new List<int>();
     List<int> trisB = new List<int>();
     List<int> edgesA = new List<int>();
     List<int> edgesB = new List<int>();
     List<int> splitData = new List<int>();



    //get data from original mesh


        //data for shader - loop sizes
        int loopSize1 = Mathf.CeilToInt(triangles.Length / 3 / threads);
        int loopSize2 = Mathf.CeilToInt(vertices.Length / threads);
        int loopSize3;

        //get kernel handles
        int handle_AssignTris = 0;
        int handle_AssignVert = 1;
        int handle_CreateVert = 2;
        //int handle_AssignTris = shader.FindKernel(calcActionsHandle);
        //int handle_AssignVert = shader.FindKernel(splitVertHandle);
        //int handle_CreateVert = shader.FindKernel(createVertHandle);


        //plane data for shader
        shader.SetFloats("planeNormal", new float[] { normal.x, normal.y, normal.z });
        shader.SetFloat("planeDistance", dist);

        //loop size for kernel #0
        shader.SetInt("maxLoopIndex", triangles.Length / 3);
        shader.SetInt("loopSize", loopSize1);

        //loop size for kernel #1
        shader.SetInt("maxVertLoopSize", vertices.Length);
        shader.SetInt("vertLoopSize", loopSize2);

        //buffers for shader
        ComputeBuffer input_OriginalVerticesBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
        ComputeBuffer input_OriginalTrianglesBuffer = new ComputeBuffer(triangles.Length, sizeof(int));
        ComputeBuffer output_triangleSorterBuffer = new ComputeBuffer(triangles.Length / 3, sizeof(int));
        ComputeBuffer output_verticesBuffer = new ComputeBuffer(vertices.Length, sizeof(uint));


        //fill buffers with data
        input_OriginalVerticesBuffer.SetData(vertices);
        input_OriginalTrianglesBuffer.SetData(triangles);
        int[] sortedTriangles = new int[triangles.Length / 3];

        output_triangleSorterBuffer.SetData(sortedTriangles);
        output_verticesBuffer.SetData(new uint[vertices.Length]);

        //assign buffers to kernels
        shader.SetBuffer(handle_AssignTris, "inputVert", input_OriginalVerticesBuffer);
        shader.SetBuffer(handle_AssignTris, "inputTri", input_OriginalTrianglesBuffer);
        shader.SetBuffer(handle_AssignTris, "outputSides", output_triangleSorterBuffer);

        shader.SetBuffer(handle_AssignVert, "inputVert", input_OriginalVerticesBuffer);
        shader.SetBuffer(handle_AssignVert, "outputVerticies", output_verticesBuffer);

        //run 2/3 shader kernels, to sort faces and to sort vertices
        shader.Dispatch(handle_AssignTris, 1, 1, 1);
        shader.Dispatch(handle_AssignVert, 1, 1, 1);


        output_triangleSorterBuffer.GetData(sortedTriangles);
        int[] sortedVertices = new int[vertices.Length];
        output_verticesBuffer.GetData(sortedVertices);







        int triangles_length_A = 0;
        int triangles_length_B = 0;

        int A = 0;
        int B = 0;


        int additionalTriangles_A = 0;
        int additionalTriangles_B = 0;

        int cutSectionVert_Length_A = 0;
        int cutSectionVert_Length_B = 0;
        int verticesLength_A = 0;
        int verticesLength_B = 0;

        int vertexOffset = 0;

        int[] vertexDictionaryA = new int[vertices.Length];
        int[] vertexDictionaryB = new int[vertices.Length];

        //sorting vertices
        //iterating through array
        //size of sortedVertices = verts
        for (int i = 0; i < sortedVertices.Length; i++)
        {
            switch (sortedVertices[i])
            {
                case 0:
                    //vertex belongs to mesh A
                    vertexDictionaryA[i]= verticesLength_A;    //old i becomes new vert
                    mesh_A_vertices.Add(vertices[i]);
                    mesh_A_normals.Add(normals[i]);
                    verticesLength_A++;
                    break;
                case 1:
                    //vertex belongs to mesh B
                    vertexDictionaryB[i]= verticesLength_B;
                    mesh_B_vertices.Add(vertices[i]);
                    mesh_B_normals.Add(normals[i]);
                    //Debug.Log("verticesLength_B: " + verticesLength_B + "\n i = " + i + "\n vertices[i] = " + vertices[i]);
                    verticesLength_B++;
                    break;
                case 2:
                    //vertex belongs to both meshes
                    vertexDictionaryA[i] = verticesLength_A;    //old i becomes new vert
                    vertexDictionaryB[i] = verticesLength_B;
                    mesh_A_vertices.Add(vertices[i]);
                    mesh_B_vertices.Add(vertices[i]);
                    edgesA.Add(cutSectionVert_Length_A);
                    edgesB.Add(cutSectionVert_Length_B);
                    mesh_B_normals.Add(normals[i]);
                    mesh_A_normals.Add(normals[i]);

                    cutSectionVert_Length_A++;
                    cutSectionVert_Length_B++;

                    verticesLength_A++;
                    verticesLength_B++;
                    break;
            }
        }
        //sorting faces
        for (int i = 0; i < sortedTriangles.Length; i++)
        {
            ///this was an issue
            int a = triangles[i * 3];
            int b = triangles[i * 3 + 1];
            int c = triangles[i * 3 + 2];

            switch (sortedTriangles[i]) //returns a number based on action to perform
            {
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                case 8:
                case 0:
                    trisA.Add(vertexDictionaryA[a]);
                    trisA.Add(vertexDictionaryA[b]);
                    trisA.Add(vertexDictionaryA[c]);
                    triangles_length_A++;

                    break;
                case 1:
                    trisB.Add(vertexDictionaryB[a]);
                    trisB.Add(vertexDictionaryB[b]);
                    trisB.Add(vertexDictionaryB[c]);
                    triangles_length_B++;
                    break;                case 2:
                    trisA.Add(vertexDictionaryA[a]);
                    trisA.Add(vertexDictionaryA[b]);
                    trisA.Add(vertexDictionaryA[c]);

                    trisB.Add(vertexDictionaryB[a]);
                    trisB.Add(vertexDictionaryB[b]);
                    trisB.Add(vertexDictionaryB[c]);

                    triangles_length_B++;
                    triangles_length_A++;
                    break;

                case 9:
                case 10:
                case 11:
                case 24:
                case 25:
                case 26:
                    splitData.AddRange(new int[] { i*3, sortedTriangles[i], A * 3, A * 3, vertexOffset });
                    additionalTriangles_A++;
                    additionalTriangles_B++;
                    triangles_length_A++;
                    triangles_length_B++;
                    A++;
                    B++;
                    vertexOffset++;
                    break;

                case 12:
                case 13:
                case 14:
                    splitData.AddRange(new int[] { i * 3, sortedTriangles[i], A * 3, B * 3, vertexOffset });
                    additionalTriangles_A += 2;
                    additionalTriangles_B++;
                    triangles_length_A += 2;
                    triangles_length_B += 1;
                    vertexOffset += 2;
                    A += 2;
                    B++;
                    break;

                case 15:
                case 16:
                case 17:
                    splitData.AddRange(new int[] { i * 3, sortedTriangles[i], A * 3, B * 3, vertexOffset });
                    additionalTriangles_A++;
                    additionalTriangles_B += 2;
                    triangles_length_A += 1;
                    triangles_length_B += 2;
                    vertexOffset += 2;
                    A++;
                    B += 2;
                    break;

                case 18:
                case 19:
                case 20:
                case 21:
                case 22:
                case 23:
                    trisB.Add(vertexDictionaryB[a]);
                    trisB.Add(vertexDictionaryB[b]);
                    trisB.Add(vertexDictionaryB[c]);
                    triangles_length_B += 1;
                    break;
            }


        }
        //splitting edges------------------------------------------------------------

        int size = splitData.Count / 5;
        if (size == 0) return false;  
        ///buffers
        
        ComputeBuffer buffer_splitData = new ComputeBuffer(size, sizeof(uint) * 5);
        ComputeBuffer buffer_outputTri_A = new ComputeBuffer(additionalTriangles_A * 3, sizeof(uint));
        ComputeBuffer buffer_outputTri_B = new ComputeBuffer(additionalTriangles_B * 3, sizeof(uint));
        ComputeBuffer buffer_outputCreatedVertices = new ComputeBuffer(vertexOffset, sizeof(float) * 3);
        ComputeBuffer buffer_outputCreatedNormals = new ComputeBuffer(vertexOffset, sizeof(float) * 3);
        ComputeBuffer buffer_originalNormals = new ComputeBuffer(normals.Length, sizeof(float) * 3);

        ///assiging data to buffers
        buffer_splitData.SetData(splitData);
        buffer_outputTri_A.SetData(new int[additionalTriangles_A * 3]);
        buffer_outputTri_B.SetData(new int[additionalTriangles_B * 3]);
        buffer_outputCreatedVertices.SetData(new float[vertexOffset * 3]);
        buffer_outputCreatedNormals.SetData(new float[vertexOffset * 3]);
        buffer_originalNormals.SetData(normals);


        ///loop size of compute shader thread
        loopSize3 = Mathf.CeilToInt(size / threads);

        ///setting values for shader
        shader.SetInt("CrossSectionLoopMaxSize", size);
        shader.SetInt("CrossSectionLoopSize", loopSize3);
        shader.SetInt("triangleVertOffset", vertices.Length);



        ///assigning 6 buffers for 3rd kernel
        shader.SetBuffer(handle_CreateVert, "inputVert", input_OriginalVerticesBuffer);
        shader.SetBuffer(handle_CreateVert, "inputTri", input_OriginalTrianglesBuffer);
        shader.SetBuffer(handle_CreateVert, "inputSplitTriangles", buffer_splitData);
        shader.SetBuffer(handle_CreateVert, "outputTrisA", buffer_outputTri_A);
        shader.SetBuffer(handle_CreateVert, "outputTrisB", buffer_outputTri_B);
        shader.SetBuffer(handle_CreateVert, "outputSplitVerts", buffer_outputCreatedVertices);
        shader.SetBuffer(handle_CreateVert, "inputNormals", buffer_originalNormals);
        shader.SetBuffer(handle_CreateVert, "outputNewNormals", buffer_outputCreatedNormals);


        ///running the compute shader with given info
        shader.Dispatch(handle_CreateVert, 1, 1, 1);

        ///output variables
        int[] ExtraATris = new int[additionalTriangles_A * 3];
        int[] ExtraBTris = new int[additionalTriangles_B * 3];
        float[] ExtraVertsF = new float[vertexOffset * 3];
        float[] ExtraNormalsF = new float[vertexOffset * 3];
        Vector3[] ExtraVerts = new Vector3[vertexOffset];
        Vector3[] ExtraNormals = new Vector3[vertexOffset];

        ///filling output vars
        buffer_outputTri_A.GetData(ExtraATris);
        buffer_outputTri_B.GetData(ExtraBTris);
        buffer_outputCreatedVertices.GetData(ExtraVertsF);
        buffer_outputCreatedNormals.GetData(ExtraNormalsF);

        ///converting array of floats to vector3
        for (int i = 0; i < ExtraVerts.Length; i++)
        {
            ExtraVerts[i] = new Vector3(ExtraVertsF[i * 3], ExtraVertsF[i * 3 + 1], ExtraVertsF[i * 3 + 2]);
            ExtraNormals[i] = new Vector3(ExtraNormalsF[i * 3], ExtraNormalsF[i * 3 + 1], ExtraNormalsF[i * 3 + 2]);
        }
        ///.........................................................................................
        //shader invoking complete
        //turning data into 2 meshes

        //foreach (var item in ExtraVerts)
        //{
        //    Debug.Log("Vertex additional "+item);
        //}
        //for (int i = 0; i < ExtraATris.Length; i++)
        //{
        //    if(ExtraATris[i]< vertices.Length)
        //    Debug.Log("Vertex "+ i%3 +"   "+vertices[ExtraATris[i]]);
        //    else
        //    Debug.Log("Vertex " + i % 3 + "   "+ExtraVerts[ExtraATris[i]- vertices.Length]);


        //}
        //Verticies
        ///A
        List<Vector3> Final_vertA = new List<Vector3>(mesh_A_vertices.Count + ExtraVerts.Length);
        Final_vertA.AddRange(mesh_A_vertices);
        Final_vertA.AddRange(ExtraVerts);

        ///B
        List<Vector3> Final_vertB = new List<Vector3>(mesh_B_vertices.Count + ExtraVerts.Length);
        Final_vertB.AddRange(mesh_B_vertices);
        Final_vertB.AddRange(ExtraVerts);



        //Triangles
        int TriSize = vertices.Length;
        int triAC = mesh_A_vertices.Count;
        int triBC = mesh_B_vertices.Count;

        //assign extra triangles created by splitting triangles
        ///A

        for (int i = 0; i < ExtraATris.Length; i++)
        {
            var VertID = ExtraATris[i];
            if (VertID < TriSize)
            {
                    trisA.Add(vertexDictionaryA[VertID]);
            }
            else
            {
                int newID = VertID - TriSize + triAC;
                trisA.Add(newID);
                edgesA.Add(newID);
            }
        }

        for (int i = 1; i < edgesA.Count - 1; i++)
        {
            trisA.Add(edgesA[0]);
            trisA.Add(edgesA[i + 1]);
            trisA.Add(edgesA[i]);
        }
        int[] Final_trisA = trisA.ToArray();

        ///B
        for (int i = 0; i < ExtraBTris.Length; i++)
        {
            var VertID = ExtraBTris[i];
            if (VertID < TriSize)
            {
                    trisB.Add(vertexDictionaryB[VertID]);
            }
            else
            {
                int newID = VertID - TriSize+ triBC;
                trisB.Add(newID);
                edgesB.Add(newID);

            }
        }
        Vector3 point = Final_vertB[edgesB[0]];

        for (int i = 1; i < edgesB.Count - 1; i++)
        {
            trisB.Add(edgesB[0]);
            trisB.Add(edgesB[i + 1]);
            trisB.Add(edgesB[i]);
        }
        int[] Final_trisB = trisB.ToArray();

        //Normals
        ///A
        List<Vector3> Final_NormalA = new List<Vector3>(mesh_A_normals.Count + vertexOffset);
        Final_NormalA.AddRange(mesh_A_normals);
        Final_NormalA.AddRange(ExtraNormals);

        ///B
        List<Vector3> Final_NormalB = new List<Vector3>(mesh_B_normals.Count + vertexOffset);
        Final_NormalB.AddRange(mesh_B_normals);
        Final_NormalB.AddRange(ExtraNormals);


        if (normal.y > 0)
        {
            finishSplitting(Final_vertA, Final_NormalA, Final_trisA, pos, rotation, forward);
            finishSplitting(Final_vertB, Final_NormalB, Final_trisB, pos, rotation, Vector3.zero);
        }
        else
        {
            finishSplitting(Final_vertA, Final_NormalA, Final_trisA, pos, rotation, Vector3.zero);
            finishSplitting(Final_vertB, Final_NormalB, Final_trisB, pos, rotation, -forward);
        }

        //watch.Stop();
        //long elapsedMs = watch.ElapsedMilliseconds;
        //Debug.Log("Cutting took " + elapsedMs + "ms");


        //cleanup ----------------------------

        output_triangleSorterBuffer.Dispose();
        input_OriginalVerticesBuffer.Dispose();
        input_OriginalTrianglesBuffer.Dispose();
        output_verticesBuffer.Dispose();
        buffer_splitData.Dispose();
        buffer_outputTri_A.Dispose();
        buffer_outputTri_B.Dispose();
        buffer_outputCreatedVertices.Dispose();
        mesh_A_vertices.Clear();
        mesh_B_vertices.Clear();
        mesh_A_normals.Clear();
        mesh_B_normals.Clear();
        trisA.Clear();
        trisB.Clear();
        edgesA.Clear();
        edgesB.Clear();
        splitData.Clear();


        return true;
    }
    public void FinishSplittingAction(List<Vector3> vertices, List<Vector3> normals, int[] triangles,Vector3 pos,Quaternion rot,Vector3 force)
    {
    Mesh mesh = new Mesh();
    mesh.SetVertices(vertices);
    mesh.SetNormals(normals);
    mesh.SetTriangles(triangles,0);
        mesh.Optimize();

        var InstantiatedA = Object.Instantiate(prefab, pos, rot);
    InstantiatedA.filter.mesh = mesh;
        if (force != Vector3.zero) 
        InstantiatedA.SetVelocity(force,10);

    }


}


//Idea one
//Pseudo code 
/* Get verts,triangles from mesh
 * Calculate plane
 * pass into buffers
 * create buffer the size of 1/3rd of triangles, contains info where each triangle will be assigned (to left of right)
 * create a buffer with output size of left and right vertex sizes
 * run a compute shader to assign verts to correct sides of cut, also number of times a new vert needs to be made to create intersection triangles
 * run a compute shader to assign triangles to correct sides of cut
   every time a side is assigned, a uint value is increased. There is one uint for left, one for right. They are the final array sizes
 * create array of result sizes
 * run a compute shader with buffers of correct sizes to fill verts and triangles based on results of prev compute shader iteration
   OR 
   loop 
   through and add them one by one on a CPU
 * fill mesh right and left
 * set mesh
 * dispose of buffers
 * 
 * also slap normals there too 
 */



/*
 
bool intersectLine(float3 a, float3 b) 
{
float3 ab = b - a;

float t = (planeDistance - dot(planeNormal, a)) / dot(planeNormal, ab);

// need to be careful and compensate for floating errors
if (t >= MinusEpsilon && t <= (1 + Epsilon)) {
    return true;
}

return false;
}   
*/

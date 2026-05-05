using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class exploder : MonoBehaviour
{
    public Vector3[] points = new Vector3[1] { Vector3.zero };
    public float radius = 1.0f;
    public float sinkRadiusExt = 0.5f;
    public float yOffset = 1.0f;
    public AnimationCurve sinkCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [SerializeField, HideInInspector] private Mesh sourceMesh;
    private Mesh runtimeMesh;
    private MeshFilter meshFilter;
    private Vector3[] initialPoints;
    private bool initialPointsCaptured;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        CaptureInitialPoints();
    }

    private void OnEnable()
    {
        meshFilter = GetComponent<MeshFilter>();
        CaptureInitialPoints();
    }

    private void CaptureInitialPoints()
    {
        if (initialPointsCaptured) return;

        if (points == null)
        {
            initialPoints = new Vector3[0];
        }
        else
        {
            initialPoints = (Vector3[])points.Clone();
        }

        initialPointsCaptured = true;
    }

    private void PrepareMeshes()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) return;

        // If current mesh is NOT our runtime mesh, it might be a new source
        if (meshFilter.sharedMesh != null && meshFilter.sharedMesh.name != "Exploder_Runtime")
        {
            sourceMesh = meshFilter.sharedMesh;
        }

        if (runtimeMesh == null)
        {
            // Try to reconnect to existing instance in the scene
            if (meshFilter.sharedMesh != null && meshFilter.sharedMesh.name == "Exploder_Runtime")
            {
                runtimeMesh = meshFilter.sharedMesh;
            }
            else
            {
                runtimeMesh = new Mesh();
                runtimeMesh.name = "Exploder_Runtime";
                runtimeMesh.MarkDynamic();
            }
        }
        
        // Ensure the mesh filter is using the runtime mesh
        if (meshFilter.sharedMesh != runtimeMesh)
        {
            meshFilter.mesh = runtimeMesh;
        }
    }

    public void applyDeform()
    {
        PrepareMeshes();
        if (sourceMesh == null || runtimeMesh == null)
        {
            Debug.LogError("Mesh initialization failed on " + gameObject.name);
            return;
        }

        Vector3[] workingVerts = sourceMesh.vertices;
        int[] workingTris = sourceMesh.triangles;
        
        HashSet<int> deletedTriStarts = new HashSet<int>();
        
        // delete any tri in radius of any point
        for (int i = 0; i < workingTris.Length; i += 3)
        {
            Vector3 v0 = transform.TransformPoint(workingVerts[workingTris[i]]);
            Vector3 v1 = transform.TransformPoint(workingVerts[workingTris[i + 1]]);
            Vector3 v2 = transform.TransformPoint(workingVerts[workingTris[i + 2]]);

            bool inRadius = false;
            foreach (Vector3 p in points)
            {
                if (Vector3.Distance(v0, p) <= radius &&
                    Vector3.Distance(v1, p) <= radius &&
                    Vector3.Distance(v2, p) <= radius)
                {
                    inRadius = true;
                    break;
                }
            }

            if (inRadius)
            {
                deletedTriStarts.Add(i);
            }
        }

        // find final edges - between a deleted and a remaining triangle
        Dictionary<string, List<int>> edgeToTriStarts = new Dictionary<string, List<int>>();
        for (int i = 0; i < workingTris.Length; i += 3)
        {
            AddEdgeToLookup(edgeToTriStarts, workingTris[i], workingTris[i + 1], i);
            AddEdgeToLookup(edgeToTriStarts, workingTris[i + 1], workingTris[i + 2], i);
            AddEdgeToLookup(edgeToTriStarts, workingTris[i + 2], workingTris[i], i);
        }

        List<int> resultTris = new List<int>();
        HashSet<string> addedBoundaryKeys = new HashSet<string>();
        List<(int v1, int v2)> boundaryEdges = new List<(int, int)>();

        // re-add remaining triangles
        for (int i = 0; i < workingTris.Length; i += 3)
        {
            if (!deletedTriStarts.Contains(i))
            {
                resultTris.Add(workingTris[i]);
                resultTris.Add(workingTris[i + 1]);
                resultTris.Add(workingTris[i + 2]);
            }
        }

        // find edges of deleted triangles that touch remaining triangles
        foreach (int triStart in deletedTriStarts)
        {
            CheckAndAddBoundary(boundaryEdges, addedBoundaryKeys, edgeToTriStarts, deletedTriStarts, workingTris[triStart], workingTris[triStart + 1]);
            CheckAndAddBoundary(boundaryEdges, addedBoundaryKeys, edgeToTriStarts, deletedTriStarts, workingTris[triStart + 1], workingTris[triStart + 2]);
            CheckAndAddBoundary(boundaryEdges, addedBoundaryKeys, edgeToTriStarts, deletedTriStarts, workingTris[triStart + 2], workingTris[triStart]);
        }

        List<Vector3> resultVerts = new List<Vector3>(workingVerts);
        HashSet<int> rimIndices = new HashSet<int>();

        foreach (var edge in boundaryEdges)
        {
            Vector3 worldV1 = transform.TransformPoint(workingVerts[edge.v1]);
            Vector3 worldV2 = transform.TransformPoint(workingVerts[edge.v2]);
            Vector3 midpoint = (worldV1 + worldV2) * 0.5f;

            // find nearest deformation point for this edge
            Vector3 nearestP = points[0];
            float minDist = float.MaxValue;
            foreach (Vector3 p in points)
            {
                float d = Vector3.Distance(midpoint, p);
                if (d < minDist) { minDist = d; nearestP = p; }
            }

            // progress 30% towards center to create a jagged star vertex
            Vector3 dir = nearestP - midpoint;
            Vector3 starWorld = midpoint + dir * 0.3f;
            Vector3 starLocal = transform.InverseTransformPoint(starWorld);
            
            // do NOT apply yOffset here because the sink logic will handle it based on distance
            // ensuring a single smooth transition from outer edge to inner jagged points.
            
            int starIdx = resultVerts.Count;
            resultVerts.Add(starLocal);

            // Track indices for vertex coloring the jagged rim
            rimIndices.Add(edge.v1);
            rimIndices.Add(edge.v2);
            rimIndices.Add(starIdx);

            // Add a single triangle for each edge to form the star pattern
            // Fixed winding order to prevent normal inversion (swapped v1/v2 from previous version)
            resultTris.Add(edge.v1);
            resultTris.Add(edge.v2);
            resultTris.Add(starIdx);
        }


        // apply sink
        Vector3[] finalVerts = resultVerts.ToArray();
        for (int i = 0; i < finalVerts.Length; i++)
        {
            Vector3 worldP = transform.TransformPoint(finalVerts[i]);
            float maxEffect = 0f;
            
            foreach (Vector3 p in points)
            {
                float dist = Vector3.Distance(worldP, p);
                
                // Map the sink from the outer extension all the way to the 30% inner star line
                float innerTargetDist = radius * 0.7f; 
                float outerLimitDist = radius + sinkRadiusExt;

                if (dist <= outerLimitDist)
                {
                    float effect = 1.0f;
                    if (dist > innerTargetDist)
                    {
                        // Map [outerLimitDist, innerTargetDist] to [0, 1] for curve evaluation
                        float t = Mathf.Clamp01((dist - outerLimitDist) / (innerTargetDist - outerLimitDist));
                        effect = sinkCurve.Evaluate(t);
                    }
                    if (effect > maxEffect) maxEffect = effect;
                }
            }

            if (maxEffect > 0)
            {
                // Applying yOffset fully here. This ensures the 30% jagged points (at innerTargetDist)
                // sit exactly at the floor, and the sink is applied only once.
                finalVerts[i].z -= yOffset * maxEffect;
            }
        }


        // set final data to runtimeMesh
        runtimeMesh.Clear();
        runtimeMesh.vertices = finalVerts;
        runtimeMesh.triangles = resultTris.ToArray();

        Vector2[] srcUVs = sourceMesh.uv;
        if (srcUVs != null && srcUVs.Length > 0)
        {
            Vector2[] finalUVs = new Vector2[finalVerts.Length];
            int copyCount = Mathf.Min(srcUVs.Length, finalVerts.Length);
            for (int i = 0; i < copyCount; i++) finalUVs[i] = srcUVs[i];
            for (int i = copyCount; i < finalVerts.Length; i++) finalUVs[i] = new Vector2(0.5f, 0.5f);
            runtimeMesh.uv = finalUVs;
        }

        Color[] srcColors = sourceMesh.colors;
        Color[] finalColors = new Color[finalVerts.Length];
        if (srcColors != null && srcColors.Length > 0)
        {
            int copyCount = Mathf.Min(srcColors.Length, finalVerts.Length);
            for (int i = 0; i < copyCount; i++) finalColors[i] = srcColors[i];
            for (int i = copyCount; i < finalVerts.Length; i++) finalColors[i] = Color.white;
        }
        else
        {
            for (int i = 0; i < finalColors.Length; i++) finalColors[i] = Color.white;
        }

        foreach (int idx in rimIndices)
        {
            if (idx < finalColors.Length) finalColors[idx] = Color.black;
        }
        runtimeMesh.colors = finalColors;

        runtimeMesh.RecalculateNormals();
        runtimeMesh.RecalculateBounds();
        
        UpdateCollider();
    }

    public void AddExplosionPoint(Vector3 worldPoint)
    {
        List<Vector3> newPoints = new List<Vector3>(points);
        newPoints.Add(worldPoint);
        points = newPoints.ToArray();
        applyDeform();
    }

    private void UpdateCollider()
    {
        MeshCollider col = GetComponent<MeshCollider>();
        if (col != null)
        {
            col.sharedMesh = null;
            col.sharedMesh = runtimeMesh;
        }
    }

    public void ResetMesh()
    {
        PrepareMeshes();
        if (sourceMesh == null || runtimeMesh == null) return;

        if (initialPointsCaptured)
        {
            points = (Vector3[])initialPoints.Clone();
        }

        runtimeMesh.Clear();
        runtimeMesh.vertices = sourceMesh.vertices;
        runtimeMesh.triangles = sourceMesh.triangles;
        runtimeMesh.uv = sourceMesh.uv;
        runtimeMesh.colors = sourceMesh.colors;
        
        Vector3[] srcNormals = sourceMesh.normals;
        if (srcNormals != null && srcNormals.Length > 0)
            runtimeMesh.normals = srcNormals;
        else
            runtimeMesh.RecalculateNormals();

        runtimeMesh.RecalculateBounds();
        
        UpdateCollider();
    }

    private void AddEdgeToLookup(Dictionary<string, List<int>> dict, int v1, int v2, int triStart)
    {
        string key = GetEdgeKey(v1, v2);
        if (!dict.ContainsKey(key)) dict[key] = new List<int>();
        dict[key].Add(triStart);
    }

    private void CheckAndAddBoundary(List<(int, int)> boundary, HashSet<string> added, Dictionary<string, List<int>> dict, HashSet<int> deleted, int v1, int v2)
    {
        string key = GetEdgeKey(v1, v2);
        if (added.Contains(key)) return;

        bool isBoundary = false;
        if (dict.ContainsKey(key))
        {
            foreach (int triStart in dict[key])
            {
                if (!deleted.Contains(triStart))
                {
                    isBoundary = true;
                    break;
                }
            }
        }

        if (isBoundary)
        {
            boundary.Add((v1, v2));
            added.Add(key);
        }
    }

    private string GetEdgeKey(int v1, int v2)
    {
        return Mathf.Min(v1, v2) + "_" + Mathf.Max(v1, v2);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(exploder))]
public class exploderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        exploder script = (exploder)target;

        EditorGUILayout.Space(10);
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("deform!", GUILayout.Height(30)))
        {
            script.applyDeform();
        }
        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("reset", GUILayout.Height(25)))
        {
            script.ResetMesh();
        }
    }

    protected virtual void OnSceneGUI()
    {
        exploder script = (exploder)target;
        if (script.points == null) return;

        bool changed = false;
        for (int i = 0; i < script.points.Length; i++)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.PositionHandle(script.points[i], Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(script, "Move Deformation Point");
                script.points[i] = newPos;
                changed = true;
            }
            
            // Draw Radius visualization
            Handles.color = Color.red;
            Handles.DrawWireDisc(script.points[i], Vector3.up, script.radius);
            
            // Draw Sink Ext visualization
            Handles.color = new Color(1, 0.5f, 0, 0.5f);
            Handles.DrawWireDisc(script.points[i], Vector3.up, script.radius + script.sinkRadiusExt);
            
            Handles.Label(script.points[i] + Vector3.up * 0.5f, "Deformer " + i);
        }

        if (changed)
        {
            EditorUtility.SetDirty(script);
        }
    }
}
#endif

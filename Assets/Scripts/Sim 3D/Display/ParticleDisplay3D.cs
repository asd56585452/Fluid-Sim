using UnityEngine;

public class ParticleDisplay3D : MonoBehaviour
{

    public Shader shader;
    public float scale;
    Mesh mesh;
    public Color col;
    public float alpha;
    Material mat;

    ComputeBuffer argsBuffer;
    Bounds bounds;

    public float velocityDisplayMax;
    bool needsUpdate;

    public int meshResolution;
    public int debug_MeshTriCount;

    private int mask=int.MaxValue;
    private Simulation3D simulation3D;
    public bool DebugMode;

    public void Init(Simulation3D sim)
    {
        mat = new Material(shader);
        mat.SetBuffer("Positions", sim.positionBuffer);
        mat.SetBuffer("Velocities", sim.velocityBuffer);
        mat.SetBuffer("Densitys", sim.densityBuffer);

        mesh = SebStuff.SphereGenerator.GenerateSphereMesh(meshResolution);
        debug_MeshTriCount = mesh.triangles.Length / 3;
        argsBuffer = ComputeHelper.CreateArgsBuffer(mesh, sim.positionBuffer.count);
        bounds = new Bounds(Vector3.zero, Vector3.one * 10000);
        simulation3D = sim;
    }

    void LateUpdate()
    {

        UpdateSettings();
        Graphics.DrawMeshInstancedIndirect(mesh, 0, mat, bounds, argsBuffer);
    }

    void UpdateSettings()
    {
        mat.SetFloat("scale", scale);
        mat.SetColor("colour", col);
        mat.SetFloat("_Alpha",alpha);
        mat.SetFloat("velocityMax", velocityDisplayMax);
        if(simulation3D != null && !DebugMode)
        {
            mask = simulation3D.numWaterParticlesMask;
        }
        else
        {
            mask = int.MaxValue;
        }
        mat.SetInt("mask", mask);

        Vector3 s = transform.localScale;
        transform.localScale = Vector3.one;
        var localToWorld = transform.localToWorldMatrix;
        transform.localScale = s;

        mat.SetMatrix("localToWorld", localToWorld);
    }

    private void OnValidate()
    {
        needsUpdate = true;
    }

    void OnDestroy()
    {
        ComputeHelper.Release(argsBuffer);
    }
}

using UnityEngine;
using Unity.Mathematics;

public class SquareParticleSpawner : MonoBehaviour
{
    public float particleDensity = 10f; // 每单位长度的粒子数量
    public Vector2 initialVelocity;
    public Transform objtransform;
    //public Vector2 spawnCentre;
    //public Vector2 spawnSize = Vector2.one;
    public int layerCount = 1; // 粒子层的厚度
    public bool showSpawnBoundsGizmos = true;

    public ParticleSpawnData GetSpawnData()
    {
        Vector2 spawnCentre = new Vector2(objtransform.position.x, objtransform.position.y);
        Vector2 spawnSize = new Vector2(objtransform.localScale.x, objtransform.localScale.y);
        // 计算每条边上粒子的数量
        int particlesPerEdgeX = Mathf.CeilToInt(particleDensity * spawnSize.x);
        int particlesPerEdgeY = Mathf.CeilToInt(particleDensity * spawnSize.y);

        // 总粒子数量 = 每条边的粒子数量 * 4条边 * 层数
        int totalParticles = (particlesPerEdgeX + particlesPerEdgeY) * 2 * layerCount - 4 * layerCount * layerCount - 4*layerCount;

        ParticleSpawnData data = new ParticleSpawnData(totalParticles);

        float2[] normals = new float2[totalParticles];

    int index = 0;
        float halfWidth = spawnSize.x / 2f;
        float halfHeight = spawnSize.y / 2f;

        // 定义每条边的起点和终点
        Vector2[] corners = new Vector2[4]
        {
            new Vector2(-halfWidth, -halfHeight), // 左下角
            new Vector2(halfWidth, -halfHeight),  // 右下角
            new Vector2(halfWidth, halfHeight),   // 右上角
            new Vector2(-halfWidth, halfHeight)   // 左上角
        };

        // 遍历四条边
        for (int edge = 0; edge < 4; edge++)
        {
            Vector2 start = corners[edge];
            Vector2 end = corners[(edge + 1) % 4];
            Vector2 edgeVector = end - start;
            Vector2 direction = edgeVector.normalized;
            Vector2 normal = new Vector2(direction.y, -direction.x); // 边的法线方向

            // 在边上生成粒子
            int particlesPerEdge = particlesPerEdgeX;
            if (edge % 2 == 1)
                particlesPerEdge = particlesPerEdgeY;
            for (int i = 1; i < particlesPerEdge -1; i++)
            {
                float t = particlesPerEdge <= 1 ? 0f : i / (float)(particlesPerEdge - 1);
                Vector2 positionOnEdge = Vector2.Lerp(start, end, t);

                // 在法线方向上生成粒子层
                for (int j = 0; j < math.min(layerCount,math.min(i, particlesPerEdge- i -1)); j++)
                {
                    float offset =  j * 1f/particleDensity;
                    Vector2 position = positionOnEdge - normal * offset;

                    data.positions[index] = position;
                    data.velocities[index] = initialVelocity;// + new Vector2(0,10f* ((edge+1) % 2));//bebug
                    data.normals[index] = normal;
                    index++;
                }
            }
        }
        //Debug.Log(index);
        //Debug.Log(totalParticles);

        return data;
    }

    public struct ParticleSpawnData
    {
        public float2[] positions;
        public float2[] velocities;
        public float2[] normals;

        public ParticleSpawnData(int num)
        {
            positions = new float2[num];
            velocities = new float2[num];
            normals= new float2[num];
        }
    }

    void OnDrawGizmos()
    {
        Vector2 spawnCentre = new Vector2(objtransform.position.x, objtransform.position.y);
        Vector2 spawnSize = new Vector2(objtransform.localScale.x, objtransform.localScale.y);
        if (showSpawnBoundsGizmos && !Application.isPlaying)
        {
            Gizmos.color = new Color(1, 1, 0, 0.5f);
            Gizmos.DrawWireCube(spawnCentre, spawnSize);
        }
    }

    public Matrix4x4 GetMatrix4x4()
    {
        return Matrix4x4.TRS(new Vector3(objtransform.position.x, objtransform.position.y, 0), objtransform.rotation, Vector3.one);
    }
}

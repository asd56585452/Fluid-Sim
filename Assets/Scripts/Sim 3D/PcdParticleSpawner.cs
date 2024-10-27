using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEngine;

public class PcdParticleSpawner : MonoBehaviour
{
    public Vector3 initialVelocity;
    public Transform objtransform;
    public string pcdFilePath;

    public Rigidbody body;

    public ParticleSpawnData GetSpawnData(uint id)
    {
        ParticleSpawnData data = new ParticleSpawnData(1);
        string filePath = Path.Combine(Application.dataPath, pcdFilePath.Replace("Assets/", ""));
        if (File.Exists(filePath))
        {
            List<Vector3> positions = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();

            LoadPLYFile(filePath, out positions, out normals);
            data = new ParticleSpawnData(positions.Count);
            for (int i = 0; i < positions.Count; i++)
            {
                data.positions[i] = positions[i];
                data.normals[i] = normals[i];
                data.index[i] = id;
                data.velocities[i] = initialVelocity;
            }
        }
        else
        {
            Debug.LogError("File does not exist at path: " + filePath);
        }


        return data;
    }

    private void LoadPLYFile(string filePath, out List<Vector3> positions, out List<Vector3> normals)
    {
        positions = new List<Vector3>();
        normals = new List<Vector3>();

        using (StreamReader reader = new StreamReader(filePath))
        {
            string line;
            bool headerEnded = false;
            int vertexCount = 0;

            // 讀取 PLY 文件頭部
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("element vertex"))
                {
                    // 解析頂點數量
                    string[] tokens = line.Split(' ');
                    vertexCount = int.Parse(tokens[2]);
                }

                if (line.StartsWith("end_header"))
                {
                    // 文件頭部結束
                    headerEnded = true;
                    break;
                }
            }

            // 讀取頂點數據
            if (headerEnded)
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    line = reader.ReadLine();
                    if (line == null) continue;

                    string[] tokens = line.Split(' ');

                    // 解析頂點位置 (x, y, z)
                    float x = float.Parse(tokens[0]);
                    float y = float.Parse(tokens[1]);
                    float z = float.Parse(tokens[2]);

                    // 解析法向量 (nx, ny, nz)
                    float nx = float.Parse(tokens[3]);
                    float ny = float.Parse(tokens[4]);
                    float nz = float.Parse(tokens[5]);

                    // 添加到位置和法向量列表
                    positions.Add(new Vector3(x, y, z));
                    normals.Add(new Vector3(nx, ny, nz));
                }
            }
        }
    }

    public struct ParticleSpawnData
    {
        public float3[] positions;
        public float3[] velocities;
        public float3[] normals;
        public uint[] index;

        public ParticleSpawnData(int num)
        {
            positions = new float3[num];
            velocities = new float3[num];
            normals = new float3[num];
            index = new uint[num];
        }
    }

    public Matrix4x4 GetMatrix4x4()
    {
        return Matrix4x4.TRS(objtransform.position, objtransform.rotation, Vector3.one);
    }

    public void AddForce(float3 force, float3 torque)
    {
        if(body != null)
        {
            body.AddForce(new Vector3(force.x, force.y, force.z));
            body.AddTorque(new Vector3(torque.x, torque.y, torque.z));
        }
        
    }
}

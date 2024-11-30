using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayModeTrans : MonoBehaviour
{
    public GameObject woodBlockPrefab;
    public GameObject rockBlockPrefab;
    public GameObject ironBlockPrefab;
    public GameObject cubeList;
    public GameObject SIM;
    public float scale = 0.5f;
    private LayerMask initBlockLayer;

    void Start()
    {
        initBlockLayer = LayerMask.GetMask("InitBlock");
        if (BlockDataStore.BlockDataList != null)
        {
            foreach (BlockData data in BlockDataStore.BlockDataList)
            {
                GameObject block = Instantiate(GetPrefabByType(data.BlockType), data.Position, data.Rotation);
                block.transform.localScale *= scale;
                Collider blockCollider = block.GetComponent<Collider>();
                if (blockCollider != null)
                {
                    // 用 Collider 的 Bounds 来检查碰撞
                    Vector3 blockSize = blockCollider.bounds.size * scale;

                    // 检测是否碰撞
                    bool isColliding = Physics.CheckBox(
                        blockCollider.bounds.center, // 中心点
                        blockSize / 2,               // 半尺寸
                        block.transform.rotation,    // 旋转
                        initBlockLayer                    // 指定层
                    );

                    // 如果与地形碰撞，销毁生成的 block 并跳过当前循环
                    if (!isColliding)
                    {
                        AddPhysicalComponents(block, data.BlockType);
                    }
                    AddPcdParticleSpawnerComponents(block);
                }
                block.transform.SetParent(cubeList.transform);
            }
        }
        SIM.SetActive(true);
    }

    void Update()
    {
        // Switching to BuildMode by pressing b
        if (Input.GetKeyDown(KeyCode.B))
        {
            SceneManager.LoadScene("BuildingDEMO");
        }
    }

    private GameObject GetPrefabByType(string blockType)
    {
        return blockType switch
        {
            "Wood" => woodBlockPrefab,
            "Rock" => rockBlockPrefab,
            "Iron" => ironBlockPrefab,
            _ => woodBlockPrefab
        };
    }

    private void AddPcdParticleSpawnerComponents(GameObject block)
    {
        PcdParticleSpawner PPS = block.AddComponent<PcdParticleSpawner>();
        PPS.pcdFilePath = "Assets/StreamingAssets/Python/cube_pcd.ply";
        PPS.objtransform = block.transform;
        PPS.body = block.GetComponent<Rigidbody>();
    }
    private void AddPhysicalComponents(GameObject block, string blockType)
    {
        Rigidbody rb = block.AddComponent<Rigidbody>();

        switch (blockType)
        {
            case "Wood":
                rb.mass = 1;
                rb.drag = 0.5f;
                break;
            case "Rock":
                rb.mass = 2;
                rb.drag = 0.7f;
                break;
            case "Iron":
                rb.mass = 5;
                rb.drag = 0.3f;
                break;
        }
    }
}
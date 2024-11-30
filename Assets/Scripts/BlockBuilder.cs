using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class BlockBuilder : MonoBehaviour
{
    public float scale = 1f;
    // Public controllers
    public Camera playerCamera;
    public float maxBuildDistance = 50f;

    // Texture Selection
    public int TextureMode = 1;  // 1: wooden, 2: rock, 3: iron
    public GameObject Texture_Pointer_Wood;
    public GameObject Texture_Pointer_Rock;
    public GameObject Texture_Pointer_Iron;

    // block quotas
    public int Wood_Quota = 20;
    public int Rock_Quota = 20;
    public int Iron_Quota = 20;
    public int blockQuota;

    // Block Prefabs
    public GameObject woodBlockPrefab;
    public GameObject rockBlockPrefab;
    public GameObject ironBlockPrefab;
    public GameObject blockPrefab;
    public GameObject previewBlockPrefab;

    // UI for quota tracing
    public GameObject Wood_Quota_UI;
    public GameObject Rock_Quota_UI;
    public GameObject Iron_Quota_UI;

    // Layers
    private LayerMask blockLayer;
    private LayerMask prevBlockLayer;
    private LayerMask initBlockLayer;

    // Private controllers
    private Vector3 initialPosition;
    private bool isLongPress = false;
    private bool isPreviewing = false;
    private List<GameObject> previewBlocks = new List<GameObject>();
    private List<GameObject> filledBlocks = new List<GameObject>();
    private Coroutine longPressCoroutine;
    private Vector3 buildDirection;
    private HashSet<Vector3> placedBlockPositions = new HashSet<Vector3>();
    
    // for scene transition
    public List<BlockData> blockDataList = new List<BlockData>();

    void Start()
    {
        // load latest construction
        if (BlockDataStore.BlockDataList != null)
        {
            foreach (BlockData data in BlockDataStore.BlockDataList)
            {
                GameObject block = Instantiate(GetPrefabByType(data.BlockType), data.Position, data.Rotation);
                block.transform.localScale = Vector3.one * scale;
            }
        }

        blockLayer = LayerMask.GetMask("Block");
        prevBlockLayer = LayerMask.GetMask("PrevBlock");
        initBlockLayer = LayerMask.GetMask("InitBlock");

        blockPrefab = woodBlockPrefab;
        blockQuota = Wood_Quota;
        TextureMode = 1;  // wood by default

        Texture_Pointer_Wood.SetActive(true);
        Texture_Pointer_Rock.SetActive(false);
        Texture_Pointer_Iron.SetActive(false);
    }

    void Update()
    {
        // Texture Switching
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            blockPrefab = woodBlockPrefab;
            blockQuota = Wood_Quota;
            TextureMode = 1;
            Texture_Pointer_Wood.SetActive(true);
            Texture_Pointer_Rock.SetActive(false);
            Texture_Pointer_Iron.SetActive(false);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            blockPrefab = rockBlockPrefab;
            blockQuota = Rock_Quota;
            TextureMode = 2;
            Texture_Pointer_Wood.SetActive(false);
            Texture_Pointer_Rock.SetActive(true);
            Texture_Pointer_Iron.SetActive(false);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            blockPrefab = ironBlockPrefab;
            blockQuota = Iron_Quota;
            TextureMode = 3;
            Texture_Pointer_Wood.SetActive(false);
            Texture_Pointer_Rock.SetActive(false);
            Texture_Pointer_Iron.SetActive(true);
        }

        // Synchronizing the quota
        if (TextureMode == 1) Wood_Quota = blockQuota;
        else if (TextureMode == 2) Rock_Quota = blockQuota;
        else if (TextureMode == 3) Iron_Quota = blockQuota;

        // Showing Quotas on UI
        Wood_Quota_UI.GetComponent<TextMeshProUGUI>().text = "        " + Wood_Quota.ToString();
        Rock_Quota_UI.GetComponent<TextMeshProUGUI>().text = "        " + Rock_Quota.ToString();
        Iron_Quota_UI.GetComponent<TextMeshProUGUI>().text = "        " + Iron_Quota.ToString();

        // Construction and Deletion
        if (Input.GetMouseButtonDown(0)) // Left-click for building
            longPressCoroutine = StartCoroutine(DetectLongPress());
        else if (Input.GetMouseButton(0) && isPreviewing) // Holding for preview update
            UpdateFilledPreview();
        else if (Input.GetMouseButtonUp(0)) // Release for placement
            EndPlacement();
        else if (Input.GetMouseButtonDown(1)) // Right-click for deleting
            DeleteBlock();

        // Clear all the basic blocks by 
        if (Input.GetKeyDown(KeyCode.C)) ResetBlocks();

        // Switch to PlayMode by pressing p
        if (Input.GetKeyDown(KeyCode.P))
        {
            SaveBlockData();
            SceneManager.LoadScene("SimulatingDEMO");
        }
    }

    private IEnumerator DetectLongPress()
    {
        float pressTime = 0f;
        initialPosition = Vector3.zero;
        isLongPress = false;
        isPreviewing = false;

        while (Input.GetMouseButton(0))
        {
            pressTime += Time.deltaTime;
            if (pressTime > 2f) // Long press threshold
            {
                isLongPress = true;
                StartLongPressPlacement();
                break;
            }
            yield return null;
        }
    }

    void StartLongPressPlacement()
    {
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, blockLayer | initBlockLayer))
        {
            initialPosition = GetGridPosition(hit.point + hit.normal * 0.5f * scale);
            buildDirection = hit.normal * scale;

            // Generate a consistent 10 preview blocks if quota allows
            GeneratePreviewBlocks();
            isPreviewing = true;
        }
    }

    void GeneratePreviewBlocks()
    {
        // Clear previous preview blocks
        foreach (GameObject previewBlock in previewBlocks)
        {
            Destroy(previewBlock);
        }
        previewBlocks.Clear();

        int previewCount = 0;
        Vector3 currentPosition = initialPosition;

        while (previewCount < 10 && previewCount < blockQuota)
        {
            // Check if the position is already occupied by a block or an InitBlock
            if (!placedBlockPositions.Contains(currentPosition))
            {
                Collider[] colliders = Physics.OverlapSphere(currentPosition, 0.1f, blockLayer | initBlockLayer);
                bool hasInitBlock = false;

                foreach (Collider collider in colliders)
                {
                    if (collider.gameObject.layer == LayerMask.NameToLayer("InitBlock"))
                    {
                        hasInitBlock = true;
                        break;
                    }
                }

                // Stop if an InitBlock is in the way
                if (hasInitBlock)
                {
                    break;
                }

                // Create a preview block if the position is unoccupied
                GameObject previewBlock = Instantiate(previewBlockPrefab, currentPosition, Quaternion.identity);
                previewBlock.transform.localScale = Vector3.one * scale;
                previewBlocks.Add(previewBlock);
                previewCount++;
            }

            // Move to the next position in the build direction
            currentPosition += buildDirection;
        }
    }

    void UpdateFilledPreview()
    {
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, prevBlockLayer))
        {
            Vector3 currentPoint = GetGridPosition(hit.point);
            int previewIndex = previewBlocks.FindIndex(block => block.transform.position == currentPoint);
            ShowFilledPreview(previewIndex);
        }
        else
        {
            ClearFilledPreview();
        }
    }

    void ShowFilledPreview(int index)
    {
        ClearFilledPreview();

        // Preview only if within quota and in unoccupied space
        for (int i = 0, x = 0; i <= index + x && i < blockQuota + x;i++)
        {
            Vector3 fillPosition = initialPosition + buildDirection * i;
            if (!placedBlockPositions.Contains(fillPosition))
            {
                GameObject filledBlock = Instantiate(blockPrefab, fillPosition, Quaternion.identity);
                filledBlock.transform.localScale = Vector3.one*scale;
                filledBlocks.Add(filledBlock);
            }
            else x++;   // x is the number of overlapped blocks
        }
    }

    void ClearFilledPreview()
    {
        foreach (GameObject block in filledBlocks)
        {
            Destroy(block);
        }
        filledBlocks.Clear();
    }

    void EndPlacement()
    {
        if (longPressCoroutine != null)
        {
            StopCoroutine(longPressCoroutine);
        }

        if (!isLongPress)
        {
            StartSinglePlacement();
        }

        if (isPreviewing)
        {
            if (filledBlocks.Count > 0)
            {
                foreach (GameObject block in filledBlocks)
                {
                    placedBlockPositions.Add(block.transform.position);
                }
                blockQuota -= filledBlocks.Count;
                filledBlocks.Clear();
            }

            foreach (GameObject previewBlock in previewBlocks)
            {
                Destroy(previewBlock);
            }
            previewBlocks.Clear();
        }

        isLongPress = false;
        isPreviewing = false;
    }

    void StartSinglePlacement()
    {
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, blockLayer | initBlockLayer))
        {
            Vector3 position = GetGridPosition(hit.point + hit.normal * 0.5f * scale);

            if (!placedBlockPositions.Contains(position) && blockQuota > 0)
            {
                PlaceBlock(position);
                blockQuota--;
            }
        }
    }

    void PlaceBlock(Vector3 position)
    {
        GameObject block = Instantiate(blockPrefab, position, Quaternion.identity);
        block.transform.localScale = Vector3.one * scale;
        placedBlockPositions.Add(position);
    }

    void DeleteBlock()
    {
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, blockLayer))
        {
            GameObject targetBlock = hit.collider.gameObject;

            if (targetBlock.layer == LayerMask.NameToLayer("Block"))
            {
                if (targetBlock.tag == "Wooden_Block" && TextureMode == 1) blockQuota++;
                else if (targetBlock.tag == "Rock_Block" && TextureMode == 2) blockQuota++;
                else if (targetBlock.tag == "Iron_Block" && TextureMode == 3) blockQuota++;
                else if (targetBlock.tag == "Wooden_Block") Wood_Quota++;
                else if (targetBlock.tag == "Rock_Block") Rock_Quota++;
                else if (targetBlock.tag == "Iron_Block") Iron_Quota++;
                Destroy(targetBlock);
                placedBlockPositions.Remove(targetBlock.transform.position);
            }
        }
    }

    void ResetBlocks()
    {
        GameObject[] allBlocks = FindObjectsOfType<GameObject>();
        foreach (GameObject block in allBlocks)
        {
            if (block.layer == LayerMask.NameToLayer("Block") && block.layer != LayerMask.NameToLayer("InitBlock"))
            {
                Destroy(block);
            }
        }
        placedBlockPositions.Clear();

        // Reset quotas
        blockQuota = 20;
        Wood_Quota = 20;
        Rock_Quota = 20;
        Iron_Quota = 20;
    }

    Vector3 GetGridPosition(Vector3 position)
    {
        position = position/scale;
        return new Vector3(Mathf.Round(position.x), Mathf.Round(position.y), Mathf.Round(position.z))*scale;
    }

    // Scene transition to PlayMode
    // transition back from PlayMode
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

    private void SaveBlockData()
    {
        blockDataList.Clear();

        GameObject[] allBlocks = FindObjectsOfType<GameObject>();
        foreach (GameObject block in allBlocks)
        {
            if (block.layer == LayerMask.NameToLayer("Block"))
            {
                string blockType = GetBlockType(block);
                blockDataList.Add(new BlockData(block.transform.position, block.transform.rotation, blockType));
            }
        }

        // Save the data to a persistent storage accessible by PlayMode (e.g., a static class or ScriptableObject)
        BlockDataStore.BlockDataList = blockDataList;
    }

    private string GetBlockType(GameObject block)
    {
        if (block.CompareTag("Wooden_Block")) return "Wood";
        if (block.CompareTag("Rock_Block")) return "Rock";
        if (block.CompareTag("Iron_Block")) return "Iron";
        return "Unknown";
    }
}

// Struct to store block data for scene transfer
[System.Serializable]
public struct BlockData
{
    public Vector3 Position;
    public Quaternion Rotation;
    public string BlockType;

    public BlockData(Vector3 position, Quaternion rotation, string blockType)
    {
        Position = position;
        Rotation = rotation;
        BlockType = blockType;
    }
}

// Persistent storage for block data
public static class BlockDataStore
{
    public static List<BlockData> BlockDataList;
}
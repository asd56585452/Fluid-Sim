using UnityEngine;
using Unity.Mathematics;
using static Spawner3D;

public class Simulation3D : MonoBehaviour
{
    public event System.Action SimulationStepCompleted;

    [Header("Settings")]
    public float timeScale = 1;
    public bool fixedTimeStep;
    public int iterationsPerFrame;
    public float gravity = -10;
    [Range(0, 1)] public float collisionDamping = 0.05f;
    public float smoothingRadius = 0.2f;
    public float targetDensity;
    public float pressureMultiplier;
    public float nearPressureMultiplier;
    public float viscosityStrength;

    [Header("References")]
    public ComputeShader compute;
    public Spawner3D spawner;
    public PcdParticleSpawner[] pcdSpawner;//MoveObstacle
    public CpuParticleSystemSpawner cpuParticleSystemSpawner;//WaterInOutput
    public float ParticleLifeTime;//WaterInOutput
    private float ParticleLifeTimeTimer=0;//WaterInOutput
    public ParticleDisplay3D display;
    public Transform floorDisplay;

    // Buffers
    public ComputeBuffer positionBuffer { get; private set; }
    public ComputeBuffer velocityBuffer { get; private set; }
    public ComputeBuffer densityBuffer { get; private set; }
    public ComputeBuffer obstaclePositionBuffer { get; private set; }//MoveObstacle
    public ComputeBuffer obstacleNormalBuffer { get; private set; }//MoveObstacle
    public ComputeBuffer obstacleFourceBuffer { get; private set; }//MoveObstacle
    public ComputeBuffer obstacleTorqueBuffer { get; private set; }//MoveObstacle
    public ComputeBuffer obstacleFourceResultBuffer { get; private set; }//MoveObstacle
    public ComputeBuffer obstacleTorqueResultBuffer { get; private set; }//MoveObstacle
    public ComputeBuffer obstacleIndexBuffer { get; private set; }//MoveObstacle
    public ComputeBuffer obstacleTransformMatrixBuffer { get; private set; }//MoveObstacle
    public ComputeBuffer obstacleStartPosBuffer { get; private set; }//MoveObstacle
    public ComputeBuffer nearestObstacleLockBuffer { get; private set; }//MoveObstacle
    public ComputeBuffer positionTemplateBuffer { get; private set; }//MoveObstacle
    public ComputeBuffer velocityTemplateBuffer { get; private set; }//MoveObstacle
    public ComputeBuffer predictedPositionsBuffer;
    ComputeBuffer spatialIndices;
    ComputeBuffer spatialOffsets;

    // Kernel IDs
    const int externalForcesKernel = 0;
    const int spatialHashKernel = 1;
    const int densityKernel = 2;
    const int pressureKernel = 3;
    const int viscosityKernel = 4;
    const int updatePositionsKernel = 5;
    const int calculateObstacleForcesKernel = 6;//MoveObstacle
    const int addObstacleForcesKernel = 7;//MoveObstacle

    GPUSort gpuSort;

    // State
    bool isPaused;
    bool pauseNextFrame;
    //MoveObstacle/*
    PcdParticleSpawner.ParticleSpawnData obstacleSpawnData;
    public float3[] obstacleFourceResult;
    public float3[] obstacleTorqueResult;
    //public float3[] velocityBufferResult;
    //public float3[] ObstacleFourcesResult;
    public uint[] startpos;
    //MoveObstacle*/
    Spawner3D.SpawnData spawnData;

    public int numObstacleParticles { get; private set; }//MoveObstacle
    public int numWaterParticles { get; private set; }//MoveObstacle
    public int numWaterParticlesMask;//WaterInOutput
    private int numWaterParticlesSpawnerStartpos=0;//WaterInOutput

    void Start()
    {
        Debug.Log("Controls: Space = Play/Pause, R = Reset");
        Debug.Log("Use transform tool in scene to scale/rotate simulation bounding box.");

        float deltaTime = 1 / 60f;
        Time.fixedDeltaTime = deltaTime;

        //MoveObstacle/*
        //spawnData = spawner.GetSpawnData();
        Spawner3D.SpawnData waterSpawnData = spawner.GetSpawnData();

        PcdParticleSpawner.ParticleSpawnData[] ts = new PcdParticleSpawner.ParticleSpawnData[pcdSpawner.Length];
        startpos = new uint[pcdSpawner.Length + 1];
        int all_l = 0;
        for (uint i = 0; i < ts.Length; i++)
        {
            startpos[i] = ((uint)all_l);
            ts[i] = pcdSpawner[i].GetSpawnData(i);
            all_l += ts[i].positions.Length;
        }
        startpos[pcdSpawner.Length] = ((uint)all_l);
        obstacleSpawnData = new PcdParticleSpawner.ParticleSpawnData(all_l);
        for (uint i = 0, ii = 0; i < all_l; i++)
        {
            if (i >= startpos[ii + 1])
                ii += 1;
            obstacleSpawnData.positions[i] = ts[ii].positions[i - startpos[ii]];
            obstacleSpawnData.velocities[i] = ts[ii].velocities[i - startpos[ii]];
            obstacleSpawnData.normals[i] = ts[ii].normals[i - startpos[ii]];
            obstacleSpawnData.index[i] = ts[ii].index[i - startpos[ii]];
        }

        obstacleFourceResult = new float3[pcdSpawner.Length];
        obstacleTorqueResult = new float3[pcdSpawner.Length];
        float3[] points = new float3[waterSpawnData.points.Length + obstacleSpawnData.positions.Length];
        float3[] velocities = new float3[waterSpawnData.points.Length + obstacleSpawnData.positions.Length];
        spawnData = new Spawner3D.SpawnData() { points = points, velocities = velocities };
        for (int i = 0; i < waterSpawnData.points.Length; i++)
        {
            spawnData.points[i] = waterSpawnData.points[i];
            spawnData.velocities[i] = waterSpawnData.velocities[i];
        }
        for (int i = 0; i < obstacleSpawnData.positions.Length; i++)
        {
            spawnData.points[waterSpawnData.points.Length + i] = obstacleSpawnData.positions[i];
            spawnData.velocities[waterSpawnData.points.Length + i] = obstacleSpawnData.velocities[i];
        }

        numObstacleParticles = obstacleSpawnData.positions.Length;
        numWaterParticles = waterSpawnData.points.Length;
        //MoveObstacle*/

        // Create buffers
        int numParticles = spawnData.points.Length;
        //velocityBufferResult = new float3[numParticles];
        //ObstacleFourcesResult = new float3[numObstacleParticles];
        positionBuffer = ComputeHelper.CreateStructuredBuffer<float3>(numParticles);
        predictedPositionsBuffer = ComputeHelper.CreateStructuredBuffer<float3>(numParticles);
        velocityBuffer = ComputeHelper.CreateStructuredBuffer<float3>(numParticles);
        densityBuffer = ComputeHelper.CreateStructuredBuffer<float2>(numParticles);
        spatialIndices = ComputeHelper.CreateStructuredBuffer<uint3>(numParticles);
        spatialOffsets = ComputeHelper.CreateStructuredBuffer<uint>(numParticles);
        obstaclePositionBuffer = ComputeHelper.CreateStructuredBuffer<float3>(numObstacleParticles);//MoveObstacle
        obstacleNormalBuffer = ComputeHelper.CreateStructuredBuffer<float3>(numObstacleParticles);//MoveObstacle
        obstacleFourceBuffer = ComputeHelper.CreateStructuredBuffer<float3>(numObstacleParticles);//MoveObstacle
        obstacleTorqueBuffer = ComputeHelper.CreateStructuredBuffer<float3>(numObstacleParticles);//MoveObstacle
        obstacleFourceResultBuffer = ComputeHelper.CreateStructuredBuffer<float3>(pcdSpawner.Length);//MoveObstacle
        obstacleTorqueResultBuffer = ComputeHelper.CreateStructuredBuffer<float3>(pcdSpawner.Length);//MoveObstacle
        obstacleIndexBuffer = ComputeHelper.CreateStructuredBuffer<uint>(numObstacleParticles);//MoveObstacle
        obstacleTransformMatrixBuffer = ComputeHelper.CreateStructuredBuffer<float4x4>(pcdSpawner.Length);//MoveObstacle
        obstacleStartPosBuffer = ComputeHelper.CreateStructuredBuffer<uint>(numObstacleParticles);//MoveObstacle
        nearestObstacleLockBuffer = ComputeHelper.CreateStructuredBuffer<uint>(numParticles);//MoveObstacle
        positionTemplateBuffer = ComputeHelper.CreateStructuredBuffer<float3>(numParticles);//MoveObstacle
        velocityTemplateBuffer = ComputeHelper.CreateStructuredBuffer<float3>(numParticles);//MoveObstacle

        // Set buffer data
        SetInitialBufferData(spawnData, obstacleSpawnData);//MoveObstacle

        // Init compute
        /*ComputeHelper.SetBuffer(compute, positionBuffer, "Positions", externalForcesKernel, updatePositionsKernel);
        ComputeHelper.SetBuffer(compute, predictedPositionsBuffer, "PredictedPositions", externalForcesKernel, spatialHashKernel, densityKernel, pressureKernel, viscosityKernel, updatePositionsKernel);
        ComputeHelper.SetBuffer(compute, spatialIndices, "SpatialIndices", spatialHashKernel, densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(compute, spatialOffsets, "SpatialOffsets", spatialHashKernel, densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(compute, densityBuffer, "Densities", densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(compute, velocityBuffer, "Velocities", externalForcesKernel, pressureKernel, viscosityKernel, updatePositionsKernel);*/
        ComputeHelper.SetBuffer(compute, positionBuffer, "Positions", externalForcesKernel, updatePositionsKernel);
        ComputeHelper.SetBuffer(compute, predictedPositionsBuffer, "PredictedPositions", externalForcesKernel, spatialHashKernel, densityKernel, pressureKernel, viscosityKernel, updatePositionsKernel);//MoveObstacle
        ComputeHelper.SetBuffer(compute, spatialIndices, "SpatialIndices", spatialHashKernel, densityKernel, pressureKernel, viscosityKernel, updatePositionsKernel, calculateObstacleForcesKernel);//MoveObstacle
        ComputeHelper.SetBuffer(compute, spatialOffsets, "SpatialOffsets", spatialHashKernel, densityKernel, pressureKernel, viscosityKernel, updatePositionsKernel, calculateObstacleForcesKernel);//MoveObstacle
        ComputeHelper.SetBuffer(compute, densityBuffer, "Densities", densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(compute, velocityBuffer, "Velocities", externalForcesKernel, pressureKernel, viscosityKernel, updatePositionsKernel, addObstacleForcesKernel);
        ComputeHelper.SetBuffer(compute, obstaclePositionBuffer, "ObstaclePositions", externalForcesKernel, updatePositionsKernel, calculateObstacleForcesKernel, addObstacleForcesKernel);//MoveObstacle
        ComputeHelper.SetBuffer(compute, obstacleNormalBuffer, "ObstacleNormals", updatePositionsKernel, calculateObstacleForcesKernel);//MoveObstacle
        ComputeHelper.SetBuffer(compute, obstacleFourceBuffer, "ObstacleFources", calculateObstacleForcesKernel, addObstacleForcesKernel);//MoveObstacle
        ComputeHelper.SetBuffer(compute, obstacleTorqueBuffer, "ObstacleTorques", calculateObstacleForcesKernel, addObstacleForcesKernel);//MoveObstacle
        ComputeHelper.SetBuffer(compute, obstacleFourceResultBuffer, "ObstacleFourceResults", addObstacleForcesKernel);//MoveObstacle
        ComputeHelper.SetBuffer(compute, obstacleTorqueResultBuffer, "ObstacleTorqueResults", addObstacleForcesKernel);//MoveObstacle
        ComputeHelper.SetBuffer(compute, obstacleIndexBuffer, "ObstacleIndexs", externalForcesKernel, updatePositionsKernel, calculateObstacleForcesKernel, addObstacleForcesKernel);//MoveObstacle
        ComputeHelper.SetBuffer(compute, obstacleTransformMatrixBuffer, "ObstacleTransformMatrixs", externalForcesKernel, updatePositionsKernel, calculateObstacleForcesKernel, addObstacleForcesKernel);//MoveObstacle
        ComputeHelper.SetBuffer(compute, obstacleStartPosBuffer, "ObstacleStartPoss", externalForcesKernel, updatePositionsKernel, addObstacleForcesKernel);//MoveObstacle
        ComputeHelper.SetBuffer(compute, nearestObstacleLockBuffer, "NearestObstacleLock", updatePositionsKernel, calculateObstacleForcesKernel);//MoveObstacle
        ComputeHelper.SetBuffer(compute, positionTemplateBuffer, "PositionsTemplate", updatePositionsKernel, calculateObstacleForcesKernel);//MoveObstacle
        ComputeHelper.SetBuffer(compute, velocityTemplateBuffer, "VelocitysTemplate", updatePositionsKernel, calculateObstacleForcesKernel);//MoveObstacle

        compute.SetInt("numParticles", positionBuffer.count);
        compute.SetInt("numObstacleParticles", numObstacleParticles);//MoveObstacle
        compute.SetInt("numWaterParticles", numWaterParticles);//MoveObstacle
        compute.SetInt("numWaterParticlesMask", numWaterParticlesMask);//WaterInOutput

        gpuSort = new();
        gpuSort.SetBuffers(spatialIndices, spatialOffsets);


        // Init display
        display.Init(this);
    }

    void FixedUpdate()
    {
        // Run simulation if in fixed timestep mode
        if (fixedTimeStep)
        {
            RunSimulationFrame(Time.fixedDeltaTime);
        }
    }

    void Update()
    {
        // Run simulation if not in fixed timestep mode
        // (skip running for first few frames as timestep can be a lot higher than usual)
        if (!fixedTimeStep && Time.frameCount > 10)
        {
            RunSimulationFrame(Time.deltaTime);
        }

        if (pauseNextFrame)
        {
            isPaused = true;
            pauseNextFrame = false;
        }
        floorDisplay.transform.localScale = new Vector3(1, 1 / transform.localScale.y * 0.1f, 1);

        HandleInput();
    }

    void RunSimulationFrame(float frameTime)
    {
        if (!isPaused)
        {
            WaterInOutput();//WaterInOutput
            float timeStep = frameTime / iterationsPerFrame * timeScale;

            UpdateSettings(timeStep);

            for (int i = 0; i < iterationsPerFrame; i++)
            {
                RunSimulationStep();
                SimulationStepCompleted?.Invoke();
                GetOutput();//MoveObstacle
            }
        }
    }
    void WaterInOutput()//MoveObstacle/*
    {
        ParticleLifeTimeTimer += Time.deltaTime;
        if(ParticleLifeTimeTimer>ParticleLifeTime)
        {
            numWaterParticlesMask = numWaterParticlesSpawnerStartpos;
            numWaterParticlesSpawnerStartpos = 0;
            ParticleLifeTimeTimer -= ParticleLifeTime;
        }
        int copylength = math.min(cpuParticleSystemSpawner.numParticlesAlive, numWaterParticles - numWaterParticlesSpawnerStartpos);
        float3[] allPoints = new float3[copylength];
        float3[] allvelocities = new float3[copylength];
        for (int i = 0;i< copylength; i++)
        {
            allPoints[i] = cpuParticleSystemSpawner.particles[i].position;
            allvelocities[i] = cpuParticleSystemSpawner.particles[i].velocity;
        }
        positionBuffer.SetData(allPoints,0, numWaterParticlesSpawnerStartpos, copylength);
        predictedPositionsBuffer.SetData(allPoints, 0, numWaterParticlesSpawnerStartpos, copylength);
        velocityBuffer.SetData(allvelocities, 0, numWaterParticlesSpawnerStartpos, copylength);
        numWaterParticlesSpawnerStartpos += copylength;
        if (numWaterParticlesSpawnerStartpos > numWaterParticles)
            numWaterParticlesSpawnerStartpos = numWaterParticles;
        if (numWaterParticlesSpawnerStartpos > numWaterParticlesMask)
            numWaterParticlesMask = numWaterParticlesSpawnerStartpos;
        if (numWaterParticlesMask > numWaterParticles)
            numWaterParticlesMask = numWaterParticles;
        if (numWaterParticlesMask < 0)
            numWaterParticlesMask = 0;

    }//MoveObstacle*/
    void GetOutput()//MoveObstacle/*
    {
        obstacleFourceResultBuffer.GetData(obstacleFourceResult);
        obstacleTorqueResultBuffer.GetData(obstacleTorqueResult);
        for (int i = 0; i < pcdSpawner.Length; i++)
            pcdSpawner[i].AddForce(obstacleFourceResult[i], obstacleTorqueResult[i]);
        //velocityBuffer.GetData(velocityBufferResult);
        //obstacleFourceBuffer.GetData(ObstacleFourcesResult);
    }//MoveObstacle*/

    void RunSimulationStep()
    {
        ComputeHelper.Dispatch(compute, positionBuffer.count, kernelIndex: externalForcesKernel);
        ComputeHelper.Dispatch(compute, positionBuffer.count, kernelIndex: spatialHashKernel);
        gpuSort.SortAndCalculateOffsets();
        ComputeHelper.Dispatch(compute, positionBuffer.count, kernelIndex: densityKernel);
        ComputeHelper.Dispatch(compute, positionBuffer.count, kernelIndex: pressureKernel);
        ComputeHelper.Dispatch(compute, positionBuffer.count, kernelIndex: viscosityKernel);
        ComputeHelper.Dispatch(compute, positionBuffer.count, kernelIndex: updatePositionsKernel);

        ComputeHelper.Dispatch(compute, numObstacleParticles, kernelIndex: calculateObstacleForcesKernel);//MoveObstacle

        int stride = 1;
        while(stride<numObstacleParticles) {stride = stride*2; }
        while(stride>0)
        {
            compute.SetInt("stride", stride);
            ComputeHelper.Dispatch(compute, numObstacleParticles, kernelIndex: addObstacleForcesKernel);//MoveObstacle
            stride /= 2;
        }
        

    }

    void UpdateSettings(float deltaTime)
    {
        Vector3 simBoundsSize = transform.localScale;
        Vector3 simBoundsCentre = transform.position;

        compute.SetFloat("deltaTime", deltaTime);
        compute.SetFloat("gravity", gravity);
        compute.SetFloat("collisionDamping", collisionDamping);
        compute.SetFloat("smoothingRadius", smoothingRadius);
        compute.SetFloat("targetDensity", targetDensity);
        compute.SetFloat("pressureMultiplier", pressureMultiplier);
        compute.SetFloat("nearPressureMultiplier", nearPressureMultiplier);
        compute.SetFloat("viscosityStrength", viscosityStrength);
        compute.SetVector("boundsSize", simBoundsSize);
        compute.SetVector("centre", simBoundsCentre);

        compute.SetMatrix("localToWorld", transform.localToWorldMatrix);
        compute.SetMatrix("worldToLocal", transform.worldToLocalMatrix);

        compute.SetInt("numWaterParticlesMask", numWaterParticlesMask);//WaterInOutput

        //MoveObstacle/*
        Matrix4x4[] matrixs = new Matrix4x4[pcdSpawner.Length];
        for (int i = 0; i < pcdSpawner.Length; i++)
        {
            matrixs[i] = pcdSpawner[i].GetMatrix4x4();
        }
        obstacleTransformMatrixBuffer.SetData(matrixs);//MoveObstacle*/
    }

    void SetInitialBufferData(Spawner3D.SpawnData spawnData, PcdParticleSpawner.ParticleSpawnData obstacleSpawnData)//MoveObstacle
    {
        float3[] allPoints = new float3[spawnData.points.Length];
        System.Array.Copy(spawnData.points, allPoints, spawnData.points.Length);

        positionBuffer.SetData(allPoints);
        predictedPositionsBuffer.SetData(allPoints);
        velocityBuffer.SetData(spawnData.velocities);

        //MoveObstacle/*
        float3[] allObstaclePoints = new float3[obstacleSpawnData.positions.Length];
        System.Array.Copy(obstacleSpawnData.positions, allObstaclePoints, obstacleSpawnData.positions.Length);

        obstaclePositionBuffer.SetData(allObstaclePoints);
        obstacleNormalBuffer.SetData(obstacleSpawnData.normals);
        obstacleIndexBuffer.SetData(obstacleSpawnData.index);
        obstacleStartPosBuffer.SetData(startpos);//MoveObstacle*/
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isPaused = !isPaused;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            isPaused = false;
            pauseNextFrame = true;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            isPaused = true;
            SetInitialBufferData(spawnData, obstacleSpawnData);
        }
    }

    void OnDestroy()
    {
        ComputeHelper.Release(positionBuffer, predictedPositionsBuffer, velocityBuffer, densityBuffer, spatialIndices, spatialOffsets, obstaclePositionBuffer, obstacleNormalBuffer, obstacleFourceBuffer, obstacleTorqueBuffer, obstacleFourceResultBuffer, obstacleTorqueResultBuffer, obstacleIndexBuffer, obstacleTransformMatrixBuffer, nearestObstacleLockBuffer,positionTemplateBuffer, velocityTemplateBuffer);//MoveObstacle
    }

    void OnDrawGizmos()
    {
        // Draw Bounds
        var m = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0, 1, 0, 0.5f);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        Gizmos.matrix = m;

    }
}

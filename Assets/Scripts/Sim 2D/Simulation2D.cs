using UnityEngine;
using Unity.Mathematics;
using static Spawner3D;
using System;

public class Simulation2D : MonoBehaviour
{
    public event System.Action SimulationStepCompleted;

    [Header("Simulation Settings")]
    public float timeScale = 1;
    public bool fixedTimeStep;
    public int iterationsPerFrame;
    public float gravity;
    [Range(0, 1)] public float collisionDamping = 0.95f;
    public float smoothingRadius = 2;
    public float targetDensity;
    public float pressureMultiplier;
    public float nearPressureMultiplier;
    public float viscosityStrength;
    public Vector2 boundsSize;
    public Vector2 obstacleSize;
    public Vector2 obstacleCentre;

    [Header("Interaction Settings")]
    public float interactionRadius;
    public float interactionStrength;

    [Header("References")]
    public ComputeShader compute;
    public ParticleSpawner spawner;
    public SquareParticleSpawner squareSpawner;//MoveObstacle
    public ParticleDisplay2D display;

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
    ComputeBuffer predictedPositionBuffer;
    ComputeBuffer spatialIndices;
    ComputeBuffer spatialOffsets;
    GPUSort gpuSort;

    // Kernel IDs
    const int externalForcesKernel = 0;
    const int spatialHashKernel = 1;
    const int densityKernel = 2;
    const int pressureKernel = 3;
    const int viscosityKernel = 4;
    const int updatePositionKernel = 5;
    const int addObstacleForcesKernel = 6;

    // State
    bool isPaused;
    ParticleSpawner.ParticleSpawnData spawnData;
    bool pauseNextFrame;
    //MoveObstacle/*
    SquareParticleSpawner.ParticleSpawnData obstacleSpawnData;
    float4x4 transformMatrix;
    public float2[] obstacleFourceResult;
    public float3[] obstacleTorqueResult;
    //MoveObstacle*/

    public int numParticles { get; private set; }
    public int numObstacleParticles { get; private set; }//MoveObstacle
    public int numWaterParticles { get; private set; }//MoveObstacle


    void Start()
    {
        Debug.Log("Controls: Space = Play/Pause, R = Reset, LMB = Attract, RMB = Repel");

        float deltaTime = 1 / 60f;
        Time.fixedDeltaTime = deltaTime;

        //MoveObstacle/*
        ParticleSpawner.ParticleSpawnData waterSpawnData = spawner.GetSpawnData();

        
        obstacleSpawnData = squareSpawner.GetSpawnData();
        obstacleFourceResult = new float2[1];
        obstacleTorqueResult = new float3[1];
        spawnData = new ParticleSpawner.ParticleSpawnData(waterSpawnData.positions.Length+obstacleSpawnData.positions.Length);
        for(int i = 0; i < waterSpawnData.positions.Length; i++)
        {
            spawnData.positions[i] = waterSpawnData.positions[i];
            spawnData.velocities[i]= waterSpawnData.velocities[i];
        }
        for (int i = 0; i < obstacleSpawnData.positions.Length; i++)
        {
            spawnData.positions[waterSpawnData.positions.Length+i] = obstacleSpawnData.positions[i];
            spawnData.velocities[waterSpawnData.positions.Length+i] = obstacleSpawnData.velocities[i];
        }

        numObstacleParticles = obstacleSpawnData.positions.Length;
        numWaterParticles = waterSpawnData.positions.Length;
        //MoveObstacle*/
        numParticles = spawnData.positions.Length;

        // Create buffers
        positionBuffer = ComputeHelper.CreateStructuredBuffer<float2>(numParticles);
        predictedPositionBuffer = ComputeHelper.CreateStructuredBuffer<float2>(numParticles);
        velocityBuffer = ComputeHelper.CreateStructuredBuffer<float2>(numParticles);
        densityBuffer = ComputeHelper.CreateStructuredBuffer<float2>(numParticles);
        spatialIndices = ComputeHelper.CreateStructuredBuffer<uint3>(numParticles);
        spatialOffsets = ComputeHelper.CreateStructuredBuffer<uint>(numParticles);
        obstaclePositionBuffer = ComputeHelper.CreateStructuredBuffer<float2>(numParticles);//MoveObstacle
        obstacleNormalBuffer = ComputeHelper.CreateStructuredBuffer<float2>(numObstacleParticles);//MoveObstacle
        obstacleFourceBuffer = ComputeHelper.CreateStructuredBuffer<float2>(numObstacleParticles);//MoveObstacle
        obstacleTorqueBuffer = ComputeHelper.CreateStructuredBuffer<float3>(numObstacleParticles);//MoveObstacle
        obstacleFourceResultBuffer = ComputeHelper.CreateStructuredBuffer<float2>(1);//MoveObstacle
        obstacleTorqueResultBuffer = ComputeHelper.CreateStructuredBuffer<float3>(1);//MoveObstacle

        // Set buffer data
        SetInitialBufferData(spawnData, obstacleSpawnData);//MoveObstacle

        // Init compute
        ComputeHelper.SetBuffer(compute, positionBuffer, "Positions", externalForcesKernel, updatePositionKernel);
        ComputeHelper.SetBuffer(compute, predictedPositionBuffer, "PredictedPositions", externalForcesKernel, spatialHashKernel, densityKernel, pressureKernel, viscosityKernel, updatePositionKernel);//MoveObstacle
        ComputeHelper.SetBuffer(compute, spatialIndices, "SpatialIndices", spatialHashKernel, densityKernel, pressureKernel, viscosityKernel, updatePositionKernel);//MoveObstacle
        ComputeHelper.SetBuffer(compute, spatialOffsets, "SpatialOffsets", spatialHashKernel, densityKernel, pressureKernel, viscosityKernel, updatePositionKernel);//MoveObstacle
        ComputeHelper.SetBuffer(compute, densityBuffer, "Densities", densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(compute, velocityBuffer, "Velocities", externalForcesKernel, pressureKernel, viscosityKernel, updatePositionKernel, addObstacleForcesKernel);
        ComputeHelper.SetBuffer(compute, obstaclePositionBuffer, "ObstaclePositions", externalForcesKernel,updatePositionKernel, addObstacleForcesKernel);//MoveObstacle
        ComputeHelper.SetBuffer(compute, obstacleNormalBuffer, "ObstacleNormals", updatePositionKernel);//MoveObstacle
        ComputeHelper.SetBuffer(compute, obstacleFourceBuffer, "ObstacleFources", addObstacleForcesKernel);//MoveObstacle
        ComputeHelper.SetBuffer(compute, obstacleTorqueBuffer, "ObstacleTorques", addObstacleForcesKernel);//MoveObstacle
        ComputeHelper.SetBuffer(compute, obstacleFourceResultBuffer, "ObstacleFourceResults", addObstacleForcesKernel);//MoveObstacle
        ComputeHelper.SetBuffer(compute, obstacleTorqueResultBuffer, "ObstacleTorqueResults", addObstacleForcesKernel);//MoveObstacle

        compute.SetInt("numParticles", numParticles);
        compute.SetInt("numObstacleParticles", numObstacleParticles);//MoveObstacle
        compute.SetInt("numWaterParticles", numWaterParticles);//MoveObstacle

        gpuSort = new();
        gpuSort.SetBuffers(spatialIndices, spatialOffsets);


        // Init display
        display.Init(this);
    }

    void FixedUpdate()
    {
        if (fixedTimeStep)
        {
            RunSimulationFrame(Time.fixedDeltaTime);
        }
    }

    void Update()
    {
        // Run simulation if not in fixed timestep mode
        // (skip running for first few frames as deltaTime can be disproportionaly large)
        if (!fixedTimeStep && Time.frameCount > 10)
        {
            RunSimulationFrame(Time.deltaTime);
        }

        if (pauseNextFrame)
        {
            isPaused = true;
            pauseNextFrame = false;
        }

        HandleInput();
    }

    void RunSimulationFrame(float frameTime)
    {
        if (!isPaused)
        {
            float timeStep = frameTime / iterationsPerFrame * timeScale;

            UpdateSettings(timeStep);

            for (int i = 0; i < iterationsPerFrame; i++)
            {
                RunSimulationStep();
                SimulationStepCompleted?.Invoke();
                GetOutput();
            }
        }
    }

    void GetOutput()
    {
        obstacleFourceResultBuffer.GetData(obstacleFourceResult);
        obstacleTorqueResultBuffer.GetData(obstacleTorqueResult);
        squareSpawner.AddForce(obstacleFourceResult, obstacleTorqueResult);
    }

    void RunSimulationStep()
    {
        ComputeHelper.Dispatch(compute, numParticles, kernelIndex: externalForcesKernel);
        ComputeHelper.Dispatch(compute, numParticles, kernelIndex: spatialHashKernel);
        gpuSort.SortAndCalculateOffsets();
        ComputeHelper.Dispatch(compute, numParticles, kernelIndex: densityKernel);
        ComputeHelper.Dispatch(compute, numParticles, kernelIndex: pressureKernel);
        ComputeHelper.Dispatch(compute, numParticles, kernelIndex: viscosityKernel);
        ComputeHelper.Dispatch(compute, numParticles, kernelIndex: updatePositionKernel); 
        ComputeHelper.Dispatch(compute, numObstacleParticles, kernelIndex: addObstacleForcesKernel);

    }

    void UpdateSettings(float deltaTime)
    {
        compute.SetFloat("deltaTime", deltaTime);
        compute.SetFloat("gravity", gravity);
        compute.SetFloat("collisionDamping", collisionDamping);
        compute.SetFloat("smoothingRadius", smoothingRadius);
        compute.SetFloat("targetDensity", targetDensity);
        compute.SetFloat("pressureMultiplier", pressureMultiplier);
        compute.SetFloat("nearPressureMultiplier", nearPressureMultiplier);
        compute.SetFloat("viscosityStrength", viscosityStrength);
        compute.SetVector("boundsSize", boundsSize);
        compute.SetVector("obstacleSize", obstacleSize);
        compute.SetVector("obstacleCentre", obstacleCentre);
        compute.SetMatrix("transformMatrix", squareSpawner.GetMatrix4x4());//MoveObstacle

        compute.SetFloat("Poly6ScalingFactor", 4 / (Mathf.PI * Mathf.Pow(smoothingRadius, 8)));
        compute.SetFloat("SpikyPow3ScalingFactor", 10 / (Mathf.PI * Mathf.Pow(smoothingRadius, 5)));
        compute.SetFloat("SpikyPow2ScalingFactor", 6 / (Mathf.PI * Mathf.Pow(smoothingRadius, 4)));
        compute.SetFloat("SpikyPow3DerivativeScalingFactor", 30 / (Mathf.Pow(smoothingRadius, 5) * Mathf.PI));
        compute.SetFloat("SpikyPow2DerivativeScalingFactor", 12 / (Mathf.Pow(smoothingRadius, 4) * Mathf.PI));

        // Mouse interaction settings:
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        bool isPullInteraction = Input.GetMouseButton(0);
        bool isPushInteraction = Input.GetMouseButton(1);
        float currInteractStrength = 0;
        if (isPushInteraction || isPullInteraction)
        {
            currInteractStrength = isPushInteraction ? -interactionStrength : interactionStrength;
        }

        compute.SetVector("interactionInputPoint", mousePos);
        compute.SetFloat("interactionInputStrength", currInteractStrength);
        compute.SetFloat("interactionInputRadius", interactionRadius);

    }

    void SetInitialBufferData(ParticleSpawner.ParticleSpawnData spawnData,SquareParticleSpawner.ParticleSpawnData obstacleSpawnData)//MoveObstacle
    {
        float2[] allPoints = new float2[spawnData.positions.Length];
        System.Array.Copy(spawnData.positions, allPoints, spawnData.positions.Length);

        positionBuffer.SetData(allPoints);
        predictedPositionBuffer.SetData(allPoints);
        velocityBuffer.SetData(spawnData.velocities);

        //MoveObstacle/*
        float2[] allObstaclePoints = new float2[obstacleSpawnData.positions.Length];
        System.Array.Copy(obstacleSpawnData.positions, allObstaclePoints, obstacleSpawnData.positions.Length);

        obstaclePositionBuffer.SetData(allObstaclePoints);
        obstacleNormalBuffer.SetData(obstacleSpawnData.normals);//MoveObstacle*/
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
            // Reset positions, the run single frame to get density etc (for debug purposes) and then reset positions again
            SetInitialBufferData(spawnData, obstacleSpawnData);
            RunSimulationStep();
            SetInitialBufferData(spawnData, obstacleSpawnData);
        }
    }


    void OnDestroy()
    {
        ComputeHelper.Release(positionBuffer, predictedPositionBuffer, velocityBuffer, densityBuffer, spatialIndices, spatialOffsets,obstaclePositionBuffer, obstacleNormalBuffer, obstacleFourceBuffer,obstacleTorqueBuffer, obstacleFourceResultBuffer, obstacleTorqueResultBuffer);//MoveObstacle
    }


    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1, 0, 0.4f);
        Gizmos.DrawWireCube(Vector2.zero, boundsSize);
        Gizmos.DrawWireCube(obstacleCentre, obstacleSize);

        if (Application.isPlaying)
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            bool isPullInteraction = Input.GetMouseButton(0);
            bool isPushInteraction = Input.GetMouseButton(1);
            bool isInteracting = isPullInteraction || isPushInteraction;
            if (isInteracting)
            {
                Gizmos.color = isPullInteraction ? Color.green : Color.red;
                Gizmos.DrawWireSphere(mousePos, interactionRadius);
            }
        }

    }
}

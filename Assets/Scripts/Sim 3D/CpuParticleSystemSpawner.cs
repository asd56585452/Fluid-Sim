using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CpuParticleSystemSpawner : MonoBehaviour
{
    public ParticleSystem particleSystem;
    public ParticleSystem.Particle[] particles;
    public int numParticlesAlive = 0;

    void Start()
    {
        if (particleSystem == null)
        {
            particleSystem = GetComponent<ParticleSystem>();
        }

        var mainModule = particleSystem.main;
        mainModule.startLifetime = Time.deltaTime; // 设置粒子生命周期为 deltaTime
        mainModule.simulationSpace = ParticleSystemSimulationSpace.World;

        particles = new ParticleSystem.Particle[particleSystem.main.maxParticles];
    }

    void Update()
    {
        numParticlesAlive = particleSystem.GetParticles(particles);
        var mainModule = particleSystem.main;
        mainModule.startLifetime = Time.deltaTime; // 设置粒子生命周期为 deltaTime
    }
}

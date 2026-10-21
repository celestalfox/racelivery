// Performance monitoring system for mobile optimization
using UnityEngine;
using UnityEngine.Profiling;

public class MobilePerformanceMonitor : MonoBehaviour
{
    [Header("Performance Targets")]
    public float targetFrameRate = 30f;
    public float maxMemoryUsageMB = 512f;
    public float thermalWarningThreshold = 0.8f;

    [Header("Monitoring Settings")]
    public float updateInterval = 1f;
    public bool enableDetailedProfiling = false;

    private float deltaTime = 0f;
    private float memoryUsage = 0f;
    private int frameCount = 0;
    private float totalFrameTime = 0f;

    void Start()
    {
        // Set appropriate target frame rate based on device capability
        Application.targetFrameRate = Mathf.RoundToInt(targetFrameRate);

        // Enable GPU profiler if needed
        if (enableDetailedProfiling)
        {
            Profiler.enabled = true;
        }

        InvokeRepeating(nameof(UpdatePerformanceMetrics), updateInterval, updateInterval);
    }

    void Update()
    {
        // Track frame time for smoothness analysis
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        frameCount++;
        totalFrameTime += Time.unscaledDeltaTime;

        // Check for frame rate drops
        if (1f / deltaTime < targetFrameRate * 0.9f)
        {
            OnFrameRateDrop();
        }
    }

    void UpdatePerformanceMetrics()
    {
        // Memory usage monitoring
        //memoryUsage = Profiler.GetTotalAllocatedMemory(false) / (1024f * 1024f);

        // Calculate average frame time
        float avgFrameTime = totalFrameTime / frameCount;
        float avgFrameRate = 1f / avgFrameTime;

        // Thermal state monitoring (iOS)
#if UNITY_IOS && !UNITY_EDITOR
        var thermalState = UnityEngine.iOS.Device.thermalState;
        if (thermalState >= UnityEngine.iOS.DeviceThermalState.Fair)
        {
            OnThermalWarning(thermalState);
        }
#endif

        // Log performance data
        if (enableDetailedProfiling)
        {
            Debug.Log($"Performance: {avgFrameRate:F1} FPS, Memory: {memoryUsage:F1} MB, " +
                     $"Frame Time: {avgFrameTime * 1000:F2} ms");
        }

        // Reset counters
        frameCount = 0;
        totalFrameTime = 0f;

        // Trigger dynamic quality adjustment if needed
        if (memoryUsage > maxMemoryUsageMB || avgFrameRate < targetFrameRate * 0.9f)
        {
            OptimizePerformance();
        }
    }

    void OnFrameRateDrop()
    {
        // Implement immediate performance recovery measures
        if (QualitySettings.GetQualityLevel() > 0)
        {
            QualitySettings.DecreaseLevel();
            Debug.Log("Quality level decreased due to frame rate drop");
        }
    }

    //void OnThermalWarning(UnityEngine.iOS.DeviceThermalState thermalState)
    //{
        // Implement thermal throttling protection
       // switch (thermalState)
       // {
           // case UnityEngine.iOS.DeviceThermalState.Fair:
            //    Application.targetFrameRate = 30;
           //     break;
         //   case UnityEngine.iOS.DeviceThermalState.Serious:
          //      Application.targetFrameRate = 15;
           //     QualitySettings.SetQualityLevel(0);
         //       break;
         //   case UnityEngine.iOS.DeviceThermalState.Critical:
       //         // Pause game or reduce to minimum functionality
         //       Time.timeScale = 0f;
       //         break;
     //   }

      //  Debug.LogWarning($"Thermal state changed: {thermalState}");
    //}

    void OptimizePerformance()
    {
        // Dynamic performance optimization system
        // Reduce particle effects
        var particleSystems = FindObjectsOfType<ParticleSystem>();
        foreach (var ps in particleSystems)
        {
            var main = ps.main;
            main.maxParticles = Mathf.Max(1, main.maxParticles / 2);
        }

        // Reduce audio quality if needed
        //AudioSettings.GetConfiguration(out var config);
        
    }

    public float GetCurrentFrameRate()
    {
        return 1f / deltaTime;
    }

    public float GetMemoryUsage()
    {
        return memoryUsage;
    }
}
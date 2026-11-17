using System.Collections;
using UnityEngine;

// Simula un sistema de luces que empieza a fallar:
// la luz principal parpadea y se va debilitando, y las lamparas del techo se apagan una por una.
public class LightFailure : MonoBehaviour
{
    [Header("Luz principal")]
    public Light flickerLight;
    [Tooltip("Segundos desde que inicia la escena hasta que empieza a parpadear.")]
    [Min(0f)] public float flickerStart = 6f;
    [Tooltip("Que tan seguido se apaga de golpe (0 = nunca, 1 = casi siempre).")]
    [Range(0f, 1f)] public float flickerAmount = 0.3f;
    [Tooltip("Intensidad final (fraccion de la original) despues de debilitarse.")]
    [Range(0f, 1f)] public float finalIntensity = 0.45f;
    [Min(0.1f)] public float dimDuration = 12f;

    [Header("Lamparas del techo")]
    public Renderer[] panels;
    [Tooltip("Material que se pone a una lampara cuando se apaga.")]
    public Material offMaterial;
    [Min(0f)] public float panelsStart = 8f;
    [Tooltip("Segundos entre que se apaga una lampara y la siguiente.")]
    [Min(0.1f)] public float panelInterval = 2f;

    float baseIntensity;
    float startTime;
    Material[] originalPanelMaterials;

    void Start()
    {
        startTime = Time.time;
        if (flickerLight != null) baseIntensity = flickerLight.intensity;

        if (panels != null)
        {
            originalPanelMaterials = new Material[panels.Length];
            for (int i = 0; i < panels.Length; i++)
                if (panels[i] != null) originalPanelMaterials[i] = panels[i].sharedMaterial;
            StartCoroutine(TurnOffPanels());
        }
    }

    void Update()
    {
        if (flickerLight == null) return;
        float t = Time.time - startTime - flickerStart;
        if (t < 0f)
        {
            flickerLight.intensity = baseIntensity;
            return;
        }

        float dim = Mathf.Lerp(1f, finalIntensity, Mathf.Clamp01(t / dimDuration));
        bool cut = Mathf.PerlinNoise(Time.time * 9f, 0.37f) < flickerAmount * 0.6f;
        flickerLight.intensity = baseIntensity * dim * (cut ? 0.08f : Random.Range(0.85f, 1f));
    }

    IEnumerator TurnOffPanels()
    {
        yield return new WaitForSeconds(panelsStart);
        for (int i = 0; i < panels.Length; i++)
        {
            var panel = panels[i];
            if (panel == null) continue;

            // Parpadea un poco antes de apagarse
            for (int k = 0; k < 4; k++)
            {
                panel.sharedMaterial = offMaterial;
                yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
                panel.sharedMaterial = originalPanelMaterials[i];
                yield return new WaitForSeconds(Random.Range(0.05f, 0.2f));
            }
            panel.sharedMaterial = offMaterial;
            yield return new WaitForSeconds(panelInterval);
        }
    }

    void OnDisable()
    {
        if (flickerLight != null && baseIntensity > 0f) flickerLight.intensity = baseIntensity;
        if (panels == null || originalPanelMaterials == null) return;
        for (int i = 0; i < panels.Length; i++)
            if (panels[i] != null) panels[i].sharedMaterial = originalPanelMaterials[i];
    }
}

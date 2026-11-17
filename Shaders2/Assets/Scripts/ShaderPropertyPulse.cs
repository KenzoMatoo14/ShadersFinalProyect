using UnityEngine;

// Anima una propiedad float de un shader entre dos valores (ida y vuelta suave).
// Sirve para mostrar en vivo una variable personalizable, por ejemplo _LensStrength del hielo.
// Usa un MaterialPropertyBlock: no modifica el material guardado en el proyecto.
public class ShaderPropertyPulse : MonoBehaviour
{
    [Tooltip("Nombre de referencia de la propiedad en el shader (ej. _LensStrength).")]
    public string propertyName = "_LensStrength";
    public float minValue = -0.08f;
    public float maxValue = -0.25f;
    [Tooltip("Segundos que tarda en ir de Min a Max y regresar.")]
    [Min(0.1f)] public float period = 6f;

    Renderer[] targets;
    MaterialPropertyBlock block;
    int propertyId;

    void Awake()
    {
        targets = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        propertyId = Shader.PropertyToID(propertyName);
    }

    void Update()
    {
        float t = 0.5f - 0.5f * Mathf.Cos(Time.time * Mathf.PI * 2f / period);
        float value = Mathf.Lerp(minValue, maxValue, t);
        foreach (var r in targets)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetFloat(propertyId, value);
            r.SetPropertyBlock(block);
        }
    }

    void OnDisable()
    {
        if (targets == null) return;
        foreach (var r in targets)
            if (r != null) r.SetPropertyBlock(null);
    }
}

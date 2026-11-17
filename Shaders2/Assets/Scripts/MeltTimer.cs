using UnityEngine;

// Hace que el derretido del MeltingAcidShader empiece cuando inicia la escena.
// El shader calcula el progreso con el tiempo global del juego: saturate((Time - _Delay) / _Duration).
// Como ese tiempo no se reinicia al cambiar de escena, aqui se le suma el tiempo actual a _Delay.
// Usa un MaterialPropertyBlock: no modifica el material guardado en el proyecto.
public class MeltTimer : MonoBehaviour
{
    [Tooltip("Segundos desde que inicia la escena hasta que empieza a derretirse.")]
    [Min(0f)] public float delay = 2f;
    [Tooltip("Objetos que se activan cuando empieza el derretido (por ejemplo, el vapor).")]
    public GameObject[] activateOnMeltStart;
    [Tooltip("Opcional: renderers a controlar. Si se deja vacio, usa los de este objeto y sus hijos.")]
    public Renderer[] targetRenderers;

    static readonly int DelayId = Shader.PropertyToID("_Delay");

    Renderer[] targets;
    MaterialPropertyBlock block;
    float meltStartTime;
    bool activated;

    void Awake()
    {
        targets = targetRenderers != null && targetRenderers.Length > 0
            ? targetRenderers
            : GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
    }

    void Start()
    {
        Restart();
    }

    // Reinicia el derretido desde el modelo completo (se puede llamar desde Timeline).
    public void Restart()
    {
        meltStartTime = Time.time + delay;
        foreach (var r in targets)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetFloat(DelayId, meltStartTime);
            r.SetPropertyBlock(block);
        }

        activated = false;
        SetActiveAll(false);
    }

    void Update()
    {
        if (!activated && Time.time >= meltStartTime)
        {
            activated = true;
            SetActiveAll(true);
        }
    }

    void SetActiveAll(bool on)
    {
        if (activateOnMeltStart == null) return;
        foreach (var go in activateOnMeltStart)
            if (go != null) go.SetActive(on);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Lanza rayos electricos desde el objeto cada cierto tiempo.
// Cada rayo es un LineRenderer en zigzag que se recalcula varias veces por segundo para que "chisporrotee",
// pega en la pared / suelo / techo mas cercano en su direccion y lleva su propia luz que ilumina donde pega.
// FireBolt() es publico para poder lanzar un rayo desde Timeline (Signal Receiver) o desde otro script.
public class LightningBolts : MonoBehaviour
{
    [Header("Frecuencia")]
    [Tooltip("Si esta activo, lanza rayos solos cada MinInterval-MaxInterval segundos.")]
    public bool autoFire = true;
    [Min(0.05f)] public float minInterval = 0.8f;
    [Min(0.05f)] public float maxInterval = 3f;

    [Header("Forma")]
    [Tooltip("Distancia maxima del rayo. Si no choca con nada antes, se apaga en el aire.")]
    [Min(0.1f)] public float maxDistance = 4f;
    [Tooltip("Numero de quiebres del rayo.")]
    [Range(4, 40)] public int segments = 16;
    [Tooltip("Que tan quebrado se ve, como fraccion del largo del rayo.")]
    [Range(0f, 0.5f)] public float jaggedness = 0.12f;
    [Tooltip("0 = cualquier direccion, 1 = siempre hacia arriba.")]
    [Range(0f, 1f)] public float upwardBias = 0.35f;
    [Tooltip("Capas con las que el rayo puede chocar.")]
    public LayerMask hitMask = ~0;

    [Header("Animacion")]
    [Min(0.02f)] public float boltDuration = 0.25f;
    [Tooltip("Cuantas veces por segundo cambia la forma del rayo mientras esta visible.")]
    [Min(1f)] public float flickerRate = 30f;
    [Range(0f, 1f)] public float branchChance = 0.5f;
    [Range(0, 4)] public int maxBranches = 2;

    [Header("Apariencia")]
    public Material boltMaterial;
    [Min(0.001f)] public float boltWidth = 0.05f;
    [Tooltip("Como se aplica la imagen del material a lo largo del rayo.")]
    public LineTextureMode textureMode = LineTextureMode.Stretch;

    [Header("Luz del rayo (se coloca donde pega cada rayo)")]
    public Color boltLightColor = new Color(0.3f, 0.9f, 1f);
    [Min(0f)] public float boltLightIntensity = 8f;
    [Min(0.1f)] public float boltLightRange = 3.5f;

    [Header("Flash de luz extra (opcional)")]
    [Tooltip("Luz fija que tambien parpadea con cada rayo (por ejemplo ShaderAccentLight). Puede quedar vacia.")]
    public Light flashLight;
    public float flashIntensity = 6f;

    [Header("Sonido (opcional)")]
    [Tooltip("Sonido que se reproduce con cada rayo.")]
    public AudioClip boltSound;
    [Range(0f, 1f)] public float boltVolume = 0.6f;
    [Tooltip("Variacion de tono al azar para que no suenen todos iguales.")]
    public Vector2 pitchRange = new Vector2(0.85f, 1.15f);

    const int MaxSimultaneousBolts = 3;
    AudioSource boltAudio;
    const int MaxBranchLines = 4;

    readonly List<LineRenderer> allLines = new List<LineRenderer>();
    readonly Queue<LineRenderer> linePool = new Queue<LineRenderer>();
    readonly List<Light> allLights = new List<Light>();
    readonly Queue<Light> lightPool = new Queue<Light>();
    // Seguro anti-congelados: hora maxima a la que cada linea/luz debe estar apagada
    readonly Dictionary<Component, float> activeUntil = new Dictionary<Component, float>();
    readonly List<Component> expired = new List<Component>();

    Renderer[] bodyRenderers;
    float baseFlashIntensity;
    float flashLevel;
    Transform poolRoot;

    void Awake()
    {
        bodyRenderers = GetComponentsInChildren<Renderer>();

        poolRoot = new GameObject("LightningBoltsPool").transform;
        poolRoot.SetParent(transform, false);

        int lineCount = MaxSimultaneousBolts * (1 + MaxBranchLines);
        for (int i = 0; i < lineCount; i++)
        {
            var line = CreateLine(i);
            allLines.Add(line);
            linePool.Enqueue(line);
        }
        for (int i = 0; i < MaxSimultaneousBolts; i++)
        {
            var light = CreateBoltLight(i);
            allLights.Add(light);
            lightPool.Enqueue(light);
        }

        if (flashLight != null) baseFlashIntensity = flashLight.intensity;

        if (boltSound != null)
        {
            boltAudio = gameObject.AddComponent<AudioSource>();
            boltAudio.playOnAwake = false;
            boltAudio.spatialBlend = 0f;
        }
    }

    void OnEnable()
    {
        StartCoroutine(AutoFireLoop());
    }

    void OnDisable()
    {
        // Unity detiene las corrutinas al desactivar el componente: se apaga todo y se reinician los pools
        activeUntil.Clear();
        linePool.Clear();
        foreach (var line in allLines)
        {
            if (line == null) continue;
            line.enabled = false;
            linePool.Enqueue(line);
        }
        lightPool.Clear();
        foreach (var light in allLights)
        {
            if (light == null) continue;
            light.enabled = false;
            lightPool.Enqueue(light);
        }
        if (flashLight != null) flashLight.intensity = baseFlashIntensity;
    }

    void LateUpdate()
    {
        if (flashLight != null)
            flashLight.intensity = baseFlashIntensity + flashIntensity * flashLevel;
        flashLevel = 0f;

        // Si algo se quedo encendido mas de lo debido, se apaga
        expired.Clear();
        foreach (var pair in activeUntil)
            if (Time.time > pair.Value) expired.Add(pair.Key);
        foreach (var c in expired) Release(c);
    }

    IEnumerator AutoFireLoop()
    {
        while (true)
        {
            float wait = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
            yield return new WaitForSeconds(wait);
            if (autoFire) FireBolt();
        }
    }

    // Lanza un rayo en una direccion al azar.
    public void FireBolt()
    {
        Vector3 start = RandomPointInBody();
        Vector3 dir = Vector3.Slerp(Random.onUnitSphere, Vector3.up, upwardBias).normalized;

        Vector3 end;
        if (Physics.Raycast(start, dir, out RaycastHit hit, maxDistance, hitMask, QueryTriggerInteraction.Ignore)
            && hit.distance > 0.3f)
            end = hit.point;
        else
            end = start + dir * maxDistance * Random.Range(0.5f, 1f);

        FireBolt(start, end);
    }

    // Lanza un rayo entre dos puntos concretos.
    public void FireBolt(Vector3 start, Vector3 end)
    {
        if (!isActiveAndEnabled || linePool.Count == 0) return; // ya hay demasiados rayos activos
        StartCoroutine(BoltRoutine(start, end));
    }

    IEnumerator BoltRoutine(Vector3 start, Vector3 end)
    {
        // Se copian los valores al inicio para que cambiarlos en el Inspector a media animacion no rompa el rayo
        int segs = segments;
        int branchSegs = Mathf.Max(4, segs / 2);
        float duration = boltDuration;
        float reshapeEvery = 1f / Mathf.Max(1f, flickerRate);
        float width = boltWidth;
        float lifetime = duration + 1f;

        if (boltAudio != null)
        {
            boltAudio.pitch = Random.Range(pitchRange.x, pitchRange.y);
            boltAudio.PlayOneShot(boltSound, boltVolume);
        }

        var main = Acquire(linePool, lifetime);
        var boltLight = lightPool.Count > 0 ? Acquire(lightPool, lifetime) : null;

        float mainLength = Vector3.Distance(start, end);
        Vector3 mainDir = mainLength > 0.0001f ? (end - start) / mainLength : Vector3.up;

        var branches = new List<(LineRenderer line, Vector3[] points, float t, Vector3 dir, float length)>();
        for (int i = 0; i < Mathf.Min(maxBranches, MaxBranchLines); i++)
        {
            if (Random.value >= branchChance || linePool.Count == 0) continue;
            Vector3 dir = Vector3.Slerp(mainDir, Random.onUnitSphere, Random.Range(0.3f, 0.6f)).normalized;
            branches.Add((Acquire(linePool, lifetime), new Vector3[branchSegs + 1], Random.Range(0.3f, 0.7f), dir,
                mainLength * Random.Range(0.25f, 0.5f)));
        }

        // La luz va donde pega el rayo, un poco separada de la superficie para que ilumine la pared
        if (boltLight != null)
        {
            boltLight.transform.position = end - mainDir * 0.3f;
            boltLight.color = boltLightColor;
            boltLight.range = boltLightRange;
        }

        var mainPoints = new Vector3[segs + 1];
        float elapsed = 0f;
        float nextReshape = 0f;
        float lightJitter = 1f;

        try
        {
            while (elapsed < duration)
            {
                if (elapsed >= nextReshape)
                {
                    BuildJaggedLine(start, end, mainPoints);
                    SetPoints(main, mainPoints);

                    foreach (var b in branches)
                    {
                        Vector3 from = mainPoints[Mathf.Clamp(Mathf.RoundToInt(b.t * segs), 0, segs)];
                        BuildJaggedLine(from, from + b.dir * b.length, b.points);
                        SetPoints(b.line, b.points);
                    }

                    lightJitter = Random.Range(0.6f, 1.2f); // la luz parpadea con cada cambio de forma
                    nextReshape = elapsed + reshapeEvery;
                }

                // Se mantiene a toda intensidad y se desvanece en el ultimo 40%
                float k = Mathf.Clamp01((1f - elapsed / duration) / 0.4f);
                ApplyFade(main, width, k);
                foreach (var b in branches) ApplyFade(b.line, width * 0.6f, k);
                if (boltLight != null) boltLight.intensity = boltLightIntensity * k * lightJitter;
                flashLevel = Mathf.Max(flashLevel, k);

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            Release(main);
            Release(boltLight);
            foreach (var b in branches) Release(b.line);
        }
    }

    T Acquire<T>(Queue<T> pool, float lifetime) where T : Component
    {
        var item = pool.Dequeue();
        activeUntil[item] = Time.time + lifetime;
        SetEnabled(item, true);
        return item;
    }

    void Release(Component item)
    {
        if (item == null || !activeUntil.Remove(item)) return; // ya estaba liberado
        SetEnabled(item, false);
        if (item is LineRenderer line) linePool.Enqueue(line);
        else if (item is Light light) lightPool.Enqueue(light);
    }

    static void SetEnabled(Component item, bool on)
    {
        if (item is Renderer r) r.enabled = on;
        else if (item is Behaviour b) b.enabled = on;
    }

    void BuildJaggedLine(Vector3 a, Vector3 b, Vector3[] points)
    {
        int count = points.Length - 1;
        Vector3 dir = b - a;
        float length = dir.magnitude;
        Vector3 forward = length > 0.0001f ? dir / length : Vector3.up;
        Vector3 side = Vector3.Cross(forward, Mathf.Abs(forward.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
        Vector3 up = Vector3.Cross(side, forward);

        for (int i = 0; i <= count; i++)
        {
            float t = (float)i / count;
            float taper = Mathf.Sin(t * Mathf.PI); // sin desplazamiento en los extremos
            Vector2 r = Random.insideUnitCircle * jaggedness * length * taper;
            points[i] = Vector3.Lerp(a, b, t) + side * r.x + up * r.y;
        }
    }

    Vector3 RandomPointInBody()
    {
        if (bodyRenderers == null || bodyRenderers.Length == 0) return transform.position;

        Bounds bounds = bodyRenderers[0].bounds;
        for (int i = 1; i < bodyRenderers.Length; i++) bounds.Encapsulate(bodyRenderers[i].bounds);

        // Punto dentro del cuerpo: el rayo "sale" atravesando la superficie
        Vector3 offset = Vector3.Scale(Random.insideUnitSphere, bounds.extents * 0.5f);
        return bounds.center + offset;
    }

    static void SetPoints(LineRenderer line, Vector3[] points)
    {
        line.positionCount = points.Length;
        line.SetPositions(points);
    }

    static void ApplyFade(LineRenderer line, float width, float k)
    {
        line.widthMultiplier = width * Mathf.Lerp(0.3f, 1f, k);
        var c = new Color(k, k, k, k);
        line.startColor = c;
        line.endColor = c;
    }

    LineRenderer CreateLine(int index)
    {
        var go = new GameObject($"Bolt_{index}");
        go.transform.SetParent(poolRoot, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.sharedMaterial = boltMaterial;
        line.textureMode = textureMode;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 2;
        line.numCornerVertices = 0;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.35f)); // mas delgado en la punta
        line.enabled = false;
        return line;
    }

    Light CreateBoltLight(int index)
    {
        var go = new GameObject($"BoltLight_{index}");
        go.transform.SetParent(poolRoot, false);
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.shadows = LightShadows.None;
        light.enabled = false;
        return light;
    }
}

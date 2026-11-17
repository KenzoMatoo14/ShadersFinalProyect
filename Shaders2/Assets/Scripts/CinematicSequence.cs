using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Recorre las escenas de los experimentos en orden y vuelve a empezar (loop).
// Hace un fundido a negro entre escenas y muestra el titulo de cada experimento.
// Se coloca en la primera escena; sobrevive a los cambios de escena (DontDestroyOnLoad).
// En el ejecutable, Esc cierra la aplicacion.
public class CinematicSequence : MonoBehaviour
{
    [Serializable]
    public class Shot
    {
        [Tooltip("Nombre de la escena (debe estar en File > Build Profiles > Scene List).")]
        public string sceneName;
        [Tooltip("Titulo que aparece al entrar a la escena.")]
        public string title;
        [Tooltip("Segundos que se queda en esta escena.")]
        [Min(1f)] public float duration = 12f;
    }

    public Shot[] shots =
    {
        new Shot { sceneName = "LabExperiment1", title = "Experimento 01 — Electricidad", duration = 12f },
        new Shot { sceneName = "LabExperiment2", title = "Experimento 02 — Lava y fuego", duration = 12f },
        new Shot { sceneName = "LabExperiment3", title = "Experimento 03 — Hielo", duration = 12f },
        new Shot { sceneName = "LabExperiment4", title = "Experimento 04 — Ácido", duration = 12f },
        new Shot { sceneName = "LabExperiment5", title = "Experimento 05 — Peste negra", duration = 16f },
    };

    [Header("Transiciones")]
    [Min(0f)] public float fadeDuration = 0.8f;
    public bool showTitles = true;
    [Min(0f)] public float titleDuration = 3f;
    public bool loop = true;

    static CinematicSequence instance;

    CanvasGroup fadeGroup;
    CanvasGroup titleGroup;
    Text titleText;

    void Awake()
    {
        // Al regresar a la escena 1 en el loop, ya existe uno: se elimina el duplicado
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
        AudioListener.volume = 0f; // empieza en negro y en silencio
    }

    IEnumerator Start()
    {
        if (instance != this || shots == null || shots.Length == 0) yield break;

        // Empieza en la escena actual si esta en la lista; si no, en la primera
        int index = Array.FindIndex(shots, s => s.sceneName == SceneManager.GetActiveScene().name);
        if (index < 0)
        {
            index = 0;
            fadeGroup.alpha = 1f;
            yield return LoadScene(shots[0].sceneName);
        }

        while (true)
        {
            var shot = shots[index];
            yield return Fade(0f);
            if (showTitles && !string.IsNullOrEmpty(shot.title)) StartCoroutine(ShowTitle(shot.title));

            yield return new WaitForSeconds(Mathf.Max(0f, shot.duration - fadeDuration * 2f));

            int next = index + 1;
            if (next >= shots.Length)
            {
                if (!loop) yield break;
                next = 0;
            }

            yield return Fade(1f);
            yield return LoadScene(shots[next].sceneName);
            index = next;
        }
    }

    void Update()
    {
        if (!Application.isEditor && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Application.Quit();
    }

    IEnumerator LoadScene(string sceneName)
    {
        var op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError($"[CinematicSequence] No se pudo cargar '{sceneName}'. ¿Esta en la lista de escenas del build?");
            yield break;
        }
        while (!op.isDone) yield return null;
        yield return null; // un frame para que la escena nueva inicialice
    }

    IEnumerator Fade(float target)
    {
        // El sonido hace fundido junto con la imagen (volumen global = 1 - cortina)
        float start = fadeGroup.alpha;
        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            fadeGroup.alpha = Mathf.Lerp(start, target, t / fadeDuration);
            AudioListener.volume = 1f - fadeGroup.alpha;
            yield return null;
        }
        fadeGroup.alpha = target;
        AudioListener.volume = 1f - target;
    }

    IEnumerator ShowTitle(string title)
    {
        titleText.text = title;
        const float fade = 0.6f;
        for (float t = 0f; t < fade; t += Time.deltaTime) { titleGroup.alpha = t / fade; yield return null; }
        titleGroup.alpha = 1f;
        yield return new WaitForSeconds(Mathf.Max(0f, titleDuration - fade * 2f));
        for (float t = 0f; t < fade; t += Time.deltaTime) { titleGroup.alpha = 1f - t / fade; yield return null; }
        titleGroup.alpha = 0f;
    }

    // Crea por codigo el canvas con la cortina negra y el texto del titulo
    void BuildOverlay()
    {
        var canvasGo = new GameObject("CinematicOverlay");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // Titulo en la parte de abajo
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(canvasGo.transform, false);
        titleGroup = titleGo.AddComponent<CanvasGroup>();
        titleGroup.alpha = 0f;
        titleText = titleGo.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 46;
        titleText.alignment = TextAnchor.LowerCenter;
        titleText.color = new Color(1f, 1f, 1f, 0.9f);
        var shadow = titleGo.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(2f, -2f);
        var titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0f);
        titleRect.anchorMax = new Vector2(1f, 0f);
        titleRect.pivot = new Vector2(0.5f, 0f);
        titleRect.anchoredPosition = new Vector2(0f, 80f);
        titleRect.sizeDelta = new Vector2(0f, 120f);

        // Cortina negra encima de todo
        var fadeGo = new GameObject("Fade");
        fadeGo.transform.SetParent(canvasGo.transform, false);
        fadeGroup = fadeGo.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 1f; // empieza en negro y aparece con fundido
        fadeGroup.blocksRaycasts = false;
        var image = fadeGo.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        var fadeRect = image.rectTransform;
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;
    }
}

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class AddressableManager : MonoBehaviour
{
    #region public
    public static AddressableManager instance;
    public int SceneNumber = 0;
    public Slider Loadingslider;
    public bool Check = false;
    public TextMeshProUGUI text, percent, Login;
    #endregion

    #region private
    [SerializeField] string bundlelable = string.Empty;
    [SerializeField] private float progressSmoothSpeed = 1.5f;

    AsyncOperationHandle handle;
    private const float ResourcePhaseEnd = 0.25f;
    private float targetProgress;
    private bool controlsProgress = true;
    #endregion

    private void Awake()
    {
        instance = this;
    }

    private void Update()
    {
        if (!controlsProgress)
            return;

        SmoothLoadingProgress();
    }

    IEnumerator Start()
    {
        Debug.Log("Resource loading started");
        yield return new WaitUntil(() => LoginManager.instance.Check == true);

        if (RuntimeAssetProvider.Backend == RuntimeAssetBackend.Resources)
        {
            yield return StartCoroutine(LoadWithResources());
            yield break;
        }

        SetLoadingStatus("Loading resources...", 0f, true);
        Check = false;

        Addressables.GetDownloadSizeAsync(bundlelable).Completed += sizeHandle =>
        {
            if (sizeHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogWarning("Addressables download size check failed. Switching to Resources.");
                Addressables.Release(sizeHandle);
                RuntimeAssetProvider.Backend = RuntimeAssetBackend.Resources;
                Check = true;
                return;
            }

            if (text != null)
                text.text = FormatBytes(sizeHandle.Result);

            Check = true;
            Addressables.Release(sizeHandle);
        };

        yield return new WaitUntil(() => Check);
        if (RuntimeAssetProvider.Backend == RuntimeAssetBackend.Resources)
        {
            yield return StartCoroutine(LoadWithResources());
            yield break;
        }

        SetLoadingStatus("Loading resources...", 0f);
        yield return new WaitForSeconds(1f);

        handle = Addressables.DownloadDependenciesAsync(bundlelable, true);
        StartCoroutine(CoPercent());
        handle.Completed += completedHandle =>
        {
            handle = completedHandle;
            SetLoadingStatus("Loading resources...", ResourcePhaseEnd);

            if (completedHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogWarning("Addressables dependency download failed. Switching to Resources.");
                RuntimeAssetProvider.Backend = RuntimeAssetBackend.Resources;
            }

            SceneLoad();
        };
    }

    IEnumerator CoPercent()
    {
        while (handle.IsValid() && !handle.IsDone)
        {
            SetLoadingStatus("Loading resources...", Mathf.Lerp(0f, ResourcePhaseEnd, handle.PercentComplete));
            yield return null;
        }
    }

    public void SceneLoad()
    {
        controlsProgress = false;
        APIData.Instance.DownLoadData();
        //SceneManager.LoadScene(SceneNumber);
    }

    private IEnumerator LoadWithResources()
    {
        Check = true;
        SetLoadingStatus("Loading resources...", ResourcePhaseEnd);
        yield return new WaitForSeconds(0.2f);
        SceneLoad();
    }

    private void SetLoadingStatus(string title, float progress, bool immediate = false)
    {
        targetProgress = Mathf.Clamp01(progress);

        if (Login != null)
            Login.text = title;

        if (text != null)
            text.text = "Loading...";

        if (immediate)
            ApplyLoadingProgress(targetProgress);
    }

    private void SmoothLoadingProgress()
    {
        float currentProgress = Loadingslider != null ? Loadingslider.value : targetProgress;
        float nextProgress = Mathf.MoveTowards(currentProgress, targetProgress, progressSmoothSpeed * Time.deltaTime);
        ApplyLoadingProgress(nextProgress);
    }

    private void ApplyLoadingProgress(float progress)
    {
        if (percent != null)
            percent.text = string.Format("{0:00.00} ", progress * 100f) + "%";

        if (Loadingslider != null)
            Loadingslider.value = progress;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
            return "No extra download";

        string[] units = { "B", "KB", "MB", "GB" };
        double size = bytes;
        int unitIndex = 0;

        while (size >= 1024d && unitIndex < units.Length - 1)
        {
            size /= 1024d;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }
}

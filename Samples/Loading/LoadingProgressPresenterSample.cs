using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PortfolioSamples.Loading
{
    public sealed class LoadingProgressPresenterSample : MonoBehaviour
    {
        [SerializeField] private Slider progressSlider;
        [SerializeField] private TMP_Text percentText;
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private float smoothSpeed = 4f;

        private float targetProgress;
        private float visibleProgress;

        private void Update()
        {
            visibleProgress = Mathf.MoveTowards(
                visibleProgress,
                targetProgress,
                smoothSpeed * Time.unscaledDeltaTime);

            if (progressSlider != null)
                progressSlider.value = visibleProgress;

            if (percentText != null)
                percentText.text = $"{visibleProgress * 100f:0.0}%";
        }

        public void SetStage(string label, float normalizedProgress)
        {
            targetProgress = Mathf.Clamp01(normalizedProgress);

            if (stateText != null)
                stateText.text = label;
        }

        public IEnumerator RunExampleFlow()
        {
            SetStage("Loading data...", 0.25f);
            yield return SimulateWork(0.5f);

            SetStage("Loading resources...", 0.65f);
            yield return SimulateWork(0.5f);

            SetStage("Preparing runtime...", 0.9f);
            yield return SimulateWork(0.35f);

            SetStage("Complete", 1f);
        }

        private static IEnumerator SimulateWork(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end)
                yield return null;
        }
    }
}


// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\ghost\Assets\Scripts\Lodding\LoadingManager.cs
// Lines: full file

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadingManager : MonoBehaviour
{
    public static LoadingManager instance;
    public TextMeshProUGUI Login, Percent;
    public Slider PercentSlider;

    [SerializeField] private string loadingLoopResourcePath = "Loading/loading_rune_loop";
    [SerializeField, Range(0f, 1f)] private float loadingLoopVolume = 0.68f;

    private void Awake()
    {
        instance = this;
        PlayLoadingLoop();
    }

    private void PlayLoadingLoop()
    {
        GameAudioManager.Ensure().PlayBgmResource(loadingLoopResourcePath, loadingLoopVolume);
    }
}

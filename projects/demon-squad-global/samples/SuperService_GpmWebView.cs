// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\DemonSquad_Global\Assets\2_Scripts\SuperService\SuperServiceManager.cs
// Lines: 488-530, 752-808

    public IEnumerator GetSSLocale()
    {
        WWWForm form = new WWWForm();
        form.AddField("client_key", "<REDACTED>");
        form.AddField("locale", Localizationstring);

        using (UnityWebRequest request = UnityWebRequest.Post(SuperHost + "<REDACTED_ENDPOINT>", form))
        {
            request.SendWebRequest();
            while (!request.isDone)
            {
                Debug.Log($"Download Progress: {request.downloadProgress * 100}%");
                yield return null;
            }
            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Error : " + request.error);
                StartCoroutine(ProcessError(request.responseCode));
            }
            else
            {
                string directoryPath = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                if (File.Exists(savePath))
                {
                    File.Delete(savePath);
                    Debug.Log("Deleted existing CSV file.");
                }

                // 새 파일 저장
                File.WriteAllBytes(savePath, request.downloadHandler.data);
                yield return new WaitUntil(() => File.Exists(savePath));
                Event("app_game_model_init_end");
                SetPlayerPrefs();
                PlayerPrefs.Save();
                StartCoroutine(LoadYourSceneAsync());
            }
        }
    }

// ...

    private void ShowWebView(string url, ScreenOrientation originalOrientation)
    {
        GpmWebView.ShowUrl(
        url,
        new GpmWebViewRequest.Configuration()
        {
            style = GpmWebViewStyle.FULLSCREEN,
            orientation = GpmOrientation.PORTRAIT,
            isClearCookie = true,
            isClearCache = true,
            isNavigationBarVisible = true,
            navigationBarColor = "#4B96E6",
            title = "Super Reward",
            isBackButtonVisible = true,
            isForwardButtonVisible = true,
            isCloseButtonVisible = true,
            supportMultipleWindows = true,
#if UNITY_IOS
        contentMode = GpmWebViewContentMode.MOBILE
#endif
        },
        (callbackType, data, error) => OnCallback(callbackType, data, error, originalOrientation),
        new List<string>()
        {
        "USER_ CUSTOM_SCHEME"
        });
    }

    private void OnCallback(GpmWebViewCallback.CallbackType callbackType, string data, GpmWebViewError error, ScreenOrientation originalOrientation)
    {
        Debug.Log("OnCallback: " + callbackType);
        switch (callbackType)
        {
            case GpmWebViewCallback.CallbackType.Close:
                if (error != null)
                {
                    Debug.LogFormat("Fail to close WebView. Error:{0}", error);
                }
                else
                {
                    StartCoroutine(RestoreOrientation(originalOrientation));
                }
                break;
        }
    }

    private IEnumerator RestoreOrientation(ScreenOrientation orientation)
    {
        yield return new WaitForSeconds(0.1f);

#if UNITY_ANDROID
        Screen.orientation = orientation;
#elif UNITY_IOS
    if (orientation == ScreenOrientation.LandscapeLeft || orientation == ScreenOrientation.LandscapeRight)
    {
        Screen.orientation = orientation;
    }

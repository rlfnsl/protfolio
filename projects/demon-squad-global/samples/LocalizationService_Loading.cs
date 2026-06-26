// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\DemonSquad_Global\Assets\Prototype\Script\Localization\LocalizationService.cs
// Lines: 218-266

    public Dictionary<string, string> LoadLocalizeFileHelper()
    {
        var dict = new Dictionary<string, string>();
        try
        {
            string[] lines = File.ReadAllLines(SuperServiceManager.instance.savePath);
            string[] headers = ParseLine(lines[0]).ToArray();

            int index = -1;
            for (int i = 0; i < headers.Length; i++)
            {
                if (headers[i].Contains(SuperServiceManager.instance.Localizationstring))
                {
                    index = i;
                    break;
                }
            }

            if (index == -1)
            {
                Debug.LogError("같은 언어가 없음");
                return null;
            }

            // 각 행을 읽어서 딕셔너리에 추가
            for (int i = 1; i < lines.Length; i++)
            {
                List<string> data = ParseLine(lines[i]);

                if (data.Count > index)
                {
                    string key = data[0].Replace("\"", "");
                    string value = data[index].Replace("\"", "");
                    if (value.Contains("\\n")) value = value.Replace("\\n", "\n");
                    if (DataManager.instance.csv_Lang == CSV_LangType.EN) value = value.Replace("~", "-");
                    dict[key] = value;
                }
            }
            CanGetLang = true;
        }
        catch (Exception e)
        {
            Debug.LogError("Error loading CSV file: " + e.Message);
        }

        ChangeLangAction?.Invoke();
        return dict;

        //var languages = Resources.Load(LocalizationFilePath(), typeof(TextAsset)) as TextAsset;

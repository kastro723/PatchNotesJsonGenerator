// Assets/Editor/PatchNotesJsonGenerator.cs
// Compatible with Unity 2020+. This is an editor-only script.
// 2025-09-30, ver. 1.0.0

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;

public class PatchNotesJsonGenerator : EditorWindow
{
    // 언어 정의
    [Serializable]
    public struct LanguageInfo
    {
        public string tag;                  // Unity/내부 태그 (ko-KR, en-US, ...)
        public string displayName;          // 에디터 표시 이름
        public string googleTranslateCode;  // Google Translate API 언어 코드
        public bool isDefault;              // 기본 선택 여부
    }

    static readonly LanguageInfo[] AllLanguages = new[]
    {
        new LanguageInfo { tag = "ko-KR", displayName = "Korean (한국어)", googleTranslateCode = "ko", isDefault = true },
        new LanguageInfo { tag = "en-US", displayName = "English (미국)", googleTranslateCode = "en", isDefault = true },
        new LanguageInfo { tag = "ja-JP", displayName = "Japanese (日本語)", googleTranslateCode = "ja", isDefault = true },
        new LanguageInfo { tag = "zh-CN", displayName = "Chinese Simplified (简体中文)", googleTranslateCode = "zh", isDefault = true },
        new LanguageInfo { tag = "zh-TW", displayName = "Chinese Traditional (繁體中文)", googleTranslateCode = "zh-TW", isDefault = false },
        new LanguageInfo { tag = "th",    displayName = "Thai (ไทย)", googleTranslateCode = "th", isDefault = false },
        new LanguageInfo { tag = "hi-IN", displayName = "Hindi (हिन्दी)", googleTranslateCode = "hi", isDefault = false },
        new LanguageInfo { tag = "it-IT", displayName = "Italian (Italiano)", googleTranslateCode = "it", isDefault = false },
        new LanguageInfo { tag = "fr-FR", displayName = "French (Français)", googleTranslateCode = "fr", isDefault = false },
        new LanguageInfo { tag = "de-DE", displayName = "German (Deutsch)", googleTranslateCode = "de", isDefault = false },
        new LanguageInfo { tag = "id",    displayName = "Indonesian (Bahasa Indonesia)", googleTranslateCode = "id", isDefault = false },
        new LanguageInfo { tag = "vi",    displayName = "Vietnamese (Tiếng Việt)", googleTranslateCode = "vi", isDefault = false },
        new LanguageInfo { tag = "ru-RU", displayName = "Russian (Русский)", googleTranslateCode = "ru", isDefault = false },
        new LanguageInfo { tag = "ar",    displayName = "Arabic (العربية)", googleTranslateCode = "ar", isDefault = false },
        new LanguageInfo { tag = "sv-SE", displayName = "Swedish (Svenska)", googleTranslateCode = "sv", isDefault = false },
        new LanguageInfo { tag = "es-ES", displayName = "Spanish Spain (Español)", googleTranslateCode = "es", isDefault = false },
        new LanguageInfo { tag = "es-419",displayName = "Spanish Latin America (Español Latinoamérica)", googleTranslateCode = "es", isDefault = false },
        new LanguageInfo { tag = "pt-BR", displayName = "Portuguese Brazil (Português do Brasil)", googleTranslateCode = "pt", isDefault = false },
        new LanguageInfo { tag = "uk",    displayName = "Ukrainian (Українська)", googleTranslateCode = "uk", isDefault = false },
        new LanguageInfo { tag = "tr-TR", displayName = "Turkish (Türkçe)", googleTranslateCode = "tr", isDefault = false },
        new LanguageInfo { tag = "pl-PL", displayName = "Polish (Polski)", googleTranslateCode = "pl", isDefault = false },
        new LanguageInfo { tag = "nl-NL", displayName = "Dutch (Nederlands)", googleTranslateCode = "nl", isDefault = false }
    };

    [Serializable]
    public class PatchNotesJson
    {
        public int version;
        public string date; // "YYYY-MM-DD"
        public List<UpdateDetail> updateDetail;
    }

    [Serializable]
    public class UpdateDetail
    {
        public string language;
        public string title;
        public List<string> messages;
    }

    // Google Cloud Translation API 응답
    [Serializable]
    public class TranslationResponse
    {
        public TranslationData data;
    }
    [Serializable]
    public class TranslationData
    {
        public Translation[] translations;
    }
    [Serializable]
    public class Translation
    {
        public string translatedText;
        public string detectedSourceLanguage;
    }

    // 에디터 입력용 로케일 엔트리
    [Serializable]
    public class LocaleEntry
    {
        public string tag;
        public string displayName;
        public string googleTranslateCode;

        [TextArea(1, 3)] public string title;               // 제목
        public List<string> messages = new List<string>(); // 줄 단위 메시지

        public bool enabled = true;
        public bool selected = true;
        public bool isTranslating = false;
    }

    // tag → JSON 언어 코드 맵
    static readonly Dictionary<string, string> TagToJsonCode = new Dictionary<string, string>
    {
        {"ko-KR","ko_KR"}, {"en-US","en_US"}, {"ja-JP","ja_JP"}, {"zh-CN","zh_CN"}, {"zh-TW","zh_TW"},
        {"th","th_TH"}, {"hi-IN","hi_IN"}, {"it-IT","it_IT"}, {"fr-FR","fr_FR"}, {"de-DE","de_DE"},
        {"id","id_ID"}, {"vi","vi_VN"}, {"ru-RU","ru_RU"}, {"ar","ar_SA"}, {"sv-SE","sv_SE"},
        {"es-ES","es_ES"}, {"es-419","es_419"}, {"pt-BR","pt_BR"}, {"uk","uk_UA"}, {"tr-TR","tr_TR"},
        {"pl-PL","pl_PL"}, {"nl-NL","nl_NL"}
    };

    // 저장 경로
    const string kSaveDir = "Assets/Resources/UpdateLogs";

    // 상태 필드
    Vector2 _scroll;
    Vector2 _languageSelectScroll;
    LocaleEntry[] _entries;
    bool _showLanguageSelection = false;
    bool _showApiSettings = false;

    // 메타데이터
    int _version = 1;
    string _date = DateTime.Now.ToString("yyyy-MM-dd");

    // Google Cloud Translation 설정
    string _apiKey = "";
    string _projectId = "";
    bool _isTranslating = false;
    int _translationProgress = 0;
    int _totalTranslations = 0;

    // 메뉴 & 초기화
    [MenuItem("Tools/Patch Notes JSON Generator")]
    static void Open()
    {
        var win = GetWindow<PatchNotesJsonGenerator>("Patch Notes JSON");
        win.minSize = new Vector2(860, 820);
        win.Init();
        win.Show();
    }

    void Init()
    {
        if (_entries == null || _entries.Length == 0)
        {
            _entries = AllLanguages.Select(lang => new LocaleEntry
            {
                tag = lang.tag,
                displayName = lang.displayName,
                googleTranslateCode = lang.googleTranslateCode,
                title = "",
                messages = new List<string>(),
                enabled = true,
                selected = lang.isDefault
            }).ToArray();
        }

        _apiKey = EditorPrefs.GetString("PatchNotesGenerator_ApiKey", "");
        _projectId = EditorPrefs.GetString("PatchNotesGenerator_ProjectId", "");
    }

    void OnGUI()
    {
        if (_entries == null || _entries.Length == 0) Init();
        EditorGUILayout.LabelField("Ver. 1.0.0", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Patch Notes – per locale", EditorStyles.boldLabel);

        // Google Cloud Translation API 설정
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Google Cloud Translation API Settings", EditorStyles.boldLabel);
        _showApiSettings = EditorGUILayout.Foldout(_showApiSettings, _showApiSettings ? "Hide" : "Show", true);
        EditorGUILayout.EndHorizontal();

        if (_showApiSettings)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.HelpBox(
                "Google Cloud Translation API를 사용하려면 API 키가 필요합니다.\n" +
                "1) Google Cloud Console에서 Translation API 활성화\n" +
                "2) API 키 생성\n" +
                "3) 아래에 입력", MessageType.Info);

            string newApiKey = EditorGUILayout.PasswordField("API Key", _apiKey);
            if (newApiKey != _apiKey)
            {
                _apiKey = newApiKey;
                EditorPrefs.SetString("PatchNotesGenerator_ApiKey", _apiKey);
            }

            string newProjectId = EditorGUILayout.TextField("Project ID (선택사항)", _projectId);
            if (newProjectId != _projectId)
            {
                _projectId = newProjectId;
                EditorPrefs.SetString("PatchNotesGenerator_ProjectId", _projectId);
            }

            bool hasApiKey = !string.IsNullOrEmpty(_apiKey);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(!hasApiKey || _isTranslating);
            if (GUILayout.Button("🔄 선택된 언어 모두 번역", GUILayout.Height(25)))
            {
                StartAutoTranslationJson();
            }
            EditorGUI.EndDisabledGroup();

            if (!hasApiKey)
                EditorGUILayout.LabelField("API Key가 필요합니다", EditorStyles.miniLabel);
            else if (_isTranslating)
                EditorGUILayout.LabelField($"번역 중... ({_translationProgress}/{_totalTranslations})", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();

            if (_isTranslating)
            {
                Rect progressRect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
                float progress = _totalTranslations > 0 ? (float)_translationProgress / _totalTranslations : 0;
                EditorGUI.ProgressBar(progressRect, progress, $"번역 진행: {_translationProgress}/{_totalTranslations}");
            }
            EditorGUILayout.EndVertical();
        }

        // 메타데이터 입력
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("📌 Patch Meta", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        _version = EditorGUILayout.IntField("Version", _version);
        EditorGUILayout.BeginHorizontal();

        //_date = EditorGUILayout.TextField("Date (YYYY-MM-DD)", _date);
        //if (GUILayout.Button("오늘 날짜", GUILayout.Width(90)))
        //    _date = DateTime.Now.ToString("yyyy-MM-dd");

        GUI.SetNextControlName("DateField");
        _date = EditorGUILayout.TextField("Date (YYYY-MM-DD)", _date);

        if (GUILayout.Button("오늘 날짜", GUILayout.Width(90)))
        {
            _date = DateTime.Now.ToString("yyyy-MM-dd");
            GUI.FocusControl(null);                // 또는 EditorGUI.FocusTextInControl("DateField");
            GUI.changed = true;
            Repaint();
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        // 언어 선택 섹션
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Language Selection", EditorStyles.boldLabel);
        _showLanguageSelection = EditorGUILayout.Foldout(_showLanguageSelection, _showLanguageSelection ? "Hide" : "Show", true);
        if (GUILayout.Button("Select Default Languages", GUILayout.Width(170)))
        {
            foreach (var e in _entries)
            {
                var langInfo = AllLanguages.FirstOrDefault(l => l.tag == e.tag);
                e.selected = langInfo.isDefault;
                e.enabled = e.selected;
            }
        }
        EditorGUILayout.EndHorizontal();

        if (_showLanguageSelection)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All")) foreach (var e in _entries) { e.selected = true; e.enabled = true; }
            if (GUILayout.Button("Deselect All")) foreach (var e in _entries) { e.selected = false; e.enabled = false; }
            EditorGUILayout.LabelField($"Selected: {_entries.Count(e => e.selected)}/{_entries.Length}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            _languageSelectScroll = EditorGUILayout.BeginScrollView(_languageSelectScroll, GUILayout.Height(150));
            int itemsPerRow = 3;
            for (int i = 0; i < _entries.Length; i += itemsPerRow)
            {
                EditorGUILayout.BeginHorizontal();
                for (int j = 0; j < itemsPerRow && i + j < _entries.Length; j++)
                {
                    var e = _entries[i + j];
                    bool wasSelected = e.selected;
                    e.selected = EditorGUILayout.ToggleLeft(e.displayName, e.selected, GUILayout.Width(260));
                    if (wasSelected != e.selected) e.enabled = e.selected;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // 한국어 원본 입력
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("📝 Patch Notes Content", EditorStyles.boldLabel);

        var koreanEntry = _entries.FirstOrDefault(e => e.tag == "ko-KR");
        if (koreanEntry != null)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("🇰🇷 한국어 원본 (자동 번역 소스)", EditorStyles.miniBoldLabel);

            koreanEntry.title = EditorGUILayout.TextField("제목 (ko-KR)", koreanEntry.title);


            string koMsgs = string.Join("\n", koreanEntry.messages ?? new List<string>());
            string newKoMsgs = EditorGUILayout.TextArea(koMsgs, GUILayout.MinHeight(100));
            if (newKoMsgs != koMsgs)
                koreanEntry.messages = SplitToLines(newKoMsgs);

            EditorGUILayout.Space(5);
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_apiKey) || _isTranslating ||
                                         string.IsNullOrEmpty(koreanEntry.title) ||
                                         (koreanEntry.messages == null || koreanEntry.messages.Count == 0));
            if (GUILayout.Button("🔄 번역 실행", GUILayout.Height(35)))
            {
                StartAutoTranslationJson();
            }
            EditorGUI.EndDisabledGroup();

            if (string.IsNullOrEmpty(_apiKey))
                EditorGUILayout.HelpBox("Google Cloud Translation API 키를 설정해주세요.", MessageType.Warning);
            else if (string.IsNullOrEmpty(koreanEntry.title) || koreanEntry.messages.Count == 0)
                EditorGUILayout.HelpBox("한국어 제목과 메시지를 입력해주세요. (메시지는 줄바꿈으로 구분)", MessageType.Info);

            EditorGUILayout.EndVertical();
        }

        // 번역 미리보기(선택된 언어만)
        var selectedEntries = _entries.Where(e => e.selected).ToArray();
        var nonKoreanEntries = selectedEntries.Where(e => e.tag != "ko-KR").ToArray();

        if (nonKoreanEntries.Length > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"📄 번역 결과 미리보기 ({nonKoreanEntries.Length}개 언어)", EditorStyles.boldLabel);
            if (GUILayout.Button("✅ 모든 언어 활성화", GUILayout.Width(140)))
                foreach (var e in nonKoreanEntries) e.enabled = true;
            if (GUILayout.Button("❌ 모든 언어 비활성화", GUILayout.Width(155)))
                foreach (var e in nonKoreanEntries) e.enabled = false;
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(360));
            foreach (var e in nonKoreanEntries)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                e.enabled = EditorGUILayout.Toggle(e.enabled, GUILayout.Width(18));
                EditorGUILayout.LabelField($"{e.displayName}  <{e.tag}>", EditorStyles.boldLabel);
                if (e.isTranslating) EditorGUILayout.LabelField("🔄 번역 중...", EditorStyles.miniLabel, GUILayout.Width(90));
                EditorGUILayout.EndHorizontal();

                EditorGUI.BeginDisabledGroup(e.isTranslating);
                e.title = EditorGUILayout.TextField("제목", e.title);

                string msgs = string.Join("\n", e.messages ?? new List<string>());
                string newMsgs = EditorGUILayout.TextArea(msgs, GUILayout.MinHeight(80));
                if (newMsgs != msgs)
                    e.messages = SplitToLines(newMsgs);
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        // JSON 출력/저장
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("📦 JSON 출력", EditorStyles.boldLabel);

        var enabledEntries = _entries.Where(x => x.selected && x.enabled &&
                                                 (!string.IsNullOrWhiteSpace(x.title) ||
                                                   (x.messages != null && x.messages.Count > 0)));
        int enabledCount = enabledEntries.Count();

        EditorGUI.BeginDisabledGroup(enabledCount == 0 || _version <= 0 || string.IsNullOrEmpty(_date));
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button($"📋 JSON 클립보드 복사 ({enabledCount}개 언어)", GUILayout.Height(30)))
        {
            string json = BuildJson(_version, _date, enabledEntries);
            EditorGUIUtility.systemCopyBuffer = json;
            ShowNotification(new GUIContent("JSON 복사됨"));
        }

        if (GUILayout.Button($"💾 파일로 저장 ({enabledCount}개 언어)", GUILayout.Height(30)))
        {
            string savePath = SaveJsonToResources(_version, _date, enabledEntries);
            if (!string.IsNullOrEmpty(savePath))
                Debug.Log($"Saved JSON: {savePath}");
        }
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();

        if (enabledCount == 0)
            EditorGUILayout.HelpBox("활성화된(선택 + enabled) 언어 중 제목/메시지가 비어있습니다.", MessageType.Warning);

        // 사용법
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "💡 사용법:\n" +
            "1) Google API 키 설정\n" +
            "2) 번역할 언어 선택\n" +
            "3) 한국어(ko-KR) 제목/메시지 입력\n" +
            "4) '번역 실행' 후 미리보기 확인/수정\n" +
            "5) '파일로 저장' → Assets/Resources/UpdateLogs/{version}.json", // ex)2.1.5
            MessageType.Info);

        Repaint();
    }

    // 번역(제목 + 메시지)
    void StartAutoTranslationJson()
    {
        if (string.IsNullOrEmpty(_apiKey) || _isTranslating) return;

        var ko = _entries.FirstOrDefault(e => e.tag == "ko-KR");

        if (ko == null || string.IsNullOrEmpty(ko.title) || ko.messages == null || ko.messages.Count == 0)
        {
            EditorUtility.DisplayDialog("오류", "한국어 제목과 메시지를 먼저 입력해주세요.", "확인");
            return;
        }

        var targets = _entries.Where(e => e.selected && e.tag != "ko-KR").ToArray();
        if (targets.Length == 0)
        {
            EditorUtility.DisplayDialog("오류", "번역할 언어를 선택해주세요.", "확인");
            return;
        }

        _isTranslating = true;
        _translationProgress = 0;
        _totalTranslations = targets.Length;

        for (int i = 0; i < targets.Length; i++)
        {
            var t = targets[i];
            t.isTranslating = true;

            float p = (float)i / targets.Length;
            bool cancel = EditorUtility.DisplayCancelableProgressBar(
                $"번역 진행 중... ({i + 1}/{targets.Length})", $"{t.displayName} 번역 중", p);
            if (cancel)
            {
                t.isTranslating = false;
                break;
            }

            // title 번역
            string titleTranslated;
            bool okTitle = TranslateSingleTextSync(ko.title, "ko", t.googleTranslateCode, out titleTranslated);
            //bool okTitle = TranslateSingleTextSync($"{ToDottedVersion(_version)} 업데이트: " + ko.title, "ko", t.googleTranslateCode, out titleTranslated);
            if (okTitle) t.title = titleTranslated;

            // messages 번역
            List<string> msgsTranslated;
            bool okMsgs = TranslateTextsSync(ko.messages, "ko", t.googleTranslateCode, out msgsTranslated);
            if (okMsgs) t.messages = msgsTranslated;

            t.isTranslating = false;
            _translationProgress++;
            System.Threading.Thread.Sleep(120);
            Repaint();
        }

        EditorUtility.ClearProgressBar();
        _isTranslating = false;
        ShowNotification(new GUIContent("번역 완료!"));
        Repaint();
    }

    // 단일 문자열 동기 번역(out 반환)
    bool TranslateSingleTextSync(string text, string sourceLanguage, string targetLanguage, out string translated)
    {
        translated = null;
        if (string.IsNullOrEmpty(_apiKey))
            return false;

        try
        {
            string url = $"https://translation.googleapis.com/language/translate/v2?key={_apiKey}";
            var requestData = new
            {
                q = text,
                source = sourceLanguage,
                target = targetLanguage,
                format = "text"
            };

            string jsonData = JsonConvert.SerializeObject(requestData);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonData);

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(jsonBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 30;

                var operation = request.SendWebRequest();
                while (!operation.isDone) System.Threading.Thread.Sleep(30);

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var resp = JsonConvert.DeserializeObject<TranslationResponse>(request.downloadHandler.text);
                    if (resp?.data?.translations != null && resp.data.translations.Length > 0)
                    {
                        translated = System.Net.WebUtility.HtmlDecode(resp.data.translations[0].translatedText);
                        return true;
                    }
                    return false;
                }
                else
                {
                    Debug.LogError($"TranslateSingle Error {request.responseCode}: {request.error}\n{request.downloadHandler.text}");
                    return false;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"TranslateSingle Exception: {e.Message}");
            return false;
        }
    }

    // 여러 문장 배치 번역
    bool TranslateTextsSync(List<string> texts, string sourceLanguage, string targetLanguage, out List<string> results)
    {
        results = new List<string>();
        if (texts == null || texts.Count == 0) return false;
        if (string.IsNullOrEmpty(_apiKey)) return false;

        try
        {
            string url = $"https://translation.googleapis.com/language/translate/v2?key={_apiKey}";
            var requestData = new
            {
                q = texts.ToArray(),   // 배열로 배치 요청
                source = sourceLanguage,
                target = targetLanguage,
                format = "text"
            };

            string jsonData = JsonConvert.SerializeObject(requestData);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonData);

            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(jsonBytes);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 30;

                var op = req.SendWebRequest();
                while (!op.isDone) System.Threading.Thread.Sleep(30);

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var resp = JsonConvert.DeserializeObject<TranslationResponse>(req.downloadHandler.text);
                    if (resp?.data?.translations != null && resp.data.translations.Length == texts.Count)
                    {
                        foreach (var t in resp.data.translations)
                            results.Add(System.Net.WebUtility.HtmlDecode(t.translatedText));
                        return true;
                    }
                    return false;
                }
                else
                {
                    Debug.LogError($"Batch Translate Error {req.responseCode}: {req.error}\n{req.downloadHandler.text}");
                    return false;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Batch Translate Exception: {e.Message}");
            return false;
        }
    }


    // JSON 빌드/저장
    static List<string> SplitToLines(string block)
    {
        return (block ?? "")
            .Replace("\r\n", "\n").Replace("\r", "\n")
            .Split('\n')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    static string BuildJson(int version, string date, IEnumerable<LocaleEntry> items)
    {
        var payload = new PatchNotesJson
        {
            version = version,
            date = date,
            updateDetail = items.Select(e => new UpdateDetail
            {
                language = TagToJsonCode.TryGetValue(e.tag, out var code) ? code : e.tag.Replace('-', '_'),
                title = e.title ?? "",
                messages = e.messages != null ? e.messages.Where(m => !string.IsNullOrWhiteSpace(m)).ToList()
                                              : new List<string>()
            }).ToList()
        };

        return JsonConvert.SerializeObject(payload, Formatting.Indented);
    }

    static string SaveJsonToResources(int version, string date, IEnumerable<LocaleEntry> items)
    {
        // 폴더 보장
        if (!Directory.Exists(kSaveDir))
            Directory.CreateDirectory(kSaveDir);


        string fileName = ToDottedVersion(version) + ".json";
        string fullPath = Path.Combine(kSaveDir, fileName);

        string json = BuildJson(version, date, items);
        File.WriteAllText(fullPath, json, new UTF8Encoding(false));

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("저장 완료",
            $"패치 노트가 저장되었습니다.\n{fullPath}", "확인");

        return fullPath;
    }

    // 파일 이름(x.x.x ----> ex) 2.1.5.json) 변환 메소드
    static string ToDottedVersion(int version)
    {
        if (version < 0) version = 0;
        int patch = version % 10;
        int minor = (version / 10) % 10;
        int major = version / 100; // 1215 -> major=12, minor=1, patch=5 => "12.1.5"
        return $"{major}.{minor}.{patch}";
    }
}
#endif
#pragma warning disable CS0618 // Google Sheets uses the retained legacy request transport until CSV replaces it.

using GameDBLibrary;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class GSheetsPopupComponent : Component
    {
        public Action OnPopupClosed = null;

        private Rect m_popupRect = new Rect(100, 100, 415, 80);

        private bool m_waitingForImport = false;
        private bool m_waitingForExport = false;
        private float m_progressBarProgress = 0;
        private bool m_inited;

        public GSheetsPopupComponent(string name) : base(name) { }

        public override void Render(params object[] args)
        {
            if (!m_inited)
            {
                m_popupRect.position = Event.current.mousePosition - (m_popupRect.size / 2);
                m_inited = true;
            }

            m_popupRect = GUILayout.Window(2, m_popupRect, RenderPopup, "Google Sheets");
        }

        private void RenderPopup(int windowID)
        {
            if (GUI.Button(new Rect(395, 0, 18, 16), "x"))
            {
                ClosePopup();
            }

            SetWebAppUrl(UIHelpers.RenderTextField("Web App Url:", GetWebAppUrl(), new UIHelpers.FieldLayout(90, 400)));
            SetSheetID(UIHelpers.RenderTextField("Sheet ID:", GetSheetID(), new UIHelpers.FieldLayout(70, 400)));

            if (GUILayout.Button("Export", GUILayout.Width(140)))
            {
                ClosePopup();
                ExportToSheets(OnExportComplete);

                m_waitingForExport = true;
                m_progressBarProgress = 0;

                Action onUpdate = null;
                onUpdate = delegate ()
                {
                    if (m_waitingForExport)
                    {
                        EditorUtility.DisplayProgressBar("Export to Google Sheets", "Please wait while the gameDB is exported to your spreadsheet", m_progressBarProgress);
                        m_progressBarProgress += 0.005f;
                        m_progressBarProgress %= 1;
                    }
                    else
                    {
                        Updater.Instance.OnUpdate -= onUpdate;
                        EditorUtility.ClearProgressBar();
                    }
                };

                Updater.Instance.OnUpdate += onUpdate;
            }

            if (GUILayout.Button("Import", GUILayout.Width(140)))
            {
                ClosePopup();

                ImportFromSheets(OnImportComplete);

                m_waitingForImport = true;
                m_progressBarProgress = 0;

                Action onUpdate = null;
                onUpdate = delegate ()
                {
                    if (m_waitingForImport)
                    {
                        EditorUtility.DisplayProgressBar("Importing from Google Sheets", "Please wait while the gameDB is imported from your spreadsheet", m_progressBarProgress);
                        m_progressBarProgress += 0.005f;
                        m_progressBarProgress %= 1;
                    }
                    else
                    {
                        Updater.Instance.OnUpdate -= onUpdate;
                        EditorUtility.ClearProgressBar();
                    }
                };

                Updater.Instance.OnUpdate += onUpdate;
            }

            GUI.DragWindow();
        }

        private void OnImportComplete(bool success)
        {
            m_waitingForImport = false;

            if (success)
            {
                EditorUtility.DisplayDialog("Import", "The import was successful", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Import ERROR", "The import failed! Please check the logs for more info", "OK");
            }
        }

        private void OnExportComplete(bool success)
        {
            m_waitingForExport = false;

            if (success)
            {
                EditorUtility.DisplayDialog("Export", "The export was successful", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Export ERROR", "The export failed! Please check the logs for more info", "OK");
            }
        }

        public void ExportToSheets(Action<bool> onExportComplete)
        {
            GameDB.Instance.Save();

            if (string.IsNullOrEmpty(GetWebAppUrl()) || string.IsNullOrEmpty(GetSheetID()))
            {
                Debug.LogError("Error - ExportToSheets: no url or sheet id set");
                onExportComplete(false);
            }
            else
            {
                var dataJSON = string.Empty;
                var schemaJSON = string.Empty;

                if (!GameDB.Instance.GetRawDataJSON(out dataJSON) || !GameDB.Instance.GetRawSchemaJSON(out schemaJSON))
                {
                    Debug.LogError("Error - ExportToSheets: unable to get raw json");
                    onExportComplete(false);
                }
                else
                {
                    RequestHelper.StartRequest(GetWebAppUrl(), RequestMethod.POST, new Dictionary<string, string>() {
                        { "mode", "import" },
                        { "id", GetSheetID() },
                        { "data", dataJSON },
                        { "schema", schemaJSON },
                    }, (reqError, response) =>
                    {
                        if (reqError == null)
                        {
                            if (response == null || string.IsNullOrEmpty(response.GetText()))
                            {
                                Debug.LogError("Error - ExportToSheets: empty response");
                                onExportComplete(false);
                            }
                            else
                            {
                                if (JsonSerialization.Deserialize(response.GetText()) is IDictionary<string, object> dic)
                                {
                                    if (dic.ContainsKey("error") || !dic.ContainsKey("success"))
                                    {
                                        Debug.LogError($"Error - ExportToSheets: {response}");
                                        onExportComplete(false);
                                    }
                                    else
                                    {
                                        onExportComplete(true);
                                    }
                                }
                            }
                        }
                        else
                        {
                            Debug.LogError(reqError.Message);
                            onExportComplete(false);
                        }
                    });
                }
            }
        }

        public void ImportFromSheets(Action<bool> onImportComplete)
        {
            if (string.IsNullOrEmpty(GetWebAppUrl()) || string.IsNullOrEmpty(GetSheetID()))
            {
                Debug.LogError("Error - ImportFromSheets: no url or sheet id set");
                onImportComplete(false);
            }
            else
            {
                RequestHelper.StartRequest(GetWebAppUrl(), RequestMethod.POST, new Dictionary<string, string>() {
                    { "mode", "export" },
                    { "id", GetSheetID() },
                    { "scope", GameDB.Instance.ScopeName }
                }, (reqError, response) =>
                {
                    if (reqError == null)
                    {
                        if (string.IsNullOrEmpty(response?.GetText()))
                        {
                            Debug.LogError("Error - ImportFromSheets: empty response");
                            onImportComplete(false);
                        }
                        else
                        {
                            if (JsonSerialization.Deserialize(response.GetText()) is IDictionary<string, object> dic)
                            {
                                if (dic.ContainsKey("error"))
                                {
                                    Debug.LogError($"Error - ImportFromSheets: {response.GetText()}");
                                    onImportComplete(false);
                                }
                                else
                                {
                                    if (!GameDB.Instance.ImportRawDataJSON(response.GetText()))
                                    {
                                        Debug.LogError($"Error - ImportFromSheets: failed to load gameDB - {response.GetText()}");
                                        onImportComplete(false);
                                    }
                                    else
                                    {
                                        EventSystem.Instance.TriggerEvent(Events.GAMEDB_LOADED);
                                        onImportComplete(true);
                                    }
                                }
                            }
                            else
                            {
                                Debug.LogError($"Error - ImportFromSheets: failed to serialize response - {response.GetText()}");
                                onImportComplete(false);
                            }
                        }
                    }
                    else
                    {
                        Debug.LogError(reqError);
                        onImportComplete(false);
                    }
                });
            }
        }

        private void ClosePopup()
        {
            m_inited = false;
            OnPopupClosed?.Invoke();
        }

        private string GetWebAppUrl()
        {
            var webAppUrl = string.Empty;

            if (Settings.Instance.GoogleSheets.ContainsKey(GameDB.Instance.ScopeName))
            {
                var settings = Settings.Instance.GoogleSheets[GameDB.Instance.ScopeName];

                webAppUrl = settings.WebAppUrl;
            }

            return webAppUrl;
        }

        private string GetSheetID()
        {
            var sheetID = string.Empty;

            if (Settings.Instance.GoogleSheets.ContainsKey(GameDB.Instance.ScopeName))
            {
                var settings = Settings.Instance.GoogleSheets[GameDB.Instance.ScopeName];

                sheetID = settings.SheetID;
            }

            return sheetID;
        }

        private void SetWebAppUrl(string url)
        {
            if (!Settings.Instance.GoogleSheets.ContainsKey(GameDB.Instance.ScopeName))
            {
                Settings.Instance.GoogleSheets[GameDB.Instance.ScopeName] = new Settings.GoogleSheetsSettings();
            }

            var settings = Settings.Instance.GoogleSheets[GameDB.Instance.ScopeName];

            if (settings.WebAppUrl != url)
            {
                settings.WebAppUrl = url;

                Settings.Instance.GoogleSheets[GameDB.Instance.ScopeName] = settings;
                Settings.Instance.Save();
            }
        }

        private void SetSheetID(string id)
        {
            if (!Settings.Instance.GoogleSheets.ContainsKey(GameDB.Instance.ScopeName))
            {
                Settings.Instance.GoogleSheets[GameDB.Instance.ScopeName] = new Settings.GoogleSheetsSettings();
            }

            var settings = Settings.Instance.GoogleSheets[GameDB.Instance.ScopeName];

            if (settings.SheetID != id)
            {
                settings.SheetID = id;

                Settings.Instance.GoogleSheets[GameDB.Instance.ScopeName] = settings;
                Settings.Instance.Save();
            }
        }
    }
}

#pragma warning restore CS0618

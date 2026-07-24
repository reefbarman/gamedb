using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class DeploymentPickerComponent : Component
    {
        private ServerDeploymentsDataSource m_deployments;

        private int m_selectedTag;
        private int m_selectedRevision;

        private int m_selectedUploadTag;
        private string m_newTag = string.Empty;

        public DeploymentPickerComponent(string name) : base(name) { }

        public override void Render(params object[] args)
        {
            var tags = new string[0];

            if (m_deployments != null)
            {
                tags = m_deployments.Deployments.Keys.ToArray();
            }

            GUILayout.Label("Deployments", EditorStyles.boldLabel, GUILayout.Width(110));

            if (GUILayout.Button("Retrieve Deployments", GUILayout.Width(150)))
            {
                EventSystem.Instance.TriggerEvent(Events.REVISION_UNLOADED);
                RetrieveDeployments();
            }

            EditorGUILayout.Separator();

            RenderUploader(tags);

            EditorGUILayout.Separator();

            RenderPicker(tags);
        }

        public void UpdateDeployments(ServerDeploymentsDataSource deployments)
        {
            m_deployments = deployments;
        }

        private void RenderUploader(string[] deploymentTags)
        {
            var tags = new List<string>(deploymentTags) {"Create Tag"};

            UIHelpers.RenderBox(delegate {
                GUILayout.Label("Upload GameDB", EditorStyles.boldLabel);

                UIHelpers.RenderHorizontalGroup(delegate {
                    m_selectedUploadTag = UIHelpers.RenderDropDown("Upload Tag:", m_selectedUploadTag, tags.ToArray(), new UIHelpers.FieldLayout(75, 200));

                    var tag = tags[m_selectedUploadTag];

                    if (m_selectedUploadTag == (tags.Count - 1))
                    {
                        m_newTag = EditorGUILayout.TextField(m_newTag, GUILayout.Width(150));
                        tag = m_newTag;
                    }

                    if (GUILayout.Button("Upload", GUILayout.Width(150)))
                    {
                        Upload(tag);
                    }
                });
            }, GUI.backgroundColor, 100);
        }

        private void RenderPicker(string[] deploymentTags)
        {
            var tags = new[] { "No Deployments" };
            var revisions = new[] { "No Deployments" };

            tags = deploymentTags.Length > 0 ? deploymentTags : tags;

            if (tags[m_selectedTag] != "No Deployments")
            {
                var numRevisions = m_deployments.Deployments[tags[m_selectedTag]].NumRevisions;

                revisions = new string[numRevisions];

                for (var i = 0; i < numRevisions; i++)
                {
                    revisions[i] = $"{i}";
                }
            }

            var tag = tags[m_selectedTag];
            var revision = revisions[m_selectedRevision];

            var currentRevision = "No deployments";

            if (m_deployments != null && tag != "No Deployments")
            {
                currentRevision = $"Current revision: {m_deployments.Deployments[tag].CurrentRevision}";
            }

            EditorGUILayout.LabelField(currentRevision);

            UIHelpers.RenderHorizontalGroup(delegate {
                m_selectedTag = UIHelpers.RenderDropDown("Tag:", m_selectedTag, tags, new UIHelpers.FieldLayout(30, 200));
                m_selectedRevision = UIHelpers.RenderDropDown("Revision:", m_selectedRevision, revisions, new UIHelpers.FieldLayout(60, 200));
            });

            UIHelpers.RenderHorizontalGroup(delegate {
                if (GUILayout.Button("View Revision", GUILayout.Width(150)))
                {
                    if (tag != "No Deployments" && revision != "No Deployments")
                    {
                        var revisionIndex = Convert.ToInt32(revision);

                        var deployment = m_deployments.Deployments[tag];

                        EventSystem.Instance.TriggerEvent(Events.REVISION_LOADED, tag, revision, deployment.Revisions[revisionIndex].Path, deployment.BasePath, deployment.SchemaPath);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("No Deployments", "Please load a gameDB and Retrieve Deployments", "OK");
                    }
                }

                if (GUILayout.Button("Promote Revision", GUILayout.Width(150)))
                {
                    if (tag != "No Deployments" && revision != "No Deployments")
                    {
                        if (EditorUtility.DisplayDialog("Promote Revision", $"Are you sure you want to promote revision {revision}?", "Yes", "No"))
                        {
                            PromoteRevision(tag, revision);
                        }
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("No Deployments", "Please load a gameDB and Retrieve Deployments", "OK");
                    }
                }
            });
        }

        private void RetrieveDeployments()
        {
            if (!string.IsNullOrEmpty(GameDB.Instance.LoadedPath))
            {
                //TODO prob need better validation ie check for http://
                if (!string.IsNullOrEmpty(Settings.Instance.GameDBServer))
                {
                    var onFinished = UIHelpers.LoadingBar("Retrieving Deployments", "Retrieving Deployments...");

                    RequestHelper.StartRequest(GameDBLibrary.Utils.UrlCombine(Settings.Instance.GameDBServer, "/gamedb/getitems"), RequestMethod.POST, new Dictionary<string, string> { { "scope", GameDB.Instance.ScopeName } }, (reqError, response) => {
                        onFinished();

                        var error = ServerResponse.HandleBasicResponse(response);

                        if (error == null)
                        {
                            var deploymentDS = new ServerDeploymentsDataSource();
                            if (deploymentDS.ParseResponse(response.GetText()))
                            {
                                m_selectedTag = 0;
                                m_selectedRevision = 0;

                                m_selectedUploadTag = 0;
                                m_newTag = string.Empty;

                                m_deployments = deploymentDS;
                            }
                            else
                            {
                                Debug.LogError("failed parsing deployments response");
                                EditorUtility.DisplayDialog("Invalid response", "An invalid response was received from the server, please check it is reachable and working", "OK");
                            }
                        }
                        else
                        {
                            Debug.LogError(error);
                            EditorUtility.DisplayDialog("Communication Error", $"An unexpected response was received from the server, please check it is reachable and working. {error}", "OK");
                        }
                    });
                }
                else
                {
                    EditorUtility.DisplayDialog("Invalid Server Host", "You need to enter a valid server host before continuing", "OK");
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Load GameDB", "You need to load a gameDB before you can retrieve the deployments", "OK");
            }
        }

        private void Upload(string tag)
        {
            if (!string.IsNullOrEmpty(GameDB.Instance.LoadedPath))
            {
                if (GameDB.Instance.GetRawDataJSON(out var rawJson))
                {
                    if (GameDB.Instance.GetRawSchemaJSON(out var rawSchemaJson))
                    {
                        var onFinished = UIHelpers.LoadingBar("Uploading revision", "Uploading revisions...");
                        
                        var form = WebRequestHelper.FormFactory.CreateNewForm();
                        form.AddField("tag", tag);
                        form.AddField("scope", GameDB.Instance.ScopeName);
                        form.AddBinaryData("gamedb", Encoding.UTF8.GetBytes(rawJson), "gamedb.json");
                        form.AddBinaryData("gamedbSchema", Encoding.UTF8.GetBytes(rawSchemaJson), "gamedbSchema.json");

                        RequestHelper.StartPostRequest(GameDBLibrary.Utils.UrlCombine(Settings.Instance.GameDBServer, "/gamedb/upload"), form, (reqError, response) => {
                            onFinished();

                            if (reqError == null)
                            {
                                var error = ServerResponse.HandleBasicResponse(response);

                                if (error == null)
                                {
                                    RetrieveDeployments();
                                }
                                else
                                {
                                    Debug.LogError(error);
                                    EditorUtility.DisplayDialog("Communication Error", $"An unexpected response was received from the server, please check it is reachable and working. {error}", "OK");
                                }
                            }
                            else
                            {
                                Debug.LogError(reqError);
                                EditorUtility.DisplayDialog("Communication Error", $"An unexpected response was received from the server, please check it is reachable and working. {reqError}", "OK");
                            }
                        });
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("GameDB Load Error", "Failed to load gameDB check console for details", "OK");
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("GameDB Load Error", "Failed to load gameDB check console for details", "OK");
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Load GameDB", "You need to load a gameDB before you can retrieve the deployments", "OK");
            }
        }

        private void PromoteRevision(string tag , string revision)
        {
            var onFinished = UIHelpers.LoadingBar("Promoting revision", "Promoting revisions...");

            RequestHelper.StartRequest(GameDBLibrary.Utils.UrlCombine(Settings.Instance.GameDBServer, "/gamedb/setcurrent"), RequestMethod.POST, 
                new Dictionary<string, string> {
                    { "scope", GameDB.Instance.ScopeName },
                    { "tag", tag },
                    { "revision", revision }
                },
                (reqError, response) => {
                    onFinished();

                    if (reqError == null)
                    {
                        var error = ServerResponse.HandleBasicResponse(response);

                        if (error == null)
                        {
                            RetrieveDeployments();
                            GameDBEditor.OnRevisionPromotion?.Invoke(GameDB.Instance.ScopeName, tag, Int32.Parse(revision));
                        }
                        else
                        {
                            Debug.LogError(error);
                            EditorUtility.DisplayDialog("Communication Error", $"An unexpected response was received from the server, please check it is reachable and working. {error}", "OK");
                        }
                    }
                    else
                    {
                        Debug.LogError(reqError);
                        EditorUtility.DisplayDialog("Communication Error", $"An unexpected response was received from the server, please check it is reachable and working. {reqError}", "OK");
                    }
                }
            );
        }
    }
}

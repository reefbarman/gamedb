using GameDBLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameDBEditorLibrary
{
    internal class ServerDeploymentsDataSource
    {
        public struct Revision
        {
            public string Path;
            public string Checksum;

            public Revision(string path, string checksum)
            {
                Path = path;
                Checksum = checksum;
            }
        }

        public struct Deployment
        {
            public string Tag;
            public string Scope;
            public string BasePath;
            public string SchemaPath;
            public int NumRevisions;
            public int CurrentRevision;
            public string CurrentPath;
            public string Checksum;
            public List<Revision> Revisions;
        }

        private Dictionary<string, Deployment> m_deployments = new Dictionary<string, Deployment>();

        public Dictionary<string, Deployment> Deployments => m_deployments;

        public bool ParseResponse(string jsonResponse)
        {
            var success = false;

            try
            {
                var response = JsonSerialization.Deserialize(jsonResponse) as IDictionary<string, object>;

                if (response != null && response.ContainsKey("success"))
                {
                    if (response["success"] != null)
                    {
                        var deployments = response["success"] as List<object>;

                        if (deployments != null)
                        {
                            foreach (var deploymentObj in deployments)
                            {
                                var deployment = ParseDeployment(deploymentObj);
                                m_deployments.Add(deployment.Tag, deployment);
                            }

                            success = true;
                        }
                        else
                        {
                            throw new FormatException("invalid deployments array");
                        }
                    }
                    else
                    {
                        success = true;
                    }
                }
                else if (response != null && response.ContainsKey("error"))
                {
                    throw new Exception($"error in response: {response["error"]}");
                }
                else
                {
                    throw new FormatException("invalid response");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogError(jsonResponse);
            }

            return success;
        }

        private Deployment ParseDeployment(object deploymentObj)
        {
            Deployment deployment = new Deployment();

            var deploymentDic = deploymentObj as IDictionary<string, object>;

            var expectedKeys = new[] { "tag", "scope", "base_path", "schema_path", "num_revisions", "current_revision", "current_path", "checksum", "revisions" };

            if (deploymentDic == null || !expectedKeys.All(deploymentDic.ContainsKey))
            {
                throw new FormatException("invalid deployment object");
            }

            deployment.NumRevisions = Convert.ToInt32(deploymentDic["num_revisions"]);
            deployment.Revisions = ParseRevisions(deploymentDic["revisions"], deployment.NumRevisions);

            deployment.Tag = deploymentDic["tag"] as string;
            deployment.Scope = deploymentDic["scope"] as string;
            deployment.BasePath = deploymentDic["base_path"] as string;
            deployment.SchemaPath = deploymentDic["schema_path"] as string;
            deployment.CurrentRevision = Convert.ToInt32(deploymentDic["current_revision"]);
            deployment.CurrentPath = deploymentDic["current_path"] as string;
            deployment.Checksum = deploymentDic["checksum"] as string;

            if (string.IsNullOrEmpty(deployment.Tag) || string.IsNullOrEmpty(deployment.Scope) || string.IsNullOrEmpty(deployment.BasePath) || string.IsNullOrEmpty(deployment.CurrentPath) || string.IsNullOrEmpty(deployment.Checksum))
            {
                throw new FormatException("invalid deployment object");
            }

            return deployment;
        }

        private List<Revision> ParseRevisions(object revisionsObj, int revisionCount)
        {
            var revisionsList = new List<Revision>();

            var revisions = revisionsObj as List<object>;

            if (revisions == null)
            {
                throw new FormatException("invalid revisions array");
            }

            for (var i = 0; i < revisionCount; i++)
            {
                if (i >= revisions.Count)
                {
                    throw new FormatException($"missing revisions object at index: {i}");
                }

                revisionsList.Add(ParseRevision(revisions[i]));
            }

            return revisionsList;
        }

        private Revision ParseRevision(object revisionObj)
        {
            var revisionDic = revisionObj as IDictionary<string, object>;

            if (revisionDic == null || !new[] { "path", "checksum" }.Any(revisionDic.ContainsKey))
            {
                throw new FormatException($"invalid revision object");
            }

            var revision = new Revision(revisionDic["path"] as string, revisionDic["checksum"] as string);

            if (string.IsNullOrEmpty(revision.Path) || string.IsNullOrEmpty(revision.Checksum))
            {
                throw new FormatException($"invalid revision object");
            }

            return revision;
        }
    }
}

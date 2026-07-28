using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GameDBEditorLibrary.Documents
{
    [Serializable]
    internal struct GameDBDiskToken : IEquatable<GameDBDiskToken>
    {
        [SerializeField] internal bool DataExists;
        [SerializeField] internal bool SchemaExists;
        [SerializeField] internal string DataSha256;
        [SerializeField] internal string SchemaSha256;

        internal static GameDBDiskToken Absent => new GameDBDiskToken();

        public bool Equals(GameDBDiskToken other)
        {
            return DataExists == other.DataExists
                && SchemaExists == other.SchemaExists
                && string.Equals(DataSha256, other.DataSha256, StringComparison.Ordinal)
                && string.Equals(SchemaSha256, other.SchemaSha256, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is GameDBDiskToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = DataExists ? 1 : 0;
                hash = (hash * 397) ^ (SchemaExists ? 1 : 0);
                hash = (hash * 397) ^ (DataSha256?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (SchemaSha256?.GetHashCode() ?? 0);
                return hash;
            }
        }

        public static bool operator ==(GameDBDiskToken left, GameDBDiskToken right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GameDBDiskToken left, GameDBDiskToken right)
        {
            return !left.Equals(right);
        }
    }

    internal sealed class GameDBResolvedPath
    {
        internal string AssetPath { get; }
        internal string SchemaAssetPath { get; }
        internal string RelativePath { get; }
        internal string AbsolutePath { get; }
        internal string SchemaAbsolutePath { get; }
        internal string LockKey { get; }

        internal GameDBResolvedPath(string assetPath, string schemaAssetPath, string relativePath,
            string absolutePath, string schemaAbsolutePath, string lockKey)
        {
            AssetPath = assetPath;
            SchemaAssetPath = schemaAssetPath;
            RelativePath = relativePath;
            AbsolutePath = absolutePath;
            SchemaAbsolutePath = schemaAbsolutePath;
            LockKey = lockKey;
        }
    }

    internal sealed class GameDBPairRead
    {
        internal GameDBResolvedPath Path { get; }
        internal byte[] DataBytes { get; }
        internal byte[] SchemaBytes { get; }
        internal GameDBDiskToken Token { get; }

        internal GameDBPairRead(GameDBResolvedPath path, byte[] dataBytes, byte[] schemaBytes,
            GameDBDiskToken token)
        {
            Path = path;
            DataBytes = dataBytes;
            SchemaBytes = schemaBytes;
            Token = token;
        }
    }

    internal enum GameDBPairCommitStatus
    {
        Committed,
        Conflict,
        Failed,
        StateUnknown
    }

    internal sealed class GameDBPairCommitResult
    {
        internal GameDBPairCommitStatus Status { get; set; }
        internal string Message { get; set; }
        internal GameDBDiskToken TokenBefore { get; set; }
        internal GameDBDiskToken TokenAfter { get; set; }
        internal IReadOnlyList<string> RecoveryArtifacts { get; set; } = Array.Empty<string>();
    }

    internal sealed class GameDBRecoveryRequiredException : IOException
    {
        internal IReadOnlyList<string> Artifacts { get; }

        internal GameDBRecoveryRequiredException(IReadOnlyList<string> artifacts)
            : base($"Interrupted GameDB save artifacts require recovery: {string.Join(", ", artifacts)}")
        {
            Artifacts = artifacts;
        }
    }

    internal interface IGameDBPairStore
    {
        StringComparer LockKeyComparer { get; }
        GameDBResolvedPath Resolve(string assetPath);
        GameDBPairRead Read(string assetPath);
        GameDBPairCommitResult Commit(string assetPath, GameDBDiskToken expectedToken,
            byte[] dataBytes, byte[] schemaBytes);
    }

    internal sealed class GameDBFilePairStore : IGameDBPairStore
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false, true);
        private static readonly Dictionary<string, object> PathLocks
            = new Dictionary<string, object>(PathKeyComparer());
        private static readonly object PathLocksGate = new object();

        internal static GameDBFilePairStore Instance { get; } = new GameDBFilePairStore();

        public StringComparer LockKeyComparer => PathKeyComparer();

        private GameDBFilePairStore()
        {
        }

        public GameDBResolvedPath Resolve(string assetPath)
        {
            GameDBModelOperations.RequireName(assetPath, nameof(assetPath));
            var normalized = assetPath.Replace('\\', '/').Trim().Normalize(NormalizationForm.FormC);
            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("DatabasePath must be an Assets-relative path beginning with 'Assets/'.");
            }

            if (!normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith(".schema.json", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("DatabasePath must identify a data .json file, not a schema file.");
            }

            var inputRelativePath = normalized.Substring("Assets/".Length);
            var assetsRoot = Path.GetFullPath(Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var absolutePath = Path.GetFullPath(Path.Combine(assetsRoot, inputRelativePath))
                .Normalize(NormalizationForm.FormC);
            var prefix = assetsRoot + Path.DirectorySeparatorChar;
            if (!absolutePath.StartsWith(prefix, PathComparison()))
            {
                throw new ArgumentException("Path resolves outside the project's Assets directory.");
            }

            RejectExistingLinks(assetsRoot, absolutePath);
            var schemaAbsolutePath = Path.ChangeExtension(absolutePath, ".schema.json");
            var relativePath = absolutePath.Substring(assetsRoot.Length + 1)
                .Replace(Path.DirectorySeparatorChar, '/');
            var canonicalAssetPath = "Assets/" + relativePath;
            var schemaAssetPath = Path.ChangeExtension(canonicalAssetPath, ".schema.json");
            var lockKey = absolutePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .Normalize(NormalizationForm.FormC);
            return new GameDBResolvedPath(canonicalAssetPath, schemaAssetPath, relativePath,
                absolutePath, schemaAbsolutePath, lockKey);
        }

        public GameDBPairRead Read(string assetPath)
        {
            var path = Resolve(assetPath);
            lock (GetPathLock(path.LockKey))
            {
                var artifacts = FindArtifacts(path);
                if (artifacts.Count > 0)
                {
                    throw new GameDBRecoveryRequiredException(artifacts);
                }

                return ReadUnlocked(path);
            }
        }

        public GameDBPairCommitResult Commit(string assetPath, GameDBDiskToken expectedToken,
            byte[] dataBytes, byte[] schemaBytes)
        {
            if (dataBytes == null)
            {
                throw new ArgumentNullException(nameof(dataBytes));
            }

            if (schemaBytes == null)
            {
                throw new ArgumentNullException(nameof(schemaBytes));
            }

            var path = Resolve(assetPath);
            lock (GetPathLock(path.LockKey))
            {
                var artifacts = FindArtifacts(path);
                if (artifacts.Count > 0)
                {
                    return new GameDBPairCommitResult
                    {
                        Status = GameDBPairCommitStatus.StateUnknown,
                        Message = "Interrupted GameDB save artifacts require recovery.",
                        RecoveryArtifacts = artifacts
                    };
                }

                var before = CaptureToken(path);
                if (before != expectedToken)
                {
                    return new GameDBPairCommitResult
                    {
                        Status = GameDBPairCommitStatus.Conflict,
                        Message = "Database files changed after this document was loaded.",
                        TokenBefore = before,
                        TokenAfter = before
                    };
                }

                var intended = Token(dataBytes, schemaBytes);
                var operationId = Guid.NewGuid().ToString("N");
                var dataTemporaryPath = path.AbsolutePath + "." + operationId + ".tmp";
                var schemaTemporaryPath = path.SchemaAbsolutePath + "." + operationId + ".tmp";
                var dataBackupPath = path.AbsolutePath + "." + operationId + ".bak";
                var schemaBackupPath = path.SchemaAbsolutePath + "." + operationId + ".bak";
                var recoveryArtifacts = new[]
                {
                    dataTemporaryPath,
                    schemaTemporaryPath,
                    dataBackupPath,
                    schemaBackupPath
                };
                var dataExisted = before.DataExists;
                var schemaExisted = before.SchemaExists;

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path.AbsolutePath));
                    File.WriteAllBytes(dataTemporaryPath, dataBytes);
                    File.WriteAllBytes(schemaTemporaryPath, schemaBytes);
                    ReplaceFile(dataTemporaryPath, path.AbsolutePath, dataBackupPath, dataExisted);
                    ReplaceFile(schemaTemporaryPath, path.SchemaAbsolutePath, schemaBackupPath, schemaExisted);
                    var after = CaptureToken(path);
                    if (after != intended)
                    {
                        return new GameDBPairCommitResult
                        {
                            Status = GameDBPairCommitStatus.StateUnknown,
                            Message = "Database files did not match the intended content after replacement.",
                            TokenBefore = before,
                            TokenAfter = after,
                            RecoveryArtifacts = ExistingArtifacts(recoveryArtifacts)
                        };
                    }

                    DeleteFiles(recoveryArtifacts);
                    return new GameDBPairCommitResult
                    {
                        Status = GameDBPairCommitStatus.Committed,
                        Message = "Database files committed.",
                        TokenBefore = before,
                        TokenAfter = after
                    };
                }
                catch (Exception exception)
                {
                    TryRestore(path.AbsolutePath, dataBackupPath, dataExisted);
                    TryRestore(path.SchemaAbsolutePath, schemaBackupPath, schemaExisted);
                    GameDBDiskToken after;
                    try
                    {
                        after = CaptureToken(path);
                    }
                    catch (Exception)
                    {
                        return new GameDBPairCommitResult
                        {
                            Status = GameDBPairCommitStatus.StateUnknown,
                            Message = exception.Message,
                            TokenBefore = before,
                            RecoveryArtifacts = ExistingArtifacts(recoveryArtifacts)
                        };
                    }

                    if (after == before)
                    {
                        DeleteFiles(recoveryArtifacts);
                        return new GameDBPairCommitResult
                        {
                            Status = GameDBPairCommitStatus.Failed,
                            Message = exception.Message,
                            TokenBefore = before,
                            TokenAfter = after
                        };
                    }

                    if (after == intended)
                    {
                        DeleteFiles(recoveryArtifacts);
                        return new GameDBPairCommitResult
                        {
                            Status = GameDBPairCommitStatus.Committed,
                            Message = exception.Message,
                            TokenBefore = before,
                            TokenAfter = after
                        };
                    }

                    return new GameDBPairCommitResult
                    {
                        Status = GameDBPairCommitStatus.StateUnknown,
                        Message = exception.Message,
                        TokenBefore = before,
                        TokenAfter = after,
                        RecoveryArtifacts = ExistingArtifacts(recoveryArtifacts)
                    };
                }
            }
        }

        internal static byte[] Encode(string value)
        {
            return Utf8NoBom.GetBytes(value);
        }

        internal static string Decode(byte[] bytes)
        {
            if (bytes == null)
            {
                return null;
            }

            var offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF
                ? 3
                : 0;
            return Utf8NoBom.GetString(bytes, offset, bytes.Length - offset);
        }

        private static GameDBPairRead ReadUnlocked(GameDBResolvedPath path)
        {
            var dataBytes = File.Exists(path.AbsolutePath) ? File.ReadAllBytes(path.AbsolutePath) : null;
            var schemaBytes = File.Exists(path.SchemaAbsolutePath) ? File.ReadAllBytes(path.SchemaAbsolutePath) : null;
            return new GameDBPairRead(path, dataBytes, schemaBytes, Token(dataBytes, schemaBytes));
        }

        private static GameDBDiskToken CaptureToken(GameDBResolvedPath path)
        {
            var dataBytes = File.Exists(path.AbsolutePath) ? File.ReadAllBytes(path.AbsolutePath) : null;
            var schemaBytes = File.Exists(path.SchemaAbsolutePath) ? File.ReadAllBytes(path.SchemaAbsolutePath) : null;
            return Token(dataBytes, schemaBytes);
        }

        private static GameDBDiskToken Token(byte[] dataBytes, byte[] schemaBytes)
        {
            return new GameDBDiskToken
            {
                DataExists = dataBytes != null,
                SchemaExists = schemaBytes != null,
                DataSha256 = dataBytes == null ? null : Hash(dataBytes),
                SchemaSha256 = schemaBytes == null ? null : Hash(schemaBytes)
            };
        }

        private static string Hash(byte[] bytes)
        {
            using (var algorithm = SHA256.Create())
            {
                return string.Concat(algorithm.ComputeHash(bytes).Select(value => value.ToString("x2")));
            }
        }

        private static object GetPathLock(string key)
        {
            lock (PathLocksGate)
            {
                if (!PathLocks.TryGetValue(key, out var pathLock))
                {
                    pathLock = new object();
                    PathLocks.Add(key, pathLock);
                }

                return pathLock;
            }
        }

        private static IReadOnlyList<string> FindArtifacts(GameDBResolvedPath path)
        {
            var directory = Path.GetDirectoryName(path.AbsolutePath);
            if (!Directory.Exists(directory))
            {
                return Array.Empty<string>();
            }

            var dataName = Path.GetFileName(path.AbsolutePath);
            var schemaName = Path.GetFileName(path.SchemaAbsolutePath);
            return Directory.GetFiles(directory)
                .Where(candidate => IsArtifact(Path.GetFileName(candidate), dataName)
                    || IsArtifact(Path.GetFileName(candidate), schemaName))
                .OrderBy(candidate => candidate, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool IsArtifact(string candidate, string destinationName)
        {
            return candidate.StartsWith(destinationName + ".", StringComparison.Ordinal)
                && (candidate.EndsWith(".tmp", StringComparison.Ordinal)
                    || candidate.EndsWith(".bak", StringComparison.Ordinal));
        }

        private static IReadOnlyList<string> ExistingArtifacts(IEnumerable<string> paths)
        {
            return paths.Where(File.Exists).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        private static void ReplaceFile(string temporaryPath, string destinationPath,
            string backupPath, bool destinationExisted)
        {
            if (destinationExisted)
            {
                File.Replace(temporaryPath, destinationPath, backupPath);
            }
            else
            {
                File.Move(temporaryPath, destinationPath);
            }
        }

        private static void TryRestore(string destinationPath, string backupPath, bool destinationExisted)
        {
            try
            {
                if (destinationExisted && File.Exists(backupPath))
                {
                    File.Copy(backupPath, destinationPath, true);
                }
                else if (!destinationExisted && File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
            }
            catch (Exception)
            {
            }
        }

        private static void DeleteFiles(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private static void RejectExistingLinks(string assetsRoot, string absolutePath)
        {
            var relative = absolutePath.Substring(assetsRoot.Length + 1);
            var current = assetsRoot;
            foreach (var segment in relative.Split(Path.DirectorySeparatorChar))
            {
                current = Path.Combine(current, segment);
                if (!File.Exists(current) && !Directory.Exists(current))
                {
                    continue;
                }

                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new ArgumentException("DatabasePath cannot traverse symbolic links.");
                }
            }
        }

        private static StringComparison PathComparison()
        {
            return IsCaseInsensitivePlatform()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }

        internal static StringComparer PathKeyComparer()
        {
            return IsCaseInsensitivePlatform()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
        }

        private static bool IsCaseInsensitivePlatform()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        }
    }

    internal interface IGameDBPostSaveActions
    {
        void Import(string assetPath);
        void Notify(string scopeName);
    }

    internal sealed class GameDBUnityPostSaveActions : IGameDBPostSaveActions
    {
        internal static GameDBUnityPostSaveActions Instance { get; } = new GameDBUnityPostSaveActions();

        private GameDBUnityPostSaveActions()
        {
        }

        public void Import(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        public void Notify(string scopeName)
        {
            GameDBEditor.OnGameDBSaved?.Invoke(scopeName);
        }
    }
}

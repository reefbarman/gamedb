using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GameDBLibrary
{
    //http://jsonpatch.com/
    //https://github.com/json-patch/json-patch-tests/blob/master/spec_tests.json

    /// <summary>
    /// Implements the json patch spec at http://jsonpatch.com/ and passes all tests https://github.com/json-patch/json-patch-tests/blob/master/spec_tests.json
    /// Allows applying a json patch to a json string.
    /// </summary>
    public
        class JsonPatch
    {
        private struct PathSection
        {
            public int index;
            public string key;
            public string original;
        }

        /// <summary>
        /// Patches the specified original json.
        /// </summary>
        /// <param name="originalJson">The original json.</param>
        /// <param name="patchJson">The patch json.</param>
        /// <returns>The patched json string</returns>
        /// <exception cref="System.FormatException">
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// </exception>
        public string Patch(string originalJson, string patchJson)
        {
            var patchesObj = JsonSerialization.Deserialize(patchJson);

            if (!(patchesObj is List<object> patches))
            {
                throw new FormatException($"JsonPatch root object not a valid json array - {patchJson}");
            }

            var originalObj = JsonSerialization.Deserialize(originalJson);

            if (originalObj == null)
            {
                throw new FormatException($"originalJson not a valid json object - {originalJson}");
            }

            foreach (var patchObj in patches)
            {
                var patch = patchObj as IDictionary<string, object>;

                ValidatePatch(patch);

                var op = patch["op"] as string;

                try
                {
                    switch (op)
                    {
                        case "add":
                            originalObj = Add(originalObj, patch["path"] as string, patch["value"]);
                            break;
                        case "remove":
                            originalObj = Remove(originalObj, patch["path"] as string);
                            break;
                        case "replace":
                            originalObj = Replace(originalObj, patch["path"] as string, patch["value"]);
                            break;
                        case "copy":
                            originalObj = Copy(originalObj, patch["from"] as string, patch["path"] as string);
                            break;
                        case "move":
                            originalObj = Move(originalObj, patch["from"] as string, patch["path"] as string);
                            break;
                        case "test":
                            originalObj = Test(originalObj, patch["path"] as string, patch["value"]);
                            break;
                    }
                }
                catch (FormatException e)
                {
                    throw new FormatException($"JsonPatch patch object invalid - {e.Message} - patch: {JsonSerialization.Serialize(patch)}");
                }
                catch (ArgumentOutOfRangeException e)
                {
                    throw new ArgumentException($"JsonPatch patch failed - {e.Message} - patch: {JsonSerialization.Serialize(patch)}");
                }
                catch (ArgumentException e)
                {
                    throw new ArgumentException($"JsonPatch patch path invalid - {e.Message} - patch: {JsonSerialization.Serialize(patch)}");
                }
            }

            return JsonSerialization.Serialize(originalObj);
        }

        private object Add(object originalObj, string path, object value)
        {
            var pathSections = ParsePath(path);

            return Navigate(originalObj, pathSections, delegate (object objectAtPath, PathSection? pathSection)
            {
                object replacementObj = null;

                if (pathSection.HasValue)
                {
                    if (objectAtPath is IDictionary<string, object> originalDic)
                    {
                        originalDic[pathSection.Value.key] = value;
                        replacementObj = originalDic;
                    }
                    else
                    {
                        if (objectAtPath is List<object> originalArray)
                        {
                            if (pathSection.Value.index < -1 && pathSection.Value.index > originalArray.Count)
                            {
                                throw new ArgumentException($"invalid array index: {pathSection.Value.index}");
                            }

                            originalArray.Insert(pathSection.Value.index == -1 ? originalArray.Count : pathSection.Value.index, value);
                            replacementObj = originalArray;
                        }
                        else
                        {
                            throw new ArgumentException($"path not valid - {pathSection.Value.original}");
                        }
                    }
                }
                else
                {
                    replacementObj = value;
                }

                return replacementObj;
            });
        }

        private object Remove(object originalObj, string path)
        {
            var pathSections = ParsePath(path);

            return Navigate(originalObj, pathSections, delegate (object objectAtPath, PathSection? pathSection)
            {
                object replacementObj = null;

                if (pathSection.HasValue)
                {
                    if (objectAtPath is IDictionary<string, object> originalDic && originalDic.ContainsKey(pathSection.Value.key))
                    {
                        originalDic.Remove(pathSection.Value.key);
                        replacementObj = originalDic;
                    }
                    else
                    {
                        if (objectAtPath is List<object> originalArray && pathSection.Value.index >= 0 && pathSection.Value.index < originalArray.Count)
                        {
                            originalArray.RemoveAt(pathSection.Value.index);
                            replacementObj = originalArray;
                        }
                        else
                        {
                            throw new ArgumentException($"path not valid - {pathSection.Value.original}");
                        }
                    }
                }
                else
                {
                    throw new ArgumentException("can't remove a scalar");
                }

                return replacementObj;
            });
        }

        private object Replace(object originalObj, string path, object value)
        {
            var pathSections = ParsePath(path);

            return Navigate(originalObj, pathSections, delegate (object objectAtPath, PathSection? pathSection)
            {
                object replacementObj = null;

                if (pathSection.HasValue)
                {
                    if (objectAtPath is IDictionary<string, object> originalDic && originalDic.ContainsKey(pathSection.Value.key))
                    {
                        originalDic[pathSection.Value.key] = value;
                        replacementObj = originalDic;
                    }
                    else
                    {
                        if (objectAtPath is List<object> originalArray && pathSection.Value.index >= 0 && pathSection.Value.index < originalArray.Count)
                        {
                            originalArray[pathSection.Value.index] = value;
                            replacementObj = originalArray;
                        }
                        else
                        {
                            throw new ArgumentException($"path not valid - {pathSection.Value.original}");
                        }
                    }
                }
                else
                {
                    replacementObj = value;
                }

                return replacementObj;
            });
        }

        private object Test(object originalObj, string path, object value)
        {
            var pathSections = ParsePath(path);

            return Navigate(originalObj, pathSections, delegate (object objectAtPath, PathSection? pathSection)
            {
                object replacementObj = null;

                if (pathSection.HasValue)
                {
                    if (objectAtPath is IDictionary<string, object> originalDic && originalDic.ContainsKey(pathSection.Value.key))
                    {
                        if (!CompareValues(value, originalDic[pathSection.Value.key]))
                        {
                            var message = $"expected: {value} got: {originalDic[pathSection.Value.key]}";
                            throw new ArgumentOutOfRangeException(message);
                        }

                        replacementObj = originalDic;
                    }
                    else
                    {
                        if (objectAtPath is List<object> originalArray && pathSection.Value.index >= 0 && pathSection.Value.index < originalArray.Count)
                        {
                            if (!CompareValues(value, originalArray[pathSection.Value.index]))
                            {
                                var message = $"expected: {value} got: {originalArray[pathSection.Value.index]}";
                                throw new ArgumentOutOfRangeException(message);
                            }

                            replacementObj = originalArray;
                        }
                        else
                        {
                            throw new ArgumentException($"path not valid - {pathSection.Value.original}");
                        }
                    }
                }
                else
                {
                    if (!CompareValues(value, originalObj))
                    {
                        var message = $"expected: {value} got: {originalObj}";
                        throw new ArgumentOutOfRangeException(message);
                    }
                }

                return replacementObj;
            });
        }

        private object Move(object originalObj, string from, string to)
        {
            var pathSections = ParsePath(from);

            object valueToMove = null;

            var removed = Navigate(originalObj, pathSections, delegate (object objectAtPath, PathSection? pathSection)
            {
                object replacementObj = null;

                if (pathSection.HasValue)
                {
                    if (objectAtPath is IDictionary<string, object> originalDic && originalDic.ContainsKey(pathSection.Value.key))
                    {
                        valueToMove = originalDic[pathSection.Value.key];
                        originalDic.Remove(pathSection.Value.key);
                        replacementObj = originalDic;
                    }
                    else
                    {
                        if (objectAtPath is List<object> originalArray && pathSection.Value.index >= 0 && pathSection.Value.index < originalArray.Count)
                        {
                            valueToMove = originalArray[pathSection.Value.index];
                            originalArray.RemoveAt(pathSection.Value.index);

                            replacementObj = originalArray;
                        }
                        else
                        {
                            throw new ArgumentException($"path not valid - {pathSection.Value.original}");
                        }
                    }
                }
                else
                {
                    throw new ArgumentException("can't move a scalar");
                }

                return replacementObj;
            });

            return Add(removed, to, valueToMove);
        }

        private object Copy(object originalObj, string from, string to)
        {
            var pathSections = ParsePath(from);

            object valueToCopy = null;

            originalObj = Navigate(originalObj, pathSections, delegate (object objectAtPath, PathSection? pathSection)
            {
                object replacementObj = null;

                if (pathSection.HasValue)
                {
                    if (objectAtPath is IDictionary<string, object> originalDic && originalDic.ContainsKey(pathSection.Value.key))
                    {
                        valueToCopy = originalDic[pathSection.Value.key];
                        replacementObj = originalDic;
                    }
                    else
                    {
                        if (objectAtPath is List<object> originalArray && pathSection.Value.index >= 0 && pathSection.Value.index < originalArray.Count)
                        {
                            valueToCopy = originalArray[pathSection.Value.index];
                            replacementObj = originalArray;
                        }
                        else
                        {
                            throw new ArgumentException($"path not valid - {pathSection.Value.original}");
                        }
                    }
                }
                else
                {
                    throw new ArgumentException("can't move a scalar");
                }

                return replacementObj;
            });

            return Add(originalObj, to, valueToCopy);
        }

        private object Navigate(object originalObj, PathSection[] paths, Func<object, PathSection?, object> onNavigationComplete)
        {
            if (paths.Length > 1)
            {
                var key = paths[0].key;

                if (originalObj is IDictionary<string, object> originalDic && originalDic.ContainsKey(key))
                {
                    originalDic[key] = Navigate(originalDic[key], paths.SubArray(1, paths.Length - 1), onNavigationComplete);
                    return originalDic;
                }
                else
                {
                    var index = paths[0].index;

                    if (originalObj is List<object> originalArray && index >= 0 && index < originalArray.Count)
                    {
                        originalArray[index] = Navigate(originalArray[index], paths.SubArray(1, paths.Length - 1), onNavigationComplete);
                        return originalArray;
                    }
                    else
                    {
                        throw new ArgumentException($"path not valid - {paths[0].original}");
                    }
                }
            }
            else if (paths.Length == 1)
            {
                return onNavigationComplete(originalObj, paths[0]);
            }
            else
            {
                return onNavigationComplete(originalObj, null);
            }
        }

        private PathSection[] ParsePath(string path)
        {
            var sections = new List<PathSection>();

            if (path != null)
            {
                var pathParts = path.Split('/');

                for (var i = 1; i < pathParts.Length; i++)
                {
                    var part = pathParts[i];

                    var section = new PathSection { original = part };

                    if (int.TryParse(part, out var index) && IsDigitsOnly(part) && !(part.Length > 1 && part[0] == '0'))
                    {
                        section.key = part;
                        section.index = index < 0 ? -99 : index;
                    }
                    else if (part == "-")
                    {
                        section.key = null;
                        section.index = -1;
                    }
                    else
                    {
                        var replacements = new Dictionary<string, string>() {
                            {"~0", "~"},
                            {"~1", "/"}
                        };

                        var regex = new Regex(string.Join("|", replacements.Keys.Select(Regex.Escape).ToArray()));
                        section.key = regex.Replace(part, m => replacements[m.Value]);
                        section.index = -99;
                    }

                    sections.Add(section);
                }
            }
            else
            {
                throw new FormatException($"invalid path format - {path}");
            }

            return sections.ToArray();
        }

        private void ValidatePatch(IDictionary<string, object> patch)
        {
            var success = false;

            if (patch != null && patch.ContainsKey("op") && patch.ContainsKey("path"))
            {
                var op = patch["op"] as string;

                switch (op)
                {
                    case "add":
                        success = patch.ContainsKey("value");
                        break;
                    case "remove":
                        success = true;
                        break;
                    case "replace":
                        success = patch.ContainsKey("value");
                        break;
                    case "copy":
                        success = patch.ContainsKey("from");
                        break;
                    case "move":
                        success = patch.ContainsKey("from");
                        break;
                    case "test":
                        success = patch.ContainsKey("value");
                        break;
                }
            }

            if (!success)
            {
                throw new FormatException($"JsonPatch patch object not a valid json object - {JsonSerialization.Serialize(patch)}");
            }
        }

        private bool CompareValues(object a, object b)
        {
            if (a == null && b == null)
            {
                return true;
            }

            if (NumericValue.IsNumber(a) && NumericValue.IsNumber(b))
            {
                return NumericValue.JsonNumbersEqual(a, b);
            }

            if (a is IComparable && b is IComparable && a.GetType() == b.GetType())
            {
                return ((IComparable)a).CompareTo(b) == 0;
            }

            if (a is IDictionary && b is IDictionary)
            {
                var aDic = a as IDictionary<string, object>;
                var bDic = b as IDictionary<string, object>;

                if (aDic.Keys.Count == bDic.Keys.Count)
                {
                    bool valid = true;

                    foreach (var keyPair in aDic)
                    {
                        if (bDic.ContainsKey(keyPair.Key))
                        {
                            valid &= CompareValues(keyPair.Value, bDic[keyPair.Key]);
                        }
                        else
                        {
                            valid = false;
                        }

                        if (!valid)
                        {
                            break;
                        }
                    }

                    return valid;
                }
            }

            if (a is IList && b is IList)
            {
                var aList = a as List<object>;
                var bList = b as List<object>;

                if (aList.Count == bList.Count)
                {
                    bool valid = true;

                    for (var i = 0; i < aList.Count; i++)
                    {
                        valid &= CompareValues(aList[i], bList[i]);

                        if (!valid)
                        {
                            break;
                        }
                    }

                    return valid;
                }
            }

            return false;
        }


        private static bool IsDigitsOnly(string str)
        {
            foreach (var c in str)
            {
                if (c < '0' || c > '9')
                    return false;
            }

            return true;
        }
    }
}

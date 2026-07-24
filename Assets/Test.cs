//using GameDBMain;

using GameDBMain;
using UnityEngine;
using UnityEngine.Assertions;

public class Test : MonoBehaviour
{
    void Start ()
    {
        GameDB gameDB = new GameDB("Main");
        gameDB.OnDBLoaded = () =>
        {
            TestStringKeySingle(gameDB);
            TestEnumKeyArrays(gameDB);
            TestTableRefEnumKeysDicts(gameDB);
            TestDictOtherKeys(gameDB);
        };

        var error = gameDB.Load("GameDBs/gameDB");
        if (error != null)
        {
            Debug.LogException(error);
        }
    }

    private void TestStringKeySingle(GameDB gameDB)
    {
        Debug.Log("TestStringKeySingle");

        var a = gameDB.StringKeySingleTable.GetByKey(StringKeySingleSchema.KeyA);

        var boolVal = a.BoolVal;
        Debug.Log(boolVal);
        Assert.AreEqual(boolVal, true);

        var colorVal = a.ColorVal;
        Debug.Log(colorVal);
        Assert.AreEqual(colorVal, Color.black);

        var enumVal = a.EnumVal;
        Debug.Log(enumVal);
        Assert.AreEqual(enumVal, Days.Sun);

        var floatVal = a.FloatVal;
        Debug.Log(floatVal);
        Assert.AreEqual(floatVal, 2.5f);

        var intVal = a.IntVal;
        Debug.Log(intVal);
        Assert.AreEqual(intVal, 1);

        var stringVal = a.StringVal;
        Debug.Log(stringVal);
        Assert.AreEqual(stringVal, "Hello");

        var tableRefVal = a.TableRefVal;

        Assert.IsFalse(tableRefVal.IsSet());
        Assert.IsNull(tableRefVal.GetValue());

        var unityObjectPathVal = a.UnityObjectPathVal;
        Debug.Log(unityObjectPathVal);
        Assert.AreEqual(unityObjectPathVal, "Assets/Resources/Prefabs/MyPrefab1.prefab");

        var unityObjectVal = a.UnityObjectObjectVal;
        Debug.Log(unityObjectVal);
        Assert.IsNotNull(unityObjectVal);

        var vector2Val = a.Vector2Val;
        Debug.Log(vector2Val);
        Assert.AreEqual(vector2Val, new Vector2(0, 0));

        var vector3Val = a.Vector3Val;
        Debug.Log(vector3Val);
        Assert.AreEqual(vector3Val, new Vector3(0, 3, 0));

        var vector4Val = a.Vector4Val;
        Debug.Log(vector4Val);
        Assert.AreEqual(vector4Val, new Vector4(0, 0, 0, 2));

        //Ensure caching is working
        Assert.AreEqual(unityObjectVal, a.UnityObjectObjectVal);
    }

    private void TestEnumKeyArrays(GameDB gameDB)
    {
        Debug.Log("TestEnumKeyArrays");

        var sun = gameDB.EnumKeyArraysTable.GetByKey(EnumKeyArraysSchema.KeySun);

        var boolArray = sun.BoolArrayVal;
        boolArray.ForEach((val) => Debug.Log(val));
        Assert.AreEqual(boolArray.Count, 2);
        Assert.AreEqual(boolArray[0], true);
        Assert.AreEqual(boolArray[1], false);

        var colorArray = sun.ColorArrayVal;
        colorArray.ForEach((val) => Debug.Log(val));
        Assert.AreEqual(colorArray.Count, 2);
        Assert.AreEqual(colorArray[0], Color.black);
        Assert.AreEqual(colorArray[1], Color.red);
    }

    private void TestTableRefEnumKeysDicts(GameDB gameDB)
    {
        Debug.Log("TestTableRefEnumKeysDicts");

        var sun = gameDB.TableRefEnumKeysDictsTable.GetByKey(TableRefEnumKeysDictsSchema.KeySun);

        var dictStrStrVal = sun.DictStrStrVal;
        foreach (var keyValuePair in dictStrStrVal)
        {
            Debug.Log(string.Format("Key: {0}, Value: {1}", keyValuePair.Key, keyValuePair.Value));
        }
        Assert.AreEqual(dictStrStrVal.Count, 2);
        Assert.AreEqual(dictStrStrVal["Hi"], "Yo");
        Assert.AreEqual(dictStrStrVal["Here"], "There");

        var dictStrBoolVal = sun.DictStrBoolVal;
        foreach (var keyValuePair in dictStrBoolVal)
        {
            Debug.Log(string.Format("Key: {0}, Value: {1}", keyValuePair.Key, keyValuePair.Value));
        }
        Assert.AreEqual(dictStrBoolVal.Count, 2);
        Assert.AreEqual(dictStrBoolVal["No"], false);
        Assert.AreEqual(dictStrBoolVal["Yes"], true);

        var dictStrEnumVal = sun.DictStrEnumVal;
        foreach (var keyValuePair in dictStrEnumVal)
        {
            Debug.Log(string.Format("Key: {0}, Value: {1}", keyValuePair.Key, keyValuePair.Value));
        }
        Assert.AreEqual(dictStrEnumVal.Count, 2);
        Assert.AreEqual(dictStrEnumVal["Rare"], Rarity.tier5);
        Assert.AreEqual(dictStrEnumVal["Uber"], Rarity.tier6);

        var dictStrFltVal = sun.DictStrFltVal;
        foreach (var keyValuePair in dictStrFltVal)
        {
            Debug.Log(string.Format("Key: {0}, Value: {1}", keyValuePair.Key, keyValuePair.Value));
        }
        Assert.AreEqual(dictStrFltVal.Count, 2);
        Assert.AreEqual(dictStrFltVal["A"], 0.5f);
        Assert.AreEqual(dictStrFltVal["B"], 1.3f);

        var dictStrIntVal = sun.DictStrIntVal;
        foreach (var keyValuePair in dictStrIntVal)
        {
            Debug.Log(string.Format("Key: {0}, Value: {1}", keyValuePair.Key, keyValuePair.Value));
        }
        Assert.AreEqual(dictStrIntVal.Count, 2);
        Assert.AreEqual(dictStrIntVal["Hi"], 1);
        Assert.AreEqual(dictStrIntVal["Ho"], 2);

        var dictStrColorVal = sun.DictStrColorVal;
        foreach (var keyValuePair in dictStrColorVal)
        {
            Debug.Log(string.Format("Key: {0}, Value: {1}", keyValuePair.Key, keyValuePair.Value));
        }
        Assert.AreEqual(dictStrColorVal.Count, 2);
        Assert.AreEqual(dictStrColorVal["Red"], Color.red);
        Assert.AreEqual(dictStrColorVal["Black"], Color.black);

        var dictStrTableRefVal = sun.DictStrTableRefVal;
        foreach (var keyValuePair in dictStrTableRefVal)
        {
            Debug.Log(string.Format("Key: {0}, Value: {1}", keyValuePair.Key, keyValuePair.Value));
        }
        Assert.AreEqual(dictStrTableRefVal.Count, 2);
        Assert.IsTrue(dictStrTableRefVal["Set"].IsSet());
        Assert.IsFalse(dictStrTableRefVal["NotSet"].IsSet());

        var dictStrUObjVal = sun.DictStrUObjVal;
        foreach (var keyValuePair in dictStrUObjVal)
        {
            Debug.Log(string.Format("Key: {0}, Value: {1}", keyValuePair.Key, keyValuePair.Value));
        }
        Assert.AreEqual(dictStrUObjVal.Count, 2);
        Assert.IsNotNull(dictStrUObjVal["1"].GetObject());
        Assert.IsNotNull(dictStrUObjVal["2"].GetObject());

        var dictStrVec2Val = sun.DictStrVec2Val;
        foreach (var keyValuePair in dictStrVec2Val)
        {
            Debug.Log(string.Format("Key: {0}, Value: {1}", keyValuePair.Key, keyValuePair.Value));
        }
        Assert.AreEqual(dictStrVec2Val.Count, 2);
        Assert.AreEqual(dictStrVec2Val["10"], new Vector2(1, 0));
        Assert.AreEqual(dictStrVec2Val["01"], new Vector2(0, 1));

        var dictStrVec3Val = sun.DictStrVec3Val;
        foreach (var keyValuePair in dictStrVec3Val)
        {
            Debug.Log(string.Format("Key: {0}, Value: {1}", keyValuePair.Key, keyValuePair.Value));
        }
        Assert.AreEqual(dictStrVec3Val.Count, 2);
        Assert.AreEqual(dictStrVec3Val["100"], new Vector3(1, 0, 0));
        Assert.AreEqual(dictStrVec3Val["001"], new Vector3(0, 0, 1));

        var dictStrVec4Val = sun.DictStrVec4Val;
        foreach (var keyValuePair in dictStrVec4Val)
        {
            Debug.Log(string.Format("Key: {0}, Value: {1}", keyValuePair.Key, keyValuePair.Value));
        }
        Assert.AreEqual(dictStrVec4Val.Count, 2);
        Assert.AreEqual(dictStrVec4Val["1000"], new Vector4(1, 0, 0, 0));
        Assert.AreEqual(dictStrVec4Val["0001"], new Vector4(0, 0, 0, 1));
    }

    private void TestDictOtherKeys(GameDB gameDB)
    {
        var a = gameDB.DictOtherKeysTable.GetByKey(DictOtherKeysSchema.KeyA);

        var dictEnumStrVal = a.DictEnumStrVal;
        foreach (var keyValuePair in dictEnumStrVal)
        {
            Debug.Log(string.Format("Key: {0}, Value: {1}", keyValuePair.Key, keyValuePair.Value));
        }
        Assert.AreEqual(dictEnumStrVal.Count, 2);
        Assert.AreEqual(dictEnumStrVal[Colors.Green], "GreenColor");
        Assert.AreEqual(dictEnumStrVal[Colors.Blue], "BlueColor");
    }
}

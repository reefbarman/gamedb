[TOC]
Usage Guide {#mainpage}
==========

## Overview {#overview}
GameDB is a Unity Plugin that provides an easy to use and powerful game and meta data editor. With this asset your scripts no longer have to use hard-coded references and instead can refer to your database for lists of values, files, prefabs, and other data types. GameDB is designed to produce JSON and associated classes to load and access this data easily within a game. It supports many data types, relationships between tables and hot-reloading to make working with game data easy and intuitive.

Some examples of usages would be for data relating to things like Loot tables, NPC balancing data, Prefab lookups for procedural assets, game configuration, and many many more.

![Screenshot of GameDB Unity Editor](Images/GameDB.png)

## Support {#support}
* For support visit the GameDB Github Issues pages here: https://github.com/reefbarman/gamedb-unity-plugin/issues and post an issue.
* Or visit my Asset Store publisher page here: https://assetstore.unity.com/publishers/23742 to contact me via email.

## Getting Started {#getting-started}
The GameDB Editor window is opened via navigating to _Window > GameDB_ menu and clicking on Open Editor menu item.

To get started creating and editing a gameDB click on the _Create GameDB_ button. This will ask you to select a location to save your gameDB. When you click OK this will create two files a .json and .schema.json file in the directory chosen (recommended location is within a Resources folder as using Resources.Load is one of the easier ways to access the data).

The gameDB will then be loaded and ready to edit.

The first thing to do with your new gameDB is set a Scope Name for the gameDB. This name will be appended to the namespace of the exported C# classes your gameDB creates. For example a scope name of “MyData” would produce the namespace GameDBMyData. This scope name is also used as an identifier for various other things explained in more detail later.

Next you can create tables by entering a _Table Name_, select a key type and clicking on _Create Table_. This will create a table below that is collapsed by default. Click on the table name and it will expand to show your table. By default this will be a table with no keys or fields.

To create a field click on the _Edit Table_ button, then enter a _Field Name_ under the _Modify Schema_ section. Select a data Type, check the tick-box for whether you would like it to be an array of data and then click _Create Field_.

This will then add a field to your table that can be removed with the [x] button (only visible after clicking _Edit Table_) if required.

To add some data enter a Key in the field at the top of the table then click _Create Key_. This will create a entry in the table below where you can now enter data for each of your fields.

Once you are happy with the data you have entered you can save the gameDB by clicking on _Save GameDB_, this will serialise out the data and the schema to the location selected earlier.

Next to use the data in your game you need to export the C# classes that will allow you to access the data. To do this click _Generate Classes_ and select where to export the classes. This will generate C# classes that will provide easy and strongly typed access to your data.

## Usage In Game {#usage-in-game}
You are free to use the json data however you like but the GameDB plugin generates code based on your data schema that provides an easy way to integrate the data with your game.

When you click _Generate Classes_ this will generate code in the selected location that provide access to the data. Each of the classes will be namespaced with a name like GameDB{SCOPE_NAME_HERE} for example: GameDBMain. These are auto-generated classes and hand editing of the files is generally not recommended.

The main class that represents your game data is the GameDB class. This class will load the json, deserialize it and provide access to each of the loaded tables. The GameDB class also has a OnDBLoaded event that is triggered when the data is loaded.

An example of loading the game data:

~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~{.cs}
using GameDBMain;
using UnityEngine;

public class Test : MonoBehaviour
{
	void Start ()
	{
		//A name is given when instantiating to identify the gameDB when editing during play
		GameDB gameDb = new GameDB("MyGameDB");
		gameDb.OnDBLoaded = delegate() {
			//Access game data here once the DB is loaded
		};

		//Load the json from the resources folder
		gameDb.Load("GameDBs/gameDB");
	}
}
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

A few classes are then created for each of the tables defined in your game data. There is a Table, Schema and Row class. Normally prefixed with the table name for example MyDataTable.cs, MyDataSchema.cs and MyData.cs. The Table class provides accessors for getting a particular row by Key or getting all the rows as a dictionary. The Schema class provides static accessors for the table name, field names and keys in the table. Lastly the Row class represents a particular row in your table and provides accessors for each field in the data.

An example of accessing data:

~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~{.cs}
    gameDb.OnDBLoaded = delegate() {
        //Access game data here once the DB is loaded
        var shipPart = gameDb.ShipPartsTable.GetByKey(ShipPartsSchema.KeyWing);

        Debug.Log(shipPart.CostVal);

        foreach (var loot in gameDb.LootTable.GetRows())
        {
            Debug.Log(loot.Value.NameVal);
        }
    };
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

## Editing in-game {#editing-in-game}
The gameDB can be edited while in game and hot-reloaded so you can balance while playing the game. If you have the GameDB Editor window opened when you are playing the game you can load one of the gameDBs you have loaded in the game by selecting the gameDB by its name in the _Runtime GameDB_ dropdown, then selecting the base gameDB it was loaded from in the _Base GameDB_ dropdown then clicking _Load GameDB_.

This will load the gameDB that is in memory in the game and allow you to edit it and then cause the data to reload in-game by clicking the _Reload In-Game_ button. This will cause the OnDBLoaded event to be triggered on the gameDB in the game and when this happens your game code can react to the change in anyway it likes (see advanced topics for working with hot reloading).

The game data you edit while playing the game won’t be saved back to the json until you click the _Save GameDB_ button, meaning you can experiment with changes while the game is running then if you stop playing the game without saving the gameDB those changes will be discarded.

## Data Types {#data-types}
Currently the GameDB plugin supports the following data types:

* String
* Int32
* Float
* Bool
* UnityEngine.Color
* UnityEngine.Vector2
* UnityEngine.Vector3
* UnityEngine.Vector4
* Prefabs (Version 1.2.0 and lower)
    - Prefabs can be imported from the project and when settings data a drop down of imported prefabs will be available.
    - Prefabs must be imported from a resources folder as they are loaded via Resource.Load when accessed in game.
    - There are two accessors added into the generated Row class for each prefab field; one returning the path of the prefab and another returning the UnityEngine.Object representing the prefab.
* UnityEngine.Object ( Version 1.3.0 and higher (Pro only currently) )
    - Supports the loading of any unity asset from the Resources folder ie Textures, Audio, Prefabs etc
    - UnityEngine.Objects must be imported from a resources folder as they are loaded via Resource.Load when accessed in game.
    - There are two accessors added into the generated Row class for each unityObject field; one returning the path of the UnityEngine.Object and another returning the UnityEngine.Object representing the unityObject.
* Enums (Pro version only)
    - Enums are imported from game code and can be selected as a type. Then when setting data for the field a drop down is given of the enum members that can be selected.
* Table References (Pro version only)
    - Fields can be a reference to another table within the gameDB. Then when setting data for the field a drop down is given with the keys of the referenced table
    - There are two accessors added into the generated Row class for each Table Reference field; one returns the string key of the entry and the other an instance of the row referenced.

_Arrays_

All the above data types can be stored as arrays. To modify the values of an array click on the [E] button next to the field and a popup will show allowing you to modify the size of the array and enter values for each index.

## Advanced Usage {#advanced-usage}
### Add an existing gameDB {#add-an-existing-gamedb}
If you have a gameDB from another project or being migrated from somewhere else (or the settings have been reset) you can load this by clicking on the _Add Existing GameDB_ button. Make sure that both the .json and .schema.json files are in the same location together.

### Importing Enums {#importing-enums}
To import enums you will need to click on the _Configuration_ Tab at the top of the editor window and expand the _Imported Enums_ section. From here you can import and remove any enum you want.

### Hot Reloading {#hot-reloading}
To support editing the data while playing or support updates of the game data from a server while the client is running. The GameDB has a OnDBLoaded event that is triggered when data has be Imported or Reimported.

Generally data shouldn’t be directly referenced from the gameDB due to overhead with runtime conversions of types (and loading prefabs for the prefab type). Best practice is to cache the values from the gameDB into your game classes for further access.

This means that when the OnDBLoaded event is triggered again you will need to likely re-cache any data you accessed as well as reloading any systems that rely on this data.

This is quite different to how serialised fields work but it allows consistent and deliberate work flows when dealing with data edited at run-time or live updating from a server for example.

### Settings {#settings}
The GameDB editor creates a GameDBEditor.settings file in the same location as the plugin under the Editor folder. This file is a JSON file representing the settings that represent your workspace. This can be edited by hand in the event of settings causing any issue. This file contains the paths of the loaded gameDBs, the export path for C# classes and paths to any imported items, plus more.

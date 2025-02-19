# DantelionDataManager
A .NET library for managing (reading and writing) game files of **FromSoftware games**. Aims to provide easy access to packed game files, but it ultimately supports access to three types of game data:
* PKG data [^1] [^2]
* encrypted (packed) data [^1] [^3]
* decryped (unpacked) data

In terms of EncrypedData, **it currently only supports:**
* Dark Souls 3
* Sekiro
* Elden Ring
* Armored Core VI

Implementing other games only needs adding a files dictionary, and a keys file.

<sub>**Other types (PKG and decrypted) were tested with (but should work with all):** Bloodborne v1.09 (PS4), Bloodborne GOTY Edition (PS4), Elden Ring CNT, Elden Ring v1.0 (PS4), Elden Ring v1.0 (PS5), Dark Souls 3 CNT (PS4), Sekiro v1.0 (PS4), Armored Core VI v1.0 (PS4) and other unpacked versions.</sub>

[^1]: only supports reading data
[^2]: must be using default pkg password
[^3]: PC versions of the game

## Initialization

Can be initialized with calling the constructors of the three main classes (`DecryptedData`, `EncryptedData` or `PKGGameData`), or feel free to use the provided static methods available through `GameData` base class.

All of them take a `rootPath` and an `outPath` parameter, while `EncryptedData` also takes an additional `BHDgame` parameter.
`rootPath` is where we're reading data from, `outPath` is where we're writing data to -- however, the latter will also get appended with game's name, so you can use the same output folder for all games. An example will be shown in the Writing files section.

```cs
//packed game data, root path must be the folder where the game .exe is located
var eldenRingData = GameData.InitGameData_PostEldenRing(@"C:\SteamLibrary\steamapps\common\ELDEN RING\Game", @"D:\outputfolder", BHD5.Game.EldenRing);
var armoredCore6Data = GameData.InitGameData_PostEldenRing(@"C:\SteamLibrary\steamapps\common\ARMORED CORE VI FIRES OF RUBICON\Game", @"D:\outputfolder", BHD5.Game.EldenRing); //some games use the same BHD implementation

var darkSouls3Data = GameData.InitGameData_PreEldenRing(@"C:\SteamLibrary\steamapps\common\Dark Souls III", @"D:\outputfolder", BHD5.Game.DarkSouls3);
var sekiroEncData = GameData.InitGameData_PreEldenRing(@"C:\SteamLibrary\steamapps\common\Sekiro", @"D:\outputfolder", BHD5.Game.DarkSouls3); //some games use the same BHD implementation

//unpacked game data, root path must be where all the unpacked folders are like chr, map etc.
var sekiroDecData = GameData.InitGameData_Decrypted(@"D:\pathToUnpackedSekiro", @"D:\outputfolder");
var bloodborneDecData = GameData.InitGameData_Decrypted(@"D:\pathToUnpackedBloodborne", @"D:\outputfolder");

//PKG game data, root path must point to the pkg itself
var bloodbornePkgData = GameData.InitGameData_PKG(@"D:\Bloodborne.pkg", @"D:\outputfolder");
var eldenRingCntData = GameData.InitGameData_PKG(@"D:\EldenRingCNT.pkg", @"D:\outputfolder");
var darkSouls3PkgData = GameData.InitGameData_PKG(@"D:\DarkSouls3.pkg", @"D:\outputfolder");
```

## Usage

We will use the example games from the Initialization section.

#### Reading data

Mainly there are two overloads for two different methods reading files: `Get(..)` for `byte[]` and `GetMem(..)` for `Memory<byte>`. If a file doesn't exist, it returns empty data (not null).

Below is an example at their first overload, which takes the `relativePath` for the file you want to read. The returned bytes can be used simply with SoulsFormats.

```cs
var bndBytes = eldenRingData.Get("/chr/c2120.chrbnd.dcx");
var msbMemory = darkSouls3Data.GetMem("/map/mapstudio/m10_00_00.msb.dcx");
var paramMemory = bloodbornePkgData.GetMem("/param/gameparam/gameparam.parambnd.dcx");

var malenia = BND4.Read(bndBytes);
```

The second overload takes a `relativePath`, `pattern` and an optional bool `load` parameter. The return value is a `KeyValuePair` collection, with the `Key` being the (relative) file path, and `Value` being the actual file data -- if the `load` parameter is false, the file data won't be loaded aka it will be empty. Only existing files will be present in the collection. 

The `pattern` parameter works similarly on how you would search in Windows file explorer.

Let's take a look on how to use this to get all `.msb` files for the Overworld in Elden Ring (`m60`).

```cs
var allMsbs = eldenRingData.GetMem("/map/mapstudio", "m60*.msb*", true);
foreach (var pair in allMsbs)
{
    var msb = MSBE.Read(pair.Value);
}
```

Another example for loading all chrbnds:
```cs
var allChrs = darkSouls3PkgData.GetMem("/chr", "*chrbnd.dcx", true);
foreach (var pair in allChrs)
{
    var chr = BND4.Read(pair.Value);
}
```

#### Writing data

Similarly like the methods for reading, there's also (only) two methods for writing files: `Set(..)` and `SetMem(..)`. You guessed it, they take a `relativePath` parameter, and either a `byte[]` or `Memory<byte>` data. These methods will always output into the `outPath` folder set earlier in the Initialization section.
```cs
var bndBytes = eldenRingData.Get("/chr/c2120.chrbnd.dcx");
var malenia = BND4.Read(bndBytes);

//do something to malenia bnd

eldenRingData.Set("/chr/c2120.chrbnd.dcx", malenia.Write());
//you can set the relativePath to anything.
//in this case our output file full path will be: "D:\outputfolder\EldenRing\chr\c2120.chrbnd.dcx"
```

Example with writing multiple files:
```cs
var allMsbs = eldenRingData.GetMem("/map/mapstudio", "m60*.msb*", true);
foreach (var pair in allMsbs)
{
    var msb = MSBE.Read(pair.Value);

    //do something to msb

    eldenRingData.SetMem(pair.Key, msb.Write());
    //we can use the Key here as it is the relative path of the file
}
```


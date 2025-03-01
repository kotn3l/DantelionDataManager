# DantelionDataManager
A .NET library for managing (reading and writing) game files of **FromSoftware games**. Aims to provide easy access to packed game files, but it ultimately supports access to any of these three types of game data:
* PKG data [^1] [^2]
* encrypted (packed) data [^1] [^3]
* decryped (unpacked) data

In terms of EncrypedData, **it currently only supports:**
* Dark Souls 3
* Sekiro
* Elden Ring
* Armored Core VI

Implementing other games only needs adding a files dictionary, and a keys file. This library is intended for tool developers.

<sub>**Other types (PKG and decrypted) were tested with (but should work with all):** Bloodborne v1.09 (PS4), Bloodborne GOTY Edition (PS4), Elden Ring CNT (PS4 and PS5), Elden Ring v1.0 (PS4 and PS5), Dark Souls 3 CNT (PS4), Sekiro v1.0 (PS4), Armored Core VI v1.0 (PS4) and other unpacked versions.</sub>

[^1]: only supports reading data
[^2]: must be using default pkg password
[^3]: PC versions of the game

## Notable features
### Automatic decrypting and BHDCache for fast re-runs (EncryptedData)

When first running the code, all bhds must be decrypted by using the keys in the working *Data* folder -- these are saved along with an MD5 hash as a `.bhdcache` file. On next runs if the cache file is valid, those are used instead for super fast startups. If invalid, they're deleted and automatically recalculated.

The retrieved files from the bdts are also automatically decrypted.

### Patch "loading" (PKGData)
Supports loading patches for PKGs. This simply means that for files that exist in the patch will be read from there (instead of the main pkg). 

## Initialization

Can be initialized with calling the constructors of the three main classes (`DecryptedData`, `EncryptedData` or `PKGGameData`), or feel free to use the provided static methods available through `GameData` base class.

All of them take a `rootPath` and an `outPath` parameter, while `EncryptedData` also takes an additional `BHDgame` parameter.
`rootPath` is where we're reading data from, `outPath` is where we're writing data to. An example will be shown in the Writing files section.

```cs
//packed game data, root path must be the folder where the game .exe is located
var eldenRingData = GameData.InitGameData_PostEldenRing(@"C:\SteamLibrary\steamapps\common\ELDEN RING\Game", @"D:\outputfolder", BHD5.Game.EldenRing);
var armoredCore6Data = GameData.InitGameData_PostEldenRing(@"C:\SteamLibrary\steamapps\common\ARMORED CORE VI FIRES OF RUBICON\Game", @"D:\outputfolder", BHD5.Game.ArmoredCore6);

var darkSouls3Data = GameData.InitGameData_PreEldenRing(@"C:\SteamLibrary\steamapps\common\Dark Souls III", @"D:\outputfolder", BHD5.Game.DarkSouls3);
var sekiroEncData = GameData.InitGameData_PreEldenRing(@"C:\SteamLibrary\steamapps\common\Sekiro", @"D:\outputfolder", BHD5.Game.Sekiro);

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

### Reading data
There are two (well, four) overloads for reading files:
* Read one specific file: `Get(relativePath)`
* Read multiple files by a provided pattern: `Get(relativePath, pattern, bool load = true)`

A `GameFile` is returned, which contains the `Path`, `Bytes` and `Data` of the file. `Data` is a `ISoulsFile`, which can be parsed automatically if you pass the correct delegate to the remaining two overloads:
* `Get(relativePath, Func<Memory<byte>, ISoulsFile> load)`
* `Get(relativePath, pattern, Func<Memory<byte>, ISoulsFile> load)`

It will make more sense with the examples just below.

#### Reading one file

The first overload takes a `relativePath` parameter. The returned file's bytes can be used simply with SoulsFormats. **If a file doesn't exist, it returns empty bytes! (not null).**

```cs
var bndFile = eldenRingData.Get("/chr/c2120.chrbnd.dcx");
var malenia = BND4.Read(bndFile.Bytes);
//any modification you do to 'malenia' will not be reflected back on the Data property of 'bndFile' obviously

var paramBnd = bloodbornePkgData.Get("/param/gameparam/gameparam.parambnd.dcx", BND4.Read);
//this way the BND4.Read function is called automatically, and the 'Data' property is filled
//unfortunately paramBnd.Data will have to be cast as BND4 if you want to work with it
```

#### Reading multiple files

The second overload takes a `relativePath`, `pattern` and an optional bool `load` parameter. The return value is an enumerable `GameFile` collection -- if the `load` parameter is false, the file bytes won't be loaded, they will be empty. (This can be useful if you want to check what files are actually present by a pattern for example.) **Only existing files will be present in the collection.**

The `pattern` parameter works similarly on how you would search in Windows file explorer.

Let's take a look on how to use this to get all chrbnds:
```cs
var allChrs = darkSouls3PkgData.Get("/chr", "*chrbnd.dcx", true);
foreach (var gamefile in allChrs)
{
    var chr = BND4.Read(gamefile.Bytes);
}
```

Another example to load all `.msb` files for the Overworld in Elden Ring (`m60`).

```cs
var allMsbs = eldenRingData.Get("/map/mapstudio", "m60*.msb*", MSBE.Read);
foreach (var gamefile in allMsbs)
{
    //do something to MSBE through gamefile.Data
}
```

### Writing data

There's (only) two methods for writing files: `Set(..)` and `SetMem(..)`. You guessed it, they take a `relativePath` parameter, and either a `byte[]` or `Memory<byte>` data this time around. These methods will always output into the `outPath` folder set earlier in the Initialization section.
```cs
var bndFile = eldenRingData.Get("/chr/c2120.chrbnd.dcx");
var malenia = BND4.Read(bndFile.Bytes);

//do something to malenia bnd

//then to write file:
eldenRingData.Set(bndFile.Path, malenia.Write());
eldenRingData.SetMem("/chr/c2120.chrbnd.dcx", malenia.Write());
//you can set the relativePath to anything.
//in both these cases our output file's full path will be: "D:\outputfolder\chr\c2120.chrbnd.dcx"

//OR use the write method available through the 'bndFile' instance, only if you passed a delegate to fill the 'Data' property
//for the simplicity of this example I will set it by hand, which is allowed.
//This following line is NOT needed if you pass the delegate. (like in the next example)
bndFile.Data = malenia;
bndFile.Write(eldenRingData);
```

Example with writing multiple files:
```cs
var allMsbs = eldenRingData.Get("/map/mapstudio", "m60*.msb*", MSBE.Read);
foreach (var msb in allMsbs)
{
    //do something to msb through 'msb.Data'
    //then simply
    msb.Write(eldenRingData);
}
```

## Dependencies
#### [SoulsFormats](https://github.com/kotn3l/SoulsFormats)
A custom SoulsFormats, that is still using the MemoryMappedFile shenanigans. I just love how performant that is. Updates were manually brought over from [SoulsFormatsNext](https://github.com/soulsmods/SoulsFormatsNEXT).

Added some custom ToStrings and other stuff needed for my yet unreleased conversion tool, but most importantly for this library, I have introduced a Dictionary for the BHD5 FileHeaders, which allows for ultra-fast lookups with the filehashes.

Current branch: [newest](https://github.com/kotn3l/SoulsFormats/tree/newest)

#### [LibOrbisPkg](https://github.com/kotn3l/LibOrbisPkg)
I ported the library from NET Framework to .NET. Because of too many test framework errors I removed the tests. <sub>(for now, don't @ me--).</sub> Quite slow, could use some refactors.

#### Others
* BouncyCastle.Cryptography
* CommunityToolkit.HighPerformance
* DotNext.IO
* DotNext.Unsafe
* ZstdNet

## Screenshot
The tool is using Serilog for logging, which is default set to write to Console. It looks pretty imo:
![Log](/img/logpic.png?raw=true "Log")

## Credits
This wouldn't be possible without the amazing modding community these games have. Thank you all!, but most notably:
* TGKP
* Nordgaren
* Smithbox contributors
* SoulsFormatsNEXT contributors
* Meowmaritus

<sub>if I missed anyone message me</sub>